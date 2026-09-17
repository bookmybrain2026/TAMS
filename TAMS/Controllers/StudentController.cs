//using Microsoft.AspNetCore.Mvc;
//using TAMS.Data;

//namespace TAMS.Controllers
//{
//    public class StudentController : Controller
//    {
//        public IActionResult Index()
//        {
//            return View();
//        }

//        //Display the Student Dashboard page
//        public IActionResult Dashboard(int id)
//        {
//            // Retrieves the StudentId stored in the session during login
//            var studentId = HttpContext.Session.GetInt32("StudentId");

//            // Checks whether a student is logged in
//            // If no StudentId is found, redirect the user to the login page
//            if (studentId == null)
//            {
//                return RedirectToAction("Login", "Login");
//            }
//            // Displays the StudentDashboard view after successful session validation
//            return View("StudentDashboard");
//        }
//    }
//}
using Microsoft.AspNetCore.Mvc;
using TAMS.Data;
using TAMS.Models;
using MongoDB.Driver;

namespace TAMS.Controllers
{
    public class StudentController : Controller
    {
        private readonly MongoDBContext dBContext;

        public StudentController(MongoDBContext dBContext)
        {
            this.dBContext = dBContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard(int id)
        {
            var studentId = HttpContext.Session.GetInt32("StudentId");

            if (studentId == null)
            {
                return RedirectToAction("Login", "Login");
            }

            var student = dBContext.Student
                .Find(s => s.StudentId == studentId.Value)
                .FirstOrDefault();

            if (student == null)
            {
                return RedirectToAction("Login", "Login");
            }

            return View("StudentDashboard", student);
        }
    }
}