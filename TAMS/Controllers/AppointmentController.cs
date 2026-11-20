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
            //find group appointment that are open or full that invold tutor or student
            var groups = dBContext.GroupAppointments
                .Find(group =>
                    (group.Status == "Open" || group.Status == "Full") &&
                    (group.TutorId == tutorId || group.StudentIds.Contains(studentId)))
                .ToList();

            //take that group 
            foreach (var group in groups)
            {
                //get start and end time
                DateTime groupStart = group.AppointmentDate.Date.Add(group.StartTime);
                DateTime groupEnd = group.AppointmentDate.Date.Add(group.EndTime);

                //check if there is overlap
                bool overlaps = newStart < groupEnd && newEnd > groupStart;

                if (!overlaps) //continue if tno overlap
                {
                    continue;
                }

                //has overlap
                if (group.StudentIds != null && group.StudentIds.Contains(studentId))
                {
                    return "You already have a group appointment during this time.";
                }

                if (group.TutorId == tutorId && group.StudentIds != null && group.StudentIds.Count > 0)
                {
                    return "This tutor already has a group appointment during this time.";
                }
            }
            //no overlap
            return null;
        }

        [HttpGet]
        public IActionResult Scheduler(int tutorId, int? month, int? year, int studentId, int? rescheduleAppointmentId = null)
        {
            var loggedinstudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedinstudentId == null) 
            { 
                return RedirectToAction("Login", "Login"); 
            }

            Appointment? rescheduleAppointment = null;

            //validate existing appointment
            if (rescheduleAppointmentId.HasValue)
            {
                rescheduleAppointment = dBContext.Appointments
                    .Find(appointment => appointment.AppointmentId == rescheduleAppointmentId.Value 
                        && appointment.StudentId == loggedinstudentId.Value 
                        && appointment.Status == "Scheduled")
                    .FirstOrDefault();

                if (rescheduleAppointment == null) { return NotFound(); }
            }

            studentId = loggedinstudentId.Value;

            var tutor = dBContext.Tutor //load tutors
                .Find(tutor => tutor.TutorId == tutorId).FirstOrDefault();

            if (tutor == null) //404 if tutor is null
            { 
                return NotFound(); 
            }

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            var startOfMonth = new DateTime( targetYear, targetMonth, 1); //new DateTime(year, month, day);
            var startOfNextMonth = startOfMonth.AddMonths(1);

            var appointments = dBContext.Appointments //load all appointments of a specific month associated with a tutor
                .Find(appointment => appointment.TutorId == tutorId 
                    && appointment.AppointmentDate >= startOfMonth 
                    && appointment.AppointmentDate < startOfNextMonth) 
                .ToList();

            foreach (var appointment in appointments) //add student info
            {
                appointment.Student = dBContext.Student
                    .Find(student => student.StudentId == appointment.StudentId).FirstOrDefault();
            }

            var schedules = dBContext.Schedule //load tutor schedules of a specific month
                .Find(s => s.TutorId == tutorId)
                .ToList()
                .Where(s => s.Date.Month == targetMonth 
                    && s.Date.Year == targetYear)
                .ToList();

            var groupAppointments = dBContext.GroupAppointments //load group appointments of tutor in a specific month
                .Find(group => group.TutorId == tutorId 
                    && (group.Status == "Open" || group.Status == "Full") 
                    && group.AppointmentDate >= startOfMonth 
                    && group.AppointmentDate < startOfNextMonth)
                .ToList();

            ViewBag.GroupAppointments = groupAppointments;
            ViewBag.Tutor = tutor;
            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.StudentId = loggedinstudentId.Value;
            ViewBag.RescheduleAppointmentId = rescheduleAppointmentId;
            ViewBag.Tutors = dBContext.Tutor.Find(tutor => true).ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment(int scheduleId, int tutorId, DateTime date, TimeSpan startTime, int studentId)
        {
            var loggedInStudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedInStudentId == null) 
            { 
                return RedirectToAction("Login", "Login"); 
            }

            studentId = loggedInStudentId.Value;

            var tutor = dBContext.Tutor
                .Find(tutor => tutor.TutorId == tutorId).FirstOrDefault();
            var student = dBContext.Student
                .Find(student => student.StudentId == studentId).FirstOrDefault();
            var schedule = dBContext.Schedule
                .Find(schedule => schedule.ScheduleId == scheduleId).FirstOrDefault();

            if (tutor == null || student == null || schedule == null)
            { 
                return NotFound(); 
            }

            //prevent individual bookings for group schedules
            if (schedule.IsGroup)
            {
                TempData["AppointmentError"] = "Please use the group enrollment option for group appointments.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            //selected schedule matches the requested tutor, date and time?
            if (schedule.TutorId != tutorId || schedule.Date.Date != date.Date || schedule.StartTime != startTime)
            {
                TempData["AppointmentError"] = "Invalid appointment selection.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            DateTime newStart = schedule.Date.Date.Add(schedule.StartTime);
            DateTime newEnd = schedule.Date.Date.Add(schedule.EndTime);

            //check for conflicts
            string? groupConflict = GetGroupAppointmentConflict(studentId, tutorId, newStart, newEnd);
            if (groupConflict != null)
            {
                TempData["AppointmentError"] = groupConflict;
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            if (newEnd <= newStart) //valid time?
            {
                TempData["AppointmentError"] = "The selected schedule has an invalid time.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year });
            }

            var possibleConflicts = dBContext.Appointments
                .Find(appointment => appointment.Status == "Scheduled" 
                    && (appointment.StudentId == studentId || appointment.TutorId == tutorId)).ToList();

            foreach (var existing in possibleConflicts)
            {
                DateTime existingStart = existing.AppointmentDate;
                DateTime existingEnd = existing.EndDate;

                if (existingEnd <= existingStart)
                {
                    var oldSchedule = dBContext.Schedule
                        .Find(schedule => schedule.ScheduleId == existing.ScheduleId).FirstOrDefault();

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

                //checl for overlap
                bool overlaps = newStart < existingEnd && newEnd > existingStart;

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

            // schedule has not been booked?
            var existingScheduleBooking = dBContext.Appointments.Find(a => a.ScheduleId == scheduleId && a.Status == "Scheduled").FirstOrDefault();
            if (existingScheduleBooking != null)
            {
                TempData["AppointmentError"] = "This schedule has already been booked.";
                return RedirectToAction("Scheduler", new {tutorId, studentId, month = date.Month, year = date.Year });
            }

            var lastAppointment = dBContext.Appointments
                .Find(appointment => true)
                .SortByDescending(appointment => appointment.AppointmentId).FirstOrDefault();

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
            if (loggedInStudentId == null) 
            { 
                return RedirectToAction("Login", "Login"); 
            }

            int studentId = loggedInStudentId.Value;

            //student's existing scheduled appointment
            var oldAppointment = dBContext.Appointments
                .Find(appointment => appointment.AppointmentId == appointmentId 
                        && appointment.StudentId == studentId 
                        && appointment.Status == "Scheduled")
                .FirstOrDefault();

            if (oldAppointment == null) 
            { 
                return NotFound(); 
            }

            DateTime oldAppointmentDate = oldAppointment.AppointmentDate;
            int oldTutorId = oldAppointment.TutorId;

            //get sudent, selected tutor and new schedule
            var student = dBContext.Student
                .Find(student => student.StudentId == studentId).FirstOrDefault();
            var newTutor = dBContext.Tutor
                .Find(tutor => tutor.TutorId == tutorId).FirstOrDefault();
            var schedule = dBContext.Schedule
                .Find(schedule => schedule.ScheduleId == scheduleId).FirstOrDefault();

            //individual appointment cannot be rescheduled to group appointments
            if (schedule != null && schedule.IsGroup)
            {
                TempData["AppointmentError"] = "Individual appointments cannot be rescheduled into group sessions.";
                return RedirectToAction("Scheduler", new { tutorId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            if (student == null || newTutor == null || schedule == null) 
            { 
                return NotFound(); 
            }

            //verify selected schedule matches tutor, date and time
            if (schedule.TutorId != tutorId || schedule.Date.Date != date.Date || schedule.StartTime != startTime)
            {
                TempData["AppointmentError"] = "Invalid appointment selection.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }
            
            DateTime newStart = schedule.Date.Date.Add(schedule.StartTime);
            DateTime newEnd = schedule.Date.Date.Add(schedule.EndTime);

            //check for conflicts
            string? groupConflict = GetGroupAppointmentConflict(studentId, tutorId, newStart, newEnd);

            if (groupConflict != null)
            {
                TempData["AppointmentError"] = groupConflict;
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            //validate appointment time
            if (newStart <= DateTime.Now || newEnd <= newStart)
            {
                TempData["AppointmentError"] = "Please select a valid future appointment time.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            //get possible indivual appointment conflicts not including the orginal
            var possibleConflicts = dBContext.Appointments
                .Find(appointment => appointment.Status == "Scheduled" 
                        && appointment.AppointmentId != appointmentId 
                        && (appointment.StudentId == studentId || appointment.TutorId == tutorId))
                .ToList();

            foreach (var existing in possibleConflicts)
            {
                DateTime existingStart = existing.AppointmentDate;
                DateTime existingEnd = existing.EndDate;

                //invalid time
                if (existingEnd <= existingStart)
                {
                    var existingSchedule = dBContext.Schedule
                        .Find(schedule => schedule.ScheduleId == existing.ScheduleId).FirstOrDefault();

                    if (existingSchedule == null)
                    {
                        TempData["AppointmentError"] = "An existing appointment has invalid time information.";
                        return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                    }
                    existingStart = existingSchedule.Date.Date.Add(existingSchedule.StartTime);
                    existingEnd = existingSchedule.Date.Date.Add(existingSchedule.EndTime);
                }

                //prevent overlap
                bool overlaps = newStart < existingEnd && newEnd > existingStart;
                if (overlaps)
                {
                    TempData["AppointmentError"] = existing.StudentId == studentId
                        ? "You already have an appointment during this time."
                        : "This tutor already has an appointment during this time.";
                    return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                }
            }

            //verify slot is not booked
            var slotBooking = dBContext.Appointments
                .Find(appointment => appointment.ScheduleId == scheduleId 
                        && appointment.Status == "Scheduled" 
                        && appointment.AppointmentId != appointmentId)
                .FirstOrDefault();

            if (slotBooking != null)
            {
                TempData["AppointmentError"] = "This time slot has already been booked. Please select another.";
                return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
            }

            //determine if same tutor 
            bool sameTutor = oldTutorId == tutorId;
            Tutor? oldTutor = null; //only needed for tutor change
            if (!sameTutor)
            {
                oldTutor = dBContext.Tutor.Find(t => t.TutorId == oldTutorId).FirstOrDefault();
                if (oldTutor == null) 
                { 
                    return NotFound(); 
                }
            }

            if (sameTutor)
            {
                //update existing appointment if same tutor
                var update = Builders<Appointment>.Update
                    .Set(appointment => appointment.ScheduleId, schedule.ScheduleId)
                    .Set(appointment => appointment.TutorId, tutorId)
                    .Set(appointment => appointment.StudentId, studentId)
                    .Set(appointment => appointment.AppointmentDate, newStart)
                    .Set(appointment => appointment.EndDate, newEnd)
                    .Set(appointment => appointment.Status, "Scheduled")
                    .Set(appointment => appointment.Reminder24HoursBeforeSent, false)
                    .Set(appointment => appointment.Reminder1HourBeforeSent, false)
                    .Set(appointment => appointment.StudentReminder24HoursAcknowledged, false)
                    .Set(appointment => appointment.StudentReminder1HourAcknowledged, false);

                var updateResult = dBContext.Appointments
                    .UpdateOne(appointment => appointment.AppointmentId == appointmentId 
                        && appointment.StudentId == studentId 
                        && appointment.Status == "Scheduled", update);

                if (updateResult.MatchedCount != 1)
                {
                    TempData["AppointmentError"] = "The appointment could not be rescheduled. Please try again.";
                    return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                }
            }
            else
            {
                // create a separate appointment with the newly selected tutor and original appointment is cancelled.
                var lastAppointment = dBContext.Appointments
                    .Find(appointment => true)
                    .SortByDescending(appointment => appointment.AppointmentId)
                    .FirstOrDefault();

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

                //cancel orginal appointment
                var cancelResult = dBContext.Appointments
                    .UpdateOne(appointment => appointment.AppointmentId == appointmentId 
                                && appointment.StudentId == studentId 
                                && appointment.Status == "Scheduled",
                            Builders<Appointment>.Update.Set(appointment => appointment.Status, "Cancelled")
                    );

                if (cancelResult.ModifiedCount != 1)
                {
                    // if the original appointment could not be cancelled after inserting the replacement delete it
                    dBContext.Appointments
                        .DeleteOne(appointment => appointment.AppointmentId == replacement.AppointmentId 
                                    && appointment.StudentId == studentId 
                                    && appointment.Status == "Scheduled");
                    TempData["AppointmentError"] = "The original appointment changed while rescheduling. Please try again.";
                    return RedirectToAction("Scheduler", new { tutorId, studentId, month = date.Month, year = date.Year, rescheduleAppointmentId = appointmentId });
                }
            }

            string studentName = $"{student.FName} {student.LName}";
            string newTutorName = $"{newTutor.FName} {newTutor.LName}";

            try
            {
                if (sameTutor) //notify student and tutor
                {
                    if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await _emailService.SendStudentAppointmentRescheduledAsync(student.Email, studentName, newTutorName, newStart.AddHours(0));
                    }

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

                    // Notify the old tutor about the cancelled appointment
                    if (oldTutor != null && !oldTutor.DisableEmailNotifications &&
                        !string.IsNullOrWhiteSpace(oldTutor.Email))
                    {
                        await _emailService.SendTutorAppointmentCancellationAsync(oldTutor.Email, $"{oldTutor.FName} {oldTutor.LName}", studentName, oldAppointmentDate.AddHours(5));
                    }

                    // Notify the new tutor
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

            //get existing scheduled appointments
            var oldAppointment = dBContext.Appointments
                .Find(appointment => appointment.AppointmentId == appointmentId
                    && appointment.StudentId == studentId.Value
                    && appointment.Status == "Scheduled")
                .FirstOrDefault();

            if (oldAppointment == null)
            {
                return NotFound();
            }         

            var group = dBContext.GroupAppointments
                .Find(group => group.GroupId == groupId)
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
                .Find(student => student.StudentId == studentId.Value)
                .FirstOrDefault();

            var newTutor = dBContext.Tutor
                .Find(tutor => tutor.TutorId == group.TutorId)
                .FirstOrDefault();

            if (student == null || newTutor == null)
            {
                return NotFound();
            }

            var groupStart = group.AppointmentDate.Date.Add(group.StartTime);
            var groupEnd = group.AppointmentDate.Date.Add(group.EndTime);

            //verify group is open and has space
            if (group.Status != "Open" || group.StudentIds == null || group.StudentIds.Count >= group.Capacity)
            {
                TempData["AppointmentError"] = "This group appointment is no longer available.";
                return ReturnToScheduler();
            }

            //ensure valid time
            if (groupStart <= DateTime.Now || groupEnd <= groupStart)
            {
                TempData["AppointmentError"] = "Please select a valid future group appointment.";
                return ReturnToScheduler();
            }

            //group has a schedule
            var groupSchedule = dBContext.Schedule
                .Find(schedule => schedule.IsGroup 
                    && schedule.GroupId == groupId 
                    && schedule.TutorId == group.TutorId)
                .FirstOrDefault();

            if (groupSchedule == null)
            {
                TempData["AppointmentError"] = "The group schedule could not be found.";
                return ReturnToScheduler();
            }

            //student can't join twice
            if (group.StudentIds.Contains(studentId.Value))
            {
                TempData["AppointmentError"] = "You are already enrolled in this group.";
                return ReturnToScheduler();
            }

            //get individual appointments that may conflict with new group
            var conflicts = dBContext.Appointments
                .Find(appointment => appointment.Status == "Scheduled" 
                    && appointment.AppointmentId != appointmentId 
                    && (appointment.StudentId == studentId.Value || appointment.TutorId == group.TutorId))
                .ToList();

            foreach (var appointment in conflicts)
            {
                var start = appointment.AppointmentDate;
                var end = appointment.EndDate;

                //invalid time
                if (end <= start)
                {
                    var schedule = dBContext.Schedule
                        .Find(scehdule => scehdule.ScheduleId == appointment.ScheduleId)
                        .FirstOrDefault();

                    if (schedule == null)
                    {
                        TempData["AppointmentError"] = "An existing appointment has invalid time information.";
                        return ReturnToScheduler();
                    }

                    start = schedule.Date.Date.Add(schedule.StartTime);
                    end = schedule.Date.Date.Add(schedule.EndTime);
                }

                //prevent conflicts with the student's or tutor's individual appointments
                if (groupStart < end && groupEnd > start)
                {
                    TempData["AppointmentError"] =
                        appointment.StudentId == studentId.Value
                        ? "You already have an appointment during this time."
                        : "This tutor has an individual appointment during this time.";

                    return ReturnToScheduler();
                }
            }

            //student's other groups
            var otherGroups = dBContext.GroupAppointments
                .Find(group => group.GroupId != groupId 
                    && (group.Status == "Open" || group.Status == "Full") 
                    && group.StudentIds.Contains(studentId.Value))
                .ToList();

            //check for overlap
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

            //open group, space and not duplicate students
            var filter = Builders<GroupAppointment>.Filter.Where(group =>
                group.GroupId == groupId 
                && group.Status == "Open" 
                && !group.StudentIds.Contains(studentId.Value) 
                && group.StudentIds.Count < group.Capacity
            );

            //add student to group
            var enrollResult = dBContext.GroupAppointments.UpdateOne(filter,
                Builders<GroupAppointment>.Update.AddToSet(group => group.StudentIds, studentId.Value));

            if (enrollResult.ModifiedCount != 1)
            {
                TempData["AppointmentError"] = "The group may be full. Please select another.";
                return ReturnToScheduler();
            }

            //cancel orginal individual appointment after joining group
            var cancelResult = dBContext.Appointments
                .UpdateOne(appointment => appointment.AppointmentId == appointmentId 
                && appointment.StudentId == studentId.Value && appointment.Status == "Scheduled",
                Builders<Appointment>.Update.Set(appointment => appointment.Status, "Cancelled"));

            //remove student from group if cancellation fails
            if (cancelResult.ModifiedCount != 1)
            {
                dBContext.GroupAppointments.UpdateOne(group => group.GroupId == groupId,
                    Builders<GroupAppointment>.Update.Pull(group => group.StudentIds, studentId.Value));
                TempData["AppointmentError"] = "Rescheduling failed. Please try again.";
                return ReturnToScheduler();
            }

            //group is full
            var fullFilter = Builders<GroupAppointment>.Filter.Where(group =>
                group.GroupId == groupId 
                && group.Status == "Open" 
                && group.StudentIds.Count >= group.Capacity
            );

            dBContext.GroupAppointments.UpdateOne(fullFilter, Builders<GroupAppointment>.Update
                    .Set(group => group.Status, "Full")
            );

            //get orginal tutors
            var oldTutor = dBContext.Tutor
                .Find(tutor => tutor.TutorId == oldAppointment.TutorId)
                .FirstOrDefault();

            try
            {
                //notify student
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

                //notify new tutor
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

                //notify old tutor
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
            if (studentId == null) 
            { 
                return RedirectToAction("Login", "Login"); 
            }

            var student = dBContext.Student
                .Find(student => student.StudentId == studentId.Value)
                .FirstOrDefault();

            if (student == null) 
            { 
                return NotFound(); 
            }

            //find group
            var group = dBContext.GroupAppointments
                .Find(group => group.GroupId == groupId)
                .FirstOrDefault();

            if (group == null) 
            { 
                return NotFound(); 
            }

            IActionResult ReturnToScheduler() //return to tutor's schedule
            {
                return RedirectToAction("Scheduler", new { tutorId = group.TutorId, studentId = studentId.Value, month = group.AppointmentDate.Month, year = group.AppointmentDate.Year });
            }

            if (group.Status != "Open") //verify group is open
            {
                TempData["AppointmentError"] = "This group is no longer available.";
                return ReturnToScheduler();
            }

            //group's appointment time
            var groupStart = group.AppointmentDate.Date.Add(group.StartTime);
            var groupEnd = group.AppointmentDate.Date.Add(group.EndTime);

            if (groupStart <= DateTime.Now || groupEnd <= groupStart)
            {
                TempData["AppointmentError"] = "This group appointment has already started or has an invalid time.";
                return ReturnToScheduler();
            }

            //group schedule exists
            var groupSchedule = dBContext.Schedule
                .Find(schedule => schedule.IsGroup 
                    && schedule.GroupId == groupId 
                    && schedule.TutorId == group.TutorId)
                .FirstOrDefault();

            if (groupSchedule == null)
            {
                TempData["AppointmentError"] = "The approved group schedule could not be found.";
                return ReturnToScheduler();
            }

            //not duplicate enrollment
            if (group.StudentIds != null && group.StudentIds.Contains(studentId.Value))
            {
                TempData["AppointmentError"] = "You have already joined this group.";
                return ReturnToScheduler();
            }

            //get individual appointment to check if there is conflict
            var conflictingAppointments = dBContext.Appointments
                .Find(appointment => appointment.Status == "Scheduled" 
                    && (appointment.StudentId == studentId.Value || appointment.TutorId == group.TutorId))
                .ToList();

            foreach (var appointment in conflictingAppointments)
            {
                DateTime existingStart = appointment.AppointmentDate;
                DateTime existingEnd = appointment.EndDate;

                //get schedule times if appointment dates are invalid
                if (existingEnd <= existingStart)
                {
                    var existingSchedule = dBContext.Schedule
                        .Find(schedule => schedule.ScheduleId == appointment.ScheduleId)
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

            //get other groups student is in
            var enrolledGroups = dBContext.GroupAppointments
                .Find(group => group.GroupId != groupId 
                    && (group.Status == "Open" || group.Status == "Full") 
                    && group.StudentIds.Contains(studentId.Value))
                .ToList();

            //check for overlay btw group appointments
            bool groupConflict = enrolledGroups.Any(group =>
            {
                var start = group.AppointmentDate.Date.Add(group.StartTime);
                var end = group.AppointmentDate.Date.Add(group.EndTime);
                return start < groupEnd && end > groupStart;
            });

            if (groupConflict)
            {
                TempData["AppointmentError"] = "You are already enrolled in another group during this time.";
                return ReturnToScheduler();
            }

            //filter open group with space
            var filter = Builders<GroupAppointment>.Filter.Where(group =>
                group.GroupId == groupId &&
                group.Status == "Open" &&
                !group.StudentIds.Contains(studentId.Value) &&
                group.StudentIds.Count < group.Capacity
            );

            //add student without duplicates
            var update = Builders<GroupAppointment>.Update.AddToSet(group => group.StudentIds, studentId.Value);

            var result = dBContext.GroupAppointments.UpdateOne(filter, update);
            if (result.ModifiedCount == 0)
            {
                TempData["AppointmentError"] = "Unable to join. The group may be full or you may already be enrolled.";
                return ReturnToScheduler();
            }

            //group is full
            var fullFilter = Builders<GroupAppointment>.Filter.Where(group =>
                group.GroupId == groupId &&
                group.Status == "Open" &&
                group.StudentIds.Count >= group.Capacity
            );

            dBContext.GroupAppointments.UpdateOne(fullFilter, Builders<GroupAppointment>.Update
                    .Set(group => group.Status, "Full")
            );

            var tutor = dBContext.Tutor
            .Find(tutor => tutor.TutorId == group.TutorId)
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
            if (studentId == null) 
            { 
                return RedirectToAction("Login", "Login"); 
            }
            //get student and verify they exist
            var student = dBContext.Student
                .Find(student => student.StudentId == studentId.Value)
                .FirstOrDefault();
            
            //student not found
            if (student == null) 
            { 
                return NotFound(); 
            }

            // group appointment.
            var group = dBContext.GroupAppointments
                .Find(group => group.GroupId == groupId)
                .FirstOrDefault();

            if (group == null)
            {
                TempData["AppointmentError"] = "Group appointment not found.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            // student is enrolled?
            if (group.StudentIds == null || !group.StudentIds.Contains(studentId.Value))
            {
                TempData["AppointmentError"] = "You are not enrolled in this group appointment.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            // only cancel before group starts
            var groupStart = group.AppointmentDate.Date.Add(group.StartTime);

            if (groupStart <= DateTime.Now)
            {
                TempData["AppointmentError"] = "You cannot cancel a group appointment that has already started.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            //get active group w/student
            var filter = Builders<GroupAppointment>.Filter.Where(group =>
                group.GroupId == groupId 
                && (group.Status == "Open" || group.Status == "Full") 
                && group.StudentIds.Contains(studentId.Value)
            );

            //remove student from group
            var update = Builders<GroupAppointment>.Update.Pull(group => group.StudentIds, studentId.Value);

            var result = dBContext.GroupAppointments.UpdateOne(filter, update);

            //cancel failure 
            if (result.ModifiedCount == 0)
            {
                TempData["AppointmentError"] = "Unable to cancel your enrollment. " + "The group may have changed.";
                return RedirectToAction("Dashboard", "Student", new { id = studentId.Value });
            }

            //repoen group if there is an spot
            var reopenFilter = Builders<GroupAppointment>.Filter.Where(group =>
                group.GroupId == groupId &&
                group.Status == "Full" &&
                group.StudentIds.Count < group.Capacity
            );

            dBContext.GroupAppointments.UpdateOne(reopenFilter, Builders<GroupAppointment>.Update
                .Set(group => group.Status, "Open"));

            // get tutor details
            var tutor = dBContext.Tutor
            .Find(tutor => tutor.TutorId == group.TutorId)
            .FirstOrDefault();

            if (tutor != null)
            {
                try
                {
                    //notify student
                    if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                    {
                        await _emailService.SendGroupAppointmentCancellationToStudentAsync(student.Email, student.FName, tutor.FName, group.AppointmentDate, group.StartTime, group.EndTime);
                    }

                    //notify tutor
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
            //get studentid
            int? studentId = HttpContext.Session.GetInt32("StudentId");
            var now = DateTime.Now;

            //open groups with upcoming times
            var openGroups = dBContext.GroupAppointments
                .Find(group => group.Status == "Open" && group.AppointmentDate >= now.Date)
                .ToList()
                .Where(group => group.AppointmentDate.Date.Add(group.StartTime) > now)
                .OrderBy(group => group.AppointmentDate)
                .ThenBy(group => group.StartTime)
                .ToList();

            //get tutor id associated with the open groups
            var tutorIds = openGroups
                .Select(group => group.TutorId)
                .Distinct()
                .ToList();

            //tutor detail for groups
            var tutors = dBContext.Tutor
                .Find(tutor => tutorIds.Contains(tutor.TutorId))
                .ToList();

            //pass tutor/student info to view
            ViewBag.Tutors = tutors;
            ViewBag.StudentId = studentId;

            return View(openGroups);
        }


    }
}
