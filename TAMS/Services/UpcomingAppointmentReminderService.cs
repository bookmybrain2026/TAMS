
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
                        await SendReminderAsync(appointment, student.DisableEmailNotifications ? null : student.Email,
                                                tutor.DisableEmailNotifications ? null : tutor.Email, studentName, tutorName, 24, cancellationToken);
                    }

                    // 1-hour reminder
                    if (!appointment.Reminder1HourBeforeSent && now >= start.AddHours(-1) && now < start)
                    {
                        await SendReminderAsync(appointment, student.DisableEmailNotifications ? null : student.Email, 
                                                tutor.DisableEmailNotifications ? null : tutor.Email, studentName, tutorName, 1, cancellationToken);
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
            // Process upcoming group appointment reminders too.
            await ProcessGroupRemindersAsync(cancellationToken);
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

            if (string.IsNullOrWhiteSpace(studentEmail) && string.IsNullOrWhiteSpace(tutorEmail))
            {
                return;
            }

            string tutorNotes = current.TutorNotes ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(studentEmail))
            {
                await _emailService.SendStudentUpcomingAppointmentReminderAsync(
                    studentEmail, studentName, tutorName, current.AppointmentDate, hoursBefore, tutorNotes);
            }

            if (!string.IsNullOrWhiteSpace(tutorEmail))
            {
                await _emailService.SendTutorUpcomingAppointmentReminderAsync(
                    tutorEmail, tutorName, studentName, current.AppointmentDate, hoursBefore);
            }

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

        private async Task ProcessGroupRemindersAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.Now;

            var groups = await _db.GroupAppointments
                .Find(g => g.Status == "Open" || g.Status == "Full")
                .ToListAsync(cancellationToken);

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Group appointments store date and time separately.
                    var start = group.AppointmentDate.Date.Add(group.StartTime);

                    if (now >= start)
                    {
                        continue;
                    }

                    if (group.StudentIds == null || group.StudentIds.Count == 0)
                    {
                        continue;
                    }

                    // 24-hour reminder window.
                    if (!group.Reminder24HoursBeforeSent && now >= start.AddHours(-24) && now < start.AddHours(-1))
                    {
                        await SendGroupReminderAsync(group.GroupId, 24, cancellationToken);
                    }

                    // 1-hour reminder window.
                    if (!group.Reminder1HourBeforeSent && now >= start.AddHours(-1) && now < start)
                    {
                        await SendGroupReminderAsync(group.GroupId, 1, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process group reminder for group {GroupId}.", group.GroupId);
                }
            }
        }

        private async Task SendGroupReminderAsync(int groupId, int hoursBefore, CancellationToken cancellationToken)
        {
            var group = await _db.GroupAppointments
                .Find(g => g.GroupId == groupId && (g.Status == "Open" || g.Status == "Full"))
                .FirstOrDefaultAsync(cancellationToken);

            if (group == null || group.StudentIds == null || group.StudentIds.Count == 0)
            {
                return;
            }

            bool alreadySent = hoursBefore == 24 ? group.Reminder24HoursBeforeSent : group.Reminder1HourBeforeSent;

            if (alreadySent)
            {
                return;
            }

            var now = DateTime.Now;
            var start = group.AppointmentDate.Date.Add(group.StartTime);

            bool withinWindow = hoursBefore == 24 ? now >= start.AddHours(-24) && now < start.AddHours(-1)
                : now >= start.AddHours(-1) && now < start;

            if (!withinWindow)
            {
                return;
            }

            var tutor = await _db.Tutor
                .Find(t => t.TutorId == group.TutorId)
                .FirstOrDefaultAsync(cancellationToken);

            if (tutor == null)
            {
                _logger.LogWarning("Tutor not found for group {GroupId}.", groupId);
                return;
            }

            string tutorName = $"{tutor.FName} {tutor.LName}";

            var students = await _db.Student
                .Find(s => group.StudentIds.Contains(s.StudentId))
                .ToListAsync(cancellationToken);

            var filter =
                Builders<GroupAppointment>.Filter.Eq(g => g.GroupId, groupId) &
                Builders<GroupAppointment>.Filter.In(g => g.Status, new[] { "Open", "Full" }) &
                Builders<GroupAppointment>.Filter.Eq(g => g.AppointmentDate, group.AppointmentDate) &
                Builders<GroupAppointment>.Filter.Eq(g => g.StartTime, group.StartTime);

            var flagFilter = hoursBefore == 24
                ? Builders<GroupAppointment>.Filter.Ne(g => g.Reminder24HoursBeforeSent, true)
                : Builders<GroupAppointment>.Filter.Ne(g => g.Reminder1HourBeforeSent, true);

            filter &= flagFilter;

            var update = hoursBefore == 24
                ? Builders<GroupAppointment>.Update.Set(g => g.Reminder24HoursBeforeSent, true)
                : Builders<GroupAppointment>.Update.Set(g => g.Reminder1HourBeforeSent, true);

            var result = await _db.GroupAppointments.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            if (result.ModifiedCount == 0)
            {
                return;
            }

            // Send a separate reminder to each enrolled student.
            foreach (var student in students)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (student.DisableEmailNotifications || string.IsNullOrWhiteSpace(student.Email))
                {
                    continue;
                }

                string studentName = $"{student.FName} {student.LName}";

                try
                {
                    await _emailService.SendStudentUpcomingGroupReminderAsync(
                        student.Email, studentName, tutorName, group.AppointmentDate, group.StartTime, group.EndTime, hoursBefore);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to queue {Hours}-hour reminder for student {StudentId}, group {GroupId}.",
                        hoursBefore, student.StudentId,  groupId);
                }
            }

            // Send only one reminder to the tutor.
            if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
            {
                try
                {
                    await _emailService.SendTutorUpcomingGroupReminderAsync(
                        tutor.Email, tutorName,  group.AppointmentDate, group.StartTime, group.EndTime, hoursBefore, group.StudentIds.Count);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to queue tutor reminder for group {GroupId}.", groupId);
                }
            }

            _logger.LogInformation("{Hours}-hour group reminder processed for group {GroupId}.", hoursBefore, groupId);
        }

    }
}
