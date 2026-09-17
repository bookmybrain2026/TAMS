using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class AdminController : Controller
    {
        private readonly MongoDBContext dBContext;

        public AdminController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        // Displays the administrator dashboard
        public IActionResult Dashboard(int id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }
            return View("AdminDashboard");
        }

        // Displays the Create Tutor page
        [HttpGet]
        public IActionResult CreateTutor()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }
            return View();
        }

        // Creates the Tutor and Login accounts
        [HttpPost]
        public IActionResult CreateTutor(TutorRegistration model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check whether the username already exists
            var existingUsername = dBContext.Login
                .Find(u => u.Username == model.Username)
                .FirstOrDefault();

            if (existingUsername != null)
            {
                ModelState.AddModelError("Username", "This username is already in use.");
                return View(model);
            }

            // Check whether the email already exists
            var existingEmail = 
                dBContext.Tutor
                .Find(t => t.Email == model.Email).FirstOrDefault();

            if (existingEmail != null)
            {
                ModelState.AddModelError("Email", "A tutor with this email already exists.");
                return View(model);
            }

            // Find the highest existing UserId
            var lastUser = dBContext.Login
                .Find(_ => true)
                .SortByDescending(u => u.UserId)
                .FirstOrDefault();

            // Generate the next UserId
            int newUserId = lastUser == null ? 1 : lastUser.UserId + 1;

            // Create the Tutor's Login account
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

            // Save Login account
            dBContext.Login.InsertOne(newLogin);

            // Create the Tutor profile
            var newTutor = new Tutor
            {
                TutorId = newUserId,
                FName = model.FName,
                LName = model.LName,
                Subject = model.Subject,
                PhoneNumber = model.PhoneNumber,
                Email = model.Email
            };

            // Save Tutor profile
            dBContext.Tutor.InsertOne(newTutor);

            // Return to the Admin Dashboard
            return RedirectToAction("Dashboard");
        }
    }
}