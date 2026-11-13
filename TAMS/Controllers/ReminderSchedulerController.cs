using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using TAMS.Services;

namespace TAMS.Controllers;

[ApiController]
[Route("api/reminder-scheduler")]
public class ReminderSchedulerController : ControllerBase
{
    private readonly UpcomingAppointmentReminderService _reminders;
    private readonly IConfiguration _configuration;

    public ReminderSchedulerController(UpcomingAppointmentReminderService reminders, IConfiguration configuration)
    {
        _reminders = reminders;
        _configuration = configuration;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var expected = _configuration["Scheduler:Secret"];
        var provided = Request.Headers["X-Scheduler-Key"].ToString();

        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(provided) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
        {
            return Unauthorized();
        }

        await _reminders.ProcessDueRemindersAsync(cancellationToken);

        return Ok(new { message = "Reminder check completed" });
    }
}
