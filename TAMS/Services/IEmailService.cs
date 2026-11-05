//provides the common interface that allows the controller to request emails
namespace TAMS.Services
{
    public interface IEmailService
    {
        Task SendPasswordRecoveryCodeAsync(string userEmail, string userName, string recoveryCode);
        Task SendStudentAppointmentConfirmationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate);

        Task SendTutorAppointmentNotificationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate);

        Task SendStudentAppointmentCancellationAsync( string studentEmail, string studentName, string tutorName, DateTime appointmentDate);
        Task SendTutorAppointmentCancellationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate);

        Task SendStudentAppointmentCancelledByTutorAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate);

        Task SendTutorCancellationConfirmationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate);

        Task SendAdminCancelledAppointmentAsync(string recipientEmail, string recipientName, string otherPersonName, DateTime appointmentDate, string recipientType, string adminName);

        Task SendReviewReminderAsync(string studentEmail, string studentName, string tutorName);

        Task SendTutorAppointmentCompletionReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int hoursAfterEnd);

        Task SendTutorAppointmentAdminFollowUpAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate);

        Task SendAdminAppointmentFollowUpAsync(string adminEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int appointmentId);

        Task SendTutorAppointmentRescheduledAsync(string tutorEmail, string tutorName, string studentName, DateTime newAppointmentDate);

        Task SendStudentAppointmentRescheduledAsync(string studentEmail, string studentName, string tutorName, DateTime newAppointmentDate);

        Task SendStudentUpcomingAppointmentReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, int hoursBefore, string? tutorNotes);

        Task SendTutorUpcomingAppointmentReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, int hoursBefore);

        Task SendGroupAppointmentConfirmationToStudentAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime);

        Task SendGroupAppointmentConfirmationToTutorAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime);

        Task SendGroupAppointmentCancellationToStudentAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime);

        Task SendGroupAppointmentCancellationToTutorAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime);

        Task SendStudentUpcomingGroupReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int hoursBefore);

        Task SendTutorUpcomingGroupReminderAsync(string tutorEmail, string tutorName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int hoursBefore, int enrolledCount);

        Task SendAdminCancelledGroupToStudentAsync(string studentEmail, string studentName, string tutorName, string adminName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime);

        Task SendAdminCancelledGroupToTutorAsync(string tutorEmail, string tutorName, string adminName, int groupId, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, int enrolledCount);

    }
}