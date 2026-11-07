using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Numerics;
using TAMS.Data;
using TAMS.Models;
using TAMS.Models.ViewModels;
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
        public IActionResult Dashboard(int? id, bool showTutorReminders = false, bool showAppointmentNotes = false, int? appointmentNotesId = null)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var loginUser = dBContext.Login
                .Find(l => l.UserId == tutorId.Value && l.Role == UserRole.Tutor)
                .FirstOrDefault();
            ViewBag.ShowDashboardTour = loginUser != null && !loginUser.HasCompletedTour;

            if (id == null) { return RedirectToAction("Login", "Login"); }
            if (tutorId.Value != id.Value) { return Unauthorized(); }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == id.Value).FirstOrDefault();
            if (tutor == null) { return NotFound(); }

            tutor.Appointments = dBContext.Appointments.Find(a => a.TutorId == tutor.TutorId).ToList();


            var tutorGroupAppointments = dBContext.GroupAppointments
                .Find(g =>
                    g.TutorId == tutor.TutorId &&
                    (g.Status == "Open" || g.Status == "Full" || g.Status == "Completed"))
                .ToList().OrderBy(g => g.AppointmentDate)
                .ThenBy(g => g.StartTime).ToList();

            ViewBag.TutorGroupAppointments = tutorGroupAppointments;

            ViewBag.ScheduleLinkedGroups = dBContext.GroupAppointments
                .Find(g => g.TutorId == tutor.TutorId && g.Status != "Cancelled" && g.Status != "Rejected")
                .ToList();

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

            if (showAppointmentNotes)
            {
                if (appointmentNotesId == null) { return BadRequest(); }

                var appointment = tutor.Appointments.FirstOrDefault(a =>
                    a.AppointmentId == appointmentNotesId.Value && a.TutorId == tutor.TutorId && 
                    a.Status == "Scheduled" && a.AppointmentDate >= DateTime.Now);

                if (appointment == null) { return NotFound(); }

                appointment.Student = dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId)
                    .FirstOrDefault();

                return PartialView("_TutorAppointmentNotesModal", appointment);
            }

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
                TempData["TutorReminderError"] = "Invalid reminder selected.";
                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            var appointment = await dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId && a.TutorId == tutorId.Value)
                .FirstOrDefaultAsync();

            if (appointment == null)
            {
                TempData["TutorReminderError"] = "Appointment not found.";
                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            if (appointment.Status != "Scheduled")
            {
                TempData["TutorReminderError"] = "This appointment is no longer scheduled.";
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
                    TempData["TutorReminderError"] = "This reminder is not available yet.";
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
                        TempData["TutorReminderError"] = "The 24-hour appointment reminder is not available yet.";

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
                        TempData["TutorReminderError"] = "The 1-hour appointment reminder is not available yet.";

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
                TempData["TutorReminderSuccess"] = successMessage;
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

                TempData["TutorProfileError"] = "Profile was not saved. " + string.Join(" | ", errors);
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
                .Set(t => t.TutoringCourseIds, validCourseIds)
                .Set(t => t.DisableEmailNotifications, model.DisableEmailNotifications);

            var result = dBContext.Tutor.UpdateOne(t => t.TutorId == tutorId.Value, update);

            if (result.MatchedCount == 0)
            {
                TempData["TutorProfileError"] = "Tutor profile was not found in the database.";
                return RedirectToAction(nameof(Profile));
            }
            TempData["TutorProfileSuccess"] = "Your profile has been updated successfully.";
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

            var loginUser = dBContext.Login
                .Find(l => l.UserId == tutorId.Value && l.Role == UserRole.Tutor)
                .FirstOrDefault();
            ViewBag.ShowScheduleTour = loginUser != null && !loginUser.HasCompletedTutorScheduleTour;

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
                TempData["TutorScheduleError"] = "You cannot add a schedule for a date that has already passed.";
                return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
            }

            if (date.Date == today && startTime <= now.TimeOfDay)
            {
                TempData["TutorScheduleError"] = "You cannot add a schedule with a start time that has already passed. Please choose a future time.";
                return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
            }

            if (endTime <= startTime)
            {
                TempData["TutorScheduleError"] = "End time must be after start time.";
                return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
            }

            var existingSchedules = dBContext.Schedule.Find(s => s.TutorId == tutorId).ToList();

            bool overlaps = existingSchedules.Any(s => s.Date.Date == date.Date && startTime < s.EndTime && endTime > s.StartTime );

            if (overlaps)
            {
                TempData["TutorScheduleError"] =
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

            TempData["TutorScheduleSuccess"] = "Schedule added successfully.";
            return RedirectToAction("Schedule", new { id = tutorId, month = date.Month, year = date.Year });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSchedule(int scheduleId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var schedule = dBContext.Schedule
                .Find(s => s.ScheduleId == scheduleId && s.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (schedule == null) { return NotFound(); }

            var tutorAppointments = dBContext.Appointments
                .Find(a => a.TutorId == tutorId.Value && a.Status == "Scheduled")
                .ToList();

            bool hasAppointments = tutorAppointments.Any(a => a.ScheduleId == scheduleId ||
                (a.AppointmentDate.Date == schedule.Date.Date && a.AppointmentDate.TimeOfDay >= schedule.StartTime &&
                 a.AppointmentDate.TimeOfDay < schedule.EndTime));

            if (hasAppointments)
            {
                TempData["TutorDashboardError"] =
                    "You cannot delete this schedule slot because it has " +
                    "a scheduled appointment. Please cancel or remove " +
                    "the associated appointment first.";

                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            var groups = dBContext.GroupAppointments
                .Find(g => g.TutorId == tutorId.Value && g.AppointmentDate >= schedule.Date.Date &&
                           g.AppointmentDate < schedule.Date.Date.AddDays(1))
                .ToList()
                .Where(g => g.StartTime < schedule.EndTime && g.EndTime > schedule.StartTime && g.Status != "Cancelled" && g.Status != "Rejected")
                .ToList();

            if (groups.Any(g => (g.StudentIds?.Count ?? 0) > 0))
            {
                TempData["TutorDashboardError"] =
                    "This schedule slot is connected to a group " +
                    "appointment with enrolled students. " +
                    "You cannot delete this schedule. " +
                    "Please contact an administrator to delete " +
                    "the group appointment for you.";

                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            // Delete associated groups only when no students are enrolled
            foreach (var group in groups)
            {
                var groupFilter = Builders<GroupAppointment>.Filter.And(
                    Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, group.GroupId),
                    Builders<GroupAppointment>.Filter.Eq(g => g.TutorId, tutorId.Value),
                    Builders<GroupAppointment>.Filter.Size(g => g.StudentIds, 0)
                );

                var result = dBContext.GroupAppointments .DeleteOne(groupFilter);

                if (result.DeletedCount == 0)
                {
                    TempData["TutorDashboardError"] =
                        "The group appointment could not be deleted. " +
                        "It may now have enrolled students. " +
                        "Please contact an administrator.";

                    return RedirectToAction("Dashboard", new { id = tutorId.Value });
                }
            }

            // Delete the schedule.
            var deleteResult = dBContext.Schedule.DeleteOne(s => s.ScheduleId == scheduleId && s.TutorId == tutorId.Value);

            if (deleteResult.DeletedCount == 0)
            {
                TempData["TutorDashboardError"] = "The schedule slot could not be deleted.";

                return RedirectToAction("Dashboard", new { id = tutorId.Value });
            }

            TempData["TutorDashboardSuccess"] = groups.Any()
                ? "Schedule slot and associated group appointment deleted successfully."
                : "Schedule deleted successfully.";

            return RedirectToAction("Dashboard",
                new { id = tutorId.Value });
        }

        [HttpGet]
        public IActionResult ScheduleCalendar(int id, int? month, int? year)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            if (tutorId.Value != id) { return Unauthorized(); }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == id).FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            if (targetMonth < 1 || targetMonth > 12 || targetYear < 1 || targetYear > 9999)
            {
                return BadRequest();
            }

            var startOfMonth = new DateTime(targetYear, targetMonth, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            var appointments = dBContext.Appointments
                .Find(a => 
                    a.TutorId == id &&
                    (a.Status == "Scheduled" || a.Status == "Cancelled") && 
                    a.AppointmentDate >= startOfMonth && a.AppointmentDate < endOfMonth)
                .ToList();

            foreach (var appointment in appointments)
            {
                appointment.Student = dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId)
                    .FirstOrDefault();
            }

            var groupAppointments = dBContext.GroupAppointments
                .Find(g => g.TutorId == id && g.AppointmentDate >= startOfMonth && g.AppointmentDate < endOfMonth &&
                    (g.Status == "Open" || g.Status == "Full" || g.Status == "Completed"))
                .ToList();

            var enrolledGroupAppointments = groupAppointments
                .Where(g => g.StudentIds != null && g.StudentIds.Count > 0)
                .OrderBy(g => g.AppointmentDate)
                .ThenBy(g => g.StartTime)
                .ToList();

            ViewBag.TutorId = id;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.Appointments = appointments;
            ViewBag.GroupAppointments = enrolledGroupAppointments;

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
                if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendStudentAppointmentCancelledByTutorAsync(student.Email, studentName, tutorName, appointment.AppointmentDate);
                }

                // Confirm the cancellation to the tutor
                if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
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

                    if (student != null && tutor != null && !student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
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

        [HttpGet]
        public IActionResult AppointmentNotes(int appointmentId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null) { return Unauthorized(); }

            var appointment = dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId && a.TutorId == tutorId.Value
                    && a.Status == "Scheduled" && a.AppointmentDate >= DateTime.Now)
                .FirstOrDefault();

            if (appointment == null) { return NotFound(); }

            appointment.Student = dBContext.Student
                .Find(s => s.StudentId == appointment.StudentId)
                .FirstOrDefault();

            return PartialView("_TutorAppointmentNotesModal", appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveAppointmentNotes(int appointmentId, string? tutorNotes)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null) { return Unauthorized(); }

            if (tutorNotes?.Length > 2000)
            {
                TempData["TutorDashboardError"] = "Notes cannot exceed 2000 characters.";
                return RedirectToAction(nameof(Dashboard), new { id = tutorId.Value });
            }

            var appointment = await dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId && a.TutorId == tutorId.Value
                    && a.Status == "Scheduled" && a.AppointmentDate >= DateTime.Now)
                .FirstOrDefaultAsync();

            if (appointment == null) { return NotFound(); }

            var notes = tutorNotes?.Trim() ?? string.Empty;
            var update = Builders<Appointment>.Update.Set(a => a.TutorNotes, notes);

            await dBContext.Appointments.UpdateOneAsync(a => a.AppointmentId == appointmentId
                    && a.TutorId == tutorId.Value && a.Status == "Scheduled", update);

            TempData["TutorDashboardSuccess"] = "Appointment notes saved successfully.";

            return RedirectToAction(nameof(Dashboard), new { id = tutorId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAppointmentNotes(int appointmentId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null) { return Unauthorized(); }

            var appointment = await dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId && a.TutorId == tutorId.Value
                    && a.Status == "Scheduled" && a.AppointmentDate >= DateTime.Now)
                .FirstOrDefaultAsync();

            if (appointment == null) { return NotFound(); }

            var update = Builders<Appointment>.Update.Set(a => a.TutorNotes, string.Empty);

            await dBContext.Appointments.UpdateOneAsync(a => a.AppointmentId == appointmentId
                    && a.TutorId == tutorId.Value && a.Status == "Scheduled", update);

            TempData["TutorDashboardSuccess"] = "Appointment notes deleted successfully.";

            return RedirectToAction(nameof(Dashboard), new { id = tutorId.Value });
        }

        [HttpGet]
        public IActionResult CreateGroupAppointment()
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            var model = new GroupAppointment
            {
                TutorId = tutorId.Value,
                AppointmentDate = DateTime.Today.AddDays(1),
                Status = "Pending"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateGroupAppointment(GroupAppointment model)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            model.TutorId = tutorId.Value;
            model.AppointmentId = null;
            model.Status = "Pending";
            model.StudentIds = new List<int>();

            ModelState.Remove(nameof(model.GroupId));
            ModelState.Remove(nameof(model.TutorId));
            ModelState.Remove(nameof(model.AppointmentId));
            ModelState.Remove(nameof(model.Status));
            ModelState.Remove(nameof(model.StudentIds));

            DateTime groupStart = model.AppointmentDate.Date.Add(model.StartTime);
            DateTime groupEnd = model.AppointmentDate.Date.Add(model.EndTime);

            if (groupStart <= DateTime.Now)
            {
                ModelState.AddModelError(nameof(model.AppointmentDate), "Please select a future appointment date and start time." );
            }

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError(nameof(model.EndTime), "End time must be after start time.");
            }

            if (model.Capacity < 2 || model.Capacity > 100)
            {
                ModelState.AddModelError(nameof(model.Capacity), "Capacity must be between 2 and 100.");
            }

            if (!ModelState.IsValid) { return View(model); }

            var existingSchedules = dBContext.Schedule.Find(s => s.TutorId == tutorId.Value).ToList();
            foreach (var schedule in existingSchedules)
            {
                if (schedule.IsGroup)
                {
                    continue;
                }

                DateTime scheduleStart = schedule.Date.Date.Add(schedule.StartTime);
                DateTime scheduleEnd = schedule.Date.Date.Add(schedule.EndTime);

                bool overlaps = groupStart < scheduleEnd && groupEnd > scheduleStart;

                if (overlaps)
                {
                    ModelState.AddModelError(string.Empty, "Scheduling Conflict: You already have an available " +
                        "schedule slot on " +
                        scheduleStart.ToString("MMM d, yyyy") +
                        " from " +
                        scheduleStart.ToString("h:mm tt") +
                        " to " +
                        scheduleEnd.ToString("h:mm tt") +
                        ". Please remove the existing time slot or choose a different time."
                    );

                    return View(model);
                }
            }

            var existingAppointments = dBContext.Appointments
                .Find(a => a.TutorId == tutorId.Value && a.Status == "Scheduled")
                .ToList();

            foreach (var appointment in existingAppointments)
            {
                DateTime existingStart = appointment.AppointmentDate;
                DateTime existingEnd = appointment.EndDate;

                if (existingEnd <= existingStart)
                {
                    var appointmentSchedule = dBContext.Schedule
                        .Find(s => s.ScheduleId == appointment.ScheduleId)
                        .FirstOrDefault();

                    if (appointmentSchedule == null)
                    {
                        ModelState.AddModelError(string.Empty, "Unable to verify an existing appointment's time. " + "Please contact the administrator.");
                        return View(model);
                    }

                    existingStart = appointmentSchedule.Date.Date.Add(appointmentSchedule.StartTime);
                    existingEnd = appointmentSchedule.Date.Date.Add(appointmentSchedule.EndTime);
                }

                bool overlaps = groupStart < existingEnd && groupEnd > existingStart;
                if (overlaps)
                {
                    ModelState.AddModelError(string.Empty, "Scheduling Conflict: You already have an individual " + "appointment on " +
                        existingStart.ToString("MMM d, yyyy") +
                        " from " +
                        existingStart.ToString("h:mm tt") +
                        " to " +
                        existingEnd.ToString("h:mm tt") +
                        ". Please select a different time."
                    );

                    return View(model);
                }
            }

            var existingGroups = dBContext.GroupAppointments
                .Find(g => g.TutorId == tutorId.Value &&
                    (g.Status == "Pending" || g.Status == "Open" || g.Status == "Full"))
                .ToList();

            foreach (var existingGroup in existingGroups)
            {
                DateTime existingStart = existingGroup.AppointmentDate.Date.Add(existingGroup.StartTime);
                DateTime existingEnd = existingGroup.AppointmentDate.Date.Add(existingGroup.EndTime);

                bool overlaps = groupStart < existingEnd && groupEnd > existingStart;
                if (overlaps)
                {
                    ModelState.AddModelError(string.Empty, "Scheduling Conflict: You already have a " +
                        existingGroup.Status.ToLower() +
                        " group appointment on " +
                        existingStart.ToString("MMM d, yyyy") +
                        " from " +
                        existingStart.ToString("h:mm tt") +
                        " to " +
                        existingEnd.ToString("h:mm tt") +
                        ". Please choose another time."
                    );

                    return View(model);
                }
            }

            var lastGroup = dBContext.GroupAppointments
                .Find(_ => true)
                .SortByDescending(g => g.GroupId)
                .FirstOrDefault();

            model.GroupId = lastGroup == null ? 1 : lastGroup.GroupId + 1;

            dBContext.GroupAppointments.InsertOne(model);

            TempData["SuccessMessage"] = "Your group appointment request has been submitted for admin approval.";

            return RedirectToAction("Dashboard", new { id = tutorId.Value });
        }

        [HttpGet]
        public async Task<IActionResult> GroupDetails(int groupId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var group = await dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId && g.TutorId == tutorId.Value)
                .FirstOrDefaultAsync();

            if (group == null) { return NotFound(); }

            var studentIds = group.StudentIds ?? new List<int>();

            var students = await dBContext.Student
                .Find(s => studentIds.Contains(s.StudentId))
                .ToListAsync();

            var completedIds = group.CompletedStudentIds ?? new List<int>();
            var absentIds = group.AbsentStudentIds ?? new List<int>();

            var model = new TutorGroupDetailsViewModel
            {
                Group = group,
                Students = students.OrderBy(s => s.LName).ThenBy(s => s.FName).ToList(),
                CompletedCount = studentIds.Count(id => completedIds.Contains(id)),
                AbsentCount = studentIds.Count(id => absentIds.Contains(id)),
                PendingCount = studentIds.Count(id => !completedIds.Contains(id) && !absentIds.Contains(id))
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGroupStudentAttendance(int groupId, int studentId, string attendanceStatus)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            if (attendanceStatus != "Completed" && attendanceStatus != "Absent")
            {
                return BadRequest("Invalid attendance status.");
            }

            var group = await dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId && g.TutorId == tutorId.Value)
                .FirstOrDefaultAsync();

            if (group == null) { return NotFound(); }

            if (group.Status != "Open" && group.Status != "Full")
            {
                TempData["GroupDetailsError"] = "Attendance cannot be updated for this group.";
                return RedirectToAction("GroupDetails", new { groupId });
            }

            var sessionEnd = group.AppointmentDate.Date.Add(group.EndTime);

            if (DateTime.Now < sessionEnd)
            {
                TempData["GroupDetailsError"] = "You cannot finalize attendance before the session ends.";
                return RedirectToAction("GroupDetails", new { groupId });
            }

            if (group.StudentIds == null || !group.StudentIds.Contains(studentId))
            {
                TempData["GroupDetailsError"] = "This student is not enrolled in the group.";

                return RedirectToAction("GroupDetails", new { groupId });
            }

            var filter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.TutorId, tutorId.Value),
                Builders<GroupAppointment>.Filter.In(g => g.Status, new[] { "Open", "Full" }),
                Builders<GroupAppointment>.Filter.AnyEq(g => g.StudentIds, studentId),
                Builders<GroupAppointment>.Filter.Ne("CompletedStudentIds", studentId),
                Builders<GroupAppointment>.Filter.Ne("AbsentStudentIds", studentId)
            );

            var update = attendanceStatus == "Completed"
                ? Builders<GroupAppointment>.Update.AddToSet(g => g.CompletedStudentIds, studentId)
                : Builders<GroupAppointment>.Update.AddToSet(g => g.AbsentStudentIds, studentId);

            var result = await dBContext.GroupAppointments.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
            {
                TempData["GroupDetailsError"] = "The student's attendance was already finalized " + "or the group is no longer active.";
                return RedirectToAction("GroupDetails", new { groupId });
            }

            var updatedGroup = await dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId)
                .FirstOrDefaultAsync();

            if (updatedGroup != null)
            {
                var enrolled = updatedGroup.StudentIds ?? new List<int>();
                var completed = updatedGroup.CompletedStudentIds ?? new List<int>();
                var absent = updatedGroup.AbsentStudentIds ?? new List<int>();

                bool everyoneProcessed = enrolled.Count > 0 && enrolled.All(id => completed.Contains(id) || absent.Contains(id));

                if (everyoneProcessed)
                {
                    var completeFilter =
                        Builders<GroupAppointment>.Filter.And(
                            Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                            Builders<GroupAppointment>.Filter.In(g => g.Status, new[] { "Open", "Full" })
                        );

                    await dBContext.GroupAppointments.UpdateOneAsync(completeFilter, Builders<GroupAppointment>.Update.Set(g => g.Status, "Completed"));
                }
            }

            if (attendanceStatus == "Completed")
            {
                try
                {
                    var student = await dBContext.Student
                        .Find(s => s.StudentId == studentId)
                        .FirstOrDefaultAsync();

                    var tutor = await dBContext.Tutor
                        .Find(t => t.TutorId == tutorId.Value)
                        .FirstOrDefaultAsync();

                    if (student != null && tutor != null)
                    {
                        var studentName = $"{student.FName} {student.LName}";

                        var tutorName = $"{tutor.FName} {tutor.LName}";

                        if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                        {
                            await _emailService.SendReviewReminderAsync(student.Email, studentName, tutorName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Group review reminder email failed: {ex.Message}");
                }
            }

            TempData["GroupDetailsSuccess"] = attendanceStatus == "Completed" ? "Student marked as completed." : "Student marked as absent.";
            return RedirectToAction("GroupDetails", new { groupId });

        }

        [HttpGet]
        public IActionResult ViewGroupAppointmentNotes(int groupId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return Unauthorized(); }

            var group = dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId && g.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (group == null) { return NotFound(); }

            return PartialView("_TutorGroupAppointmentNotesModal", group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveGroupAppointmentNotes(int groupId, string? tutorNotes)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return Unauthorized(); }

            if (tutorNotes?.Length > 2000)
            {
                TempData["TutorDashboardError"] = "Group appointment notes cannot exceed 2000 characters.";

                return RedirectToAction(nameof(Dashboard), new { id = tutorId.Value });
            }

            var filter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.TutorId, tutorId.Value),
                Builders<GroupAppointment>.Filter.In(g => g.Status,
                    new[] { "Open", "Full", "Completed" })
            );

            var notes = tutorNotes?.Trim() ?? string.Empty;

            var update = Builders<GroupAppointment>.Update.Set(g => g.TutorNotes, notes);

            var result = await dBContext.GroupAppointments.UpdateOneAsync(filter, update);

            if (result.MatchedCount == 0) { return NotFound(); }

            TempData["TutorDashboardSuccess"] = "Group appointment notes saved successfully.";

            return RedirectToAction(nameof(Dashboard), new { id = tutorId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroupAppointmentNotes(int groupId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");
            if (tutorId == null) { return Unauthorized(); }

            var filter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.TutorId, tutorId.Value),
                Builders<GroupAppointment>.Filter.In(g => g.Status,
                    new[] { "Open", "Full", "Completed" })
            );

            var update = Builders<GroupAppointment>.Update.Set(g => g.TutorNotes, string.Empty);

            var result = await dBContext.GroupAppointments.UpdateOneAsync(filter, update);

            if (result.MatchedCount == 0) { return NotFound(); }

            TempData["TutorDashboardSuccess"] = "Group appointment notes deleted successfully.";

            return RedirectToAction(nameof(Dashboard), new { id = tutorId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompletePageTour()
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null) { return Unauthorized(); }

            var update = Builders<Login>.Update.Set(u => u.HasCompletedTutorScheduleTour, true);

            dBContext.Login.UpdateOne(u => u.UserId == tutorId.Value && u.Role == UserRole.Tutor, update);

            return Ok(new { success = true });
        }


    }
}