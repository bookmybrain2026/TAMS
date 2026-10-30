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

        public IActionResult Index()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }
            var courses = dBContext.Course.Find(_ => true).SortBy(c => c.CourseCode).ToList();
            LoadSubjects();
            return View(courses);
        }

        private void LoadSubjects()
        {
            ViewBag.Subjects = dBContext.Subject.Find(_ => true).SortBy(s => s.Code).ToList();
        }

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

            var existingCourse =  dBContext.Course.Find(c => c.CourseCode == model.CourseCode).FirstOrDefault();
            if (existingCourse != null)
            {
                TempData["CourseError"] = "A course with this course code already exists.";
                return RedirectToAction(nameof(Index));
            }

            var subjectExists = dBContext.Subject.Find(s => s.Code == model.SubjectCode).Any();
            if (!subjectExists)
            {
                TempData["CourseError"] = "The selected subject does not exist.";
                return RedirectToAction(nameof(Index));
            }
            dBContext.Course.InsertOne(model);

            TempData["CourseSuccess"] = "Course was successfully created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(string id, Course model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            if (!ModelState.IsValid)
            {
                TempData["CourseError"] = "Please enter valid course information.";
                return RedirectToAction(nameof(Index));
            }
            var existingCourse = dBContext.Course.Find(c => c.CourseId == id).FirstOrDefault();
            if (existingCourse == null) { return NotFound(); }

            var duplicateCourse = dBContext.Course.Find(c => c.CourseCode == model.CourseCode && c.CourseId != id).FirstOrDefault();
            if (duplicateCourse != null)
            {
                TempData["CourseError"] = "A course with this course code already exists.";
                return RedirectToAction(nameof(Index));
            }

            var subjectExists = dBContext.Subject.Find(s => s.Code == model.SubjectCode).Any();
            if (!subjectExists)
            {
                TempData["CourseError"] = "The selected subject does not exist.";
                return RedirectToAction(nameof(Index));
            }

            var update = Builders<Course>.Update
                .Set(c => c.SubjectCode, model.SubjectCode)
                .Set(c => c.CourseCode, model.CourseCode)
                .Set(c => c.CourseName, model.CourseName);

            dBContext.Course.UpdateOne(c => c.CourseId == id, update);

            TempData["CourseSuccess"] = "Course was successfully updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var course = dBContext.Course.Find(c => c.CourseId == id).FirstOrDefault();

            if (course == null) { return NotFound(); }
            dBContext.Course.DeleteOne( c => c.CourseId == id);

            TempData["CourseSuccess"] = "Course was successfully deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}