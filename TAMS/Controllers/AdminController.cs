using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text;
using TAMS.Data;
using TAMS.Models;
using TAMS.Services;

namespace TAMS.Controllers
{
    public class AdminController : Controller
    {
        private readonly MongoDBContext dBContext;
        private readonly IEmailService _emailService;

        public AdminController(MongoDBContext dBContext, IEmailService emailService)
        {
            this.dBContext = dBContext;
            _emailService = emailService;
        }


        [HttpGet]
        public IActionResult Dashboard(int? id, int? editTutorId, int? viewTutorId, bool showAdminFollowUps = false)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            ViewBag.AdminId = adminId;
            ViewBag.Tutors = dBContext.Tutor.Find(_ => true).ToList();
            ViewBag.Students = dBContext.Student.Find(_ => true).ToList();
            ViewBag.ShowAdminFollowUps = showAdminFollowUps;

            var appointments = dBContext.Appointments
                .Find(_ => true)
                .SortBy(a => a.AppointmentDate)
                .ToList();

            foreach (var appointment in appointments)
            {
                appointment.Tutor = dBContext.Tutor
                    .Find(t => t.TutorId == appointment.TutorId)
                    .FirstOrDefault();

                appointment.Student = dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId)
                    .FirstOrDefault();
            }

            ViewBag.Appointments = appointments;

            if (editTutorId.HasValue)
            {
                var tutor = dBContext.Tutor
                    .Find(t => t.TutorId == editTutorId.Value)
                    .FirstOrDefault();

                if (tutor != null)
                {
                    ViewBag.EditTutor = tutor;
                    ViewBag.EditLogin = dBContext.Login
                        .Find(l => l.UserId == editTutorId.Value)
                        .FirstOrDefault();
                }
            }

            if (viewTutorId.HasValue)
            {
                var tutor = dBContext.Tutor
                    .Find(t => t.TutorId == viewTutorId.Value)
                    .FirstOrDefault();

                if (tutor != null)
                {
                    var schedules = dBContext.Schedule
                        .Find(s => s.TutorId == viewTutorId.Value)
                        .ToList()
                        .OrderBy(s => s.Date)
                        .ThenBy(s => s.StartTime)
                        .ToList();

                    ViewBag.ViewTutorSchedules = schedules;
                    ViewBag.ViewTutorName = $"{tutor.FName} {tutor.LName}";
                    ViewBag.CurrentTutorId = viewTutorId.Value;
                }
            }

