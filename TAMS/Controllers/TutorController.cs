using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class TutorController : Controller
    {
        private readonly MongoDBContext dBContext;

        public TutorController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        // Public Tutor Directory
        [HttpGet]
        public IActionResult Index()
        {
            var tutors = dBContext.Tutor
                .Find(_ => true)
                .SortBy(t => t.LName)
                .ThenBy(t => t.FName)
                .ToList();

            return View(tutors);
        }

        // Public Tutor Profile
        [HttpGet]
        public IActionResult Details(int id)
        {
            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == id)
                .FirstOrDefault();

            if (tutor == null) { return NotFound(); }

            // Get the courses this tutor tutors
            var courseIds = tutor.TutoringCourseIds ?? new List<string>();

            var courses = dBContext.Course
                .Find(c => c.CourseId != null && courseIds.Contains(c.CourseId))
                .SortBy(c => c.CourseCode)
                .ToList();

            // Get the subject name
            var subject = dBContext.Subject
                .Find(s => s.Code == tutor.Subject)
                .FirstOrDefault();

            ViewBag.Courses = courses;
            ViewBag.SubjectName = subject?.Name ?? tutor.Subject;

            return View(tutor);
        }

        // Tutor Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            // Retrieve the TutorId stored in the session during login
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Retrieve the tutor
            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (tutor == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Retrieve all appointments belonging to this tutor
            var appointments = dBContext.Appointments
                .Find(a => a.TutorId == tutor.TutorId)
                .SortBy(a => a.AppointmentDate)
                .ToList();

            // Retrieve the student connected to each appointment
            foreach (var appointment in appointments)
            {
                appointment.Student = dBContext.Student
                    .Find(s => s.StudentId == appointment.StudentId)
                    .FirstOrDefault();
            }

            // Retrieve all schedules belonging to this tutor
            var schedules = dBContext.Schedule
                .Find(s => s.TutorId == tutor.TutorId)
                .SortBy(s => s.Date)
                .ToList();

            // Attach the information to the tutor object
            // so the dashboard view can access it.
            ViewBag.Appointments = appointments;
            ViewBag.Schedules = schedules;

            return View("TutorDashboard", tutor);
        }


        // Display Tutor Profile
        [HttpGet]
        public IActionResult Profile()
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (tutor == null) { return RedirectToAction("Login", "Login"); }

            LoadProfileData();
            return View(tutor);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(Tutor model)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var existingTutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (existingTutor == null) { return NotFound("Tutor profile was not found."); } 

            // If validation fails, show the actual error
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e =>
                        $"{x.Key}: {e.ErrorMessage} {e.Exception?.Message}"))
                    .ToList();

                TempData["ErrorMessage"] = "Profile was not saved. " + string.Join(" | ", errors);
                LoadProfileData();
                return View(model);
            }

            // Get selected courses
            var selectedCourseIds = model.TutoringCourseIds ?? new List<string>();

            // Only save courses that belong to the selected subject
            var validCourseIds = dBContext.Course
                .Find(c =>
                    c.CourseId != null &&
                    selectedCourseIds.Contains(c.CourseId) &&
                    c.SubjectCode == model.Subject)
                .ToList()
                .Select(c => c.CourseId!)
                .ToList();

            // Update tutor
            var update = Builders<Tutor>.Update
                .Set(t => t.FName, model.FName)
                .Set(t => t.LName, model.LName)
                .Set(t => t.Email, model.Email)
                .Set(t => t.PhoneNumber, model.PhoneNumber)
                .Set(t => t.Subject, model.Subject)
                .Set(t => t.TutoringCourseIds, validCourseIds);

            var result = dBContext.Tutor.UpdateOne(
                t => t.TutorId == tutorId.Value,
                update);

            if (result.MatchedCount == 0)
            {
                TempData["ErrorMessage"] = "Tutor profile was not found in the database.";
                return RedirectToAction(nameof(Profile));
            }
            TempData["SuccessMessage"] = "Your profile has been updated successfully.";
            return RedirectToAction(nameof(Profile));
        }


        // Load subjects and courses for the profile page
        private void LoadProfileData()
        {
            ViewBag.Subjects = dBContext.Subject
                .Find(_ => true)
                .SortBy(s => s.Name)
                .ToList();

            ViewBag.Courses = dBContext.Course
                .Find(_ => true)
                .SortBy(c => c.CourseCode)
                .ToList();
        }

        // Displays the tutor's schedule for a selected month
        [HttpGet]
        public IActionResult Schedule(int id, int? month, int? year)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Prevent a tutor from viewing another tutor's schedule
            if (tutorId.Value != id)
            {
                return Unauthorized();
            }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == id)
                .FirstOrDefault();

            if (tutor == null)
            {
                return NotFound();
            }

            int targetMonth = month ?? DateTime.Today.Month;
            int targetYear = year ?? DateTime.Today.Year;

            var schedules = dBContext.Schedule
                .Find(s =>
                    s.TutorId == id &&
                    s.Date.Month == targetMonth &&
                    s.Date.Year == targetYear)
                .ToList();

            ViewBag.TutorId = id;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;

            return View(schedules);
        }

        // Adds an available time period to the tutor's schedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddSchedule(
            int tutorId,
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime)
        {
            var loggedInTutorId = HttpContext.Session.GetInt32("TutorId");

            if (loggedInTutorId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // Prevent a tutor from modifying another tutor's schedule
            if (loggedInTutorId.Value != tutorId)
            {
                return Unauthorized();
            }

            if (endTime <= startTime)
            {
                TempData["Error"] = "End time must be after start time.";

                return RedirectToAction(
                    "Schedule",
                    new
                    {
                        id = tutorId,
                        month = date.Month,
                        year = date.Year
                    });
            }

            var existingSchedules = dBContext.Schedule
                .Find(s =>
                    s.TutorId == tutorId &&
                    s.Date.Date == date.Date)
                .ToList();

            // Check for overlapping schedules
            bool overlap = existingSchedules.Any(s =>
                startTime < s.EndTime &&
                endTime > s.StartTime);

            if (overlap)
            {
                TempData["Error"] =
                    "This schedule overlaps with an existing schedule.";

                return RedirectToAction(
                    "Schedule",
                    new
                    {
                        id = tutorId,
                        month = date.Month,
                        year = date.Year
                    });
            }

            // Generate a new ScheduleId
            var lastSchedule = dBContext.Schedule
                .Find(_ => true)
                .SortByDescending(s => s.ScheduleId)
                .FirstOrDefault();

            int newScheduleId =
                lastSchedule == null
                    ? 1
                    : lastSchedule.ScheduleId + 1;

            var schedule = new Schedule
            {
                ScheduleId = newScheduleId,
                TutorId = tutorId,
                Date = date.Date,
                StartTime = startTime,
                EndTime = endTime
            };

            dBContext.Schedule.InsertOne(schedule);

            TempData["Success"] = "Schedule added successfully.";

            return RedirectToAction(
                "Schedule",
                new
                {
                    id = tutorId,
                    month = date.Month,
                    year = date.Year
                });
        }

        // Deletes a tutor's available schedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSchedule(int scheduleId)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var schedule = dBContext.Schedule
                .Find(s => s.ScheduleId == scheduleId)
                .FirstOrDefault();

            if (schedule == null)
            {
                return NotFound();
            }

            // Prevent a tutor from deleting another tutor's schedule
            if (schedule.TutorId != tutorId.Value)
            {
                return Unauthorized();
            }

            dBContext.Schedule.DeleteOne(
                s => s.ScheduleId == scheduleId);

            TempData["Success"] = "Schedule deleted successfully.";

            return RedirectToAction(
                "Schedule",
                new
                {
                    id = tutorId.Value,
                    month = schedule.Date.Month,
                    year = schedule.Date.Year
                });
        }
        [HttpGet]
        public IActionResult ScheduleCalendar(
    int id,
    int? month,
    int? year)
        {
            var tutorId =
                HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            // A tutor can only view their own appointment calendar.
            if (tutorId.Value != id)
            {
                return Unauthorized();
            }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == id)
                .FirstOrDefault();

            if (tutor == null)
            {
                return NotFound();
            }

            int targetMonth =
                month ?? DateTime.Today.Month;

            int targetYear =
                year ?? DateTime.Today.Year;

            var startOfMonth =
                new DateTime(
                    targetYear,
                    targetMonth,
                    1);

            var startOfNextMonth =
                startOfMonth.AddMonths(1);

            var appointments =
                dBContext.Appointments
                    .Find(a =>
                        a.TutorId == id &&
                        a.AppointmentDate >= startOfMonth &&
                        a.AppointmentDate < startOfNextMonth)
                    .ToList();

            // Attach student information
            foreach (var appointment in appointments)
            {
                appointment.Student =
                    dBContext.Student
                        .Find(s =>
                            s.StudentId ==
                            appointment.StudentId)
                        .FirstOrDefault();
            }

            ViewBag.TutorId = id;
            ViewBag.Month = targetMonth;
            ViewBag.Year = targetYear;
            ViewBag.Appointments = appointments;

            return View("ScheduleCalendar");
        }
    }
}