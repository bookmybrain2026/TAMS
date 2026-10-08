using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;
using TAMS.Services;

namespace TAMS.Controllers
{
    public class LoginController : Controller
    {
        private readonly MongoDBContext dBContext; //Provides access to MongoDB database
        private readonly IEmailService _emailService;

        public LoginController( MongoDBContext dBContext, IEmailService emailService)
        {
            this.dBContext = dBContext;
            _emailService = emailService;
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
            var model = new ForgotPassword();

            // Check whether the password fields should be displayed
            if (TempData["ShowPasswordFields"] != null)
            {
                model.ShowPasswordFields = (bool)TempData["ShowPasswordFields"];
            }

            // Retrieve the username
            if (TempData["Username"] != null)
            {
                model.Username = TempData["Username"].ToString();
            }

            // Retrieve the email
            if (TempData["Email"] != null)
            {
                model.Email = TempData["Email"].ToString();
            }
            return View(model);
        }

        // Verifies the username and email address
        [HttpPost]
        public async Task<IActionResult> VerifyUser(ForgotPassword model)
        {
            ModelState.Remove("RecoveryCode");
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmPassword");

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
                ModelState.AddModelError( "", "The username or email is incorrect.");
                return View("ForgotPassword", model);
            }

            // Verify that the email belongs to the account.
            bool emailMatches = false;
            string recipientName = "";

            switch (user.Role)
            {
                case UserRole.Student:
                    var student = dBContext.Student
                        .Find(s =>
                            s.StudentId == user.UserId &&
                            s.Email == model.Email)
                        .FirstOrDefault();
                    if (student != null)
                    {
                        emailMatches = true;
                        recipientName = $"{student.FName} {student.LName}";
                    }
                    break;

                case UserRole.Tutor:
                    var tutor = dBContext.Tutor
                        .Find(t =>
                            t.TutorId == user.UserId &&
                            t.Email == model.Email)
                        .FirstOrDefault();
                    if (tutor != null)
                    {
                        emailMatches = true;
                        recipientName = $"{tutor.FName} {tutor.LName}";
                    }
                    break;

                case UserRole.Administrator:
                    var admin = dBContext.Admin
                        .Find(a =>
                            a.AdminId == user.UserId &&
                            a.Email == model.Email)
                        .FirstOrDefault();
                    if (admin != null)
                    {
                        emailMatches = true;
                        recipientName = $"{admin.FName} {admin.LName}";
                    }
                    break;
            }

            // Username exists, but email does not belong to the account.
            if (!emailMatches)
            {
                ModelState.AddModelError("", "The username or email is incorrect.");
                return View("ForgotPassword", model);
            }

            // Generate a random 6-digit recovery code.
            var recoveryCode = Random.Shared.Next(100000, 1000000).ToString();

            // Create the new recovery record.
            var recoveryRecord = new PasswordRecovery
            {
                UserId = user.UserId,
                RecoveryCode = recoveryCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false
            };

            // Delete any previous unused recovery codes for this user.
            dBContext.PasswordRecoveries.DeleteMany(r => r.UserId == user.UserId && !r.IsUsed);        

            // Save the recovery code to MongoDB.
            dBContext.PasswordRecoveries.InsertOne(recoveryRecord);


            // Track whether the email was successfully sent.
            bool emailSent = false;

            try
            {
                // Attempt to send the recovery code.
                await _emailService.SendPasswordRecoveryCodeAsync(model.Email, recipientName, recoveryCode);
                emailSent = true;
            }
            catch (Exception ex)
            {
                // Do not allow an email failure to crash the application.
                emailSent = false;
                Console.WriteLine($"Password recovery email error: {ex.Message}");
            }

            model.ShowRecoveryCodeField = true;

            // Store information needed for the next recovery step.
            TempData["RecoveryUserId"] = user.UserId;

            // Email was successfully sent.
            if (!emailSent)
            {
                ViewBag.RecoveryCode = recoveryCode;
                TempData["RecoveryCode"] = recoveryCode;
                TempData["EmailError"] = "The recovery email could not be sent. Please use the recovery code shown below.";
            }
            else
            {
                TempData["Success"] = "A recovery code has been sent to your email address.";
            }
            return View("ForgotPassword", model);
        }

        // Verifies the recovery code
        [HttpPost]
        public IActionResult VerifyRecoveryCode(ForgotPassword model)
        {
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmPassword");

            // Retrieve the user ID from the recovery session.
            var userId = TempData["RecoveryUserId"];

            // Check whether the recovery session exists.
            if (userId == null)
            {
                ModelState.AddModelError("", "Your recovery session has expired. Please start again.");
                return View("ForgotPassword", model);
            }

            // Convert the user ID to an integer.
            int parsedUserId = Convert.ToInt32(userId);

            // Find a valid recovery record.
            var recovery = 
                dBContext.PasswordRecoveries
                .Find(r =>r.UserId == parsedUserId &&
                    r.RecoveryCode == model.RecoveryCode &&
                    !r.IsUsed &&
                    r.ExpiresAt > DateTime.UtcNow).FirstOrDefault();

            // Check whether the recovery code is invalid or expired.
            if (recovery == null)
            {
                ModelState.AddModelError("RecoveryCode", "The recovery code is invalid or has expired.");
                model.ShowRecoveryCodeField = true;
                return View("ForgotPassword", model);
            }

            model.CodeVerified = true;
            model.ShowPasswordFields = true;

            // Store the recovery information needed for the next request.
            TempData["RecoveryUserId"] = parsedUserId;
            TempData["RecoveryCodeVerified"] = true;
            TempData["ShowPasswordFields"] = true;

            return View("ForgotPassword", model);
        }

        // Resets the user's password
        [HttpPost]
        public IActionResult ResetPassword(ForgotPassword model)
        {
            // Remove validation for fields that are not needed when creating the new password.
            ModelState.Remove("Email");
            ModelState.Remove("RecoveryCode");

            // Check whether the recovery code was already verified.
            if (TempData["RecoveryCodeVerified"] == null)
            {
                ModelState.AddModelError("", "Please verify your recovery code first.");
                model.ShowRecoveryCodeField = true;
                return View("ForgotPassword", model);
            }

            // Validate the new password and confirmation.
            if (!ModelState.IsValid)
            {
                model.ShowPasswordFields = true;
                model.CodeVerified = true;
                return View("ForgotPassword", model);
            }

            // Retrieve the user's ID from the recovery session.
            var userId = TempData["RecoveryUserId"];

            if (userId == null)
            {
                ModelState.AddModelError("", "Your recovery session has expired. Please start again.");
                return View("ForgotPassword", model);
            }

            // Convert the user ID to an integer.
            int parsedUserId = Convert.ToInt32(userId);

            // Find the active login account.
            var user = 
                dBContext.Login
                .Find(l => l.UserId == parsedUserId &&
                    l.isActive).FirstOrDefault();

            // Check whether the account was found.
            if (user == null)
            {
                ModelState.AddModelError("", "Account not found. Please try again.");
                return View("ForgotPassword", model);
            }

            // Assign the new password.
            user.Password = model.NewPassword;

            // Save the updated login account.
            dBContext.Login
                .ReplaceOne(l => l.UserId == parsedUserId,user);

            // Mark the recovery code as used.
            dBContext.PasswordRecoveries
                .UpdateOne(r => r.UserId == parsedUserId && r.RecoveryCode == model.RecoveryCode,
                    Builders<PasswordRecovery>.Update.Set(r => r.IsUsed, true));

            TempData["Success"] = "Password updated successfully. Please log in.";
            return RedirectToAction("Login");
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