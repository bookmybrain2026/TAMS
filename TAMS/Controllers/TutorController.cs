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

        // Tutor Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null) { return RedirectToAction("Login", "Login"); }

            var tutor = dBContext.Tutor
                .Find(t => t.TutorId == tutorId.Value)
                .FirstOrDefault();

            if (tutor == null) { return RedirectToAction("Login", "Login"); }

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

    }
}