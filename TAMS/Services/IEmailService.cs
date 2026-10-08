namespace TAMS.Services
{
    public interface IEmailService
    {
        Task SendPasswordRecoveryCodeAsync(string userEmail, string userName, string recoveryCode);
        Task SendAppointmentConfirmationAsync(
            string studentEmail,
            string studentName,
            string tutorName,
            DateTime appointmentDate);

        Task SendTutorAppointmentNotificationAsync(
            string tutorEmail,
            string tutorName,
            string studentName,
            DateTime appointmentDate);
        Task SendAppointmentCancellationAsync(
            string patientEmail,
            string patientName,
            string doctorName,
            DateTime appointmentDate);

        Task SendTutorAppointmentCancellationAsync(
            string tutorEmail,
            string tutorName,
            string studentName,
            DateTime appointmentDate);

        Task SendAdminCancelledAppointmentAsync(
            string recipientEmail,
            string recipientName,
            string otherPersonName,
            DateTime appointmentDate,
            string recipientType);

        Task SendReviewReminderAsync(
            string studentEmail,
            string studentName,
            string tutorName);
    }
}