using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class SubjectController : Controller
    {
        private readonly MongoDBContext dBContext;

        public SubjectController(MongoDBContext context)
        {
            dBContext = context;
        }

        //// Displays all subjects
        //public IActionResult Index()
        //{
        //    var subjects = dBContext.Subject
        //        .Find(_ => true)
        //        .SortBy(s => s.Name)
        //        .ToList();

        //    return View(subjects);
        //}
        public IActionResult Index()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var subjects = dBContext.Subject
                .Find(_ => true)
                .SortBy(s => s.Name)
                .ToList();

            return View(subjects);
        }

        // Creates a new subject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Subject model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Action = "Create";

                var subjects = 
                    dBContext.Subject
                    .Find(_ => true)
                    .SortBy(s => s.Name).ToList();

                return View("Index", subjects);
            }

            // Check if the subject code already exists
            var existingSubject = dBContext.Subject
                .Find(s => s.Code == model.Code)
                .FirstOrDefault();

            if (existingSubject != null)
            {
                TempData["ErrorMessage"] = "A subject with this code already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Add the new subject
            dBContext.Subject.InsertOne(model);
            TempData["SuccessMessage"] = "Subject was successfully created.";
            return RedirectToAction(nameof(Index));
        }

        // Edits an existing subject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(string id, Subject model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Please enter valid subject information.";

                return RedirectToAction(nameof(Index));
            }

            var existingSubject = dBContext.Subject
                .Find(s => s.SubjectId == id)
                .FirstOrDefault();

            if (existingSubject == null) { return NotFound(); }

            // Check whether another subject already uses this code
            var duplicateSubject = 
                dBContext.Subject.Find(s =>
                    s.Code == model.Code &&
                    s.SubjectId != id).FirstOrDefault();

            if (duplicateSubject != null)
            {
                TempData["ErrorMessage"] = "A subject with this code already exists.";
                return RedirectToAction(nameof(Index));
            }

            // Update the subject
            var update = Builders<Subject>.Update
                .Set(s => s.Code, model.Code)
                .Set(s => s.Name, model.Name);

            dBContext.Subject.UpdateOne(s => s.SubjectId == id, update);

            TempData["SuccessMessage"] = "Subject was successfully updated.";
            return RedirectToAction(nameof(Index));
        }

        // Deletes an existing subject
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");

            if (adminId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var subject = dBContext.Subject
                .Find(s => s.SubjectId == id)
                .FirstOrDefault();

            if (subject == null) { return NotFound(); }

            // Check whether courses are associated with this subject
            var coursesExist = dBContext.Course
                .Find(c => c.SubjectCode == subject.Code)
                .Any();

            if (coursesExist)
            {
                TempData["ErrorMessage"] = "This subject cannot be deleted because courses are associated with it.";
                return RedirectToAction(nameof(Index));
            }

            // Delete the subject
            dBContext.Subject.DeleteOne(s => s.SubjectId == id);

            TempData["SuccessMessage"] = "Subject was successfully deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}