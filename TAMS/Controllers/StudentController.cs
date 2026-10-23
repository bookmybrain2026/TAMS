using AMS.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Numerics;
using TAMS.Data;
using TAMS.Models;
using TAMS.Services;

namespace TAMS.Controllers
{
    public class StudentController : Controller
    {
        private readonly MongoDBContext dBContext;
        private readonly IEmailService _emailService;

        public StudentController(MongoDBContext dBContext, IEmailService emailService)
        {
            this.dBContext = dBContext;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Dashboard(int? id, string searchTerm = "", bool showStudentReminders = false)
        {
            var loggedInStudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedInStudentId == null) { return RedirectToAction("Login", "Login"); }

            if (id == null || loggedInStudentId.Value != id.Value) { return Unauthorized(); }

            var student = dBContext.Student.Find(s => s.StudentId == id.Value).FirstOrDefault();

            if (student == null) { return NotFound(); }

            var tutors = dBContext.Tutor.Find(_ => true).ToList();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                tutors = tutors
                    .Where(t =>
                        ($"{t.FName} {t.LName}").Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                        ||
                        (!string.IsNullOrWhiteSpace(t.Subject) && t.Subject.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var reviews = dBContext.Reviews.Find(_ => true).ToList();
            foreach (var tutor in tutors)
            {
                var tutorReviews = reviews.Where(r => r.TutorId == tutor.TutorId).ToList();
                tutor.Reviews = tutorReviews;
                tutor.ReviewCount = tutorReviews.Count;
                tutor.AverageRating = tutorReviews.Any() ? Math.Round(tutorReviews.Average(r => r.Rating), 1) : 0;
            }

            student.Appointments = dBContext.Appointments.Find(a => a.StudentId == student.StudentId).ToList();

            foreach (var appointment in student.Appointments)
            {
                appointment.Tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();
            }

            var reviewedAppointmentIds = dBContext.Reviews.Find(r => r.StudentId == student.StudentId).ToList().Select(r => r.AppointmentId).ToHashSet();

            student.AwaitingReviews = student.Appointments
                .Where(a => a.Status == "Completed" && !reviewedAppointmentIds.Contains(a.AppointmentId)).OrderByDescending(a => a.AppointmentDate)
                .Select(a => new Review
                {
                    AppointmentId = a.AppointmentId, AppointmentDate = a.AppointmentDate, StudentId = a.StudentId, TutorId = a.TutorId,
                    StudentName = $"{student.FName} {student.LName}", TutorName = a.Tutor != null ? $"{a.Tutor.FName} {a.Tutor.LName}" : "Unknown Tutor"
                })
                .ToList();

            ViewBag.Tutors = tutors;
            ViewBag.SearchTerm = searchTerm;

            var studentReminderAppointments = student.Appointments
                .Where(a => a.Status == "Scheduled")
                .Where(a => (a.Reminder24HoursBeforeSent && !a.StudentReminder24HoursAcknowledged) || (a.Reminder1HourBeforeSent && !a.StudentReminder1HourAcknowledged))
                .OrderBy(a => a.AppointmentDate).ToList();

            ViewBag.StudentReminderAppointments = studentReminderAppointments;

            if (showStudentReminders) { return PartialView("_StudentReminderModal", studentReminderAppointments); }

            return View("StudentDashboard", student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmAppointmentReminder(int appointmentId, string reminderType)
        {
            var studentId = HttpContext.Session.GetInt32("StudentId");
            if (studentId == null) { return RedirectToAction("Login", "Login"); }

            if (reminderType != "24Hours" && reminderType != "1Hour")
            {
                TempData["ErrorMessage"] = "Invalid reminder selected.";
                return RedirectToAction("Dashboard", new { id = studentId.Value });
            }

            var appointment = await dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId && a.StudentId == studentId.Value).FirstOrDefaultAsync();

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction("Dashboard", new { id = studentId.Value });
            }

            if (appointment.Status != "Scheduled")
            {
                TempData["ErrorMessage"] = "This appointment is no longer scheduled.";
                return RedirectToAction("Dashboard", new { id = studentId.Value });
            }

            var filter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointmentId)
                & Builders<Appointment>.Filter.Eq(a => a.StudentId, studentId.Value)
                & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled");
            UpdateDefinition<Appointment> update;

            if (reminderType == "24Hours")
            {
                if (!appointment.Reminder24HoursBeforeSent)
                {
                    TempData["ErrorMessage"] = "The 24-hour reminder has not been sent yet.";

                    return RedirectToAction("Dashboard", new { id = studentId.Value });
                }
                filter &= Builders<Appointment>.Filter.Ne(a => a.StudentReminder24HoursAcknowledged, true);
                update = Builders<Appointment>.Update.Set(a => a.StudentReminder24HoursAcknowledged, true);
            }
            else
            {
                if (!appointment.Reminder1HourBeforeSent)
                {
                    TempData["ErrorMessage"] = "The 1-hour reminder has not been sent yet.";
                    return RedirectToAction("Dashboard", new { id = studentId.Value });
                }
                filter &= Builders<Appointment>.Filter.Ne(a => a.StudentReminder1HourAcknowledged, true);
                update = Builders<Appointment>.Update.Set(a => a.StudentReminder1HourAcknowledged, true);
            }

            var result = await dBContext.Appointments.UpdateOneAsync(filter, update);

            if (result.ModifiedCount > 0)
            {
                TempData["SuccessMessage"] = reminderType == "24Hours" ? "24-hour appointment reminder confirmed." : "1-hour appointment reminder confirmed.";
            }
            else
            {
                TempData["InfoMessage"] = "This reminder was already confirmed or the appointment has changed.";
            }
            return RedirectToAction("Dashboard", new { id = studentId.Value });
        }

        [HttpGet]
        public IActionResult TutorProfile(int tutorId, int studentId)
        {
            var loggedinstudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedinstudentId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor.Find(t => t.TutorId == tutorId).FirstOrDefault();
            if (tutor == null) { return NotFound(); }

            var reviews = dBContext.Reviews.Find(r => r.TutorId == tutorId).ToList();

            tutor.Reviews = reviews;
            tutor.ReviewCount = reviews.Count;
            tutor.AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

            var courseIds = tutor.TutoringCourseIds ?? new List<string>();

            var courses = dBContext.Course.Find(c => c.CourseId != null && courseIds.Contains(c.CourseId)).SortBy(c => c.CourseCode) .ToList();

            ViewBag.StudentId = studentId;
            ViewBag.Courses = courses;

            return View("TutorProfile", tutor);
        }

        [HttpPost]
        public IActionResult UpdateInfo(Student updatedStudent)
        {
            var loggedinstudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedinstudentId == null) { return RedirectToAction("Login", "Login"); }

            var student =
                dBContext.Student
                .Find(s => s.StudentId == updatedStudent.StudentId).FirstOrDefault();

            if (student == null){return NotFound(); }

            var update =
                Builders<Student>.Update
                .Set(s => s.FName, updatedStudent.FName)
                .Set(s => s.LName, updatedStudent.LName)
                .Set(s => s.Email, updatedStudent.Email)
                .Set(s => s.PhoneNumber, updatedStudent.PhoneNumber)
                .Set(s => s.DOB, updatedStudent.DOB);
            dBContext.Student.UpdateOne(s => s.StudentId == updatedStudent.StudentId,update);

            return RedirectToAction("Dashboard", new { id = updatedStudent.StudentId });
        }

