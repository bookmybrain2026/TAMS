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

        public AppointmentController(MongoDBContext dBContext, IEmailService emailService)
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(int scheduleId, int tutorId, DateTime date, TimeSpan startTime, int studentId)
        {
            var loggedInStudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedInStudentId == null) { return RedirectToAction("Login", "Login"); }

            studentId = loggedInStudentId.Value;

            var tutor = dBContext.Tutor.Find(t => t.TutorId == tutorId).FirstOrDefault();
            var student = dBContext.Student.Find(s => s.StudentId == studentId).FirstOrDefault();
            var schedule = dBContext.Schedule.Find(s => s.ScheduleId == scheduleId).FirstOrDefault();

            if (tutor == null || student == null || schedule == null){ return NotFound(); }

            if (schedule.TutorId != tutorId || schedule.Date.Date != date.Date || schedule.StartTime != startTime)
            {
                TempData["Error"] = "Invalid appointment selection.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            DateTime newStart = schedule.Date.Date.Add(schedule.StartTime);
            DateTime newEnd = schedule.Date.Date.Add(schedule.EndTime);

            if (newEnd <= newStart)
            {
                TempData["Error"] = "The selected schedule has an invalid time.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            var possibleConflicts = dBContext.Appointments.Find(a => a.Status == "Scheduled" && (a.StudentId == studentId || a.TutorId == tutorId)).ToList();

            foreach (var existing in possibleConflicts)
            {
                DateTime existingStart = existing.AppointmentDate;
                DateTime existingEnd = existing.EndDate;

                if (existingEnd <= existingStart)
                {
                    var oldSchedule = dBContext.Schedule.Find(s => s.ScheduleId == existing.ScheduleId).FirstOrDefault();

                    if (oldSchedule != null)
                    {
                        existingStart = oldSchedule.Date.Date.Add(oldSchedule.StartTime);
                        existingEnd = oldSchedule.Date.Date.Add(oldSchedule.EndTime);
                    }
                    else
                    {
                        TempData["Error"] = "An existing appointment has an invalid time. " + "Please contact the administrator.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
                    }
                }

                bool overlaps = newStart < existingEnd &&
                    newEnd > existingStart;

                if (overlaps)
                {
                    if (existing.StudentId == studentId)
                    {
                        TempData["Error"] = "You already have an appointment during this time.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
                    }

                    if (existing.TutorId == tutorId)
                    {
                        TempData["Error"] = "This tutor already has an appointment during this time.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
                    }
                }
            }

            var existingScheduleBooking = dBContext.Appointments.Find(a => a.ScheduleId == scheduleId && a.Status == "Scheduled").FirstOrDefault();
            if (existingScheduleBooking != null)
            {
                TempData["Error"] = "This schedule has already been booked.";
                return RedirectToAction("Scheduler", new {tutorId, studentId, month = date.Month, year = date.Year });
            }

            var lastAppointment = dBContext.Appointments.Find(_ => true).SortByDescending(a => a.AppointmentId).FirstOrDefault();
            int newAppointmentId = lastAppointment == null ? 1 : lastAppointment.AppointmentId + 1;
            var newAppointment = new Appointment
            {
                AppointmentId = newAppointmentId,
                ScheduleId = schedule.ScheduleId,
                TutorId = tutorId,
                StudentId = studentId,
                AppointmentDate = newStart,
                EndDate = newEnd,
                Status = "Scheduled"
            };
            dBContext.Appointments.InsertOne(newAppointment);

            string studentName = $"{student.FName} {student.LName}";
            string tutorName = $"{tutor.FName} {tutor.LName}";

            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendStudentAppointmentConfirmationAsync(student.Email, studentName, tutorName, newStart.AddHours(5));
                }

                if (!string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService.SendTutorAppointmentNotificationAsync(tutor.Email, tutorName, studentName, newStart.AddHours(5));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email failed: {ex.Message}");
            }

            TempData["Success"] = "Appointment booked successfully!";

            return RedirectToAction("Dashboard", "Student", new { id = studentId });
        }

    }
}