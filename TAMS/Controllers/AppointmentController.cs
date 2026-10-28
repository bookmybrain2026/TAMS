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
        public IActionResult Scheduler(int tutorId, int? month, int? year, int studentId, int? rescheduleAppointmentId = null)
        {
            var loggedinstudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedinstudentId == null) { return RedirectToAction("Login", "Login"); }

            Appointment? rescheduleAppointment = null;

            if (rescheduleAppointmentId.HasValue)
            {
                rescheduleAppointment = dBContext.Appointments
                    .Find(a => a.AppointmentId == rescheduleAppointmentId.Value && a.StudentId == loggedinstudentId.Value && a.Status == "Scheduled")
                    .FirstOrDefault();

                if (rescheduleAppointment == null) { return NotFound(); }
            }

            studentId = loggedinstudentId.Value;

            var tutor = dBContext.Tutor .Find(t => t.TutorId == tutorId).FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            var startOfMonth = new DateTime( targetYear, targetMonth, 1);
            var startOfNextMonth = startOfMonth.AddMonths(1);

            var appointments = dBContext.Appointments .Find(a =>  a.TutorId == tutorId && a.AppointmentDate >= startOfMonth && a.AppointmentDate < startOfNextMonth) .ToList();

            foreach (var appointment in appointments)
            {
                appointment.Student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            }

            var schedules = dBContext.Schedule.Find(s => s.TutorId == tutorId).ToList().Where(s => s.Date.Month == targetMonth && s.Date.Year == targetYear).ToList();

            ViewBag.Tutor = tutor;
            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.StudentId = loggedinstudentId.Value;

            ViewBag.RescheduleAppointmentId = rescheduleAppointmentId;
            ViewBag.Tutors = dBContext.Tutor.Find(_ => true).ToList();

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
                if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendStudentAppointmentConfirmationAsync(student.Email, studentName, tutorName, newStart.AddHours(5));
                }

                if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleAppointment(int appointmentId, int scheduleId, int tutorId, DateTime date, TimeSpan startTime)
        {
            var loggedInStudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedInStudentId == null) { return RedirectToAction("Login", "Login"); }

            int studentId = loggedInStudentId.Value;

            var oldAppointment = dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId && a.StudentId == studentId && a.Status == "Scheduled")
                .FirstOrDefault();
            if (oldAppointment == null) { return NotFound(); }

            DateTime oldAppointmentDate = oldAppointment.AppointmentDate;
            int oldTutorId = oldAppointment.TutorId;

            var student = dBContext.Student.Find(s => s.StudentId == studentId).FirstOrDefault();
            var newTutor = dBContext.Tutor.Find(t => t.TutorId == tutorId).FirstOrDefault();
            var schedule = dBContext.Schedule.Find(s => s.ScheduleId == scheduleId).FirstOrDefault();

            if (student == null || newTutor == null || schedule == null) { return NotFound(); }

            if (schedule.TutorId != tutorId || schedule.Date.Date != date.Date || schedule.StartTime != startTime)
            {
                TempData["Error"] = "Invalid appointment selection.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }
            
            DateTime newStart = schedule.Date.Date.Add(schedule.StartTime);
            DateTime newEnd = schedule.Date.Date.Add(schedule.EndTime);

            if (newStart <= DateTime.Now || newEnd <= newStart)
            {
                TempData["Error"] = "Please select a valid future appointment time.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            var possibleConflicts = dBContext.Appointments
                .Find(a => a.Status == "Scheduled" && a.AppointmentId != appointmentId && (a.StudentId == studentId || a.TutorId == tutorId)).ToList();

            foreach (var existing in possibleConflicts)
            {
                DateTime existingStart = existing.AppointmentDate;
                DateTime existingEnd = existing.EndDate;

                if (existingEnd <= existingStart)
                {
                    var existingSchedule = dBContext.Schedule.Find(s => s.ScheduleId == existing.ScheduleId).FirstOrDefault();

                    if (existingSchedule == null)
                    {
                        TempData["Error"] = "An existing appointment has invalid time information.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                    }
                    existingStart = existingSchedule.Date.Date.Add(existingSchedule.StartTime);
                    existingEnd = existingSchedule.Date.Date.Add(existingSchedule.EndTime);
                }

                bool overlaps = newStart < existingEnd && newEnd > existingStart;
                if (overlaps)
                {
                    TempData["Error"] = existing.StudentId == studentId
                        ? "You already have an appointment during this time."
                        : "This tutor already has an appointment during this time.";
                    return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                }
            }

            var slotBooking = dBContext.Appointments
                .Find(a => a.ScheduleId == scheduleId && a.Status == "Scheduled" && a.AppointmentId != appointmentId)
                .FirstOrDefault();

            if (slotBooking != null)
            {
                TempData["Error"] = "This time slot has already been booked. Please select another.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            bool sameTutor = oldTutorId == tutorId;
            Tutor? previousTutor = null; //only needed for tutor change
            if (!sameTutor)
            {
                previousTutor = dBContext.Tutor.Find(t => t.TutorId == oldTutorId).FirstOrDefault();
                if (previousTutor == null) { return NotFound(); }
            }

            if (sameTutor)
            {
                var update = Builders<Appointment>.Update
                    .Set(a => a.ScheduleId, schedule.ScheduleId)
                    .Set(a => a.TutorId, tutorId)
                    .Set(a => a.StudentId, studentId)
                    .Set(a => a.AppointmentDate, newStart)
                    .Set(a => a.EndDate, newEnd)
                    .Set(a => a.Status, "Scheduled")
                    .Set(a => a.Reminder24HoursBeforeSent, false)
                    .Set(a => a.Reminder1HourBeforeSent, false)
                    .Set(a => a.StudentReminder24HoursAcknowledged, false)
                    .Set(a => a.StudentReminder1HourAcknowledged, false);

                var updateResult = dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId && a.StudentId == studentId && a.Status == "Scheduled", update);

                if (updateResult.MatchedCount != 1)
                {
                    TempData["Error"] = "The appointment could not be rescheduled. Please try again.";
                    return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                }
            }
            else
            {
                // create a separate appointment with the newly selected tutor and original appointment is cancelled.
                var lastAppointment = dBContext.Appointments.Find(_ => true).SortByDescending(a => a.AppointmentId).FirstOrDefault();
                var replacement = new Appointment
                {
                    AppointmentId = lastAppointment == null ? 1 : lastAppointment.AppointmentId + 1,
                    ScheduleId = schedule.ScheduleId,
                    TutorId = tutorId,
                    StudentId = studentId,
                    AppointmentDate = newStart,
                    EndDate = newEnd,
                    Status = "Scheduled"
                };
                dBContext.Appointments.InsertOne(replacement);

                var cancelResult = dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId && a.StudentId == studentId && a.Status == "Scheduled",
                    Builders<Appointment>.Update.Set(a => a.Status, "Cancelled"));

                if (cancelResult.ModifiedCount != 1)
                {
                    // if the original appointment could not be cancelled after inserting the replacement delete it
                    dBContext.Appointments.DeleteOne( a => a.AppointmentId == replacement.AppointmentId && a.StudentId == studentId && a.Status == "Scheduled");
                    TempData["Error"] = "The original appointment changed while rescheduling. Please try again.";
                    return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                }
            }

            string studentName = $"{student.FName} {student.LName}";
            string newTutorName = $"{newTutor.FName} {newTutor.LName}";

            try
            {
                if (sameTutor)
                {
                    //if (!string.IsNullOrWhiteSpace(student.Email))
                    if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await _emailService.SendStudentAppointmentRescheduledAsync(student.Email, studentName, newTutorName, newStart.AddHours(0));
                    }

                    //if (!string.IsNullOrWhiteSpace(newTutor.Email))
                    if (!newTutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(newTutor.Email))
                    {
                        await _emailService.SendTutorAppointmentRescheduledAsync(newTutor.Email, newTutorName, studentName, newStart.AddHours(5));
                    }
                }
                else
                {
                    // Confirm the replacement appointment to the student.
                    if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await _emailService.SendStudentAppointmentConfirmationAsync(student.Email, studentName, newTutorName, newStart.AddHours(5));
                    }

                    // Notify the previous tutor about the cancelled appointment
                    if (previousTutor != null && !previousTutor.DisableEmailNotifications &&
                        !string.IsNullOrWhiteSpace(previousTutor.Email))
                    {
                        await _emailService.SendTutorAppointmentCancellationAsync(previousTutor.Email, $"{previousTutor.FName} {previousTutor.LName}", studentName, oldAppointmentDate.AddHours(5));
                    }

                    // Notify the newly selected tutor about the new appointment
                    if (!newTutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(newTutor.Email))
                    {
                        await _emailService.SendTutorAppointmentNotificationAsync(newTutor.Email, newTutorName, studentName, newStart.AddHours(5));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rescheduling email error: {ex.Message}");
            }

            TempData["Success"] = sameTutor
                ? "Your appointment has been rescheduled with the same tutor."
                : "Your previous appointment was cancelled and a new appointment was scheduled.";

            return RedirectToAction("Dashboard", "Student", new { id = studentId });
        }


    }
}