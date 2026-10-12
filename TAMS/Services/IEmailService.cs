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

        Task SendAdminCancelledAppointmentAsync(string recipientEmail, string recipientName, string otherPersonName, DateTime appointmentDate, string recipientType);
        Task SendReviewReminderAsync(string studentEmail, string studentName, string tutorName);
    }
}