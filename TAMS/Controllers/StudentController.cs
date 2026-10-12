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
        public IActionResult Dashboard(int? id, string searchTerm = "")
        {
            var loggedinstudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedinstudentId == null) { return RedirectToAction("Login", "Login"); }

            // Redirect to login if no student ID was provided
            if (id == null){ return RedirectToAction("Login", "Login");}

            // Get the student
            var student = dBContext.Student
                .Find(s => s.StudentId == id.Value)
                .FirstOrDefault();

            if (student == null) { return NotFound(); }

            // Get all tutors
            var tutors = dBContext.Tutor.Find(_ => true).ToList();

            // Search tutors by name or subject
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

            // Get all reviews
            var reviews = dBContext.Reviews.Find(_ => true).ToList();


            // Calculate each tutor's average rating and review count
            foreach (var tutor in tutors)
            {
                var tutorReviews = reviews.Where(r => r.TutorId == tutor.TutorId).ToList();

                // Store the tutor's reviews
                tutor.Reviews = tutorReviews;

                // Calculate the average rating and total reviews
                tutor.ReviewCount = tutorReviews.Count;
                tutor.AverageRating = tutorReviews.Any() ? Math.Round(tutorReviews.Average(r => r.Rating), 1) : 0;
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

            // Get appointment IDs that this student has already reviewed
            var reviewedAppointmentIds = dBContext.Reviews
                .Find(r => r.StudentId == student.StudentId).ToList().Select(r => r.AppointmentId).ToHashSet();

            // Build the list of completed appointments awaiting a review
            student.AwaitingReviews = student.Appointments
                .Where(a => a.Status == "Completed" && !reviewedAppointmentIds.Contains(a.AppointmentId))
                .OrderByDescending(a => a.AppointmentDate)
                .Select(a => new Review
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    StudentId = a.StudentId,
                    TutorId = a.TutorId,
                    StudentName = $"{student.FName} {student.LName}",
                    TutorName = a.Tutor != null ? $"{a.Tutor.FName} {a.Tutor.LName}" : "Unknown Tutor"
                })
                .ToList();

            ViewBag.Tutors = tutors;
            ViewBag.SearchTerm = searchTerm;

            return View("StudentDashboard", student);
        }

        [HttpGet]
        public IActionResult TutorProfile(int tutorId, int studentId)
        {
            var loggedinstudentId = HttpContext.Session.GetInt32("StudentId");
            if (loggedinstudentId == null) { return RedirectToAction("Login", "Login"); }
            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId)
                .FirstOrDefault();

            if (tutor == null) { return NotFound(); }
            var reviews =
                dBContext.Reviews
                .Find(r => r.TutorId == tutorId).ToList();

            tutor.Reviews = reviews;
            tutor.ReviewCount = reviews.Count;
            tutor.AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

            var courseIds = tutor.TutoringCourseIds ?? new List<string>();

            var courses = dBContext.Course
                .Find(c => c.CourseId != null &&
                 courseIds.Contains(c.CourseId))
                .SortBy(c => c.CourseCode) .ToList();

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

            var lastLogin =
                dBContext.Login.Find(_ => true)
                .SortByDescending(l => l.UserId).FirstOrDefault();

            int newUserId = lastLogin == null ? 1 : lastLogin.UserId + 1;

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

        [HttpPost]
        public async Task<IActionResult> CancelAppointment(int appointmentId) 
        {
            var appointment = 
                dBContext.Appointments                                          
                .Find(a => a.AppointmentId == appointmentId).FirstOrDefault();  

            if (appointment == null) { return NotFound(); } 

            var student = dBContext.Student
                .Find(s => s.StudentId == appointment.StudentId)
                .FirstOrDefault();

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == appointment.TutorId)
                .FirstOrDefault();

            if (student == null || tutor == null)
            {
                return NotFound();
            }

            var update = 
                Builders<Appointment>.Update                                                    
                .Set(a => a.Status, "Cancelled");                                               
            dBContext.Appointments.UpdateOne(a => a.AppointmentId == appointmentId, update);    

            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    await _emailService.SendAppointmentCancellationAsync(
                        student.Email,
                        $"{student.FName} {student.LName}",
                        $"{tutor.FName} {tutor.LName}",
                        appointment.AppointmentDate
                        );
                }

                if (!string.IsNullOrWhiteSpace(tutor.Email))
                {
                    await _emailService.SendTutorAppointmentCancellationAsync(
                        tutor.Email,
                        $"{tutor.FName} {tutor.LName}",
                        $"{student.FName} {student.LName}",
                        appointment.AppointmentDate
                        );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cancellation email failed: {ex.Message}");
            }

            return RedirectToAction("Dashboard", new { id = appointment.StudentId });
        }

        [HttpGet]
        public IActionResult Scheduler(int tutorId, int studentId, int? month, int? year)
        {
            var tutor = 
                dBContext.Tutor    
                .Find(t => t.TutorId == tutorId).FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            if (targetMonth < 1)
            {
                targetMonth = 12;
                targetYear--;
            }
            else if (targetMonth > 12)
            {
                targetMonth = 1;
                targetYear++;
            }

            var firstDay = new DateTime(targetYear, targetMonth, 1);
            var nextMonth = firstDay.AddMonths(1);

            var appointments = 
                dBContext.Appointments                         
                .Find(a => a.TutorId == tutorId &&            
                    a.AppointmentDate >= firstDay &&            
                    a.AppointmentDate < nextMonth).ToList();    

            foreach (var appointment in appointments)
            {                                 
                appointment.Student =                                                   
                    dBContext.Student                                                   
                    .Find(s => s.StudentId == appointment.StudentId).FirstOrDefault();  
            }

            var schedules = 
                dBContext.Schedule                              
                .Find(s => s.TutorId == tutorId).ToList()     
                .Where(s => s.Date.Month == targetMonth &&      
                    s.Date.Year == targetYear).ToList();        

            ViewBag.Tutor = tutor;
            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.StudentId = studentId;

            return View("StudentScheduler");
        }

        [HttpPost]
        public async Task<IActionResult> BookAppointment(int studentId, int tutorId, DateTime appointmentDate)
        {
            var student =  
                dBContext.Student                                        
                .Find(s => s.StudentId == studentId).FirstOrDefault();   
            if (student == null) { return NotFound(); }  

            var tutor =  
                dBContext.Tutor                                         
                .Find(d => d.TutorId == tutorId).FirstOrDefault();     

            if (tutor == null) { return NotFound(); }  

            if (appointmentDate <= DateTime.Now)  
            {
                TempData["Error"] = "Please select a future appointment time.";
                return RedirectToAction("Scheduler", new { tutorId, studentId });
            }

            var existingAppointment =                          
                dBContext.Appointments.Find(a =>                 
                    a.TutorId == tutorId &&                    
                    a.AppointmentDate == appointmentDate &&      
                    a.Status != "Cancelled").FirstOrDefault();   

            if (existingAppointment != null) 
            {
                TempData["Error"] = "That appointment time is already booked.";
                return RedirectToAction("Scheduler", new { tutorId, studentId });
            }

            var tutorSchedules =  
                dBContext.Schedule                              
                .Find(s => s.TutorId == tutorId).ToList();     

            var matchingSchedule = tutorSchedules  
                .FirstOrDefault(s =>                                 
                    s.Date.Date == appointmentDate.Date &&           
                    appointmentDate.TimeOfDay >= s.StartTime &&      
                    appointmentDate.TimeOfDay < s.EndTime);          

            if (matchingSchedule == null)  
            {
                TempData["Error"] = "The selected time is not available.";

                return RedirectToAction("Scheduler", new { tutorId, studentId });
            }

            var lastAppointment =  
                dBContext.Appointments.Find(_ => true)                       
                .SortByDescending(a => a.AppointmentId).FirstOrDefault();    

            int newAppointmentId = lastAppointment == null ? 1 : lastAppointment.AppointmentId + 1;  

            var appointment = new Appointment  
            {
                AppointmentId = newAppointmentId,
                StudentId = studentId,
                TutorId = tutorId,
                AppointmentDate = appointmentDate, //DUE TO TEMP FIX OF TIME
                //AppointmentDate = TimeZoneHelper.ToUtc(appointmentDate),StudentId = studentId,TutorId = tutorId,Status = "Scheduled" //temp fix as well
                Status = "Scheduled"
            };
            dBContext.Appointments.InsertOne(appointment);  

            
            try  
            {
                if (!string.IsNullOrWhiteSpace(student.Email)) 
                { 
                    await _emailService.SendAppointmentConfirmationAsync( 
                        student.Email,
                        $"{student.FName} {student.LName}",
                        $"{tutor.FName} {tutor.LName}",
                        appointmentDate);
                }

                if (!string.IsNullOrWhiteSpace(tutor.Email)) 
                {
                    await _emailService.SendTutorAppointmentNotificationAsync( 
                        tutor.Email,
                        $"{tutor.FName} {tutor.LName}",
                        $"{student.FName} {student.LName}",
                        appointmentDate);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email failed: {ex.Message}");
            }

            // Return the student to their dashboard
            return RedirectToAction("Dashboard", new { id = studentId });
        }

        [HttpPost]
        public IActionResult SubmitReview(int appointmentId, int rating, string comment)
        {
            var appointment = dBContext.Appointments
                .Find(a => a.AppointmentId == appointmentId)
                .FirstOrDefault();

            if (appointment == null) { return NotFound(); }

            if (appointment.Status != "Completed")
            {
                TempData["Error"] = "You can only review completed appointments.";
                return RedirectToAction("Dashboard", new { id = appointment.StudentId });
            }

            var existingReview = dBContext.Reviews
                .Find(r => r.AppointmentId == appointmentId)
                .FirstOrDefault();

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

            var student = dBContext.Student
                .Find(s => s.StudentId == appointment.StudentId)
                .FirstOrDefault();

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == appointment.TutorId)
                .FirstOrDefault();

            if (student == null || tutor == null) { return NotFound(); }

            // Create the review
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