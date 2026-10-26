using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Numerics;
using TAMS.Data;
using TAMS.Models;
using TAMS.Services;

namespace TAMS.Controllers
{
    public class TutorController : Controller
    {
        private readonly MongoDBContext dBContext;
        private readonly IEmailService _emailService;

        public TutorController(MongoDBContext dBContext, IEmailService emailService)
        {
            this.dBContext = dBContext;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult OurTutors()
        {
            var tutors = dBContext.Tutor.Find(_ => true).SortBy(t => t.LName).ThenBy(t => t.FName).ToList();

            foreach (var tutor in tutors)
            {
                tutor.Reviews = dBContext.Reviews.Find(r => r.TutorId == tutor.TutorId).ToList();
                tutor.AverageRating = tutor.Reviews.Any() ? tutor.Reviews.Average(r => r.Rating) : 0;
                tutor.ReviewCount = tutor.Reviews.Count;
            }
            return View(tutors);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == id)
                .FirstOrDefault();

            if (tutor == null)
            {
                return NotFound();
            }

            tutor.Reviews = dBContext.Reviews
                .Find(r => r.TutorId == tutor.TutorId)
                .ToList();

            tutor.AverageRating = tutor.Reviews.Any() ? tutor.Reviews.Average(r => r.Rating) : 0;
            tutor.ReviewCount = tutor.Reviews.Count;

            var courseIds = tutor.TutoringCourseIds ?? new List<string>();

            var courses = dBContext.Course
                .Find(c => c.CourseId != null && courseIds.Contains(c.CourseId))
                .SortBy(c => c.CourseCode)
                .ToList();

            var subject = dBContext.Subject
                .Find(s => s.Code == tutor.Subject)
                .FirstOrDefault();

            ViewBag.Courses = courses;
            ViewBag.SubjectName = subject?.Name ?? tutor.Subject;

            return View(tutor);
        }

        [HttpGet]
        public IActionResult Dashboard(int? id, bool showTutorReminders = false)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            if (id == null) { return RedirectToAction("Login", "Login"); }
            if (tutorId.Value != id.Value) { return Unauthorized(); }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == id.Value).FirstOrDefault();
            if (tutor == null) { return NotFound(); }

            tutor.Appointments = dBContext.Appointments.Find(a => a.TutorId == tutor.TutorId).ToList();


            var now = DateTime.Now;

            var tutorReminderAppointments = tutor.Appointments
                .Where(a => a.Status == "Scheduled")
                .Where(a =>
                    // Completion reminders
                    (now >= a.EndDate.AddHours(1) && !a.TutorReminder1Acknowledged) ||
                    (now >= a.EndDate.AddHours(24) && !a.TutorReminder2Acknowledged) ||
                    (now >= a.EndDate.AddHours(48) && !a.TutorReminder3Acknowledged) ||

                    // Upcoming appointment reminders
                    (a.Reminder24HoursBeforeSent && !a.TutorAppointmentReminder24HoursAcknowledged) ||
                    (a.Reminder1HourBeforeSent && !a.TutorAppointmentReminder1HourAcknowledged)
                )
                .OrderBy(a => a.AppointmentDate)
                .ToList();

            ViewBag.TutorReminderAppointments = tutorReminderAppointments;
            ViewBag.ShowTutorReminders = showTutorReminders;


