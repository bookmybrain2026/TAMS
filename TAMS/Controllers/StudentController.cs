using Microsoft.AspNetCore.Mvc;

namespace TAMS.Controllers
{
    public class StudentController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard(int id)
        {
            return View("StudentDashboard");
        }
    }
}
