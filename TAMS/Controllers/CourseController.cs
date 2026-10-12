using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class CourseController : Controller
    {
        private readonly MongoDBContext dBContext;

        public CourseController(MongoDBContext context)
        {
            dBContext = context;
        }


        // Displays all courses
        public IActionResult Index()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }
            var courses = dBContext.Course.Find(_ => true).SortBy(c => c.CourseCode).ToList();
            LoadSubjects();
            return View(courses);
        }


        // Loads subjects from MongoDB for the Subject dropdown
        private void LoadSubjects()
        {
            ViewBag.Subjects = 
                dBContext.Subject.Find(_ => true)
                .SortBy(s => s.Code).ToList();
        }


        // Creates a new course
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Course model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            if (!ModelState.IsValid)
            {
                LoadSubjects();
                var courses = dBContext.Course.Find(_ => true).SortBy(c => c.CourseCode).ToList();
                ViewBag.Action = "Create";
                return View("Index", courses);
            }

            // Check whether the course code already exists
            var existingCourse =  dBContext.Course.Find(c => c.CourseCode == model.CourseCode).FirstOrDefault();

            if (existingCourse != null)
            {
                TempData["ErrorMessage"] = "A course with this course code already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Check whether the selected subject exists
            var subjectExists = dBContext.Subject.Find(s => s.Code == model.SubjectCode).Any();

            if (!subjectExists)
            {
                TempData["ErrorMessage"] = "The selected subject does not exist.";
                return RedirectToAction(nameof(Index));
            }
            // Add the course
            dBContext.Course.InsertOne(model);

            TempData["SuccessMessage"] = "Course was successfully created.";
            return RedirectToAction(nameof(Index));
        }


        // Edits an existing course
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(string id, Course model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please enter valid course information.";
                return RedirectToAction(nameof(Index));
            }
            // Find the existing course
            var existingCourse = dBContext.Course.Find(c => c.CourseId == id).FirstOrDefault();

            if (existingCourse == null) { return NotFound(); }


            // Check whether another course already uses this course code
            var duplicateCourse = dBContext.Course.Find(c => c.CourseCode == model.CourseCode && c.CourseId != id).FirstOrDefault();

            if (duplicateCourse != null)
            {
                TempData["ErrorMessage"] = "A course with this course code already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Check whether the selected subject exists
            var subjectExists = dBContext.Subject.Find(s => s.Code == model.SubjectCode).Any();

            if (!subjectExists)
            {
                TempData["ErrorMessage"] = "The selected subject does not exist.";
                return RedirectToAction(nameof(Index));
            }

            // Update the course
            var update = Builders<Course>.Update
                .Set(c => c.SubjectCode, model.SubjectCode)
                .Set(c => c.CourseCode, model.CourseCode)
                .Set(c => c.CourseName, model.CourseName);

            dBContext.Course.UpdateOne(c => c.CourseId == id, update);

            TempData["SuccessMessage"] = "Course was successfully updated.";
            return RedirectToAction(nameof(Index));
        }


        // Deletes an existing course
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var course = dBContext.Course.Find(c => c.CourseId == id).FirstOrDefault();

            if (course == null) { return NotFound(); }
            // Delete the course
            dBContext.Course.DeleteOne( c => c.CourseId == id);

            TempData["SuccessMessage"] = "Course was successfully deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}