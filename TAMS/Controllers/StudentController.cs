using Microsoft.AspNetCore.Mvc;

namespace TAMS.Controllers
{
    public class StudentController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
