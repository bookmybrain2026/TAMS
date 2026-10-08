using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TAMS.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendPasswordRecoveryCodeAsync(string userEmail, string userName, string recoveryCode)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(userName, userEmail));
            message.Subject = "TAMS Password Recovery Code";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                Hello {userName},

                Your TAMS password recovery code is:

                {recoveryCode}

                This code will expire in 10 minutes and can only be used once.

                If you did not request a password reset, you can ignore this email.

                Thank you for using TAMS.
                """
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _configuration["EmailSettings:SmtpServer"],
                int.Parse(_configuration["EmailSettings:SmtpPort"]!),
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]);

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }
    }
}