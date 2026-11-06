using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class ReportsController : Controller
    {
        private readonly MongoDBContext dBContext;

        public ReportsController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GenerateAppointmentHistory(int studentId)
        {
            var student = dBContext.Student.Find(s => s.StudentId == studentId).FirstOrDefault();
            if (student == null) { return NotFound(); }

            var appointments = dBContext.Appointments.Find(a => a.StudentId == studentId).SortBy(a => a.AppointmentDate).ToList();
            var groupAppointments = dBContext.GroupAppointments.Find(g => g.StudentIds.Contains(studentId)).SortBy(g => g.AppointmentDate).ToList();

            foreach (var appointment in appointments)
            {
                appointment.Tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();
            }

            var allAppointments = appointments
                .Select(a => new
                {
                    Date = a.AppointmentDate, Type = "Individual", TutorId = a.TutorId, Status = a.Status
                })
                .Concat(groupAppointments.Select(g => new
                {
                    Date = g.AppointmentDate.AddHours(6), Type = "Group", TutorId = g.TutorId, Status = g.Status
                }))
                .OrderBy(a => a.Date)
                .ToList();

            var lines = new List<string>
            {
                $"Appointment History Report for {student.FName} {student.LName}",
                $"Generated: {DateTime.Now:MM/dd/yyyy hh:mm tt}",
                "---------------------------------------------------"
            };

            if (allAppointments.Count == 0)
            {
                lines.Add("No appointments were found for this student.");
            }

            foreach (var appointment in allAppointments)
            {
                var tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();
                var tutorName = tutor == null ? "Tutor information unavailable" : $"{tutor.FName} {tutor.LName}";

                lines.Add($"Date: {appointment.Date:MM/dd/yyyy hh:mm tt}");
                lines.Add($"Type: {appointment.Type}");
                lines.Add($"Tutor: {tutorName}");
                lines.Add($"Status: {appointment.Status}");
                lines.Add("---------------------------------------------------");
            }

            string reportContent = string.Join(Environment.NewLine, lines);
            byte[] fileBytes = Encoding.UTF8.GetBytes(reportContent);

            string fileName = $"AppointmentHistory_{studentId}_{DateTime.Now:yyyyMMddHHmmss}.txt";

            var lastReport = dBContext.Reports.Find(_ => true).SortByDescending(r => r.ReportId).FirstOrDefault();
            int newReportId = lastReport == null ? 1 : lastReport.ReportId + 1;
            var reportRecord = new Report
            {
                ReportId = newReportId,
                StudentId = studentId,
                ReportType = "AppointmentHistory",
                DateGenerated = DateTime.Now,
                GeneratedByRole = "Student",
                Summary =
                    $"Generated appointment history containing {appointments.Count} individual appointment(s) and {groupAppointments.Count} group appointment(s)."
            };
            dBContext.Reports.InsertOne(reportRecord);

            return File(fileBytes, "text/plain", fileName);
        }

        [HttpPost]
        public IActionResult GenerateTutorScheduleReport(int tutorId)
        {
            var tutor = dBContext.Tutor.Find(t => t.TutorId == tutorId).FirstOrDefault();
            if (tutor == null) { return NotFound(); }

            var schedules = dBContext.Schedule.Find(s => s.TutorId == tutorId).SortBy(s => s.Date).ThenBy(s => s.StartTime).ToList();
            var appointments = dBContext.Appointments.Find(a => a.TutorId == tutorId).SortBy(a => a.AppointmentDate).ToList();
            var groupAppointments = dBContext.GroupAppointments.Find(g => g.TutorId == tutorId).SortBy(g => g.AppointmentDate).ToList();

            foreach (var appointment in appointments)
            {
                appointment.Student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            }

            DateTime now = DateTime.Now;

            var lines = new List<string>
                {
                    $"Tutor Schedule Report for {tutor.FName} {tutor.LName}",
                    $"Generated: {now:MM/dd/yyyy hh:mm tt}",
                    "-----------------------------------------------------",
                    "",
                    "=== SCHEDULE SLOTS ===",
                    "Date          Start Time - End Time       Status       Student",
                    "-----------------------------------------------------"
                };

            int displayedScheduleCount = 0;

            foreach (var schedule in schedules)
            {
                var slotAppointments = appointments.Where(a => a.ScheduleId == schedule.ScheduleId).ToList();
                var bookedAppointment = slotAppointments.FirstOrDefault(a =>
                    a.Status?.Trim().Equals("Scheduled", StringComparison.OrdinalIgnoreCase) == true);
                var completedAppointment = slotAppointments.FirstOrDefault(a =>
                    a.Status?.Trim().Equals("Completed", StringComparison.OrdinalIgnoreCase) == true);

                string status;
                string studentName = "N/A";

                if (bookedAppointment != null)
                {
                    status = "Booked";
                    studentName = bookedAppointment.Student == null ? "Student information unavailable"
                        : $"{bookedAppointment.Student.FName} {bookedAppointment.Student.LName}";
                }
                else if (completedAppointment != null)
                {
                    status = "Completed";
                    studentName = completedAppointment.Student == null ? "Student information unavailable"
                        : $"{completedAppointment.Student.FName} {completedAppointment.Student.LName}";
                }
                else
                {
                    DateTime slotDateTime = schedule.Date.Date.Add(schedule.StartTime);

                    // Skip expired slots
                    if (slotDateTime < now)
                    {
                        continue;
                    }

                    status = "Available";
                }

                string startTime = DateTime.Today.Add(schedule.StartTime).ToString("hh:mm tt");
                string endTime = DateTime.Today.Add(schedule.EndTime).ToString("hh:mm tt");

                lines.Add(
                    $"{schedule.Date:MM/dd/yyyy}  " +
                    $"{startTime} - {endTime}  |  " +
                    $"{status}  |  " +
                    $"{studentName}"
                );

                displayedScheduleCount++;
            }

            if (displayedScheduleCount == 0)
            {
                lines.Add("No schedule slots were found.");
            }

            lines.Add("");
            lines.Add("=== APPOINTMENTS ===");

            var allAppointments = new List<(DateTime Date, string Status, string Student)>();

            // Add regular appointments
            foreach (var appointment in appointments)
            {
                // Skip cancelled appointments.
                if (string.Equals(appointment.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string studentName = appointment.Student == null ? "Student information unavailable"
                    : $"{appointment.Student.FName} {appointment.Student.LName}";

                allAppointments.Add((
                    appointment.AppointmentDate, appointment.Status ?? "Unknown", studentName
                ));
            }

            // Add group appointments.
            foreach (var group in groupAppointments)
            {
                // Skip cancelled group appointments.
                if (string.Equals(group.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                allAppointments.Add((
                    group.AppointmentDate.AddHours(6), group.Status ?? "Unknown", "Group"
                ));
            }

            // Sort both appointment types together by date and time
            allAppointments = allAppointments.OrderBy(a => a.Date).ToList();

            if (allAppointments.Count == 0)
            {
                lines.Add("No appointments were found.");
            }

            foreach (var appointment in allAppointments)
            {
                lines.Add(
                    $"{appointment.Date:MM/dd/yyyy hh:mm tt}  |  " +
                    $"{appointment.Status}  |  " +
                    $"Student: {appointment.Student}"
                );
            }

            string reportContent = string.Join(Environment.NewLine, lines);
            byte[] fileBytes = Encoding.UTF8.GetBytes(reportContent);

            string fileName = $"TutorSchedule_{tutor.FName}_{tutor.LName}_{tutorId}_{now:yyyyMMddHHmmss}.txt";

            var lastReport = dBContext.Reports.Find(_ => true).SortByDescending(r => r.ReportId).FirstOrDefault();
            int newReportId = lastReport == null ? 1 : lastReport.ReportId + 1;

            var reportRecord = new Report
            {
                ReportId = newReportId,
                TutorId = tutorId,
                ReportType = "TutorSchedule",
                DateGenerated = now,
                GeneratedByRole = "Tutor",
                Summary =
                    $"Generated schedule and appointments report " +
                    $"({displayedScheduleCount} schedule slots, " +
                    $"{allAppointments.Count} appointments)."
            };
            dBContext.Reports.InsertOne(reportRecord);

            return File(fileBytes, "text/plain", fileName);
        }

    }
}