using Microsoft.AspNetCore.Mvc;

namespace TAMS.Controllers
{
    public class StudentController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        //Display the Student Dashboard page
        public IActionResult Dashboard(int id)
        {
            // Retrieves the StudentId stored in the session during login
            var studentId = HttpContext.Session.GetInt32("StudentId");

            // Checks whether a student is logged in
            // If no StudentId is found, redirect the user to the login page
            if (studentId == null)
            {
                return RedirectToAction("Login", "Login");
            }
            // Displays the StudentDashboard view after successful session validation
            return View("StudentDashboard");
        }
    }
}
