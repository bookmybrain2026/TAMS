using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Text;
using TAMS.Data;
using TAMS.Models;
using TAMS.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TAMS.Controllers
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class AdminController : Controller
    {
        private readonly MongoDBContext dBContext;
        private readonly IEmailService _emailService;
        private readonly IPasswordHasher<Login> _passwordHasher;

        public AdminController(MongoDBContext dBContext, IEmailService emailService, IPasswordHasher<Login> passwordHasher)
        {
            this.dBContext = dBContext;
            _emailService = emailService;
            _passwordHasher = passwordHasher;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                context.Result = RedirectToAction("Login", "Login");
                return;
            }

            var activeAdmin = dBContext.Login.Find(l =>
                l.UserId == adminId.Value &&
                l.Role == UserRole.Administrator &&
                l.isActive).FirstOrDefault();

            if (activeAdmin == null)
            {
                HttpContext.Session.Clear();
                context.Result = RedirectToAction("Login", "Login");
                return;
            }

            base.OnActionExecuting(context);
        }

        [HttpGet]
        public IActionResult Dashboard(int? id, int? editTutorId, int? viewTutorId, int? reviewTutorId, bool showAdminFollowUps = false)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var loginUser = dBContext.Login
                .Find(l => l.UserId == adminId.Value && l.Role == UserRole.Administrator)
                .FirstOrDefault();
            ViewBag.ShowDashboardTour = loginUser != null && !loginUser.HasCompletedTour;

            ViewBag.AdminId = adminId;
            ViewBag.Tutors = dBContext.Tutor.Find(_ => true).ToList();
            ViewBag.Students = dBContext.Student.Find(_ => true).ToList();
            ViewBag.ShowAdminFollowUps = showAdminFollowUps;

            var pendingGroupRequests = dBContext.GroupAppointments
                .Find(g => g.Status == "Pending")
                .SortBy(g => g.AppointmentDate)
                .ToList();

            ViewBag.PendingGroupRequests = pendingGroupRequests;

            var appointments = dBContext.Appointments.Find(_ => true).SortBy(a => a.AppointmentDate).ToList();

            foreach (var appointment in appointments)
            {
                appointment.Tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();
                appointment.Student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            }
            ViewBag.Appointments = appointments;

            var adminGroupAppointments = dBContext.GroupAppointments
                .Find(g =>
                    g.Status == "Open" ||
                    g.Status == "Full" ||
                    g.Status == "Completed")
                .ToList()
                .OrderBy(g => g.AppointmentDate)
                .ThenBy(g => g.StartTime)
                .ToList();

            ViewBag.AdminGroupAppointments = adminGroupAppointments;

            if (editTutorId.HasValue)
            {
                var tutor = dBContext.Tutor.Find(t => t.TutorId == editTutorId.Value).FirstOrDefault();

                if (tutor != null)
                {
                    ViewBag.EditTutor = tutor;
                    ViewBag.EditLogin = dBContext.Login.Find(l => l.UserId == editTutorId.Value).FirstOrDefault();
                }
            }

            if (viewTutorId.HasValue)
            {
                var tutor = dBContext.Tutor.Find(t => t.TutorId == viewTutorId.Value).FirstOrDefault();

                if (tutor != null)
                {
                    var schedules = dBContext.Schedule.Find(s => s.TutorId == viewTutorId.Value).ToList()
                        .OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();
                    ViewBag.ViewTutorSchedules = schedules;
                    ViewBag.ViewTutorName = $"{tutor.FName} {tutor.LName}";
                    ViewBag.CurrentTutorId = viewTutorId.Value;
                }
            }

            if (reviewTutorId.HasValue)
            {
                var tutor = dBContext.Tutor.Find(t => t.TutorId == reviewTutorId.Value).FirstOrDefault();
                if (tutor != null)
                {
                    var reviews = dBContext.Reviews
                        .Find(r => r.TutorId == reviewTutorId.Value)
                        .SortByDescending(r => r.AppointmentDate).ToList();

                    ViewBag.TutorReviews = reviews;
                    ViewBag.ReviewTutorName = $"{tutor.FName} {tutor.LName}";
                }
            }

            return View("AdminDashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveGroupAppointment(int groupId)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var group = dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId)
                .FirstOrDefault();

            if (group == null || group.Status != "Pending")
            {
                TempData["AdminDashboardError"] = "Group request not found or already reviewed.";
                return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
            }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == group.TutorId)
                .FirstOrDefault();

            if (tutor == null)
            {
                TempData["AdminDashboardError"] = "The tutor for this request was not found.";
                return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
            }

            DateTime startDate = group.AppointmentDate.Date.Add(group.StartTime);
            DateTime endDate = group.AppointmentDate.Date.Add(group.EndTime);

            if (startDate <= DateTime.Now || endDate <= startDate || group.Capacity < 2)
            {
                TempData["AdminDashboardError"] = "The requested date, time, or capacity is invalid.";
                return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
            }

            var conflict = dBContext.Appointments
                .Find(a => a.TutorId == group.TutorId &&
                    a.Status == "Scheduled" &&
                    a.AppointmentDate < endDate &&
                    a.EndDate > startDate)
                .Any();

            if (conflict)
            {
                TempData["AdminDashboardError"] = "The tutor already has an appointment during this time.";
                return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
            }

            var claim = dBContext.GroupAppointments.UpdateOne(g => g.GroupId == groupId && g.Status == "Pending",
                Builders<GroupAppointment>.Update.Set(g => g.Status, "Approving"));

            if (claim.ModifiedCount == 0)
            {
                TempData["AdminDashboardError"] = "This request is already being reviewed.";
                return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
            }

            //int newAppointmentId = 0;

            try
            {
                var existingSchedules = dBContext.Schedule
                    .Find(s => s.TutorId == group.TutorId)
                    .ToList();

                bool scheduleConflict = existingSchedules.Any(s =>
                    s.Date.Date == group.AppointmentDate.Date &&
                    group.StartTime < s.EndTime &&
                    group.EndTime > s.StartTime);

                if (scheduleConflict)
                {
                    throw new InvalidOperationException("The tutor already has availability during this time.");
                }

                bool appointmentConflict = dBContext.Appointments
                    .Find(a => a.TutorId == group.TutorId &&
                        a.Status == "Scheduled" &&
                        a.AppointmentDate < endDate &&
                        a.EndDate > startDate)
                    .Any();

                if (appointmentConflict)
                {
                    throw new InvalidOperationException("The tutor already has an appointment during this time.");
                }

                var lastSchedule = dBContext.Schedule
                    .Find(_ => true)
                    .SortByDescending(s => s.ScheduleId)
                    .FirstOrDefault();

                int newScheduleId = lastSchedule == null ? 1 : lastSchedule.ScheduleId + 1;

                var schedule = new Schedule
                {
                    ScheduleId = newScheduleId,
                    TutorId = group.TutorId,
                    Date = group.AppointmentDate.Date,
                    StartTime = group.StartTime,
                    EndTime = group.EndTime,
                    IsGroup = true,
                    GroupId = group.GroupId
                };

                dBContext.Schedule.InsertOne(schedule);

                // Open the group for student enrollment.
                var result = dBContext.GroupAppointments.UpdateOne(g => g.GroupId == groupId && g.Status == "Approving",
                    Builders<GroupAppointment>.Update.Set(g => g.Status, "Open")
                        .Set(g => g.AppointmentId, (int?)null));


                if (result.ModifiedCount == 0)
                {
                    throw new InvalidOperationException("Could not finalize group approval.");
                }

                TempData["AdminDashboardSuccess"] = "Group appointment approved and added to the tutor schedule.";

                // Send approval notification to tutor
                if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
                {
                    try
                    {
                        await _emailService.SendTutorGroupApprovedAsync(tutor.Email, $"{tutor.FName} {tutor.LName}", group.GroupId,
                            group.AppointmentDate, group.StartTime, group.EndTime, group.Capacity);
                    }
                    catch (Exception)
                    {
                        TempData["AdminDashboardSuccess"] = "Group appointment approved, but the email " + "notification could not be queued.";
                    }
                }

                TempData["AdminDashboardSuccess"] = "Group appointment approved and added to the tutor schedule.";
            }
            catch (Exception ex)
            {
                TempData["AdminDashboardError"] = "Approval requires review: " + ex.Message;
            }

            return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGroupAppointment(int groupId)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var group = dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId && g.Status == "Pending")
                .FirstOrDefault();

            if (group == null)
            {
                TempData["AdminDashboardError"] = "Group request not found or already reviewed.";
                return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
            }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == group.TutorId)
                .FirstOrDefault();

            var result = dBContext.GroupAppointments.UpdateOne(
                g => g.GroupId == groupId && g.Status == "Pending",
                Builders<GroupAppointment>.Update.Set(g => g.Status, "Rejected"));

            if (result.ModifiedCount == 0)
            {
                TempData["AdminDashboardError"] = "Group request not found or already reviewed.";
                return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
            }

            TempData["AdminDashboardSuccess"] = "Group appointment request rejected.";

            // Send rejection notification to tutor
            if (tutor != null && !tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
            {
                try
                {
                    await _emailService.SendTutorGroupRejectedAsync(tutor.Email, $"{tutor.FName} {tutor.LName}", group.GroupId,
                        group.AppointmentDate, group.StartTime, group.EndTime, group.Capacity);
                }
                catch (Exception)
                {
                    TempData["AdminDashboardSuccess"] = "Group request rejected, but the email " + "notification could not be queued.";
                }
            }

            return RedirectToAction("Dashboard", new { showAdminFollowUps = true });
        }

        private void LoadSubjects()
        {
            ViewBag.Subjects = dBContext.Subject.Find(_ => true).SortBy(s => s.Name).ToList();
        }

        [HttpGet]
        public IActionResult CreateTutor()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }
            LoadSubjects();
            return View();
        }
      
        [HttpPost]
        public IActionResult CreateTutor(TutorRegistration model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null){ return RedirectToAction("Login", "Login"); }

            model.Email = model.Email?.Trim().ToLowerInvariant();

            if (!ModelState.IsValid)
            {
                LoadSubjects();
                return View(model);
            }

            var existingUsername = dBContext.Login.Find(u => u.Username == model.Username).FirstOrDefault();
            if (existingUsername != null)
            {
                ModelState.AddModelError("Username", "This username is already in use.");
                LoadSubjects();
                return View(model);
            }

            var existingEmail = dBContext.Tutor.Find(t => t.Email == model.Email).FirstOrDefault();
            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "A tutor with this email already exists.");
                LoadSubjects();
                return View(model);
            }

            var lastUser = dBContext.Login.Find(_ => true).SortByDescending(u => u.UserId).FirstOrDefault();
            int newUserId = lastUser == null ? 1 : lastUser.UserId + 1;

            var newLogin = new Login
            {
                UserId = newUserId,
                Username = model.Username,
                Password = "", // Set the hash immediately below
                Role = UserRole.Tutor,
                CreatedDate = DateTime.Now,
                LastLogin = DateTime.Now,
                isActive = true,
                CreatedBy = adminId.Value
            };

            newLogin.Password = _passwordHasher.HashPassword(newLogin, model.Password);
            dBContext.Login.InsertOne(newLogin);

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

        [HttpGet]
        public IActionResult CreateAdmin()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            return View(new AdminRegistration());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAdmin(AdminRegistration model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Username = model.Username?.Trim() ?? "";
            model.FName = model.FName?.Trim() ?? "";
            model.LName = model.LName?.Trim() ?? "";
            model.PhoneNumber = model.PhoneNumber?.Trim() ?? "";
            model.Email = model.Email?.Trim().ToLowerInvariant() ?? "";

            if (dBContext.Login.Find(u => u.Username == model.Username).FirstOrDefault() != null)
            {
                ModelState.AddModelError(nameof(model.Username), "This username is already in use.");
                return View(model);
            }

            if (dBContext.Admin.Find(a => a.Email == model.Email).FirstOrDefault() != null ||
                dBContext.Tutor.Find(t => t.Email == model.Email).FirstOrDefault() != null ||
                dBContext.Student.Find(s => s.Email == model.Email).FirstOrDefault() != null)
            {
                ModelState.AddModelError(nameof(model.Email), "This email address is already in use.");
                return View(model);
            }

            var lastUser = dBContext.Login.Find(_ => true).SortByDescending(u => u.UserId).FirstOrDefault();
            int newUserId = lastUser == null ? 1 : lastUser.UserId + 1;

            var newLogin = new Login
            {
                UserId = newUserId,
                Username = model.Username,
                Password = "",
                Role = UserRole.Administrator,
                CreatedDate = DateTime.Now,
                LastLogin = DateTime.Now,
                isActive = true,
                CreatedBy = adminId.Value
            };
            newLogin.Password = _passwordHasher.HashPassword(newLogin, model.Password);
            dBContext.Login.InsertOne(newLogin);

            var newAdmin = new Admin
            {
                AdminId = newUserId,
                FName = model.FName,
                LName = model.LName,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email
            };
            dBContext.Admin.InsertOne(newAdmin);

            TempData["AdminDashboardSuccess"] = "Admin account created successfully.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateTutor(int tutorId, string fName, string lName, string subject, string email, string phoneNumber)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == tutorId).FirstOrDefault();

            if (tutor == null)
            {
                TempData["AdminDashboardError"] = "The selected tutor could not be found.";
                return RedirectToAction("Dashboard");
            }

            fName = fName?.Trim() ?? "";
            lName = lName?.Trim() ?? "";
            subject = subject?.Trim() ?? "";
            email = email?.Trim().ToLowerInvariant() ?? "";
            phoneNumber = phoneNumber?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(fName) || string.IsNullOrWhiteSpace(lName) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
            {
                TempData["AdminDashboardError"] = "Please complete all required tutor fields.";
                return RedirectToAction("Dashboard", new { editTutorId = tutorId });
            }

            var duplicateEmail = dBContext.Tutor.Find(t => t.Email == email && t.TutorId != tutorId).FirstOrDefault();
            if (duplicateEmail != null)
            {
                TempData["AdminDashboardError"] = "Another tutor is already using this email.";
                return RedirectToAction("Dashboard", new { editTutorId = tutorId });
            }

            var update = Builders<Tutor>.Update
                .Set(t => t.FName, fName)
                .Set(t => t.LName, lName)
                .Set(t => t.Subject, subject)
                .Set(t => t.Email, email)
                .Set(t => t.PhoneNumber, phoneNumber);
            dBContext.Tutor.UpdateOne(t => t.TutorId == tutorId, update);

            TempData["AdminDashboardSuccess"] = "Tutor information updated successfully.";
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
                dBContext.Tutor.DeleteOne(t => t.TutorId == id);
                dBContext.Login.DeleteOne(l => l.UserId == id);

                var affectedAppointments = dBContext.Appointments.Find(a => a.TutorId == id && a.Status == "Scheduled").ToList();

                foreach (var appointment in affectedAppointments)
                {
                    var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled");
                    dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointment.AppointmentId, update);
                }
                dBContext.Schedule.DeleteMany(s => s.TutorId == id);
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteStudent(int id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var student = dBContext.Student.Find(s => s.StudentId == id).FirstOrDefault();

            if (student != null)
            {
                var appointments = dBContext.Appointments.Find(a => a.StudentId == id && a.Status == "Scheduled").ToList();
                foreach (var appointment in appointments)
                {
                    var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled");
                    dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointment.AppointmentId, update);
                }

                dBContext.Login.DeleteOne(l => l.UserId == id);
                dBContext.Student.DeleteOne(s => s.StudentId == id);
            }
            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public IActionResult Scheduler(int? month, int? year)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var loginUser = dBContext.Login
                .Find(l => l.UserId == adminId.Value && l.Role == UserRole.Administrator)
                .FirstOrDefault();

            ViewBag.ShowAdminScheduleTour = loginUser != null
                && !loginUser.HasCompletedAdminScheduleTour;

            ViewBag.AdminId = adminId;

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            if (targetMonth < 1 || targetMonth > 12 || targetYear < 1 || targetYear > 9998)
            {
                targetMonth = DateTime.Today.Month;
                targetYear = DateTime.Today.Year;
            }

            var startOfMonth = new DateTime(targetYear, targetMonth, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var appointments = dBContext.Appointments.Find(_ => true).ToList()
                .Where(a => a.AppointmentDate.Date >= startOfMonth.Date && a.AppointmentDate.Date <= endOfMonth.Date)
                .OrderBy(a => a.AppointmentDate).ToList();

            foreach (var appointment in appointments)
            {
                appointment.Tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();
                appointment.Student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            }

            var schedules = dBContext.Schedule.Find(_ => true).ToList()
                .Where(s => s.Date.Month == targetMonth && s.Date.Year == targetYear)
                .OrderBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

            foreach (var schedule in schedules)
            {
                schedule.Tutor = dBContext.Tutor.Find(t => t.TutorId == schedule.TutorId).FirstOrDefault();
            }

            var tutors = dBContext.Tutor.Find(_ => true).ToList();

            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;
            ViewBag.Tutors = tutors;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;

            return View("AdminScheduler");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CompleteSchedulerTour()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null) { return Unauthorized(); }

            var update = Builders<Login>.Update.Set(u => u.HasCompletedAdminScheduleTour, true);

            var result = dBContext.Login.UpdateOne(u => u.UserId == adminId.Value && u.Role == UserRole.Administrator, update);

            if (result.MatchedCount == 0) { return NotFound(); }

            return Ok(new { success = true });
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
                TempData["AdminSchedulerError"] = "You cannot add a schedule slot for a date that has already passed.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            if (date.Date == today && startTime <= now.TimeOfDay)
            {
                TempData["AdminSchedulerError"] = "You cannot add a schedule slot with a start time that has already passed. Please choose a future time.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            if (endTime <= startTime)
            {
                TempData["AdminSchedulerError"] = "End time must be after start time.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == tutorId).FirstOrDefault();
            if (tutor == null)
            {
                TempData["AdminSchedulerError"] = "The selected tutor was not found.";
                return RedirectToAction("Scheduler", new { month = date.Month, year = date.Year });
            }

            var existingSlots = dBContext.Schedule.Find(s => s.TutorId == tutorId).ToList();
            bool overlaps = existingSlots.Any(s => s.Date.Date == date.Date && startTime < s.EndTime && endTime > s.StartTime);
            if (overlaps)
            {
                TempData["AdminSchedulerError"] = "An existing schedule already overlaps with this time for the selected tutor. " + "If you want to add this time, please delete the preexisting schedule first.";
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
            TempData["AdminSchedulerSuccess"] = "Schedule added successfully.";

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
                TempData["AdminDashboardError"] = "Only scheduled appointments can be cancelled.";
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
                TempData["AdminDashboardError"] = "The appointment could not be cancelled because the associated student or tutor could not be found.";
                return RedirectToAction("Dashboard");
            }

            var admin = dBContext.Admin.Find(a => a.AdminId == adminId.Value).FirstOrDefault();

            if (admin == null)
            {
                TempData["AdminDashboardError"] = "The administrator account could not be found.";

                return RedirectToAction("Dashboard");
            }

            var adminName = $"{admin.FName} {admin.LName}".Trim();

            if (string.IsNullOrWhiteSpace(adminName))
            {
                adminName = $"Admin #{adminId.Value}";
            }

            var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled")
                .Set(a => a.CancelledByAdminId, adminId.Value).Set(a => a.CancelledByAdminName, adminName);

            var result = dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId && a.Status == "Scheduled", update);

            if (result.ModifiedCount == 0)
            {
                TempData["AdminDashboardError"] = "The appointment could not be cancelled. " + "It may already have been cancelled.";
                return RedirectToAction("Dashboard");
            }

            var studentName = $"{student.FName} {student.LName}";
            var tutorName = $"{tutor.FName} {tutor.LName}";
            try
            {
                // Notify the student.
                if (!student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendAdminCancelledAppointmentAsync(student.Email, studentName, tutorName, appointment.AppointmentDate.AddHours(5), "Student", adminName);
                }

                // Notify the tutor.
                if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService.SendAdminCancelledAppointmentAsync(tutor.Email, tutorName, studentName, appointment.AppointmentDate.AddHours(5), "Tutor", adminName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cancellation email failed: {ex.Message}");
                TempData["AdminDashboardError"] = "The appointment was cancelled, but a notification email could not be sent.";
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteAppointment(int appointmentId)
        {
            var appointment = await dBContext.Appointments.Find(a => a.AppointmentId == appointmentId).FirstOrDefaultAsync();
            if (appointment == null) { return NotFound(); }

            if (appointment.Status != "Scheduled" || !appointment.AdminNotificationSent)
            {
                TempData["AdminDashboardError"] = "This appointment cannot be completed until the Admin notification has been sent.";
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
                TempData["AdminDashboardError"] = "The appointment could not be completed. It may have already been updated.";
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

                if (student != null && tutor != null && !student.DisableEmailNotifications && !string.IsNullOrWhiteSpace(student.Email))
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

            TempData["AdminDashboardSuccess"] = $"Appointment #{appointmentId} has been marked as completed.";

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelGroupAppointment(int groupId)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var group = await dBContext.GroupAppointments
                .Find(g => g.GroupId == groupId)
                .FirstOrDefaultAsync();

            if (group == null)
            {
                TempData["AdminDashboardError"] = "The selected group appointment could not be found.";
                return RedirectToAction("Dashboard");
            }

            if (group.Status != "Open" && group.Status != "Full")
            {
                TempData["AdminDashboardError"] = "Only open or full group appointments can be cancelled.";
                return RedirectToAction("Dashboard");
            }

            var admin = await dBContext.Admin
                .Find(a => a.AdminId == adminId.Value)
                .FirstOrDefaultAsync();

            if (admin == null)
            {
                TempData["AdminDashboardError"] = "The administrator account could not be found.";
                return RedirectToAction("Dashboard");
            }

            string adminName = $"{admin.FName} {admin.LName}".Trim();

            if (string.IsNullOrWhiteSpace(adminName))
            {
                adminName = $"Admin #{adminId.Value}";
            }

            // Only one administrator can successfully cancel the group.
            var filter = Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId)
                &
                Builders<GroupAppointment>.Filter.In(g => g.Status, new[] { "Open", "Full" });

            var update = Builders<GroupAppointment>.Update.Set(g => g.Status, "Cancelled");

            var result = await dBContext.GroupAppointments.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
            {
                TempData["AdminDashboardError"] = "This group appointment has already been updated.";
                return RedirectToAction("Dashboard");
            }

            // Remove only this group's schedule
            try
            {
                await dBContext.Schedule.DeleteManyAsync(s => s.GroupId == groupId && s.IsGroup);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Group schedule removal failed: {ex.Message}");
                TempData["AdminDashboardError"] = "The group was cancelled, but its schedule could not be removed. Please review the tutor schedule.";
                return RedirectToAction("Dashboard");
            }

            var tutor = await dBContext.Tutor
                .Find(t => t.TutorId == group.TutorId)
                .FirstOrDefaultAsync();

            string tutorName = tutor != null ? $"{tutor.FName} {tutor.LName}".Trim() : "Tutor";

            var studentIds = group.StudentIds?.Distinct().ToList() ?? new List<int>();

            var students = await dBContext.Student
                .Find(s => studentIds.Contains(s.StudentId))
                .ToListAsync();

            bool emailFailed = false;

            // Notify the tutor.
            if (tutor != null && !tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
            {
                try
                {
                    await _emailService.SendAdminCancelledGroupToTutorAsync(tutor.Email, tutorName, adminName,
                        group.GroupId, group.AppointmentDate, group.StartTime, group.EndTime, studentIds.Count);
                }
                catch (Exception ex)
                {
                    emailFailed = true;
                    Console.WriteLine($"Tutor group cancellation email failed: {ex.Message}");
                }
            }

            // Notify every enrolled student.
            foreach (var student in students)
            {
                if (student.DisableEmailNotifications || string.IsNullOrWhiteSpace(student.Email))
                {
                    continue;
                }

                try
                {
                    string studentName = $"{student.FName} {student.LName}".Trim();
                    await _emailService.SendAdminCancelledGroupToStudentAsync(student.Email, studentName, tutorName,
                        adminName, group.GroupId, group.AppointmentDate, group.StartTime, group.EndTime);
                }
                catch (Exception ex)
                {
                    emailFailed = true;
                    Console.WriteLine($"Student group cancellation email failed: {ex.Message}");
                }
            }

            TempData["AdminDashboardSuccess"] = $"Group appointment #{groupId} has been cancelled.";

            if (emailFailed)
            {
                TempData["AdminDashboardError"] = "The group was cancelled, but one or more " + "notification emails could not be queued.";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteScheduleSlot(int scheduleId)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var slot = dBContext.Schedule.Find(s => s.ScheduleId == scheduleId).FirstOrDefault();
            if (slot == null)
            {
                TempData["AdminDashboardError"] = "Schedule slot not found.";
                return RedirectToAction("Dashboard");
            }

            int tutorId = slot.TutorId;
            var tutorAppointments = dBContext.Appointments.Find(a => a.TutorId == tutorId).ToList();
            var hasAppointments = tutorAppointments.Any(a =>
            {
                if (a.Status != "Scheduled")
                    return false;
                var localAppointmentDate = a.AppointmentDate;
                return localAppointmentDate.Date == slot.Date.Date && localAppointmentDate.TimeOfDay >= slot.StartTime && localAppointmentDate.TimeOfDay < slot.EndTime;
            });

            if (hasAppointments)
            {
                TempData["AdminDashboardError"] =
                    "You cannot delete this schedule slot because it has a scheduled appointment. " +
                    "Please cancel or remove the associated appointment first.";
                return RedirectToAction("Dashboard", new { viewTutorId = tutorId });
            }
            dBContext.Schedule.DeleteOne(s => s.ScheduleId == scheduleId && s.TutorId == tutorId);

            TempData["AdminDashboardSuccess"] = "Schedule deleted successfully.";
            return RedirectToAction("Dashboard", new { viewTutorId = tutorId });
        }

        [HttpGet]
        public IActionResult DownloadSystemUsageReport()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var allLogins = dBContext.Login.Find(_ => true).ToList();
            var last7Days = DateTime.Today.AddDays(-6);

            var loginsPerDay = allLogins.Where(l => l.LastLogin >= last7Days).GroupBy(l => l.LastLogin.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() }).OrderBy(g => g.Date).ToList();

            var roleLogins = allLogins.GroupBy(l => l.Role).Select(g => new { Role = g.Key.ToString(), Count = g.Count() }).OrderBy(g => g.Role).ToList();

            var userLastLogins = allLogins
                .Select(l => new
                {
                    l.Username, Role = l.Role.ToString(), l.LastLogin
                }).OrderBy(u => u.Username).ToList();

            int dateWidth = 14;
            int totalLoginsWidth = 24;
            int roleWidth = roleLogins.Any() ? Math.Max(roleLogins.Max(r => r.Role.Length), "Role".Length) + 4 : "Role".Length + 4;
            int usernameWidth = userLastLogins.Any() ? Math.Max(userLastLogins.Max(u => u.Username.Length), "Username".Length) + 2 : "Username".Length + 2;
            int roleUserWidth = userLastLogins.Any() ? Math.Max(userLastLogins.Max(u => u.Role.Length), "Role".Length) + 4 : "Role".Length + 4;
            int lastLoginWidth = 24;

            string separator = new string('-', usernameWidth + roleUserWidth + lastLoginWidth + 10);

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

            string reportContent = string.Join(Environment.NewLine, lines);
            byte[] fileBytes = Encoding.UTF8.GetBytes(reportContent);
            string fileName = $"TAMS_SystemUsageReport_{DateTime.Now:yyyyMMddHHmmss}.txt";

            return File(fileBytes, "text/plain", fileName);
        }

        [HttpGet]
        public IActionResult DownloadDailyAppointmentsReport(DateTime? date)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            DateTime reportDate = (date ?? DateTime.Today).Date;
            DateTime nextDate = reportDate.AddDays(1);

            var appointments = dBContext.Appointments.Find(a => a.AppointmentDate >= reportDate && a.AppointmentDate < nextDate).ToList()
                .Where(a => !string.Equals(a.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.AppointmentDate).ToList();

            var groupAppointments = dBContext.GroupAppointments.Find(g => g.AppointmentDate >= reportDate && g.AppointmentDate < nextDate).ToList()
                .Where(g => !string.Equals(g.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(g.Status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(g.Status?.Trim(), "Rejected", StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.AppointmentDate.Date.Add(g.StartTime)).ToList();

            var allAppointments = appointments
                .Select(a => new
                {
                    AppointmentId = a.AppointmentId.ToString(),
                    AppointmentDate = a.AppointmentDate,
                    EndDate = a.EndDate,
                    TutorId = a.TutorId,
                    StudentId = (int?)a.StudentId,
                    Status = a.Status ?? "Scheduled",
                    Type = "Individual"
                })
                .Concat(groupAppointments.Select(g => new
                {
                    AppointmentId = g.GroupId.ToString(),
                    AppointmentDate = g.AppointmentDate.Date.Add(g.StartTime),
                    EndDate = g.AppointmentDate.Date.Add(g.EndTime),
                    TutorId = g.TutorId,
                    StudentId = (int?)null,
                    Status = g.Status,
                    Type = "Group"
                }))
                .OrderBy(a => a.AppointmentDate).ToList();

            var lines = new List<string>
            {
                "TAMS Daily Appointments Report",
                $"Report Date: {reportDate:MM/dd/yyyy}",
                $"Generated: {DateTime.Now:MM/dd/yyyy hh:mm tt}",
                new string('-', 100),
                "",
                $"Total Appointments: {allAppointments.Count}",
                "",
                "APPOINTMENT DETAILS",
                new string('-', 100)
            };

            if (allAppointments.Count == 0)
            {
                lines.Add("No appointments found for this date.");
            }
            else
            {
                foreach (var appt in allAppointments)
                {
                    var tutor = dBContext.Tutor.Find(t => t.TutorId == appt.TutorId).FirstOrDefault();
                    var student = appt.StudentId.HasValue
                        ? dBContext.Student.Find(s => s.StudentId == appt.StudentId.Value).FirstOrDefault()
                        : null;

                    string tutorName = tutor != null ? $"{tutor.FName} {tutor.LName}" : "Tutor unavailable";
                    string studentName = appt.Type == "Group" ? "Group" :
                        student != null ? $"{student.FName} {student.LName}" : "Student unavailable";

                    lines.Add($"{(appt.Type == "Group" ? "Group ID" : "Appointment ID")} : {appt.AppointmentId}");
                    lines.Add($"Type           : {appt.Type}");
                    lines.Add($"Date           : {appt.AppointmentDate:MM/dd/yyyy}");
                    lines.Add($"Start Time     : {appt.AppointmentDate:h:mm tt}");
                    lines.Add($"End Time       : {appt.EndDate:h:mm tt}");
                    lines.Add($"Tutor          : {tutorName}");
                    lines.Add($"Student        : {studentName}");
                    lines.Add($"Status         : {appt.Status}");
                    lines.Add(new string('-', 100));
                }
            }

            byte[] fileBytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
            string fileName = $"TAMS_DailyAppointments_{reportDate:yyyyMMdd}.txt";

            return File(fileBytes, "text/plain", fileName);
        }

        [HttpGet]
        public IActionResult DownloadTutorSchedulesReport()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            DateTime now = DateTime.Now;

            var schedules = dBContext.Schedule.Find(_ => true).ToList();
            var tutors = dBContext.Tutor.Find(_ => true).ToList();
            var tutorSchedules = schedules
                .Where(s => s.Date.Date.Add(s.EndTime) > now)
                .Select(s =>
                {
                    var tutor = tutors.FirstOrDefault(t => t.TutorId == s.TutorId);
                    return new
                    {
                        s.ScheduleId,
                        s.TutorId,
                        TutorName = tutor == null ? "Unknown Tutor" : $"{tutor.FName} {tutor.LName}",
                        s.Date,
                        s.StartTime,
                        s.EndTime
                    };
                })
                .OrderBy(s => s.TutorId).ThenBy(s => s.Date).ThenBy(s => s.StartTime).ToList();

            int tutorWidth = Math.Max("Tutor Name".Length, tutorSchedules.Any() ? tutorSchedules.Max(s => s.TutorName.Length) : 0) + 4;
            int dateWidth = 12;
            int timeWidth = 14;
            int idWidth = 12;

            string separator = new string('-', idWidth + tutorWidth + dateWidth + timeWidth * 2 + 16);

            var lines = new List<string>
            {
                "TAMS Tutor Schedules Report",
                $"Generated: {DateTime.Now:MM/dd/yyyy hh:mm tt}",
                separator,
                "",
                string.Format(
                    "{0,-" + idWidth +
                    "} | {1,-" + tutorWidth +
                    "} | {2,-" + dateWidth +
                    "} | {3,-" + timeWidth +
                    "} | {4,-" + timeWidth + "}",
                    "Schedule ID",
                    "Tutor Name",
                    "Date",
                    "Start Time",
                    "End Time"),
                separator
            };

            if (!tutorSchedules.Any())
            {
                lines.Add("No active or upcoming tutor schedules were found.");
            }
            else
            {
                foreach (var schedule in tutorSchedules)
                {
                    lines.Add(string.Format(
                        "{0,-" + idWidth +
                        "} | {1,-" + tutorWidth +
                        "} | {2,-" + dateWidth +
                        "} | {3,-" + timeWidth +
                        "} | {4,-" + timeWidth + "}",
                        schedule.ScheduleId,
                        schedule.TutorName,
                        schedule.Date.ToString("MM/dd/yyyy"),
                        DateTime.Today.Add(schedule.StartTime).ToString("hh:mm tt"),
                        DateTime.Today.Add(schedule.EndTime).ToString("hh:mm tt")));
                }
            }

            lines.Add("");
            lines.Add($"Total schedules: {tutorSchedules.Count}");

            string reportContent = string.Join(Environment.NewLine, lines);
            byte[] fileBytes = Encoding.UTF8.GetBytes(reportContent);
            string fileName = $"TAMS_TutorSchedulesReport_{DateTime.Now:yyyyMMddHHmmss}.txt";

            return File(fileBytes, "text/plain", fileName);
        }

        [HttpGet]
        public IActionResult DownloadAllAppointmentsReport()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var appointments = dBContext.Appointments.Find(_ => true).ToList()
                .Where(a => !string.Equals(a.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.AppointmentDate).ToList();

            var groupAppointments = dBContext.GroupAppointments.Find(_ => true).ToList()
                .Where(g => !string.Equals(g.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(g.Status?.Trim(), "Rejected", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(g.Status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase))
                .OrderBy(g => g.AppointmentDate).ToList();

            var tutors = dBContext.Tutor.Find(_ => true).ToList();

            var allAppointments = appointments
                .Select(a => new
                {
                    AppointmentId = a.AppointmentId.ToString(),
                    AppointmentDate = a.AppointmentDate,
                    EndDate = a.EndDate,
                    TutorName = a.Tutor != null ? $"{a.Tutor.FName} {a.Tutor.LName}" : "Tutor unavailable",
                    StudentName = a.Student != null ? $"{a.Student.FName} {a.Student.LName}" : "Student unavailable",
                    Status = a.Status ?? "Scheduled",
                    Type = "Individual"
                })
                .Concat(groupAppointments.Select(g => new
                {
                    AppointmentId = g.GroupId.ToString(),
                    AppointmentDate = g.AppointmentDate.Date.Add(g.StartTime),
                    EndDate = g.AppointmentDate.Date.Add(g.EndTime),
                    TutorName = tutors.FirstOrDefault(t => t.TutorId == g.TutorId) is var tutor && tutor != null
                        ? $"{tutor.FName} {tutor.LName}" : "Tutor unavailable",
                    StudentName = "Group",
                    Status = g.Status,
                    Type = "Group"
                }))
                .OrderBy(a => a.AppointmentDate).ToList();

            int scheduledCount = allAppointments.Count(a =>
                string.Equals(a.Status?.Trim(), "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Status?.Trim(), "Open", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Status?.Trim(), "Full", StringComparison.OrdinalIgnoreCase));

            int completedCount = allAppointments.Count(a =>
                string.Equals(a.Status?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase));

            int cancelledCount = 0;

            var lines = new List<string>
            {
                "TAMS All Appointments Report",
                $"Generated: {DateTime.Now:MM/dd/yyyy hh:mm tt}",
                new string('=', 90),
                "",
                "APPOINTMENT SUMMARY",
                new string('-', 40),
                $"Total Appointments : {allAppointments.Count}",
                $"Scheduled          : {scheduledCount}",
                $"Completed          : {completedCount}",
                $"Cancelled          : {cancelledCount}",
                "",
                "ALL APPOINTMENTS",
                new string('=', 90)
            };

            if (allAppointments.Count == 0)
            {
                lines.Add("No appointments found.");
            }
            else
            {
                foreach (var appt in allAppointments)
                {
                    lines.Add($"{(appt.Type == "Group" ? "Group ID" : "Appointment ID")} : {appt.AppointmentId}");
                    lines.Add($"Type           : {appt.Type}");
                    lines.Add($"Date           : {appt.AppointmentDate:MM/dd/yyyy}");
                    lines.Add($"Start Time     : {appt.AppointmentDate:h:mm tt}");
                    lines.Add($"End Time       : {appt.EndDate:h:mm tt}");
                    lines.Add($"Tutor          : {appt.TutorName}");
                    lines.Add($"Student        : {appt.StudentName}");
                    lines.Add($"Status         : {appt.Status}");
                    lines.Add(new string('-', 90));
                }
            }

            byte[] fileBytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
            string fileName = $"TAMS_AllAppointments_{DateTime.Now:yyyyMMddHHmmss}.txt";

            return File(fileBytes, "text/plain", fileName);
        }

        [HttpGet]
        public IActionResult DownloadUserAccountsReport()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var tutors = dBContext.Tutor.Find(_ => true).ToList().OrderBy(t => t.TutorId).ToList();
            var students = dBContext.Student.Find(_ => true).ToList().OrderBy(s => s.StudentId).ToList();

            var lines = new List<string>
                {
                    "TAMS User Accounts Report",
                    $"Generated: {DateTime.Now:MM/dd/yyyy hh:mm tt}",
                    new string('=', 80),
                    "",
                    "ACCOUNT SUMMARY",
                    new string('-', 40),
                    $"Total Tutors   : {tutors.Count}",
                    $"Total Students : {students.Count}",
                    $"Total Accounts : {tutors.Count + students.Count}",
                    "",
                    "REGISTERED TUTORS",
                    new string('=', 80)
                };

            if (tutors.Count == 0)
            {
                lines.Add("No tutors found.");
            }
            else
            {
                foreach (var tutor in tutors)
                {
                    lines.Add($"Tutor ID    : {tutor.TutorId}");
                    lines.Add($"Name        : {tutor.FName} {tutor.LName}");
                    lines.Add($"Subject     : {tutor.Subject}");
                    lines.Add($"Email       : {tutor.Email}");
                    lines.Add($"Phone       : {tutor.PhoneNumber}");
                    lines.Add(new string('-', 80));
                }
            }

            lines.Add("");
            lines.Add("REGISTERED STUDENTS");
            lines.Add(new string('=', 80));

            if (students.Count == 0)
            {
                lines.Add("No students found.");
            }
            else
            {
                foreach (var student in students)
                {
                    lines.Add($"Student ID  : {student.StudentId}");
                    lines.Add($"Name        : {student.FName} {student.LName}");
                    lines.Add($"Email       : {student.Email}");
                    lines.Add($"Phone       : {student.PhoneNumber}");
                    lines.Add(new string('-', 80));
                }
            }

            byte[] fileBytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
            string fileName = $"TAMS_UserAccounts_{DateTime.Now:yyyyMMddHHmmss}.txt";

            return File(fileBytes, "text/plain", fileName);
        }

    }
}
