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

        // Displays the Forgot Password page
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult VerifyUser(ForgotPassword model)
        {
            if (!ModelState.IsValid)
            {
                return View("ForgotPassword", model);
            }

            // Find the active login account using the username.
            var user = dBContext.Login
                .Find(u =>
                    u.Username == model.Username &&
                    u.isActive == true)
                .FirstOrDefault();

            // Username was not found.
            if (user == null)
            {
                ModelState.AddModelError("", "The username or email is incorrect.");
                return View("ForgotPassword", model);
            }

            // Verify that the email belongs to this account.
            bool emailMatches = false;

            switch (user.Role)
            {
                case UserRole.Student:

                    var student = dBContext.Student
                        .Find(s =>
                            s.StudentId == user.UserId &&
                            s.Email == model.Email)
                        .FirstOrDefault();
                    emailMatches = student != null;
                    break;

                case UserRole.Tutor:

                    var tutor = dBContext.Tutor
                        .Find(t =>
                            t.TutorId == user.UserId &&
                            t.Email == model.Email)
                        .FirstOrDefault();

                    emailMatches = tutor != null;

                    break;

                case UserRole.Administrator:

                    var admin = dBContext.Admin
                        .Find(a =>
                            a.AdminId == user.UserId &&
                            a.Email == model.Email)
                        .FirstOrDefault();
                    emailMatches = admin != null;
                    break;
            }

            // Username exists, but email does not belong to the account
            if (!emailMatches)
            {
                ModelState.AddModelError("", "The username or email is incorrect.");
                return View("ForgotPassword", model);
            }

            // random 6-digit recovery code
            var recoveryCode = Random.Shared.Next(100000, 1000000).ToString();

            // Delete any previous unused recovery codes
            // for this user.
            dBContext.PasswordRecoveries.DeleteMany(r => r.UserId == user.UserId && !r.IsUsed);

            // Create the new recovery record.
            var recoveryRecord = new PasswordRecovery
            {
                UserId = user.UserId,
                RecoveryCode = recoveryCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false
            };

            // Save the recovery code to MongoDB.
            dBContext.PasswordRecoveries.InsertOne(recoveryRecord);

            // Store the user ID for the next recovery step.
            TempData["RecoveryUserId"] = user.UserId;

            // Store the email for the next step.
            TempData["RecoveryEmail"] = model.Email;

            // Show the recovery-code field.
            model.AccountVerified = true;

            ViewBag.SuccessMessage = "Your account has been verified. A recovery code has been generated.";

            // TEMP
            ViewBag.RecoveryCode = recoveryCode;

            return View("ForgotPassword", model);
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