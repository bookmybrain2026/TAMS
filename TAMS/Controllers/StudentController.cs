using Microsoft.AspNetCore.Mvc;
using TAMS.Data;
using TAMS.Models;
using MongoDB.Driver;

namespace TAMS.Controllers
{
    // Handles pages and functionality related to students
    public class StudentController : Controller
    {
        private readonly MongoDBContext dBContext;

        // Constructor receives the MongoDB context
        public StudentController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        // Displays the student dashboard
        public IActionResult Dashboard(int id)
        {
            // Retrieves the StudentId stored in the session during login
            var studentId = HttpContext.Session.GetInt32("StudentId");

            // Checks whether a student is logged in
            if (studentId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Find the student's profile in MongoDB
            var student = dBContext.Student
                .Find(s => s.StudentId == studentId.Value)
                .FirstOrDefault();

            // Make sure the student profile exists
            if (student == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Pass the student information to StudentDashboard.cshtml
            return View("StudentDashboard", student);
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