            foreach (var appointment in tutor.Appointments)
            {
                appointment.Student = dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId)
                    .FirstOrDefault();
            }

            tutor.Schedules = dBContext.Schedule.Find(s => s.TutorId == tutor.TutorId).ToList();
            tutor.Reviews = dBContext.Reviews.Find(r => r.TutorId == tutor.TutorId).ToList();
            tutor.AverageRating = tutor.Reviews.Count > 0 ? tutor.Reviews.Average(r => r.Rating) : 0;
            tutor.ReviewCount = tutor.Reviews.Count;

            ViewBag.Appointments = tutor.Appointments;
            ViewBag.Schedules = tutor.Schedules;

            if (showTutorReminders) { return PartialView("_TutorReminderModal", tutor); }
            return View("TutorDashboard", tutor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmTutorReminder(int appointmentId, int? reminderHours, string? reminderType)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var isCompletionReminder = reminderHours == 1 || reminderHours == 24 || reminderHours == 48;
            var isAppointmentReminder = reminderType == "24Hours" || reminderType == "1Hour";

            if (!isCompletionReminder && !isAppointmentReminder)
            {
                TempData["ErrorMessage"] = "Invalid reminder selected.";
                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            var appointment = await dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId && a.TutorId == tutorId.Value)
                .FirstOrDefaultAsync();

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            if (appointment.Status != "Scheduled")
            {
                TempData["ErrorMessage"] = "This appointment is no longer scheduled.";
                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            var filter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointmentId)
                & Builders<Appointment>.Filter.Eq(a => a.TutorId, tutorId.Value)
                & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled");
            UpdateDefinition<Appointment> update;

            string successMessage;

            if (isCompletionReminder)
            {
                if (DateTime.Now < appointment.EndDate.AddHours(reminderHours!.Value))
                {
                    TempData["ErrorMessage"] = "This reminder is not available yet.";
                    return RedirectToAction("Dashboard", new { id = tutorId.Value });
                }

                switch (reminderHours.Value)
                {
                    case 1:
                        filter &= Builders<Appointment>.Filter.Ne(a => a.TutorReminder1Acknowledged, true);
                        update = Builders<Appointment>.Update.Set(a => a.TutorReminder1Acknowledged, true);
                        break;

                    case 24:
                        filter &= Builders<Appointment>.Filter.Ne(a => a.TutorReminder2Acknowledged, true);
                        update = Builders<Appointment>.Update.Set(a => a.TutorReminder2Acknowledged, true);
                        break;

                    default: // 48 hours
                        filter &= Builders<Appointment>.Filter.Ne(a => a.TutorReminder3Acknowledged, true);
                        update = Builders<Appointment>.Update.Set(a => a.TutorReminder3Acknowledged, true);
                        break;
                }

                successMessage = $"{reminderHours}-hour completion reminder confirmed.";
            }
            else
            {
                if (reminderType == "24Hours")
                {
                    if (!appointment.Reminder24HoursBeforeSent)
                    {
                        TempData["ErrorMessage"] = "The 24-hour appointment reminder is not available yet.";

                        return RedirectToAction("Dashboard", new { id = tutorId.Value });
                    }

                    filter &= Builders<Appointment>.Filter.Ne(a => a.TutorAppointmentReminder24HoursAcknowledged, true);
                    update = Builders<Appointment>.Update.Set(a => a.TutorAppointmentReminder24HoursAcknowledged, true);

                    successMessage = "24-hour appointment reminder confirmed.";
                }
                else
                {
                    if (!appointment.Reminder1HourBeforeSent)
                    {
                        TempData["ErrorMessage"] = "The 1-hour appointment reminder is not available yet.";

                        return RedirectToAction("Dashboard", new { id = tutorId.Value });
                    }

                    filter &= Builders<Appointment>.Filter.Ne(a => a.TutorAppointmentReminder1HourAcknowledged, true);
                    update = Builders<Appointment>.Update.Set(a => a.TutorAppointmentReminder1HourAcknowledged, true);

                    successMessage = "1-hour appointment reminder confirmed.";
                }
            }

            var result = await dBContext.Appointments.UpdateOneAsync(filter, update);

            if (result.ModifiedCount > 0)
            {
                TempData["SuccessMessage"] = successMessage;
            }
            else
            {
                TempData["InfoMessage"] = "This reminder was already confirmed or the appointment has changed.";
            }

            return RedirectToAction("Dashboard", new { id = tutorId.Value });
        }

        [HttpGet]
        public IActionResult Profile()
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == tutorId.Value).FirstOrDefault();
            if (tutor == null) { return RedirectToAction("Login", "Login"); }

            LoadProfileData();
            return View(tutor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(Tutor model)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var existingTutor = dBContext.Tutor.Find(t => t.TutorId == tutorId.Value).FirstOrDefault();
            if (existingTutor == null) { return NotFound("Tutor profile was not found."); } 

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e =>
                        $"{x.Key}: {e.ErrorMessage} {e.Exception?.Message}"))
                    .ToList();

                TempData["ErrorMessage"] = "Profile was not saved. " + string.Join(" | ", errors);
                LoadProfileData();
                return View(model);
            }

            var selectedCourseIds = model.TutoringCourseIds ?? new List<string>();

            var validCourseIds = dBContext.Course
                .Find(c => c.CourseId != null && selectedCourseIds.Contains(c.CourseId) && c.SubjectCode == model.Subject)
                .ToList()
                .Select(c => c.CourseId!)
                .ToList();

            var update = Builders<Tutor>.Update
                .Set(t => t.FName, model.FName)
                .Set(t => t.LName, model.LName)
                .Set(t => t.Email, model.Email)
                .Set(t => t.PhoneNumber, model.PhoneNumber)
                .Set(t => t.Subject, model.Subject)
                .Set(t => t.TutoringCourseIds, validCourseIds);

            var result = dBContext.Tutor.UpdateOne(t => t.TutorId == tutorId.Value, update);

            if (result.MatchedCount == 0)
            {
                TempData["ErrorMessage"] = "Tutor profile was not found in the database.";
                return RedirectToAction(nameof(Profile));
            }
            TempData["SuccessMessage"] = "Your profile has been updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        private void LoadProfileData()
        {
            ViewBag.Subjects = dBContext.Subject.Find(_ => true).SortBy(s => s.Name).ToList();
            ViewBag.Courses = dBContext.Course.Find(_ => true).SortBy(c => c.CourseCode).ToList();
        }

        [HttpGet]
        public IActionResult Schedule(int id, int? month, int? year)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) {return RedirectToAction("Login", "Login");}
            if (tutorId.Value != id) {return Unauthorized();}
            var tutor = dBContext.Tutor.Find(t => t.TutorId == id).FirstOrDefault();

            if (tutor == null) {return NotFound();}

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            var schedules =  dBContext.Schedule.Find(s => s.TutorId == id && s.Date.Month == targetMonth && s.Date.Year == targetYear).ToList();

            ViewBag.TutorId = id;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.StudentId = HttpContext.Session.GetInt32("StudentId");

            return View(schedules);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddSchedule(int tutorId, DateTime date, TimeSpan startTime, TimeSpan endTime)
        {
            var loggedInTutorId = HttpContext.Session.GetInt32("TutorId");
            if (loggedInTutorId == null) { return RedirectToAction("Login", "Login"); }
            if (loggedInTutorId.Value != tutorId) { return Unauthorized(); }

            var now = DateTime.Now;
            var today = now.Date;

            if (date.Date < today)
            {
                TempData["Error"] = "You cannot add a schedule for a date that has already passed.";
                return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
            }

            if (date.Date == today && startTime <= now.TimeOfDay)
            {
                TempData["Error"] = "You cannot add a schedule with a start time that has already passed. Please choose a future time.";
                return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
            }

            if (endTime <= startTime)
            {
                TempData["Error"] = "End time must be after start time.";
                return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
            }

            var existingSchedules = dBContext.Schedule.Find(s => s.TutorId == tutorId).ToList();

            bool overlaps = existingSchedules.Any(s => s.Date.Date == date.Date && startTime < s.EndTime && endTime > s.StartTime );

            if (overlaps)
            {
                TempData["Error"] =
                    "An existing schedule already overlaps with this time. " +
                    "Please choose a different time or delete the existing schedule first.";

                return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
            }

            var lastSchedule = dBContext.Schedule.Find(_ => true).SortByDescending(s => s.ScheduleId).FirstOrDefault();
            int newScheduleId = lastSchedule == null ? 1 : lastSchedule.ScheduleId + 1;
            var schedule = new Schedule
            {
                ScheduleId = newScheduleId,
                TutorId = tutorId,
                Date = date.Date,
                StartTime = startTime,
                EndTime = endTime
            };
            dBContext.Schedule.InsertOne(schedule);

            TempData["Success"] = "Schedule added successfully.";
            return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSchedule(int scheduleId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var schedule = dBContext.Schedule.Find(s => s.ScheduleId == scheduleId).FirstOrDefault();

            if (schedule == null) { return NotFound(); }
            if (schedule.TutorId != tutorId.Value) { return Unauthorized(); }

            var tutorAppointments = dBContext.Appointments.Find(a => a.TutorId == tutorId.Value).ToList();
            var hasAppointments = tutorAppointments.Any(a =>
            {
                if (a.Status != "Scheduled") { return false; }                 
                var localAppointmentDate = a.AppointmentDate;
                return localAppointmentDate.Date == schedule.Date.Date && localAppointmentDate.TimeOfDay >= schedule.StartTime && localAppointmentDate.TimeOfDay < schedule.EndTime;
            });

            if (hasAppointments)
            {
                TempData["Error"] =
                    "You cannot delete this schedule slot because it has a scheduled appointment. " +
                    "Please cancel or remove the associated appointment first.";
                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }
            dBContext.Schedule.DeleteOne(s => s.ScheduleId == scheduleId && s.TutorId == tutorId.Value);

            TempData["Success"] = "Schedule deleted successfully.";
            return RedirectToAction("Dashboard", new { id = tutorId.Value });
        }

        [HttpGet]
        public IActionResult ScheduleCalendar(int id, int? month, int? year)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            if (tutorId.Value != id) {return Unauthorized(); }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == id).FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            var startOfMonth = new DateTime(targetYear, targetMonth, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var appointments = dBContext.Appointments.Find(a => a.TutorId == id && a.AppointmentDate >= startOfMonth && a.AppointmentDate <= endOfMonth).ToList();

            foreach (var appointment in appointments)
            {
                appointment.Student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            }

            ViewBag.TutorId = id;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.Appointments = appointments;

            return View("ScheduleCalendar");
        }

        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int appointmentId)
        {
            var appointment = dBContext.Appointments.Find(a => a.AppointmentId == appointmentId).FirstOrDefault();
            if (appointment == null) { return NotFound(); }

            if (appointment.Status == "Cancelled")
            {
                return RedirectToAction( "Dashboard", "Tutor",  new { id = appointment.TutorId });
            }

            var student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            var tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();

            if (student == null || tutor == null) { return NotFound(); }

            var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled");
            var result = dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId, update);

            if (result.ModifiedCount == 0)
            {
                return RedirectToAction("Dashboard", "Tutor", new { id = appointment.TutorId });
            }

            var studentName = $"{student.FName} {student.LName}";
            var tutorName = $"{tutor.FName} {tutor.LName}";

            try
            {
                // Notify the student that the tutor cancelled
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendStudentAppointmentCancelledByTutorAsync(student.Email, studentName, tutorName, appointment.AppointmentDate);
                }

                // Confirm the cancellation to the tutor
                if (!string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService.SendTutorCancellationConfirmationAsync(tutor.Email, tutorName, studentName, appointment.AppointmentDate);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cancellation email failed: {ex.Message}");
            }
            return RedirectToAction("Dashboard", "Tutor", new { id = appointment.TutorId });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteAppointment(int appointmentId)
        {
            var appointment = dBContext.Appointments .Find(a => a.AppointmentId == appointmentId).FirstOrDefault();
            if (appointment == null) { return NotFound(); }

            if (appointment.Status != "Completed")
            {
                var update = Builders<Appointment> .Update.Set(a => a.Status, "Completed");

                dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId, update);

                var localAppointmentDate = appointment.AppointmentDate;
                var appointmentDate = localAppointmentDate.Date; 
                var appointmentTime = localAppointmentDate.TimeOfDay;
                
                var schedule = dBContext.Schedule.Find(s => s.TutorId == appointment.TutorId).ToList() .FirstOrDefault(s => s.Date.Date == appointmentDate 
                    && appointmentTime >= s.StartTime && appointmentTime < s.EndTime); 

                if (schedule != null) 
                { 
                    dBContext.Schedule.DeleteOne(s => s.ScheduleId == schedule.ScheduleId && s.TutorId == appointment.TutorId); 
                }

                try
                {
                    var student = dBContext.Student.Find(p => p.StudentId == appointment.StudentId).FirstOrDefault();
                    var tutor = dBContext.Tutor.Find(d => d.TutorId == appointment.TutorId).FirstOrDefault();

                    var studentName = $"{student.FName} {student.LName}";
                    var tutorName = $"{tutor.FName} {tutor.LName}";

                    if (student != null && tutor != null && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await _emailService.SendReviewReminderAsync(student.Email, studentName, tutorName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Review reminder email failed: {ex.Message}");
                }
            }
            return RedirectToAction("Dashboard", "Tutor", new { id = appointment.TutorId });
        }

    }
}