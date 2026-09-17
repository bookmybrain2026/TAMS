using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class StudentRegistrationController : Controller
    {
        private readonly MongoDBContext dBContext;

        public StudentRegistrationController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(StudentRegistration model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check whether the username already exists
            var existingUser = dBContext.Login
                .Find(u => u.Username == model.Username)
                .FirstOrDefault();

            if (existingUser != null)
            {
                ModelState.AddModelError("Username", "This username is already in use.");
                return View(model);
            }

            // Create a new UserId
            var lastUser = 
                dBContext.Login
                .Find(_ => true)
                .SortByDescending(u => u.UserId).FirstOrDefault();

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

            // Insert both documents into MongoDB
            dBContext.Login.InsertOne(login);
            dBContext.Student.InsertOne(student);

            return RedirectToAction("Login", "Login");
        }
    }
}