            return View("AdminDashboard");
        }

        // Load subjects from MongoDB.
        private void LoadSubjects()
        {
            ViewBag.Subjects = dBContext.Subject
                .Find(_ => true)
                .SortBy(s => s.Name)
                .ToList();
        }

        [HttpGet]
        public IActionResult CreateTutor()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }
            LoadSubjects();
            return View();
        }
      
        // Create tutor and login accounts.
        [HttpPost]
        public IActionResult CreateTutor(TutorRegistration model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null){ return RedirectToAction("Login", "Login"); }

            if (!ModelState.IsValid)
            {
                LoadSubjects();
                return View(model);
            }

            // Check whether the username already exists.
            var existingUsername = dBContext.Login
                .Find(u => u.Username == model.Username).FirstOrDefault();

            if (existingUsername != null)
            {
                ModelState.AddModelError("Username", "This username is already in use.");
                LoadSubjects();
                return View(model);
            }

            // Check whether the tutor email already exists.
            var existingEmail = dBContext.Tutor
                .Find(t => t.Email == model.Email).FirstOrDefault();

            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "A tutor with this email already exists.");
                LoadSubjects();
                return View(model);
            }

            // Generate the next user ID.
            var lastUser = dBContext.Login.Find(_ => true)
                .SortByDescending(u => u.UserId).FirstOrDefault();

            int newUserId = lastUser == null ? 1 : lastUser.UserId + 1;

            // Create the tutor login.
            var newLogin = new Login
            {
                UserId = newUserId,
                Username = model.Username,
                Password = model.Password,
                Role = UserRole.Tutor,
                CreatedDate = DateTime.Now,
                LastLogin = DateTime.Now,
                isActive = true
            };

            dBContext.Login.InsertOne(newLogin);

            // Create the tutor profile.
            var newTutor = new Tutor
            {
                TutorId = newUserId,
                FName = model.FName,
                LName = model.LName,
                Subject = model.Subject,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email
            };

            dBContext.Tutor.InsertOne(newTutor);
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateTutor(int tutorId, string fName, string lName, string subject, string email, string phoneNumber)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId)
                .FirstOrDefault();

            if (tutor == null)
            {
                TempData["Error"] = "The selected tutor could not be found.";
                return RedirectToAction("Dashboard");
            }

            fName = fName?.Trim() ?? "";
            lName = lName?.Trim() ?? "";
            subject = subject?.Trim() ?? "";
            email = email?.Trim() ?? "";
            phoneNumber = phoneNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(fName) || string.IsNullOrWhiteSpace(lName) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Please complete all required tutor fields.";
                return RedirectToAction("Dashboard", new { editTutorId = tutorId });
            }

            var duplicateEmail = dBContext.Tutor
                .Find(t => t.Email == email && t.TutorId != tutorId)
                .FirstOrDefault();

            if (duplicateEmail != null)
            {
                TempData["Error"] = "Another tutor is already using this email.";
                return RedirectToAction("Dashboard", new { editTutorId = tutorId });
            }

            var update = Builders<Tutor>.Update
                .Set(t => t.FName, fName)
                .Set(t => t.LName, lName)
                .Set(t => t.Subject, subject)
                .Set(t => t.Email, email)
                .Set(t => t.PhoneNumber, phoneNumber);

            dBContext.Tutor.UpdateOne(t => t.TutorId == tutorId, update);

            TempData["Success"] = "Tutor information updated successfully.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public IActionResult DeleteTutor(int id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null){ return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == id).FirstOrDefault();

            if (tutor != null)
            {
                // Delete the tutor profile and associated login.
                dBContext.Tutor.DeleteOne(t => t.TutorId == id);
                dBContext.Login.DeleteOne(l => l.UserId == id);

                // Cancel future scheduled appointments for this tutor.
                var affectedAppointments = dBContext.Appointments
                    .Find(a => a.TutorId == id && a.Status == "Scheduled").ToList();

                foreach (var appointment in affectedAppointments)
                {
                    var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled");
                    dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointment.AppointmentId, update);
                }

                // Remove the tutor's schedule slots.
                dBContext.Schedule.DeleteMany(s => s.TutorId == id);
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteStudent(int id)
        {
            // Verify that an administrator is logged in.
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            // Find the student.
            var student = dBContext.Student
                .Find(s => s.StudentId == id).FirstOrDefault();

            if (student != null)
            {
                // Cancel scheduled appointments associated with the student.
                var appointments = dBContext.Appointments
                    .Find(a => a.StudentId == id && a.Status == "Scheduled")
                    .ToList();

                foreach (var appointment in appointments)
                {
                    var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled");

                    dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointment.AppointmentId,
                        update);
                }

                // Delete the student's login account.
                dBContext.Login.DeleteOne(l => l.UserId == id);

                // Delete the student record.
                dBContext.Student.DeleteOne(s => s.StudentId == id);
            }
            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public IActionResult Scheduler(int? month, int? year)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            ViewBag.AdminId = adminId;

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            // Validate the requested calendar month.
            if (targetMonth < 1 || targetMonth > 12 || targetYear < 1 || targetYear > 9998)
            {
                targetMonth = DateTime.Today.Month;
                targetYear = DateTime.Today.Year;
            }

            var startOfMonth = new DateTime(targetYear, targetMonth, 1);

            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            // Load appointments in the requested month.
            var appointments = dBContext.Appointments
                .Find(_ => true).ToList()
                .Where(a => a.AppointmentDate.Date >= startOfMonth.Date && a.AppointmentDate.Date <= endOfMonth.Date)
                .OrderBy(a => a.AppointmentDate)
                .ToList();

            // Load associated tutors and students.
            foreach (var appointment in appointments)
            {
                appointment.Tutor = dBContext.Tutor
                    .Find(t => t.TutorId == appointment.TutorId)
                    .FirstOrDefault();

                appointment.Student = dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId)
                    .FirstOrDefault();
            }

            // Load schedules for the requested month.
            var schedules = dBContext.Schedule
                .Find(_ => true).ToList()
                .Where(s => s.Date.Month == targetMonth && s.Date.Year == targetYear)
                .OrderBy(s => s.Date)
                .ThenBy(s => s.StartTime)
                .ToList();

            // Load each schedule's tutor.
            foreach (var schedule in schedules)
            {
                schedule.Tutor = dBContext.Tutor
                    .Find(t => t.TutorId == schedule.TutorId)
                    .FirstOrDefault();
            }

            var tutors = dBContext.Tutor
                .Find(_ => true).ToList();

            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;
            ViewBag.Tutors = tutors;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;

            return View("AdminScheduler");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddSlot(int tutorId, DateTime date, TimeSpan startTime, TimeSpan endTime)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var now = DateTime.Now;
            var today = now.Date;

            if (date.Date < today)
            {
                TempData["Error"] = "You cannot add a schedule slot for a date that has already passed.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            if (date.Date == today && startTime <= now.TimeOfDay)
            {
                TempData["Error"] = "You cannot add a schedule slot with a start time that has already passed. Please choose a future time.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            if (endTime <= startTime)
            {
                TempData["Error"] = "End time must be after start time.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == tutorId).FirstOrDefault();

            if (tutor == null)
            {
                TempData["Error"] = "The selected tutor was not found.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            var existingSlots = dBContext.Schedule.Find(s => s.TutorId == tutorId).ToList();

            // Prevent overlapping schedules for the same tutor and date.
            bool overlaps = existingSlots.Any(s => s.Date.Date == date.Date && startTime < s.EndTime && endTime > s.StartTime);

            if (overlaps)
            {
                TempData["Error"] = "An existing schedule already overlaps with this time for the selected tutor. " + "If you want to add this time, please delete the preexisting schedule first.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
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

            return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
        }

        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int appointmentId)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var appointment = dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId)
                .FirstOrDefault();

            if (appointment == null) { return NotFound(); }

            if (appointment.Status?.Trim() != "Scheduled")
            {
                TempData["Error"] = "Only scheduled appointments can be cancelled.";
                return RedirectToAction("Dashboard");
            }

            var student = dBContext.Student
                .Find(s => s.StudentId == appointment.StudentId)
                .FirstOrDefault();

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == appointment.TutorId)
                .FirstOrDefault();

            if (student == null || tutor == null)
            {
                TempData["Error"] = "The appointment could not be cancelled because the associated student or tutor could not be found.";
                return RedirectToAction("Dashboard");
            }

            var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled");
            dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId, update);

            var studentName = $"{student.FName} {student.LName}";
            var tutorName = $"{tutor.FName} {tutor.LName}";
            try
            {
                // Notify the student.
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendAdminCancelledAppointmentAsync(student.Email, studentName, tutorName, appointment.AppointmentDate.AddHours(5), "Student"
                    );
                }

                // Notify the tutor.
                if (!string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService.SendAdminCancelledAppointmentAsync(tutor.Email, tutorName, studentName, appointment.AppointmentDate.AddHours(5), "Tutor"
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cancellation email failed: {ex.Message}");
                TempData["Error"] = "The appointment was cancelled, but a notification email could not be sent.";
            }
            return RedirectToAction("Dashboard");
        }

        // Deletes a tutor's available schedule slot (Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteScheduleSlot(int scheduleId)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var slot = dBContext.Schedule
                .Find(s => s.ScheduleId == scheduleId)
                .FirstOrDefault();

            if (slot == null)
            {
                TempData["Error"] = "Schedule slot not found.";
                return RedirectToAction("Dashboard");
            }

            int tutorId = slot.TutorId;
            var tutorAppointments = dBContext.Appointments
                .Find(a => a.TutorId == tutorId)
                .ToList();

            var hasAppointments = tutorAppointments.Any(a =>
            {
                if (a.Status != "Scheduled")
                    return false;
                var localAppointmentDate = a.AppointmentDate;
                return localAppointmentDate.Date == slot.Date.Date && localAppointmentDate.TimeOfDay >= slot.StartTime && localAppointmentDate.TimeOfDay < slot.EndTime;
            });

            if (hasAppointments)
            {
                TempData["Error"] =
                    "You cannot delete this schedule slot because it has a scheduled appointment. " +
                    "Please cancel or remove the associated appointment first.";
                return RedirectToAction("Dashboard", new { viewTutorId = tutorId });
            }

            dBContext.Schedule.DeleteOne(s => s.ScheduleId == scheduleId && s.TutorId == tutorId);

            TempData["Success"] = "Schedule deleted successfully.";
            return RedirectToAction("Dashboard", new { viewTutorId = tutorId });
        }

        [HttpGet]
        public IActionResult DownloadSystemUsageReport()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var allLogins = dBContext.Login
                .Find(_ => true)
                .ToList();

            var last7Days = DateTime.Today.AddDays(-6);

            var loginsPerDay = allLogins
                .Where(l => l.LastLogin >= last7Days)
                .GroupBy(l => l.LastLogin.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(g => g.Date)
                .ToList();

            var roleLogins = allLogins
                .GroupBy(l => l.Role)
                .Select(g => new
                {
                    Role = g.Key.ToString(),
                    Count = g.Count()
                })
                .OrderBy(g => g.Role)
                .ToList();

            var userLastLogins = allLogins
                .Select(l => new
                {
                    l.Username,
                    Role = l.Role.ToString(),
                    l.LastLogin
                })
                .OrderBy(u => u.Username)
                .ToList();

            int dateWidth = 14;
            int totalLoginsWidth = 24;

            int roleWidth = roleLogins.Any()
                ? Math.Max(
                    roleLogins.Max(r => r.Role.Length),
                    "Role".Length) + 4
                : "Role".Length + 4;

            int usernameWidth = userLastLogins.Any()
                ? Math.Max(
                    userLastLogins.Max(u => u.Username.Length),
                    "Username".Length) + 2
                : "Username".Length + 2;

            int roleUserWidth = userLastLogins.Any()
                ? Math.Max(
                    userLastLogins.Max(u => u.Role.Length),
                    "Role".Length) + 4
                : "Role".Length + 4;

            int lastLoginWidth = 24;

            string separator = new string(
                '-',
                usernameWidth + roleUserWidth + lastLoginWidth + 10);

            var lines = new List<string>
            {
                "TAMS System Usage Analytics Report",
                $"Generated: {DateTime.Now}",
                separator,
                "",
                "================ Logins per Day (Last 7 Days) ================",
                string.Format(
                    "{0,-" + dateWidth + "} | {1," +
                    totalLoginsWidth + "}|",
                    "Date",
                    "Total Logins")
            };

            foreach (var login in loginsPerDay)
            {
                lines.Add(string.Format(
                    "{0,-" + dateWidth + "} | {1," +
                    totalLoginsWidth + "}|",
                    login.Date.ToString("MM/dd/yyyy"),
                    login.Count));
            }

            lines.Add("");
            lines.Add("====================== Logins by Role ======================");

            lines.Add(string.Format(
                "{0,-" + roleWidth + "} | {1," +
                totalLoginsWidth + "}|",
                "Role",
                "Login Count"));

            foreach (var role in roleLogins)
            {
                lines.Add(string.Format(
                    "{0,-" + roleWidth + "} | {1," +
                    totalLoginsWidth + "}|",
                    role.Role,
                    role.Count));
            }

            lines.Add("");
            lines.Add("=================== Last Login by User ====================");

            lines.Add(string.Format(
                "{0,-" + usernameWidth +
                "} | {1,-" + roleUserWidth +
                "} | {2," + lastLoginWidth + "}|",
                "Username",
                "Role",
                "Last Login"));

            foreach (var user in userLastLogins)
            {
                lines.Add(string.Format(
                    "{0,-" + usernameWidth +
                    "} | {1,-" + roleUserWidth +
                    "} | {2," + lastLoginWidth + "}|",
                    user.Username,
                    user.Role,
                    user.LastLogin.AddHours(-5).ToString("MM/dd/yyyy hh:mm tt")));
            }

            string reportContent = string.Join(
                Environment.NewLine,
                lines);

            byte[] fileBytes = Encoding.UTF8.GetBytes(reportContent);

            string fileName =
                $"TAMS_SystemUsageReport_{DateTime.Now:yyyyMMddHHmmss}.txt";

            return File(
                fileBytes,
                "text/plain",
                fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteAppointment(int appointmentId)
        {
            var appointment = await dBContext.Appointments.Find(a => a.AppointmentId == appointmentId).FirstOrDefaultAsync();
            if (appointment == null) { return NotFound(); }

            if (appointment.Status != "Scheduled" || !appointment.AdminNotificationSent)
            {
                TempData["ErrorMessage"] = "This appointment cannot be completed until the Admin notification has been sent.";
                return RedirectToAction("AdminDashboard");
            }

            var filter =
                Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointmentId)
                &
                Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled")
                &
                Builders<Appointment>.Filter.Eq(a => a.AdminNotificationSent, true);

            var update = Builders<Appointment>.Update.Set(a => a.Status, "Completed");
            var result = await dBContext.Appointments.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
            {
                TempData["ErrorMessage"] = "The appointment could not be completed. It may have already been updated.";
                return RedirectToAction("AdminDashboard");
            }

            // Find and delete the associated schedule slot.
            var appointmentDate = appointment.AppointmentDate.Date;
            var appointmentTime = appointment.AppointmentDate.TimeOfDay;

            var schedules = await dBContext.Schedule.Find(s => s.TutorId == appointment.TutorId).ToListAsync();
            var schedule = schedules.FirstOrDefault(s => s.Date.Date == appointmentDate && appointmentTime >= s.StartTime && appointmentTime < s.EndTime);

            if (schedule != null)
            {
                await dBContext.Schedule.DeleteOneAsync(s => s.ScheduleId == schedule.ScheduleId && s.TutorId == appointment.TutorId);
            }

            // Send the student a review reminder.
            try
            {
                var student = await dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefaultAsync();
                var tutor = await dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefaultAsync();

                if (student != null && tutor != null && !string.IsNullOrWhiteSpace(student.Email))
                {
                    var studentName = $"{student.FName} {student.LName}";
                    var tutorName = $"{tutor.FName} {tutor.LName}";
                    await _emailService.SendReviewReminderAsync(student.Email, studentName, tutorName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Review reminder email failed: {ex.Message}");
            }

            TempData["SuccessMessage"] = $"Appointment #{appointmentId} has been marked as completed.";

            return RedirectToAction("Dashboard");
        }
    }
}
