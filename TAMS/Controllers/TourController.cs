
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Controllers
{
    public class TourController : Controller
    {
        private readonly MongoDBContext _dbContext;

        public TourController(MongoDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Complete()
        {
            int? userId =
                HttpContext.Session.GetInt32("StudentId") ??
                HttpContext.Session.GetInt32("TutorId") ??
                HttpContext.Session.GetInt32("AdminId");

            if (userId == null) { return Unauthorized(); }

            var update = Builders<Login>.Update.Set(u => u.HasCompletedTour, true);

            _dbContext.Login.UpdateOne(u => u.UserId == userId.Value, update);

            return Ok(new { success = true });
        }
    }
}
