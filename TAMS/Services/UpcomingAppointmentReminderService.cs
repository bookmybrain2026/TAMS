
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Services
{
    public class UpcomingAppointmentReminderService
    {
        private readonly MongoDBContext _db;
        private readonly IEmailService _emailService;
        private readonly ILogger<UpcomingAppointmentReminderService> _logger;

        public UpcomingAppointmentReminderService(MongoDBContext db, IEmailService emailService, ILogger<UpcomingAppointmentReminderService> logger)
        {
            _db = db;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task ProcessDueRemindersAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var appointments = await _db.Appointments .Find(a => a.Status == "Scheduled").ToListAsync(cancellationToken);
            foreach (var appointment in appointments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var start = appointment.AppointmentDate;

                    // Do not send reminders after an appointment starts.
                    if (now >= start)
                    {
                        continue;
                    }

                    var tutor = await _db.Tutor
                        .Find(t => t.TutorId == appointment.TutorId)
                        .FirstOrDefaultAsync(cancellationToken);

                    var student = await _db.Student
                        .Find(s => s.StudentId == appointment.StudentId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (tutor == null || student == null)
                    {
                        _logger.LogWarning("Missing student or tutor for appointment {AppointmentId}.", appointment.AppointmentId);
                        continue;
                    }

                    string studentName = $"{student.FName} {student.LName}";
                    string tutorName = $"{tutor.FName} {tutor.LName}";

                    // 24-hour reminder
                    if (!appointment.Reminder24HoursBeforeSent && now >= start.AddHours(-24) && now < start.AddHours(-1))
                    {
                        await SendReminderAsync(appointment, student.Email, tutor.Email, studentName, tutorName, 24, cancellationToken);
                    }

                    // 1-hour reminder
                    if (!appointment.Reminder1HourBeforeSent && now >= start.AddHours(-1) && now < start)
                    {
                        await SendReminderAsync(appointment, student.Email, tutor.Email, studentName, tutorName, 1, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process upcoming reminders for appointment {AppointmentId}.", appointment.AppointmentId);
                }
            }
        }

        private async Task SendReminderAsync(Appointment appointment, string? studentEmail, string? tutorEmail, string studentName, string tutorName, int hoursBefore, CancellationToken cancellationToken)
        {
            var current = await _db.Appointments
                .Find(a => a.AppointmentId == appointment.AppointmentId && a.Status == "Scheduled")
                .FirstOrDefaultAsync(cancellationToken);
            if (current == null) { return; }

            bool alreadySent = hoursBefore == 24 ? current.Reminder24HoursBeforeSent : current.Reminder1HourBeforeSent;

            if (alreadySent) { return; }

            var now = DateTime.Now;
            var start = current.AppointmentDate;

            bool withinWindow = hoursBefore == 24 ? now >= start.AddHours(-24) && now < start.AddHours(-1) : now >= start.AddHours(-1) && now < start;

            if (!withinWindow) { return; }

            // Both email addresses are required before processing.
            if (string.IsNullOrWhiteSpace(studentEmail))
            {
                _logger.LogWarning("Student email missing for appointment {AppointmentId}.", current.AppointmentId);
                return;
            }

            if (string.IsNullOrWhiteSpace(tutorEmail))
            {
                _logger.LogWarning("Tutor email missing for appointment {AppointmentId}.", current.AppointmentId);
                return;
            }

            string tutorNotes = current.TutorNotes ?? string.Empty;

            await _emailService.SendStudentUpcomingAppointmentReminderAsync(studentEmail, studentName, tutorName, current.AppointmentDate, hoursBefore, tutorNotes);

            await _emailService.SendTutorUpcomingAppointmentReminderAsync(tutorEmail, tutorName, studentName, current.AppointmentDate, hoursBefore);

            // Only mark the reminder after both emails succeed.
            var filter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, current.AppointmentId)
                & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled")
                & Builders<Appointment>.Filter.Eq(a => a.AppointmentDate, current.AppointmentDate);

            var flagFilter = hoursBefore == 24
                ? Builders<Appointment>.Filter.Or(Builders<Appointment>.Filter.Eq(a => a.Reminder24HoursBeforeSent, false),
                    Builders<Appointment>.Filter.Exists(nameof(Appointment.Reminder24HoursBeforeSent), false))
                : Builders<Appointment>.Filter.Or(Builders<Appointment>.Filter.Eq(a => a.Reminder1HourBeforeSent, false),
                    Builders<Appointment>.Filter.Exists(nameof(Appointment.Reminder1HourBeforeSent), false));

            filter &= flagFilter;

            var update = hoursBefore == 24
                ? Builders<Appointment>.Update.Set(a => a.Reminder24HoursBeforeSent, true)
                : Builders<Appointment>.Update.Set(a => a.Reminder1HourBeforeSent, true);

            var result = await _db.Appointments.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            if (result.ModifiedCount > 0)
            {
                _logger.LogInformation("{Hours}-hour reminder processed for appointment {AppointmentId}.", hoursBefore, current.AppointmentId);
            }
        }
    
    }
}
