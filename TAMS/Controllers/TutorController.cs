using Microsoft.AspNetCore.Mvc;

namespace TAMS.Controllers
{
    public class TutorController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard(int id)
        {
            var tutorId = HttpContext.Session.GetInt32("TutorId");

            if (tutorId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            return View("TutorDashboard");
        }
    }
}