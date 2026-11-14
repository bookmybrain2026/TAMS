using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver; 
using System; 
using TAMS.Data;
using TAMS.Models; 
using TAMS.Services;

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

        private string? GetGroupAppointmentConflict(int studentId, int tutorId, DateTime newStart, DateTime newEnd)
        {
            var groups = dBContext.GroupAppointments
                .Find(g =>
                    (g.Status == "Open" || g.Status == "Full") &&
                    (g.TutorId == tutorId || g.StudentIds.Contains(studentId)))
                .ToList();

            foreach (var group in groups)
            {
                DateTime groupStart = group.AppointmentDate.Date.Add(group.StartTime);
                DateTime groupEnd = group.AppointmentDate.Date.Add(group.EndTime);

                bool overlaps = newStart < groupEnd && newEnd > groupStart;

                if (!overlaps)
                {
                    continue;
                }

                if (group.StudentIds != null && group.StudentIds.Contains(studentId))
                {
                    return "You already have a group appointment during this time.";
                }

                if (group.TutorId == tutorId &&
                    group.StudentIds != null &&
                    group.StudentIds.Count > 0)
                {
                    return "This tutor already has a group appointment during this time.";
                }
            }

            return null;
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


            var groupAppointments = dBContext.GroupAppointments
                .Find(g => g.TutorId == tutorId && (g.Status == "Open" || g.Status == "Full") &&
                    g.AppointmentDate >= startOfMonth && g.AppointmentDate < startOfNextMonth)
                .ToList();

            ViewBag.GroupAppointments = groupAppointments;

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

            if (schedule.IsGroup)
            {
                TempData["AppointmentError"] = "Please use the group enrollment option for group appointments.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            if (schedule.TutorId != tutorId || schedule.Date.Date != date.Date || schedule.StartTime != startTime)
            {
                TempData["AppointmentError"] = "Invalid appointment selection.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            DateTime newStart = schedule.Date.Date.Add(schedule.StartTime);
            DateTime newEnd = schedule.Date.Date.Add(schedule.EndTime);

            string? groupConflict = GetGroupAppointmentConflict(studentId, tutorId, newStart, newEnd);
            if (groupConflict != null)
            {
                TempData["AppointmentError"] = groupConflict;
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            if (newEnd <= newStart)
            {
                TempData["AppointmentError"] = "The selected schedule has an invalid time.";
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
                        TempData["AppointmentError"] = "An existing appointment has an invalid time. " + "Please contact the administrator.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
                    }
                }

                bool overlaps = newStart < existingEnd &&
                    newEnd > existingStart;

                if (overlaps)
                {
                    if (existing.StudentId == studentId)
                    {
                        TempData["AppointmentError"] = "You already have an appointment during this time.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
                    }

                    if (existing.TutorId == tutorId)
                    {
                        TempData["AppointmentError"] = "This tutor already has an appointment during this time.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
                    }
                }
            }

            var existingScheduleBooking = dBContext.Appointments.Find(a => a.ScheduleId == scheduleId && a.Status == "Scheduled").FirstOrDefault();
            if (existingScheduleBooking != null)
            {
                TempData["AppointmentError"] = "This schedule has already been booked.";
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

            TempData["AppointmentSuccess"] = "Appointment booked successfully!";

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

            if (schedule != null && schedule.IsGroup)
            {
                TempData["AppointmentError"] = "Individual appointments cannot be rescheduled into group sessions.";
                return RedirectToAction("Scheduler", new { tutorId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            if (student == null || newTutor == null || schedule == null) { return NotFound(); }

            if (schedule.TutorId != tutorId || schedule.Date.Date != date.Date || schedule.StartTime != startTime)
            {
                TempData["AppointmentError"] = "Invalid appointment selection.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }
            
            DateTime newStart = schedule.Date.Date.Add(schedule.StartTime);
            DateTime newEnd = schedule.Date.Date.Add(schedule.EndTime);

            string? groupConflict = GetGroupAppointmentConflict(studentId, tutorId, newStart, newEnd);

            if (groupConflict != null)
            {
                TempData["AppointmentError"] = groupConflict;
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            if (newStart <= DateTime.Now || newEnd <= newStart)
            {
                TempData["AppointmentError"] = "Please select a valid future appointment time.";
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
                        TempData["AppointmentError"] = "An existing appointment has invalid time information.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                    }
                    existingStart = existingSchedule.Date.Date.Add(existingSchedule.StartTime);
                    existingEnd = existingSchedule.Date.Date.Add(existingSchedule.EndTime);
                }

                bool overlaps = newStart < existingEnd && newEnd > existingStart;
                if (overlaps)
                {
                    TempData["AppointmentError"] = existing.StudentId == studentId
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
                TempData["AppointmentError"] = "This time slot has already been booked. Please select another.";
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
                    TempData["AppointmentError"] = "The appointment could not be rescheduled. Please try again.";
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
                    TempData["AppointmentError"] = "The original appointment changed while rescheduling. Please try again.";
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

            TempData["AppointmentSuccess"] = sameTutor
                ? "Your appointment has been rescheduled with the same tutor."
                : "Your previous appointment was cancelled and a new appointment was scheduled.";

            return RedirectToAction("Dashboard", "Student", new { id = studentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleToGroup(int appointmentId, int groupId)
        {
            var studentId = HttpContext.Session.GetInt32("StudentId");
            if (studentId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var oldAppointment = dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId
                    && a.StudentId == studentId.Value
                    && a.Status == "Scheduled")
                .FirstOrDefault();

            if (oldAppointment == null)
            {
                return NotFound();
            }         

            var group = dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId)
                .FirstOrDefault();

            if (group == null)
            {
                return NotFound();
            }
                
            IActionResult ReturnToScheduler()
            {
                return RedirectToAction("Scheduler", new
                {
                    tutorId = group.TutorId,
                    month = group.AppointmentDate.Month,
                    year = group.AppointmentDate.Year,
                    rescheduleAppointmentId = appointmentId
                });
            }

            var student = dBContext.Student
                .Find(s => s.StudentId == studentId.Value)
                .FirstOrDefault();

            var newTutor = dBContext.Tutor
                .Find(t => t.TutorId == group.TutorId)
                .FirstOrDefault();

            if (student == null || newTutor == null)
            {
                return NotFound();
            }

            var groupStart = group.AppointmentDate.Date.Add(group.StartTime);
            var groupEnd = group.AppointmentDate.Date.Add(group.EndTime);

            if (group.Status != "Open" || group.StudentIds == null || group.StudentIds.Count >= group.Capacity)
            {
                TempData["AppointmentError"] = "This group appointment is no longer available.";
                return ReturnToScheduler();
            }

            if (groupStart <= DateTime.Now || groupEnd <= groupStart)
            {
                TempData["AppointmentError"] = "Please select a valid future group appointment.";
                return ReturnToScheduler();
            }

            var groupSchedule = dBContext.Schedule
                .Find(s => s.IsGroup && s.GroupId == groupId && s.TutorId == group.TutorId)
                .FirstOrDefault();

            if (groupSchedule == null)
            {
                TempData["AppointmentError"] = "The group schedule could not be found.";
                return ReturnToScheduler();
            }

            if (group.StudentIds.Contains(studentId.Value))
            {
                TempData["AppointmentError"] = "You are already enrolled in this group.";
                return ReturnToScheduler();
            }

            var conflicts = dBContext.Appointments
                .Find(a => a.Status == "Scheduled" && a.AppointmentId != appointmentId &&
                    (a.StudentId == studentId.Value || a.TutorId == group.TutorId))
                .ToList();

            foreach (var appointment in conflicts)
            {
                var start = appointment.AppointmentDate;
                var end = appointment.EndDate;

                if (end <= start)
                {
                    var schedule = dBContext.Schedule
                        .Find(s => s.ScheduleId == appointment.ScheduleId)
                        .FirstOrDefault();

                    if (schedule == null)
                    {
                        TempData["AppointmentError"] = "An existing appointment has invalid time information.";
                        return ReturnToScheduler();
                    }

                    start = schedule.Date.Date.Add(schedule.StartTime);
                    end = schedule.Date.Date.Add(schedule.EndTime);
                }

                if (groupStart < end && groupEnd > start)
                {
                    TempData["AppointmentError"] =
                        appointment.StudentId == studentId.Value
                        ? "You already have an appointment during this time."
                        : "This tutor has an individual appointment during this time.";

                    return ReturnToScheduler();
                }
            }

            var otherGroups = dBContext.GroupAppointments
                .Find(g => g.GroupId != groupId &&
                    (g.Status == "Open" || g.Status == "Full") && g.StudentIds.Contains(studentId.Value))
                .ToList();

            foreach (var existingGroup in otherGroups)
            {
                var start = existingGroup.AppointmentDate.Date.Add(existingGroup.StartTime);
                var end = existingGroup.AppointmentDate.Date.Add(existingGroup.EndTime);

                if (groupStart < end && groupEnd > start)
                {
                    TempData["AppointmentError"] = "You already have a group appointment during this time.";
                    return ReturnToScheduler();
                }
            }

            var capacityFilter = 
                new BsonDocumentFilterDefinition<GroupAppointment>(
                    new BsonDocument("$expr",
                        new BsonDocument("$lt", new BsonArray
                    {
                            new BsonDocument("$size", "$StudentIds"), "$Capacity"
                    }
                        )));

            var filter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.Status, "Open"),
                Builders<GroupAppointment>.Filter.Not(
                Builders<GroupAppointment>.Filter.AnyEq(
                    g => g.StudentIds, studentId.Value)), capacityFilter
            );

            var enrollResult = dBContext.GroupAppointments.UpdateOne(filter,
                Builders<GroupAppointment>.Update.AddToSet(g => g.StudentIds, studentId.Value));

            if (enrollResult.ModifiedCount != 1)
            {
                TempData["AppointmentError"] = "The group may be full. Please select another.";
                return ReturnToScheduler();
            }

            var cancelResult = dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId &&
                     a.StudentId == studentId.Value && a.Status == "Scheduled",
                Builders<Appointment>.Update.Set(a => a.Status, "Cancelled"));

            if (cancelResult.ModifiedCount != 1)
            {
                dBContext.GroupAppointments.UpdateOne(g => g.GroupId == groupId,
                    Builders<GroupAppointment>.Update.Pull(g => g.StudentIds, studentId.Value));
                TempData["AppointmentError"] = "Rescheduling failed. Please try again.";
                return ReturnToScheduler();
            }


            var fullFilter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.Status, "Open"),
                new BsonDocumentFilterDefinition<GroupAppointment>(
                    new BsonDocument(
                        "$expr",
                        new BsonDocument(
                            "$gte",
                            new BsonArray
                            {
                    new BsonDocument("$size", "$StudentIds"),
                    "$Capacity"
                            }
                        )
                    )
                )
            );

            dBContext.GroupAppointments.UpdateOne(fullFilter,
                Builders<GroupAppointment>.Update.Set(g => g.Status, "Full"));

            var oldTutor = dBContext.Tutor
                .Find(t => t.TutorId == oldAppointment.TutorId)
                .FirstOrDefault();

            try
            {
                if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService
                        .SendGroupAppointmentConfirmationToStudentAsync(
                            student.Email,
                            student.FName,
                            newTutor.FName,
                            group.AppointmentDate,
                            group.StartTime,
                            group.EndTime);
                }

                if (!newTutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(newTutor.Email))
                {
                    await _emailService
                        .SendGroupAppointmentConfirmationToTutorAsync(
                            newTutor.Email,
                            newTutor.FName,
                            student.FName,
                            group.AppointmentDate,
                            group.StartTime,
                            group.EndTime);
                }

                if (oldTutor != null && !oldTutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(oldTutor.Email))
                {
                    await _emailService
                        .SendTutorAppointmentCancellationAsync(
                            oldTutor.Email,
                            $"{oldTutor.FName} {oldTutor.LName}",
                            $"{student.FName} {student.LName}",
                            oldAppointment.AppointmentDate.AddHours(5));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Group rescheduling email error: {ex.Message}");
            }

            TempData["SuccessMessage"] = "Your individual appointment was successfully rescheduled to a group appointment.";

            return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinGroupAppointment(int groupId)
        {
            var studentId = HttpContext.Session.GetInt32("StudentId");
            if (studentId == null) { return RedirectToAction("Login", "Login"); }

            var student = dBContext.Student
                .Find(s => s.StudentId == studentId.Value)
                .FirstOrDefault();

            if (student == null) { return NotFound(); }

            var group = dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId)
                .FirstOrDefault();

            if (group == null) { return NotFound(); }

            IActionResult ReturnToScheduler()
            {
                return RedirectToAction("Scheduler", new { tutorId = group.TutorId, studentId = studentId.Value, month = group.AppointmentDate.Month, year = group.AppointmentDate.Year });
            }

            if (group.Status != "Open")
            {
                TempData["AppointmentError"] = "This group is no longer available.";
                return ReturnToScheduler();
            }

            var groupStart = group.AppointmentDate.Date.Add(group.StartTime);
            var groupEnd = group.AppointmentDate.Date.Add(group.EndTime);

            if (groupStart <= DateTime.Now || groupEnd <= groupStart)
            {
                TempData["AppointmentError"] = "This group appointment has already started or has an invalid time.";
                return ReturnToScheduler();
            }

            var groupSchedule = dBContext.Schedule
                .Find(s => s.IsGroup && s.GroupId == groupId && s.TutorId == group.TutorId)
                .FirstOrDefault();

            if (groupSchedule == null)
            {
                TempData["AppointmentError"] = "The approved group schedule could not be found.";
                return ReturnToScheduler();
            }

            if (group.StudentIds != null && group.StudentIds.Contains(studentId.Value))
            {
                TempData["AppointmentError"] = "You have already joined this group.";
                return ReturnToScheduler();
            }

            var conflictingAppointments = dBContext.Appointments
                .Find(a => a.Status == "Scheduled" &&
                    (a.StudentId == studentId.Value || a.TutorId == group.TutorId))
                .ToList();

            foreach (var appointment in conflictingAppointments)
            {
                DateTime existingStart = appointment.AppointmentDate;
                DateTime existingEnd = appointment.EndDate;

                if (existingEnd <= existingStart)
                {
                    var existingSchedule = dBContext.Schedule
                        .Find(s => s.ScheduleId == appointment.ScheduleId)
                        .FirstOrDefault();

                    if (existingSchedule == null)
                    {
                        TempData["AppointmentError"] = "An existing appointment has invalid time information.";
                        return ReturnToScheduler();
                    }

                    existingStart = existingSchedule.Date.Date.Add(existingSchedule.StartTime);
                    existingEnd = existingSchedule.Date.Date.Add(existingSchedule.EndTime);
                }

                // Check if the times overlap
                bool overlaps = groupStart < existingEnd && groupEnd > existingStart;
                if (overlaps)
                {
                    if (appointment.StudentId == studentId.Value)
                    {
                        TempData["AppointmentError"] = "You already have an individual appointment during this time.";
                    }
                    else
                    {
                        TempData["AppointmentError"] = "This tutor already has an individual appointment during this time.";
                    }
                    return ReturnToScheduler();
                }
            }

            var enrolledGroups = dBContext.GroupAppointments
                .Find(g => g.GroupId != groupId && (g.Status == "Open" || g.Status == "Full") && g.StudentIds.Contains(studentId.Value))
                .ToList();

            bool groupConflict = enrolledGroups.Any(g =>
            {
                var start = g.AppointmentDate.Date.Add(g.StartTime);
                var end = g.AppointmentDate.Date.Add(g.EndTime);
                return start < groupEnd && end > groupStart;
            });

            if (groupConflict)
            {
                TempData["AppointmentError"] = "You are already enrolled in another group during this time.";
                return ReturnToScheduler();
            }

            var capacityFilter = new BsonDocument("$expr",
                new BsonDocument("$lt", new BsonArray {
                    new BsonDocument("$size", "$StudentIds"),
                    "$Capacity"
                }));

            var studentNotEnrolledFilter =
                new BsonDocumentFilterDefinition<GroupAppointment>(
                    new BsonDocument("StudentIds",
                        new BsonDocument("$ne", studentId.Value))
                );

            var filter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.Status, "Open"),
                studentNotEnrolledFilter,
                new BsonDocumentFilterDefinition<GroupAppointment>(capacityFilter)
            );

            var update = Builders<GroupAppointment>.Update.AddToSet(g => g.StudentIds, studentId.Value);
            var result = dBContext.GroupAppointments.UpdateOne(filter, update);
            if (result.ModifiedCount == 0)
            {
                TempData["AppointmentError"] = "Unable to join. The group may be full or you may already be enrolled.";
                return ReturnToScheduler();
            }

            var fullFilter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.Status, "Open"),
                new BsonDocumentFilterDefinition<GroupAppointment>(
                    new BsonDocument("$expr",
                        new BsonDocument("$gte", new BsonArray
                        {
                            new BsonDocument("$size", "$StudentIds"), "$Capacity"
                        })))
            );

            dBContext.GroupAppointments.UpdateOne(fullFilter, Builders<GroupAppointment>.Update.Set(g => g.Status, "Full"));

            var tutor = dBContext.Tutor
            .Find(t => t.TutorId == group.TutorId)
            .FirstOrDefault();

            if (tutor != null)
            {
                try
                {
                    if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await _emailService.SendGroupAppointmentConfirmationToStudentAsync(student.Email, student.FName, tutor.FName, group.AppointmentDate, group.StartTime, group.EndTime);
                    }

                    if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
                    {
                        await _emailService.SendGroupAppointmentConfirmationToTutorAsync(tutor.Email, tutor.FName, student.FName, group.AppointmentDate, group.StartTime, group.EndTime);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Group confirmation email failed: {ex.Message}");
                }
            }

            TempData["SuccessMessage"] = "You have successfully joined the group appointment.";

            return RedirectToAction("Dashboard", "Student", new { id = studentId });
            //return ReturnToScheduler();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelGroupAppointment(int groupId)
        {
            var studentId = HttpContext.Session.GetInt32("StudentId");
            if (studentId == null) { return RedirectToAction("Login", "Login"); }

            var student = dBContext.Student
                .Find(s => s.StudentId == studentId.Value)
                .FirstOrDefault();

            if (student == null) { return NotFound(); }

            // Find the group appointment.
            var group = dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId)
                .FirstOrDefault();

            if (group == null)
            {
                TempData["AppointmentError"] = "Group appointment not found.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            // Check that the student is enrolled
            if (group.StudentIds == null || !group.StudentIds.Contains(studentId.Value))
            {
                TempData["AppointmentError"] = "You are not enrolled in this group appointment.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            // Only allow cancellation for upcoming sessions
            var groupStart = group.AppointmentDate.Date.Add(group.StartTime);

            if (groupStart <= DateTime.Now)
            {
                TempData["AppointmentError"] = "You cannot cancel a group appointment that has already started.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            var filter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.In(g => g.Status, new[] { "Open", "Full" }),
                Builders<GroupAppointment>.Filter.AnyEq(g => g.StudentIds, studentId.Value)
            );

            var update = Builders<GroupAppointment>.Update.Pull(g => g.StudentIds, studentId.Value);

            var result = dBContext.GroupAppointments.UpdateOne(filter, update);

            if (result.ModifiedCount == 0)
            {
                TempData["AppointmentError"] = "Unable to cancel your enrollment. " + "The group may have changed.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            var reopenFilter = Builders<GroupAppointment>.Filter.And(
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId),
                Builders<GroupAppointment>.Filter.Eq(g => g.Status, "Full"),
                new BsonDocumentFilterDefinition<GroupAppointment>(
                    new BsonDocument("$expr",
                        new BsonDocument("$lt", new BsonArray
                        {
                            new BsonDocument("$size", "$StudentIds"), "$Capacity"
                        }))
            ));

            dBContext.GroupAppointments.UpdateOne(reopenFilter, Builders<GroupAppointment>.Update.Set(g => g.Status, "Open"));

            var tutor = dBContext.Tutor
            .Find(t => t.TutorId == group.TutorId)
            .FirstOrDefault();

            if (tutor != null)
            {
                try
                {
                    if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await _emailService.SendGroupAppointmentCancellationToStudentAsync(student.Email, student.FName, tutor.FName, group.AppointmentDate, group.StartTime, group.EndTime);
                    }

                    if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
                    {
                        await _emailService.SendGroupAppointmentCancellationToTutorAsync(tutor.Email, tutor.FName, student.FName, group.AppointmentDate, group.StartTime, group.EndTime);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Group cancellation email failed: {ex.Message}");
                }
            }

            TempData["SuccessMessage"] = "You have successfully cancelled your group appointment enrollment.";

            return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Groups()
        {
            int? studentId = HttpContext.Session.GetInt32("StudentId");
            var now = DateTime.Now;

            var openGroups = dBContext.GroupAppointments
                .Find(g => g.Status == "Open" && g.AppointmentDate >= now.Date)
                .ToList()
                .Where(g => g.AppointmentDate.Date.Add(g.StartTime) > now)
                .OrderBy(g => g.AppointmentDate)
                .ThenBy(g => g.StartTime)
                .ToList();

            var tutorIds = openGroups
                .Select(g => g.TutorId)
                .Distinct()
                .ToList();

            var tutors = dBContext.Tutor
                .Find(t => tutorIds.Contains(t.TutorId))
                .ToList();

            ViewBag.Tutors = tutors;
            ViewBag.StudentId = studentId;

            return View(openGroups);
        }


    }
}
