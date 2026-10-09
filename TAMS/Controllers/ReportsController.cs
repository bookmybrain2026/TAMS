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

        // Displays the Reports page.
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // Generates and downloads a student's appointment history report.
        [HttpPost]
        public IActionResult GenerateAppointmentHistory(int studentId)
        {
            // Find the student using the supplied ID.
            var student = dBContext.Student
                .Find(s => s.StudentId == studentId)
                .FirstOrDefault();

            if (student == null)
            {
                return NotFound();
            }

            // Retrieve the student's appointments in chronological order.
            var appointments = dBContext.Appointments
                .Find(a => a.StudentId == studentId)
                .SortBy(a => a.AppointmentDate)
                .ToList();

            // Attach tutor information to each appointment.
            foreach (var appointment in appointments)
            {
                appointment.Tutor = dBContext.Tutor
                    .Find(t => t.TutorId == appointment.TutorId)
                    .FirstOrDefault();
            }

            // Create the report content.
            var lines = new List<string>
            {
                $"Appointment History Report for {student.FName} {student.LName}",
                $"Generated: {DateTime.Now:MM/dd/yyyy hh:mm tt}",
                "---------------------------------------------------"
            };

            if (appointments.Count == 0)
            {
                lines.Add("No appointments were found for this student.");
            }

            // Add each appointment to the report.
            foreach (var appointment in appointments)
            {
                var tutorName = appointment.Tutor == null
                    ? "Tutor information unavailable"
                    : $"{appointment.Tutor.FName} {appointment.Tutor.LName}";

                lines.Add($"Date: {appointment.AppointmentDate:MM/dd/yyyy hh:mm tt}");
                lines.Add($"Tutor: {tutorName}");
                lines.Add($"Status: {appointment.Status}");
                lines.Add("---------------------------------------------------");
            }

            // Convert the report content into bytes.
            string reportContent = string.Join(Environment.NewLine, lines);
            byte[] fileBytes = Encoding.UTF8.GetBytes(reportContent);

            // Generate a unique filename.
            string fileName =
                $"AppointmentHistory_{studentId}_{DateTime.Now:yyyyMMddHHmmss}.txt";

            // Generate a new report ID.
            var lastReport = dBContext.Reports
                .Find(_ => true)
                .SortByDescending(r => r.ReportId)
                .FirstOrDefault();

            int newReportId = lastReport == null
                ? 1
                : lastReport.ReportId + 1;

            // Save a record of the generated report in MongoDB.
            var reportRecord = new Report
            {
                ReportId = newReportId,
                StudentId = studentId,
                ReportType = "AppointmentHistory",
                DateGenerated = DateTime.Now,
                GeneratedByRole = "Student",
                Summary =
                    $"Generated appointment history containing {appointments.Count} appointment(s)."
            };

            dBContext.Reports.InsertOne(reportRecord);

            // Return the report as a downloadable text file.
            return File(fileBytes, "text/plain", fileName);
        }

        // Generates and downloads a tutor's schedule and appointments report.
        [HttpPost]
        public IActionResult GenerateTutorScheduleReport(int tutorId)
        {
            // Find the tutor using the supplied ID.
            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId)
                .FirstOrDefault();

            if (tutor == null)
            {
                return NotFound();
            }

            // Retrieve the tutor's schedule slots in chronological order.
            var schedules = dBContext.Schedule
                .Find(s => s.TutorId == tutorId)
                .SortBy(s => s.Date)
                .ThenBy(s => s.StartTime)
                .ToList();

            // Retrieve the tutor's appointments in chronological order.
            var appointments = dBContext.Appointments
                .Find(a => a.TutorId == tutorId)
                .SortBy(a => a.AppointmentDate)
                .ToList();

            // Attach student information to each appointment.
            foreach (var appointment in appointments)
            {
                appointment.Student = dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId)
                    .FirstOrDefault();
            }

            // Create the report header.
            var lines = new List<string>
            {
                $"Tutor Schedule Report for {tutor.FName} {tutor.LName}",
                $"Generated: {DateTime.Now:MM/dd/yyyy hh:mm tt}",
                "-----------------------------------------------------",
                "",
                "=== SCHEDULE SLOTS ==="
            };

            if (schedules.Count == 0)
            {
                lines.Add("No schedule slots were found.");
            }

            // Add schedule slot information.
            foreach (var schedule in schedules)
            {
                lines.Add(
                    $"{schedule.Date:MM/dd/yyyy}  " +
                    $"{schedule.StartTime:hh\\:mm} - " +
                    $"{schedule.EndTime:hh\\:mm}"
                );
            }

            // Add the appointments section.
            lines.Add("");
            lines.Add("=== APPOINTMENTS ===");

            if (appointments.Count == 0)
            {
                lines.Add("No appointments were found.");
            }

            // Add appointment information.
            foreach (var appointment in appointments)
            {
                var studentName = appointment.Student == null
                    ? "Student information unavailable"
                    : $"{appointment.Student.FName} {appointment.Student.LName}";

                lines.Add(
                    $"{appointment.AppointmentDate:MM/dd/yyyy hh:mm tt}  |  " +
                    $"{appointment.Status}  |  " +
                    $"Student: {studentName}"
                );
            }

            // Convert the report content into bytes.
            string reportContent = string.Join(Environment.NewLine, lines);
            byte[] fileBytes = Encoding.UTF8.GetBytes(reportContent);

            // Generate a unique filename.
            string fileName =
                $"TutorSchedule_{tutorId}_{DateTime.Now:yyyyMMddHHmmss}.txt";

            // Generate a new report ID.
            var lastReport = dBContext.Reports
                .Find(_ => true)
                .SortByDescending(r => r.ReportId)
                .FirstOrDefault();

            int newReportId = lastReport == null
                ? 1
                : lastReport.ReportId + 1;

            // Save a record of the generated report in MongoDB.
            var reportRecord = new Report
            {
                ReportId = newReportId,
                TutorId = tutorId,
                ReportType = "TutorSchedule",
                DateGenerated = DateTime.Now,
                GeneratedByRole = "Tutor",
                Summary =
                    $"Generated schedule and appointments report " +
                    $"({schedules.Count} schedule slots, " +
                    $"{appointments.Count} appointments)."
            };

            dBContext.Reports.InsertOne(reportRecord);

            // Return the report as a downloadable text file.
            return File(fileBytes, "text/plain", fileName);
        }
    }
}