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
        public IActionResult Index()
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }
            var subjects = dBContext.Subject.Find(_ => true).SortBy(s => s.Name).ToList();
            return View(subjects);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Subject model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            if (!ModelState.IsValid)
            {
                ViewBag.Action = "Create";
                var subjects = dBContext.Subject.Find(_ => true).SortBy(s => s.Name).ToList();
                return View("Index", subjects);
            }

            var existingSubject = dBContext.Subject.Find(s => s.Code == model.Code).FirstOrDefault();
            if (existingSubject != null)
            {
                TempData["SubjectError"] = "A subject with this code already exists.";
                return RedirectToAction(nameof(Index));
            }

            dBContext.Subject.InsertOne(model);
            TempData["SubjectSuccess"] = "Subject was successfully created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(string id, Subject model)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            if (!ModelState.IsValid)
            {
                TempData["SubjectError"] = "Please enter valid subject information.";
                return RedirectToAction(nameof(Index));
            }

            var existingSubject = dBContext.Subject.Find(s => s.SubjectId == id).FirstOrDefault();
            if (existingSubject == null) { return NotFound(); }

            var duplicateSubject = dBContext.Subject.Find(s => s.Code == model.Code && s.SubjectId != id).FirstOrDefault();
            if (duplicateSubject != null)
            {
                TempData["SubjectError"] = "A subject with this code already exists.";
                return RedirectToAction(nameof(Index));
            }

            var update = Builders<Subject>.Update.Set(s => s.Code, model.Code).Set(s => s.Name, model.Name);
            dBContext.Subject.UpdateOne(s => s.SubjectId == id, update);

            TempData["SubjectSuccess"] = "Subject was successfully updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string id)
        {
            var adminId = HttpContext.Session.GetInt32("AdminId");
            if (adminId == null) { return RedirectToAction("Login", "Login"); }

            var subject = dBContext.Subject.Find(s => s.SubjectId == id).FirstOrDefault();
            if (subject == null) { return NotFound(); }

            var coursesExist = dBContext.Course.Find(c => c.SubjectCode == subject.Code).Any();
            if (coursesExist)
            {
                TempData["SubjectError"] = "This subject cannot be deleted because courses are associated with it.";
                return RedirectToAction(nameof(Index));
            }

            dBContext.Subject.DeleteOne(s => s.SubjectId == id);

            TempData["SubjectSuccess"] = "Subject was successfully deleted.";
            return RedirectToAction(nameof(Index));
        }
    
    }
}