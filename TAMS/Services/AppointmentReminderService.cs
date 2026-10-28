
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using TAMS.Data;
using TAMS.Models;

namespace TAMS.Services
{
    public class AppointmentReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentReminderService> _logger;

        public AppointmentReminderService(IServiceScopeFactory scopeFactory, ILogger<AppointmentReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Appointment reminder service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MongoDBContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var appointments = await db.Appointments.Find(a => a.Status == "Scheduled").ToListAsync(stoppingToken);
                    var now = DateTime.Now;

                    foreach (var appointment in appointments)
                    {
                        if (stoppingToken.IsCancellationRequested) { break; }

                        try
                        {
                            // Only process appointments whose end time has passed
                            if (now < appointment.EndDate) { continue; }

                            var tutor = await db.Tutor.Find(t => t.TutorId == appointment.TutorId).FirstOrDefaultAsync(stoppingToken);
                            var student = await db.Student.Find(s => s.StudentId == appointment.StudentId).FirstOrDefaultAsync(stoppingToken);

                            if (tutor == null)
                            {
                                _logger.LogWarning("Tutor {TutorId} not found for appointment {AppointmentId}.", appointment.TutorId, appointment.AppointmentId);
                            }

                            var tutorName = tutor == null ? "Unknown tutor" : $"{tutor.FName} {tutor.LName}";
                            var tutorEmail = tutor?.Email;
                            var studentName = student == null ? "the student" : $"{student.FName} {student.LName}";

                            if (string.IsNullOrWhiteSpace(tutorEmail))
                            {
                                _logger.LogWarning("Tutor email is missing for appointment {AppointmentId}. " +
                                    "Database reminders will still be processed.", appointment.AppointmentId);
                            }                         

                            // Reminder 1: one hour after the appointment ends
                            if (!appointment.Reminder1Sent && now >= appointment.EndDate.AddHours(1))
                            {
                                // Recheck that the appointment is still scheduled
                                if (!await IsStillScheduledAsync(db, appointment.AppointmentId, stoppingToken))
                                {
                                    continue;
                                }

                                // FIRST: Mark the reminder as sent in MongoDB
                                var reminder1Filter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointment.AppointmentId)
                                    & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled")
                                    & Builders<Appointment>.Filter.Or(Builders<Appointment>.Filter.Eq(a => a.Reminder1Sent, false),
                                        Builders<Appointment>.Filter.Exists("Reminder1Sent", false));
                                var reminder1Update = Builders<Appointment>.Update.Set(a => a.Reminder1Sent, true);
                                var reminder1Result = await db.Appointments.UpdateOneAsync(reminder1Filter, reminder1Update, cancellationToken: stoppingToken);

                                if (reminder1Result.ModifiedCount > 0)
                                {
                                    appointment.Reminder1Sent = true;
                                    _logger.LogInformation("One-hour reminder recorded for appointment {AppointmentId}.", appointment.AppointmentId);

                                    // SECOND: Attempt email delivery independently.
                                    try
                                    {
                                        if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
                                        {
                                            await emailService.SendTutorAppointmentCompletionReminderAsync(
                                                tutor.Email, tutorName, studentName, appointment.AppointmentDate, appointment.EndDate, 1);
                                        }
                                        _logger.LogInformation("One-hour reminder email sent for appointment {AppointmentId}.", appointment.AppointmentId);
                                    }
                                    catch (OperationCanceledException)
                                        when (stoppingToken.IsCancellationRequested)
                                    {
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "One-hour reminder email failed for appointment {AppointmentId}. " +
                                            "The database reminder flag remains recorded.", appointment.AppointmentId);
                                    }
                                }
                            }

                            // Reminder 2: 24 hours after the appointment ends
                            if (!appointment.Reminder2Sent && now >= appointment.EndDate.AddHours(24))
                            {
                                // Recheck that the appointment is still scheduled
                                if (!await IsStillScheduledAsync(db, appointment.AppointmentId, stoppingToken))
                                {
                                    continue;
                                }

                                // FIRST: Mark the reminder as sent in MongoDB
                                var reminder2Filter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointment.AppointmentId)
                                    & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled")
                                    & Builders<Appointment>.Filter.Or(Builders<Appointment>.Filter.Eq(a => a.Reminder2Sent, false),
                                        Builders<Appointment>.Filter.Exists("Reminder2Sent", false));
                                var reminder2Update = Builders<Appointment>.Update.Set(a => a.Reminder2Sent, true);
                                var reminder2Result = await db.Appointments.UpdateOneAsync(reminder2Filter, reminder2Update, cancellationToken: stoppingToken);

                                if (reminder2Result.ModifiedCount > 0)
                                {
                                    appointment.Reminder2Sent = true;
                                    _logger.LogInformation("24-hour reminder recorded for appointment {AppointmentId}.", appointment.AppointmentId);

                                    // SECOND: Attempt email delivery independently
                                    try
                                    {
                                        if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
                                        {
                                            await emailService.SendTutorAppointmentCompletionReminderAsync(
                                                tutor.Email, tutorName, studentName, appointment.AppointmentDate, appointment.EndDate, 24);
                                        }
                                        _logger.LogInformation("24-hour reminder email sent for appointment {AppointmentId}.", appointment.AppointmentId);
                                    }
                                    catch (OperationCanceledException)
                                        when (stoppingToken.IsCancellationRequested)
                                    {
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "24-hour reminder email failed for appointment {AppointmentId}. " +
                                            "The database reminder flag remains recorded.", appointment.AppointmentId);
                                    }
                                }
                            }

                            // Reminder 3: 48 hours after the appointment ends
                            if (!appointment.NeedsAdminFollowUp && now >= appointment.EndDate.AddHours(48))
                            {
                                // Recheck that the appointment is still scheduled
                                if (!await IsStillScheduledAsync(db, appointment.AppointmentId, stoppingToken))
                                {
                                    continue;
                                }

                                // FIRST: Record the Admin follow-up flag in MongoDB
                                var followUpFilter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointment.AppointmentId)
                                    & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled")
                                    & Builders<Appointment>.Filter.Or(Builders<Appointment>.Filter.Eq(a => a.NeedsAdminFollowUp, false),
                                        Builders<Appointment>.Filter.Exists("NeedsAdminFollowUp", false));
                                var followUpUpdate = Builders<Appointment>.Update.Set(a => a.NeedsAdminFollowUp, true);
                                var followUpResult = await db.Appointments.UpdateOneAsync(followUpFilter, followUpUpdate, cancellationToken: stoppingToken);

                                if (followUpResult.ModifiedCount > 0)
                                {
                                    appointment.NeedsAdminFollowUp = true;
                                    _logger.LogInformation("Appointment {AppointmentId} flagged for Admin follow-up.", appointment.AppointmentId);

                                    // SECOND: Attempt the optional tutor email independently
                                    try
                                    {
                                        if (!tutor.DisableEmailNotifications && !string.IsNullOrWhiteSpace(tutor.Email))
                                        {
                                            await emailService.SendTutorAppointmentAdminFollowUpAsync(
                                                tutor.Email, tutorName, studentName, appointment.AppointmentDate, appointment.EndDate);
                                        }
                                        _logger.LogInformation("48-hour Admin follow-up email sent for appointment {AppointmentId}.",
                                            appointment.AppointmentId);
                                    }
                                    catch (OperationCanceledException)
                                        when (stoppingToken.IsCancellationRequested)
                                    {
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "48-hour follow-up email failed for appointment {AppointmentId}. " +
                                            "The Admin follow-up flag remains recorded.", appointment.AppointmentId);
                                    }
                                }
                            }

                            // 72 hours after the appointment ends, notify the Admin and remove all tutor reminders after an email succeeds
                            if (!appointment.AdminNotificationSent && now >= appointment.EndDate.AddHours(72))
                            {
                                if (!await IsStillScheduledAsync(db, appointment.AppointmentId, stoppingToken))
                                {
                                    continue;
                                }

                                var admins = await db.Admin.Find(a => a.Email != null && a.Email != "").ToListAsync(stoppingToken);

                                if (admins.Count == 0)
                                {
                                    _logger.LogWarning("No Admin email addresses found for appointment {AppointmentId}.", appointment.AppointmentId);
                                    continue;
                                }

                                var adminEmailSent = false;

                                foreach (var admin in admins)
                                {
                                    try
                                    {
                                        await emailService.SendAdminAppointmentFollowUpAsync(admin.Email, tutorName, studentName, appointment.AppointmentDate, appointment.EndDate, appointment.AppointmentId);
                                        adminEmailSent = true;
                                        _logger.LogInformation("Admin follow-up email sent for appointment {AppointmentId}.", appointment.AppointmentId);
                                    }
                                    catch (OperationCanceledException)
                                        when (stoppingToken.IsCancellationRequested)
                                    {
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Admin email failed for appointment {AppointmentId}.", appointment.AppointmentId);
                                    }
                                }

                                // Only remove the tutor reminders if an Admin email was successfully sent
                                if (adminEmailSent)
                                {
                                    var adminNotificationFilter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointment.AppointmentId)
                                        & Builders<Appointment>.Filter.Eq(a => a.TutorId, appointment.TutorId)
                                        & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled")
                                        & Builders<Appointment>.Filter.Ne(a => a.AdminNotificationSent, true);

                                    var adminNotificationUpdate = Builders<Appointment>.Update.Set(a => a.AdminNotificationSent, true)
                                            .Set(a => a.TutorReminder1Acknowledged, true).Set(a => a.TutorReminder2Acknowledged, true)
                                            .Set(a => a.TutorReminder3Acknowledged, true);

                                    var result = await db.Appointments.UpdateOneAsync(adminNotificationFilter, adminNotificationUpdate, cancellationToken: stoppingToken);
                                    if (result.ModifiedCount > 0)
                                    {
                                        appointment.AdminNotificationSent = true;
                                        appointment.TutorReminder1Acknowledged = true;
                                        appointment.TutorReminder2Acknowledged = true;
                                        appointment.TutorReminder3Acknowledged = true;
                                        _logger.LogInformation("Admin notification recorded and all tutor reminders " + "cleared for appointment {AppointmentId}.", appointment.AppointmentId);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("No Admin email was successfully sent for appointment " + "{AppointmentId}. Tutor reminders remain pending.", appointment.AppointmentId);
                                }
                            }

                        }
                        catch (OperationCanceledException)
                            when (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Reminder processing failed for appointment {AppointmentId}.", appointment.AppointmentId);
                        }
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check appointment reminders.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("Appointment reminder service stopped.");
        }

        private static async Task<bool> IsStillScheduledAsync(MongoDBContext db, int appointmentId, CancellationToken cancellationToken)
        {
            var filter = Builders<Appointment>.Filter.Eq(a => a.AppointmentId, appointmentId) & Builders<Appointment>.Filter.Eq(a => a.Status, "Scheduled");
            return await db.Appointments.Find(filter).AnyAsync(cancellationToken);
        }

    }
}
