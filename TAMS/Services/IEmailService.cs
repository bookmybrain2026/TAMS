namespace TAMS.Services
{
    public interface IEmailService
    {
        Task SendPasswordRecoveryCodeAsync(string userEmail, string userName, string recoveryCode);

    }
}