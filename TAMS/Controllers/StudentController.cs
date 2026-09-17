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

        // Displays the student registration page
        [HttpGet]
        public IActionResult StudentRegistration()
        {
            return View();
        }

        [HttpPost]
        public IActionResult StudentRegistration(StudentRegistration model)
        {
            // Check whether the submitted information is valid
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check whether the username already exists
            var existingUsername = 
                dBContext.Login
                .Find(u => u.Username == model.Username).FirstOrDefault();

            if (existingUsername != null)
            {
                ModelState.AddModelError("Username", "This username is already in use.");
                return View(model);
            }

            // Check whether the email already exists
            var existingEmail = dBContext.Student
                .Find(s => s.Email == model.Email)
                .FirstOrDefault();

            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            // Find the highest existing UserId
            var lastUser = 
                dBContext.Login.Find(_ => true)
                .SortByDescending(u => u.UserId).FirstOrDefault();

            // Generate the next UserId
            int newUserId = lastUser == null ? 1 : lastUser.UserId + 1;

            // Create the student's login account
            var newLogin = new Login
            {
                UserId = newUserId,
                Username = model.Username,
                Password = model.Password,
                Role = UserRole.Student,
                CreatedDate = DateTime.Now,
                LastLogin = DateTime.Now,
                isActive = true
            };

            // Save the login information to MongoDB
            dBContext.Login.InsertOne(newLogin);

            // Create the student's profile
            var newStudent = new Student
            {
                StudentId = newUserId,
                FName = model.FName,
                LName = model.LName,
                DOB = model.DOB,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email
            };

            // Save the student information to MongoDB
            dBContext.Student.InsertOne(newStudent);

            // Return the student to the login page
            return RedirectToAction("Login", "Login");
        }
    }
}