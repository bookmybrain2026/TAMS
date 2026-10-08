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

    }
}