        [HttpGet]
        public IActionResult CreateAccount()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateAccount(string username, string password, Student studentModel)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View(studentModel);
            }

            var existingLogin =
                dBContext.Login
                .Find(l => l.Username == username).FirstOrDefault();

            if (existingLogin != null)
            {
                ViewBag.Error = "That username already exists.";
                return View(studentModel);
            }

            var lastLogin = dBContext.Login.Find(_ => true).SortByDescending(l => l.UserId).FirstOrDefault();

            int newUserId = lastLogin == null ? 1 : lastLogin.UserId + 1;

            var login = new Login
                {
                    UserId = newUserId,
                    Username = username,
                    Password = password,
                    Role = UserRole.Student,
                    CreatedDate = DateTime.Now,
                    LastLogin = DateTime.Now,
                    isActive = true
                };

            dBContext.Login.InsertOne(login);

            studentModel.StudentId = newUserId;
            dBContext.Student.InsertOne(studentModel);

            return RedirectToAction( "Dashboard", new { id = newUserId });
        }

        [HttpGet]
        public IActionResult StudentRegistration()
        {
            return View();
        }

        [HttpPost]
        public IActionResult StudentRegistration(StudentRegistration model)
        {
            if (!ModelState.IsValid) { return View(model); }

            var existingUsername = dBContext.Login.Find(u => u.Username == model.Username).FirstOrDefault();
            var existingEmail = dBContext.Student.Find(s => s.Email == model.Email).FirstOrDefault();

            if (existingUsername != null || existingEmail != null)
            {
                ModelState.AddModelError("", "An account with this username or email already exists. Please use the password reset option to recover your account.");
                return View(model);
            }

            var lastUser = dBContext.Login.Find(_ => true).SortByDescending(u => u.UserId).FirstOrDefault();
            int newUserId = lastUser == null ? 1 : lastUser.UserId + 1;

            var login = new Login
            {
                UserId = newUserId,
                Role = UserRole.Student,
                Username = model.Username,
                Password = model.Password,
                CreatedDate = DateTime.Now,
                LastLogin = DateTime.Now,
                isActive = true
            };

            var student = new Student
            {
                StudentId = newUserId,
                FName = model.FName,
                LName = model.LName,
                DOB = model.DOB,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email
            };

            dBContext.Login.InsertOne(login);
            dBContext.Student.InsertOne(student);

            return RedirectToAction("Login", "Login");
        }

        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int appointmentId)
        {
            var appointment =  dBContext.Appointments.Find(a => a.AppointmentId == appointmentId).FirstOrDefault();

            if (appointment == null) { return NotFound(); }

            if (appointment.Status == "Cancelled")
            {
                return RedirectToAction("Dashboard", new { id = appointment.StudentId });
            }

            var student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            var tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();

            if (student == null || tutor == null) { return NotFound(); }

            var update = Builders<Appointment>.Update.Set(a => a.Status, "Cancelled");
            var result = dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId, update);
            if (result.ModifiedCount == 0)
            {
                return RedirectToAction("Dashboard", new { id = appointment.StudentId });
            }

            var studentName = $"{student.FName} {student.LName}";
            var tutorName = $"{tutor.FName} {tutor.LName}";

            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendStudentAppointmentCancellationAsync(student.Email, studentName, tutorName, appointment.AppointmentDate.AddHours(5));
                }
                if (!string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService.SendTutorAppointmentCancellationAsync(tutor.Email, tutorName,  studentName, appointment.AppointmentDate.AddHours(5));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cancellation email failed: {ex.Message}");
            }

            return RedirectToAction( "Dashboard", new { id = appointment.StudentId });
        }     

        [HttpPost]
        public IActionResult SubmitReview(int appointmentId, int rating, string comment)
        {
            var appointment = dBContext.Appointments.Find(a => a.AppointmentId == appointmentId).FirstOrDefault();
            if (appointment == null) { return NotFound(); }

            if (appointment.Status != "Completed")
            {
                TempData["Error"] = "You can only review completed appointments.";
                return RedirectToAction("Dashboard", new { id = appointment.StudentId });
            }

            var existingReview = dBContext.Reviews.Find(r => r.AppointmentId == appointmentId).FirstOrDefault();

            if (existingReview != null)
            {
                TempData["Error"] = "You have already reviewed this appointment.";
                return RedirectToAction("Dashboard", new { id = appointment.StudentId });
            }

            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "Please select a rating from 1 to 5.";
                return RedirectToAction("Dashboard", new { id = appointment.StudentId });
            }

            var student = dBContext.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();
            var tutor = dBContext.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefault();

            if (student == null || tutor == null) { return NotFound(); }

            var review = new Review
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentDate = appointment.AppointmentDate,
                StudentId = student.StudentId,
                TutorId = tutor.TutorId,
                StudentName = $"{student.FName} {student.LName}",
                TutorName = $"{tutor.FName} {tutor.LName}",
                Rating = rating,
                Comment = comment ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            dBContext.Reviews.InsertOne(review);
            TempData["Success"] = "Your review has been submitted successfully.";

            return RedirectToAction("Dashboard", new { id = student.StudentId });
        } 
        
    }
}