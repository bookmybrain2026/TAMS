using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class LoginController : Controller
    {
        //Provides access to MongoDB database
        private readonly MongoDBContext dBContext;

        //Constructor that initializes the MongoDBContext for database access
        public LoginController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        //Display the Login page
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        //Processes the Login form submission
        [HttpPost]
        public IActionResult Login(Login model)
        {
            //Check whether the submitted Login information is valid
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            //Searches MongoDB for an active user matching the submitted username, password, and role
            var user = 
                dBContext.Login.Find(u => 
                    u.Username == model.Username &&
                    u.Password == model.Password &&
                    u.Role == model.Role &&
                    u.isActive == true).FirstOrDefault();

            //If no matching user is found, display an error message
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid username, password, or role.");

                return View(model);
            }

            var update = //Creates an update to record the user's most recent login time
                Builders<Login>.Update
                .Set(u => u.LastLogin, DateTime.Now);

            //Updates the user's LastLogin field in MongoDB
            dBContext.Login.UpdateOne(
                u => u.UserId == user.UserId,
                update);

            //Redirects the user to the appropriate dahsboard based on their role
            switch (user.Role)
            {
                case UserRole.Student:
                    {
                        HttpContext.Session.SetInt32("StudentId", user.UserId);
                        return RedirectToAction("Dashboard", "Student", new { id = user.UserId });
                    }

                case UserRole.Tutor:
                    {
                        HttpContext.Session.SetInt32("TutorId", user.UserId);
                        return RedirectToAction("Dashboard", "Tutor", new { id = user.UserId });
                    }

                case UserRole.Administrator:
                    {
                        HttpContext.Session.SetInt32("AdminId", user.UserId);

                        return RedirectToAction("Dashboard", "Admin", new { id = user.UserId });
                    }

                default: //Handles any unexpected or unsupported user roles
                    {
                        ModelState.AddModelError("", "Unknown user role."); 
                        return View(model);
                    }
            }
        }

        //Logs the current user out
        [HttpPost]
        public IActionResult Logout()
        {
            //Clears all session information for the current user
            HttpContext.Session.Clear();

            //Redirects the user back to the Login page
            return RedirectToAction("Login", "Login");
        }
    }
}