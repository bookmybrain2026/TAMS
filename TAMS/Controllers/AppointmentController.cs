using TAMS.Data;
using TAMS.Models; 
using TAMS.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver; 
using System; 

namespace TAMS.Controllers
{
    public class AppointmentController : Controller
    {
        private readonly MongoDBContext dBContext;
        private readonly IEmailService _emailService;

        public AppointmentController(
            MongoDBContext dBContext,
            IEmailService emailService)
        {
            this.dBContext = dBContext;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Scheduler(int tutorId, int? month, int? year, int studentId)
        {
            var loggedinstudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedinstudentId == null) { return RedirectToAction("Login", "Login"); }
            var tutor =
                dBContext.Tutor
                .Find(t => t.TutorId == tutorId).FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            var startOfMonth = new DateTime( targetYear, targetMonth, 1);
            var startOfNextMonth = startOfMonth.AddMonths(1);

            var appointments =
                dBContext.Appointments
                .Find(a =>  a.TutorId == tutorId &&
                a.AppointmentDate >= startOfMonth &&
                a.AppointmentDate < startOfNextMonth) .ToList();

            foreach (var appointment in appointments)
            {
                appointment.Student =
                    dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            }

            var schedules =
                dBContext.Schedule
                .Find(s => s.TutorId == tutorId).ToList()
                .Where(s => s.Date.Month == targetMonth 
                && s.Date.Year == targetYear).ToList();

            ViewBag.Tutor = tutor;
            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.StudentId = loggedinstudentId.Value;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> BookAppointment(int tutorId, DateTime date, TimeSpan startTime, int studentId)
        {
            // Find the tutor.
            var tutor =
                dBContext.Tutor
                .Find(t =>t.TutorId == tutorId).FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            var student =
                dBContext.Student
                .Find(s => s.StudentId == studentId) .FirstOrDefault();

            if (student == null) { return NotFound(); }

            DateTime appointmentDate = date.Date + startTime;

            var appointmentExists =
                dBContext.Appointments
                .Find(a => a.TutorId == tutorId &&
                    a.AppointmentDate == appointmentDate &&
                    a.Status == "Scheduled").FirstOrDefault();

            if (appointmentExists != null)
            {
                TempData["Error"] = "This time slot is already booked.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            var tutorSchedules =
                dBContext.Schedule
                .Find(s => s.TutorId == tutorId).ToList();

            var matchingSchedule =
                tutorSchedules.FirstOrDefault(s => s.Date.Date == appointmentDate.Date &&
                appointmentDate.TimeOfDay >= s.StartTime &&
                appointmentDate.TimeOfDay < s.EndTime);

            if (matchingSchedule == null)
            {
                TempData["Error"] = "The selected time is not available.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            var lastAppointment =
                dBContext.Appointments.Find(_ => true)
                .SortByDescending(a => a.AppointmentId).FirstOrDefault();

            int newAppointmentId =lastAppointment == null ? 1 : lastAppointment.AppointmentId + 1;

            var newAppointment =
                new Appointment
                {
                    AppointmentId = newAppointmentId,
                    TutorId = tutorId,
                    StudentId = studentId,
                    AppointmentDate = appointmentDate,
                    Status = "Scheduled"
                };
            dBContext.Appointments.InsertOne(newAppointment);

            var studentName = $"{student.FName} {student.LName}";
            var tutorName = $"{tutor.FName} {tutor.LName}";
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendStudentAppointmentConfirmationAsync(student.Email, studentName, tutorName, appointmentDate);
                }
                if (!string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService.SendTutorAppointmentNotificationAsync(tutor.Email,tutorName, studentName, appointmentDate);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Email failed: {ex.Message}");
            }

            TempData["Success"] ="Appointment booked successfully!";

            return RedirectToAction("Dashboard", "Student", new { id = studentId });
        }
    }
}