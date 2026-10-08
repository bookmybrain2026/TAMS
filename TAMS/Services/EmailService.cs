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

        public async Task SendAppointmentConfirmationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        //Student confirmation method //async allows the method to use await
        {
            var message = new MimeMessage(); //email message object

            message.From.Add(new MailboxAddress( //Sender's display name & email
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(studentName, studentEmail)); //sets the recipient

            message.Subject = "Appointment Confirmation"; //subject line

            message.Body = new TextPart("plain") //email body
            {
                Text = $"""
                Hello {studentName},

                Your appointment has been successfully booked.

                Tutor: {tutorName}
                Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
                Time: {appointmentDate.AddHours(-5):h:mm tt}
                Status: Scheduled

                Thank you for using AMS.
                """
            };

            using var smtp = new SmtpClient(); //creates an SMTP client used to connect to the email provide
                                               //Using keyword ensure that the SMTP client is properly disposed of when the method finishes 
                                               //even if an error occurs

            await smtp.ConnectAsync( //asynchronous connection to the email provider’s SMTP server
                _configuration["EmailSettings:SmtpServer"], //read SMTP server name and port
                int.Parse(_configuration["EmailSettings:SmtpPort"]!),
                SecureSocketOptions.StartTls); //tells MailKit to use STARTTLS security when connecting to the SMTP server

            await smtp.AuthenticateAsync( //begins the login process with the email provider
                _configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]);

            await smtp.SendAsync(message); //sends the email

            await smtp.DisconnectAsync(true); //close the connection between application and email provider
        }

        public async Task SendTutorAppointmentNotificationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        //sends an email notification to the tutor after a student schedules an appointment.
        {
            var message = new MimeMessage(); //email message object

            message.From.Add(new MailboxAddress( //Sender's display name & email
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(tutorName, tutorEmail)); //sets the recipient

            message.Subject = "New Appointment Scheduled"; //subject line

            message.Body = new TextPart("plain") //email body
            {
                Text = $"""
                Hello Dr. {tutorName},

                A new appointment has been scheduled.

                Student: {studentName}
                Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
                Time: {appointmentDate.AddHours(-5):h:mm tt}
                Status: Scheduled

                Thank you for using AMS.
                """
            };

            using var smtp = new SmtpClient(); //creates an SMTP client used to connect to the email provide
                                               //Using keyword ensure that the SMTP client is properly disposed of when the method finishes 
                                               //even if an error occurs

            await smtp.ConnectAsync( //asynchronous connection to the email provider’s SMTP server
                _configuration["EmailSettings:SmtpServer"], //read SMTP server name and port
                int.Parse(_configuration["EmailSettings:SmtpPort"]!),
                SecureSocketOptions.StartTls); //tells MailKit to use STARTTLS security when connecting to the SMTP server

            await smtp.AuthenticateAsync( //begins the login process with the email provider
                _configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]);

            await smtp.SendAsync(message); //sends the email

            await smtp.DisconnectAsync(true); //close the connection between application and email provider
        }
    }
}