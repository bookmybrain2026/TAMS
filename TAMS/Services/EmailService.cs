using MailKit.Net.Smtp; //imports MailKit’s SMTP functionality
using MailKit.Security; //imports security settings for the SMTP connection
using MimeKit; //used to create the email message

namespace TAMS.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        //IConfiguration allows the class to read values from configuration files like appsetting.json

        public EmailService(IConfiguration configuration) //runs when ASP.NET Core creates an EmailService object
        {
            _configuration = configuration; //stores the configuration object in the class field so the email methods can use it
        }

        public async Task SendStudentAppointmentConfirmationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
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
                Hello {tutorName},

                A new appointment has been scheduled.

                Student: {studentName}
                Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
                Time: {appointmentDate.AddHours(-5):h:mm tt}
                Status: Scheduled

                Thank you for using TAMS.
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





        public async Task SendStudentAppointmentCancellationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(studentName, studentEmail));

            message.Subject = "Appointment Cancellation Confirmation";

            message.Body = new TextPart("plain")
            {
                Text = $"""
        Hello {studentName},

        This email confirms that you have cancelled your appointment.

        Tutor: {tutorName}
        Appointment Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
        Appointment Time: {appointmentDate.AddHours(-5):h:mm tt}
        Status: Cancelled
        Cancelled By: You

        If you did not request this cancellation, please contact the
        TAMS administrator.

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
        public async Task SendTutorAppointmentCancellationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(tutorName, tutorEmail));

            message.Subject = "Student Cancelled an Appointment";

            message.Body = new TextPart("plain")
            {
                Text = $"""
        Hello {tutorName},

        The following appointment has been cancelled by the student.

        Student: {studentName}
        Appointment Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
        Appointment Time: {appointmentDate.AddHours(-5):h:mm tt}
        Status: Cancelled
        Cancelled By: {studentName}

        Please review your schedule for the updated availability.

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





        public async Task SendStudentAppointmentCancelledByTutorAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(studentName, studentEmail));

            message.Subject = "Appointment Cancelled by Tutor";

            message.Body = new TextPart("plain")
            {
                Text = $"""
        Hello {studentName},

        We are writing to inform you that your tutor has cancelled
        your scheduled appointment.

        Tutor: {tutorName}
        Appointment Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
        Appointment Time: {appointmentDate.AddHours(-5):h:mm tt}
        Status: Cancelled
        Cancelled By: {tutorName}

        Please log in to TAMS to review your appointments and
        schedule another appointment if needed.

        If you have any questions, please contact the TAMS administrator.

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
        public async Task SendTutorCancellationConfirmationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(tutorName, tutorEmail));

            message.Subject = "Appointment Cancellation Confirmation";

            message.Body = new TextPart("plain")
            {
                Text = $"""
        Hello {tutorName},

        This email confirms that you have cancelled your appointment.

        Student: {studentName}
        Appointment Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
        Appointment Time: {appointmentDate.AddHours(-5):h:mm tt}
        Status: Cancelled
        Cancelled By: You

        Please review your schedule to confirm the updated availability.

        If you did not request this cancellation, please contact
        the TAMS administrator.

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






        public async Task SendAdminCancelledAppointmentAsync(string recipientEmail, string recipientName, string otherPersonName, DateTime appointmentDate, string recipientType)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(recipientName, recipientEmail));

            message.Subject = "Appointment Cancelled by Administrator";

            var otherPersonLabel = recipientType.Equals(
                "student", StringComparison.OrdinalIgnoreCase)
                ? "Tutor"
                : "Student";

            message.Body = new TextPart("plain")
            {
                Text = $"""
        Hello {recipientName},

        Your appointment has been cancelled by a TAMS administrator.

        {otherPersonLabel}: {otherPersonName}
        Appointment Date: {appointmentDate.AddHours(-5):MMMM dd, yyyy}
        Appointment Time: {appointmentDate.AddHours(-5):h:mm tt}
        Status: Cancelled
        Cancelled By: TAMS Administrator

        Please review your updated schedule. If you have any
        questions, please contact the TAMS administrator.

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





        public async Task SendPasswordRecoveryCodeAsync(string studentEmail, string studentName, string recoveryCode)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress( //Sender's display name & email
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(
                new MailboxAddress(studentName, studentEmail)
            );

            message.Subject = "TAMS Password Recovery Code";

            message.Body = new TextPart("plain") //email body
            {
                Text = $"""
                Hello {studentName},
                "Your password recovery code is: {recoveryCode}
                This code will expire in 10 minutes and can only be used once
                If you did not request a password reset, you can ignore this email

                Thank you for using TAMS.
                """
            };
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _configuration["EmailSettings:SmtpServer"],
                int.Parse(_configuration["EmailSettings:SmtpPort"]),
                MailKit.Security.SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]
            );

            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }





        public async Task SendReviewReminderAsync(string studentEmail, string studentName, string tutorName)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(studentName, studentEmail));

            message.Subject = "Awaiting Review or Rating";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                Hello {studentName},

                You have an awaiting review or rating.

                Please log in to your dashboard to review your appointment with Tutor {tutorName}.
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