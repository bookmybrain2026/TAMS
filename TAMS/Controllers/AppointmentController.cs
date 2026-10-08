using TAMS.Data; // Gives access to MongoDBContext.cs
using TAMS.Models; // Gives access to ALL class models
using TAMS.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver; // Provides MongoDB operations such as Find(), InsertOne(), and SortByDescending().
using System; // Provides types such as DateTime and TimeSpan

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
        public IActionResult Scheduler(
            int tutorId,
            int? month,
            int? year,
            int studentId)
        // Displays a tutor's available schedule and existing appointments
        // for a selected month.
        {
            var tutor =
                dBContext.Tutor
                .Find(t => t.TutorId == tutorId)
                .FirstOrDefault();

            if (tutor == null)
            {
                return NotFound();
            }

            int targetMonth =
                month ?? DateTime.Today.Month;

            int targetYear =
                year ?? DateTime.Today.Year;

            // Calculate the beginning of the month
            // and the beginning of the next month.
            var startOfMonth =
                new DateTime(
                    targetYear,
                    targetMonth,
                    1);

            var startOfNextMonth =
                startOfMonth.AddMonths(1);

            // Retrieve the tutor's appointments.
            var appointments =
                dBContext.Appointments
                .Find(a =>
                    a.TutorId == tutorId &&
                    a.AppointmentDate >= startOfMonth &&
                    a.AppointmentDate < startOfNextMonth)
                .ToList();

            // Attach student information to each appointment.
            foreach (var appointment in appointments)
            {
                appointment.Student =
                    dBContext.Student
                    .Find(s =>
                        s.StudentId == appointment.StudentId)
                    .FirstOrDefault();
            }

            // Retrieve the tutor's schedule
            // for the selected month and year.
            var schedules =
                dBContext.Schedule
                .Find(s =>
                    s.TutorId == tutorId)
                .ToList()
                .Where(s =>
                    s.Date.Month == targetMonth &&
                    s.Date.Year == targetYear)
                .ToList();

            ViewBag.Tutor = tutor;
            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.StudentId = studentId;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> BookAppointment(
            int tutorId,
            DateTime date,
            TimeSpan startTime,
            int studentId)
        // Checks whether a selected time is valid and available,
        // then creates the appointment.
        {
            // Find the tutor.
            var tutor =
                dBContext.Tutor
                .Find(t =>
                    t.TutorId == tutorId)
                .FirstOrDefault();

            if (tutor == null)
            {
                return NotFound();
            }

            // Verify that the student exists.
            var student =
                dBContext.Student
                .Find(s =>
                    s.StudentId == studentId)
                .FirstOrDefault();

            if (student == null)
            {
                return NotFound();
            }

            // Combine the selected date and time
            // into one DateTime value.
            DateTime appointmentDate =
                date.Date + startTime;

            // Check whether the time is already booked.
            var appointmentExists =
                dBContext.Appointments
                .Find(a =>
                    a.TutorId == tutorId &&
                    a.AppointmentDate == appointmentDate &&
                    a.Status == "Scheduled")
                .FirstOrDefault();

            if (appointmentExists != null)
            {
                TempData["Error"] =
                    "This time slot is already booked.";

                return RedirectToAction(
                    "Scheduler",
                    new
                    {
                        tutorId,
                        studentId,
                        month = date.Month,
                        year = date.Year
                    });
            }

            // Retrieve all schedules belonging to the tutor.
            var tutorSchedules =
                dBContext.Schedule
                .Find(s =>
                    s.TutorId == tutorId)
                .ToList();

            // Check whether the selected appointment
            // fits inside one of the tutor's schedules.
            var matchingSchedule =
                tutorSchedules.FirstOrDefault(s =>
                    s.Date.Date == appointmentDate.Date &&
                    appointmentDate.TimeOfDay >= s.StartTime &&
                    appointmentDate.TimeOfDay < s.EndTime);

            if (matchingSchedule == null)
            {
                TempData["Error"] =
                    "The selected time is not available.";

                return RedirectToAction(
                    "Scheduler",
                    new
                    {
                        tutorId,
                        studentId,
                        month = date.Month,
                        year = date.Year
                    });
            }

            // Find the appointment with the highest AppointmentId.
            var lastAppointment =
                dBContext.Appointments
                .Find(_ => true)
                .SortByDescending(a =>
                    a.AppointmentId)
                .FirstOrDefault();

            // Generate the new appointment ID.
            int newAppointmentId =
                lastAppointment == null
                    ? 1
                    : lastAppointment.AppointmentId + 1;

            // Create the appointment.
            var newAppointment =
                new Appointment
                {
                    AppointmentId = newAppointmentId,
                    TutorId = tutorId,
                    StudentId = studentId,
                    AppointmentDate = appointmentDate,
                    Status = "Scheduled"
                };

            // Save the appointment to MongoDB.
            dBContext.Appointments.InsertOne(
                newAppointment);

            // Send emails after the appointment is saved.
            try
            {
                // Send confirmation email to the student.
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService
                        .SendAppointmentConfirmationAsync(
                            student.Email,
                            $"{student.FName} {student.LName}",
                            $"{tutor.FName} {tutor.LName}",
                            appointmentDate);
                }

                // Send notification email to the tutor.
                if (!string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService
                        .SendTutorAppointmentNotificationAsync(
                            tutor.Email,
                            $"{tutor.FName} {tutor.LName}",
                            $"{student.FName} {student.LName}",
                            appointmentDate);
                }
            }
            catch (Exception ex)
            {
                // Email failure does not prevent
                // the appointment from being saved.
                Console.WriteLine(
                    $"Email failed: {ex.Message}");
            }

            TempData["Success"] =
                "Appointment booked successfully!";

            // Return the student to their dashboard.
            return RedirectToAction(
                "Dashboard",
                "Student",
                new { id = studentId });
        }
    }
}