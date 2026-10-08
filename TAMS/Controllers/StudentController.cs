using TAMS.Data;
using TAMS.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace TAMS.Controllers
{
    public class StudentController : Controller
    {
        private readonly MongoDBContext dBContext;

        public StudentController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        [HttpGet]
        public IActionResult Dashboard(int? id, string searchTerm = "")
        {
            // Redirect to login if no student ID was provided
            if (id == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Get the student
            var student = dBContext.Student
                .Find(s => s.StudentId == id.Value)
                .FirstOrDefault();

            if (student == null)
            {
                return NotFound();
            }

            // Get all tutors
            var tutors = dBContext.Tutor
                .Find(_ => true)
                .ToList();

            // Search tutors by name or subject
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();

                tutors = tutors
                    .Where(t =>
                        ($"{t.FName} {t.LName}")
                            .Contains(
                                searchTerm,
                                StringComparison.OrdinalIgnoreCase)
                        ||
                        (!string.IsNullOrWhiteSpace(t.Subject) &&
                         t.Subject.Contains(
                             searchTerm,
                             StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            // Get all appointments belonging to this student
            student.Appointments = dBContext.Appointments
                .Find(a => a.StudentId == student.StudentId)
                .ToList();

            // Attach tutor information to each appointment
            foreach (var appointment in student.Appointments)
            {
                appointment.Tutor =
                    dBContext.Tutor
                    .Find(t => t.TutorId == appointment.TutorId)
                    .FirstOrDefault();
            }

            ViewBag.Tutors = tutors;
            ViewBag.SearchTerm = searchTerm;

            return View("StudentDashboard", student);
        }

        [HttpGet]
        public IActionResult TutorProfile(int tutorId, int studentId)
        {
            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId)
                .FirstOrDefault();

            if (tutor == null)
            {
                return NotFound();
            }

            // Get the courses taught by this tutor
            var courseIds = tutor.TutoringCourseIds
                ?? new List<string>();

            var courses = dBContext.Course
                .Find(c =>
                    c.CourseId != null &&
                    courseIds.Contains(c.CourseId))
                .SortBy(c => c.CourseCode)
                .ToList();

            ViewBag.StudentId = studentId;
            ViewBag.Courses = courses;

            return View("TutorProfile", tutor);
        }

        [HttpPost]
        public IActionResult UpdateInfo(Student updatedStudent)
        {
            // Searches for the student using StudentId
            var student =
                dBContext.Student
                .Find(s => s.StudentId == updatedStudent.StudentId)
                .FirstOrDefault();

            if (student == null)
            {
                return NotFound();
            }

            // Specifies which fields should be changed in MongoDB
            var update =
                Builders<Student>.Update
                .Set(s => s.FName, updatedStudent.FName)
                .Set(s => s.LName, updatedStudent.LName)
                .Set(s => s.Email, updatedStudent.Email)
                .Set(s => s.PhoneNumber, updatedStudent.PhoneNumber)
                .Set(s => s.DOB, updatedStudent.DOB);

            dBContext.Student.UpdateOne(
                s => s.StudentId == updatedStudent.StudentId,
                update);

            return RedirectToAction(
                "Dashboard",
                new { id = updatedStudent.StudentId });
        }

        [HttpGet]
        public IActionResult CreateAccount()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateAccount(
            string username,
            string password,
            Student studentModel)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error =
                    "Username and password are required.";

                return View(studentModel);
            }

            var existingLogin =
                dBContext.Login
                .Find(l => l.Username == username)
                .FirstOrDefault();

            if (existingLogin != null)
            {
                ViewBag.Error =
                    "That username already exists.";

                return View(studentModel);
            }

            var lastLogin =
                dBContext.Login
                .Find(_ => true)
                .SortByDescending(l => l.UserId)
                .FirstOrDefault();

            int newUserId =
                lastLogin == null
                    ? 1
                    : lastLogin.UserId + 1;

            var login =
                new Login
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

            return RedirectToAction(
                "Dashboard",
                new { id = newUserId });
        }
        [HttpGet]
        public IActionResult StudentRegistration()
        {
            return View();
        }

        [HttpPost]
        public IActionResult StudentRegistration(StudentRegistration model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check whether the username already exists
            var existingUsername = dBContext.Login
                .Find(u => u.Username == model.Username)
                .FirstOrDefault();

            // Check whether the email already exists
            var existingEmail = dBContext.Student
                .Find(s => s.Email == model.Email)
                .FirstOrDefault();

            // If either the username or email is already registered, do not create another account.
            if (existingUsername != null || existingEmail != null)
            {
                ModelState.AddModelError("", "An account with this username or email already exists. Please use the password reset option to recover your account.");
                return View(model);
            }

            // Create a new UserId
            var lastUser = dBContext.Login
                .Find(_ => true)
                .SortByDescending(u => u.UserId)
                .FirstOrDefault();

            int newUserId = lastUser == null ? 1 : lastUser.UserId + 1;

            // Create Login account
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

            // Create Student profile
            var student = new Student
            {
                StudentId = newUserId,
                FName = model.FName,
                LName = model.LName,
                DOB = model.DOB,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email
            };

            // Insert into MongoDB
            dBContext.Login.InsertOne(login);
            dBContext.Student.InsertOne(student);

            return RedirectToAction("Login", "Login");
        }
    }
}