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
        private readonly IConfiguration _configuration;

        public LoginController( MongoDBContext dBContext, IEmailService emailService, IConfiguration configuration)
        {
            this.dBContext = dBContext;
            _emailService = emailService;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(Login model)
        {
            if (!ModelState.IsValid) { return View(model); }

            var user =  dBContext.Login.Find(u => u.Username == model.Username && u.Password == model.Password && u.Role == model.Role && u.isActive == true).FirstOrDefault();
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid username, password, or role.");
                return View(model);
            }

            var update = Builders<Login>.Update.Set(u => u.LastLogin, DateTime.Now);
            dBContext.Login.UpdateOne(u => u.UserId == user.UserId, update);

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

                default:
                    {
                        ModelState.AddModelError("", "Unknown user role."); 
                        return View(model);
                    }
            }
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            var model = new ForgotPassword();

            if (TempData["ShowPasswordFields"] != null)
            {
                model.ShowPasswordFields = (bool)TempData["ShowPasswordFields"];
            }

            if (TempData["Username"] != null)
            {
                model.Username = TempData["Username"].ToString();
            }

            if (TempData["Email"] != null)
            {
                model.Email = TempData["Email"].ToString();
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> VerifyUser(ForgotPassword model)
        {
            ModelState.Remove("RecoveryCode");
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmPassword");

            if (!ModelState.IsValid) { return View("ForgotPassword", model); }

            var user = dBContext.Login.Find(u => u.Username == model.Username && u.isActive == true).FirstOrDefault();

            if (user == null)
            {
                ModelState.AddModelError( "", "The username or email is incorrect.");
                return View("ForgotPassword", model);
            }

            bool emailMatches = false;
            string recipientName = "";

            switch (user.Role)
            {
                case UserRole.Student:
                    var student = dBContext.Student.Find(s => s.StudentId == user.UserId && s.Email == model.Email).FirstOrDefault();
                    if (student != null)
                    {
                        emailMatches = true;
                        recipientName = $"{student.FName} {student.LName}";
                    }
                    break;

                case UserRole.Tutor:
                    var tutor = dBContext.Tutor.Find(t => t.TutorId == user.UserId && t.Email == model.Email).FirstOrDefault();
                    if (tutor != null)
                    {
                        emailMatches = true;
                        recipientName = $"{tutor.FName} {tutor.LName}";
                    }
                    break;

                case UserRole.Administrator:
                    var admin = dBContext.Admin.Find(a => a.AdminId == user.UserId && a.Email == model.Email).FirstOrDefault();
                    if (admin != null)
                    {
                        emailMatches = true;
                        recipientName = $"{admin.FName} {admin.LName}";
                    }
                    break;
            }

            if (!emailMatches)
            {
                ModelState.AddModelError("", "The username or email is incorrect.");
                return View("ForgotPassword", model);
            }

            var recoveryCode = Random.Shared.Next(100000, 1000000).ToString();
            var recoveryRecord = new PasswordRecovery
            {
                UserId = user.UserId,
                RecoveryCode = recoveryCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false
            };

            dBContext.PasswordRecoveries.DeleteMany(r => r.UserId == user.UserId && !r.IsUsed);        
            dBContext.PasswordRecoveries.InsertOne(recoveryRecord);

            bool emailSent = false;

            bool emailCredentialsAvailable =
                !string.IsNullOrWhiteSpace(_configuration["EmailSettings:SenderEmail"]) &&
                !string.IsNullOrWhiteSpace(_configuration["EmailSettings:SmtpServer"]) &&
                !string.IsNullOrWhiteSpace(_configuration["EmailSettings:SmtpPort"]) &&
                !string.IsNullOrWhiteSpace(_configuration["EmailSettings:Username"]) &&
                !string.IsNullOrWhiteSpace(_configuration["EmailSettings:Password"]);

            if (emailCredentialsAvailable)
            {
                try
                {
                    await _emailService.SendPasswordRecoveryCodeAsync(
                        model.Email,
                        recipientName,
                        recoveryCode);

                    emailSent = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Password recovery email error: {ex.Message}");
                }
            }

            model.ShowRecoveryCodeField = true;

            TempData["RecoveryUserId"] = user.UserId;

            if (!emailSent)
            {
                ViewBag.RecoveryCode = recoveryCode;
                TempData["RecoveryCode"] = recoveryCode;
                TempData["EmailError"] =
                    "The recovery email could not be sent. Please use the recovery code shown below.";
            }
            else
            {
                TempData["Success"] =
                    "A recovery code has been sent to your email address.";
            }

            return View("ForgotPassword", model);

        }

        [HttpPost]
        public IActionResult VerifyRecoveryCode(ForgotPassword model)
        {
            ModelState.Remove("NewPassword");
            ModelState.Remove("ConfirmPassword");

            var userId = TempData["RecoveryUserId"];

            if (userId == null)
            {
                ModelState.AddModelError("", "Your recovery session has expired. Please start again.");
                return View("ForgotPassword", model);
            }

            int parsedUserId = Convert.ToInt32(userId);
            var recovery = dBContext.PasswordRecoveries.Find(r =>r.UserId == parsedUserId && r.RecoveryCode == model.RecoveryCode && !r.IsUsed && r.ExpiresAt > DateTime.UtcNow).FirstOrDefault();

            if (recovery == null)
            {
                ModelState.AddModelError("RecoveryCode", "The recovery code is invalid or has expired.");
                model.ShowRecoveryCodeField = true;
                return View("ForgotPassword", model);
            }

            model.CodeVerified = true;
            model.ShowPasswordFields = true;

            TempData["RecoveryUserId"] = parsedUserId;
            TempData["RecoveryCodeVerified"] = true;
            TempData["ShowPasswordFields"] = true;

            return View("ForgotPassword", model);
        }

        [HttpPost]
        public IActionResult ResetPassword(ForgotPassword model)
        {
            ModelState.Remove("Email");
            ModelState.Remove("RecoveryCode");

            if (TempData["RecoveryCodeVerified"] == null)
            {
                ModelState.AddModelError("", "Please verify your recovery code first.");
                model.ShowRecoveryCodeField = true;
                return View("ForgotPassword", model);
            }

            if (!ModelState.IsValid)
            {
                model.ShowPasswordFields = true;
                model.CodeVerified = true;
                return View("ForgotPassword", model);
            }

            var userId = TempData["RecoveryUserId"];

            if (userId == null)
            {
                ModelState.AddModelError("", "Your recovery session has expired. Please start again.");
                return View("ForgotPassword", model);
            }

            int parsedUserId = Convert.ToInt32(userId);
            var user = dBContext.Login.Find(l => l.UserId == parsedUserId && l.isActive).FirstOrDefault();
            if (user == null)
            {
                ModelState.AddModelError("", "Account not found. Please try again.");
                return View("ForgotPassword", model);
            }

            user.Password = model.NewPassword;
            dBContext.Login.ReplaceOne(l => l.UserId == parsedUserId,user);

            dBContext.PasswordRecoveries.UpdateOne(r => r.UserId == parsedUserId && r.RecoveryCode == model.RecoveryCode, Builders<PasswordRecovery>.Update.Set(r => r.IsUsed, true));

            TempData["Success"] = "Password updated successfully. Please log in.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Login");
        }
    
    }
}