using System;
using System.Threading.Tasks;

namespace TAMS.Services;

// The Task completes after MongoDB persists the job, NOT after email delivery.
public sealed class QueuedEmailService : IEmailService
{
    private readonly EmailOutbox _queue;
    public QueuedEmailService(EmailOutbox queue) => _queue = queue;

    public Task SendPasswordRecoveryCodeAsync(string userEmail, string userName, string recoveryCode) => _queue.EnqueueAsync(nameof(SendPasswordRecoveryCodeAsync), new object?[] { userEmail, userName, recoveryCode });

    public Task SendStudentAppointmentConfirmationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate) => _queue.EnqueueAsync(nameof(SendStudentAppointmentConfirmationAsync), new object?[] { studentEmail, studentName, tutorName, appointmentDate });

    public Task SendTutorAppointmentNotificationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate) => _queue.EnqueueAsync(nameof(SendTutorAppointmentNotificationAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate });

    public Task SendStudentAppointmentCancellationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate) => _queue.EnqueueAsync(nameof(SendStudentAppointmentCancellationAsync), new object?[] { studentEmail, studentName, tutorName, appointmentDate });

    public Task SendTutorAppointmentCancellationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate) => _queue.EnqueueAsync(nameof(SendTutorAppointmentCancellationAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate });

    public Task SendStudentAppointmentCancelledByTutorAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate) => _queue.EnqueueAsync(nameof(SendStudentAppointmentCancelledByTutorAsync), new object?[] { studentEmail, studentName, tutorName, appointmentDate });

    public Task SendTutorCancellationConfirmationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate) => _queue.EnqueueAsync(nameof(SendTutorCancellationConfirmationAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate });

    public Task SendAdminCancelledAppointmentAsync(string recipientEmail, string recipientName, string otherPersonName, DateTime appointmentDate, string recipientType, string adminName) => _queue.EnqueueAsync(nameof(SendAdminCancelledAppointmentAsync), new object?[] { recipientEmail, recipientName, otherPersonName, appointmentDate, recipientType, adminName });

    public Task SendReviewReminderAsync(string studentEmail, string studentName, string tutorName) => _queue.EnqueueAsync(nameof(SendReviewReminderAsync), new object?[] { studentEmail, studentName, tutorName });

    public Task SendTutorAppointmentCompletionReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int hoursAfterEnd) => _queue.EnqueueAsync(nameof(SendTutorAppointmentCompletionReminderAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate, endDate, hoursAfterEnd });

    public Task SendTutorAppointmentAdminFollowUpAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate) => _queue.EnqueueAsync(nameof(SendTutorAppointmentAdminFollowUpAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate, endDate });

    public Task SendAdminAppointmentFollowUpAsync(string adminEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int appointmentId) => _queue.EnqueueAsync(nameof(SendAdminAppointmentFollowUpAsync), new object?[] { adminEmail, tutorName, studentName, appointmentDate, endDate, appointmentId });

    public Task SendTutorAppointmentRescheduledAsync(string tutorEmail, string tutorName, string studentName, DateTime newAppointmentDate) => _queue.EnqueueAsync(nameof(SendTutorAppointmentRescheduledAsync), new object?[] { tutorEmail, tutorName, studentName, newAppointmentDate });

    public Task SendStudentAppointmentRescheduledAsync(string studentEmail, string studentName, string tutorName, DateTime newAppointmentDate) => _queue.EnqueueAsync(nameof(SendStudentAppointmentRescheduledAsync), new object?[] { studentEmail, studentName, tutorName, newAppointmentDate });

    public Task SendStudentUpcomingAppointmentReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, int hoursBefore, string? tutorNotes) => _queue.EnqueueAsync(nameof(SendStudentUpcomingAppointmentReminderAsync), new object?[] { studentEmail, studentName, tutorName, appointmentDate, hoursBefore, tutorNotes });

    public Task SendTutorUpcomingAppointmentReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, int hoursBefore) => _queue.EnqueueAsync(nameof(SendTutorUpcomingAppointmentReminderAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate, hoursBefore });

    public Task SendGroupAppointmentConfirmationToStudentAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime) => _queue.EnqueueAsync(nameof(SendGroupAppointmentConfirmationToStudentAsync), new object?[] { studentEmail, studentName, tutorName, appointmentDate, startTime, endTime });

    public Task SendGroupAppointmentConfirmationToTutorAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime) => _queue.EnqueueAsync(nameof(SendGroupAppointmentConfirmationToTutorAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate, startTime, endTime });

    public Task SendGroupAppointmentCancellationToStudentAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime) => _queue.EnqueueAsync(nameof(SendGroupAppointmentCancellationToStudentAsync), new object?[] { studentEmail, studentName, tutorName, appointmentDate, startTime, endTime });

    public Task SendGroupAppointmentCancellationToTutorAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime) => _queue.EnqueueAsync(nameof(SendGroupAppointmentCancellationToTutorAsync), new object?[] { tutorEmail, tutorName, studentName, appointmentDate, startTime, endTime });

    public Task SendStudentUpcomingGroupReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int hoursBefore) => _queue.EnqueueAsync(nameof(SendStudentUpcomingGroupReminderAsync), new object?[] { studentEmail, studentName, tutorName, appointmentDate, startTime, endTime, hoursBefore });

    public Task SendTutorUpcomingGroupReminderAsync(string tutorEmail, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int hoursBefore, int enrolledCount) => _queue.EnqueueAsync(nameof(SendTutorUpcomingGroupReminderAsync), new object?[] { tutorEmail, tutorName, appointmentDate, startTime, endTime, hoursBefore, enrolledCount });

    public Task SendAdminCancelledGroupToStudentAsync(string studentEmail, string studentName, string tutorName, string adminName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime) => _queue.EnqueueAsync(nameof(SendAdminCancelledGroupToStudentAsync), new object?[] { studentEmail, studentName, tutorName, adminName, groupId, appointmentDate, startTime, endTime });

    public Task SendAdminCancelledGroupToTutorAsync(string tutorEmail, string tutorName, string adminName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int enrolledCount) => _queue.EnqueueAsync(nameof(SendAdminCancelledGroupToTutorAsync), new object?[] { tutorEmail, tutorName, adminName, groupId, appointmentDate, startTime, endTime, enrolledCount });

    public Task SendTutorGroupApprovedAsync(string tutorEmail, string tutorName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int capacity) => _queue.EnqueueAsync(nameof(SendTutorGroupApprovedAsync), new object?[] { tutorEmail, tutorName, groupId, appointmentDate, startTime, endTime, capacity });

    public Task SendTutorGroupRejectedAsync(string tutorEmail, string tutorName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int capacity) => _queue.EnqueueAsync(nameof(SendTutorGroupRejectedAsync), new object?[] { tutorEmail, tutorName, groupId, appointmentDate, startTime, endTime, capacity });
}
