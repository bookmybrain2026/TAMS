//Builds the message, authenticates with the SMTP server, and sends the email.
using MailKit.Net.Smtp; //imports MailKit’s SMTP functionality
using MailKit.Security; //imports security settings for the SMTP connection
using MimeKit; //used to create the email message

namespace TAMS.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        //IConfiguration allows the class to read values from configuration files like appsetting.json
        private readonly IWebHostEnvironment _environment; // Provides the application's file paths.

        public SmtpEmailService(IConfiguration configuration, IWebHostEnvironment environment) //runs when ASP.NET Core creates an SmtpEmailService object
        {
            _configuration = configuration; //stores the configuration object in the class field so the email methods can use it
            _environment = environment; //stors the application's environment information.
        }

        //Styling Done for SendStudentAppointmentConfirmationAsync
        public async Task SendStudentAppointmentConfirmationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        { //async allows the method to use await
            var message = new MimeMessage(); //email message object

            message.From.Add(new MailboxAddress( //Sender's display name & email
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(studentName, studentEmail)); //sets the recipient

            message.Subject = "Appointment Confirmation"; //subject line

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "StudentAppointmentConfirmation.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine( _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var localAppointmentDate = appointmentDate.AddHours(-5);

            var formattedDate = localAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = localAppointmentDate.ToString("h:mm tt");

            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                TAMS Appointment Confirmation

                Hello {studentName},

                This email confirms the appointment you booked through TAMS.

                Appointment Information

                Tutor: {tutorName}
                Date: {formattedDate}
                Time: {formattedTime}
                Status: Scheduled

                We will send a reminder for your appointment at a later date.

                Thank you for using TAMS.

                The BookMyBrain Team
                Tutoring Appointment Management System
                """;

            message.Body = builder.ToMessageBody();

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

        //Styling Done for SendTutorAppointmentNotificationAsync
        public async Task SendTutorAppointmentNotificationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));
            message.Subject = "New Appointment Scheduled";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "TutorAppointmentConfirmation.html");
            var html = await File.ReadAllTextAsync(templatePath);
            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var localAppointmentDate = appointmentDate.AddHours(-5);

            var formattedDate = localAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = localAppointmentDate.ToString("h:mm tt");

            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                TAMS New Appointment Notification

                Hello {tutorName},

                A new appointment has been scheduled.

                Student: {studentName}
                Date: {formattedDate}
                Time: {formattedTime}
                Status: Scheduled

                We will send a reminder for your appointment at a later date.

                Thank you for using TAMS.

                The BookMyBrain Team
                Tutoring Appointment Management System
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling Done for SendStudentAppointmentCancellationAsync
        public async Task SendStudentAppointmentCancellationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(studentName, studentEmail));
            message.Subject = "Appointment Cancellation Confirmation";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "StudentAppointmentCancellationToStudent.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var localAppointmentDate = appointmentDate.AddHours(-5);

            var formattedDate = localAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = localAppointmentDate.ToString("h:mm tt");

            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                TAMS Appointment Cancellation

                Hello {studentName},

                This email confirms that you have cancelled your upcoming appointment through TAMS.

                Tutor: {tutorName}
                Appointment Date: {formattedDate}
                Appointment Time: {formattedTime}
                Status: Cancelled
                Cancelled By: You

                If you did not request this cancellation, please contact the
                TAMS administrator.

                Thank you for choosing TAMS. We hope to see you back.
                               
                The BookMyBrain Team
                Tutoring Appointment Management System
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling Done for SendTutorAppointmentCancellationAsync
        public async Task SendTutorAppointmentCancellationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));
            message.Subject = "Student Cancelled an Appointment";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "StudentAppointmentCancellationToTutor.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
            <img src="cid:{banner.ContentId}"
                 alt="BookMyBrain" width="560"
                 style="display:block;width:100%;max-width:560px;
                        height:auto;border:0;margin:0 auto;">
            """;
            }

            var localAppointmentDate = appointmentDate.AddHours(-5);

            var formattedDate = localAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = localAppointmentDate.ToString("h:mm tt");

            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                Hello {tutorName},

                The following appointment has been cancelled by the student.

                Student: {studentName}
                Appointment Date: {formattedDate}
                Appointment Time: {formattedTime}
                Status: Cancelled
                Cancelled By: {studentName}

                Please review your schedule for the updated availability.

                Thank you for using TAMS.
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling Done for SendStudentAppointmentCancelledByTutorAsync
        public async Task SendStudentAppointmentCancelledByTutorAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(studentName, studentEmail));
            message.Subject = "Appointment Cancelled by Tutor";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "TutorAppointmentCancellationToStudent.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var localAppointmentDate = appointmentDate.AddHours(-5);

            var formattedDate = localAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = localAppointmentDate.ToString("h:mm tt");

            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                TAMS Appointment Cancellation Done by Tutor

                Hello {studentName},

                We are writing to inform you that your tutor has cancelled
                your scheduled appointment.

                Cancelled Appointment Information

                Tutor: {tutorName}
                Date: {formattedDate}
                Time: {formattedTime}
                Status: Cancelled

                Please log in to TAMS to review your appointments and schedule
                another appointment if needed.

                If you have any questions, please contact the TAMS administrator.

                Thank you for using TAMS.

                The BookMyBrain Team
                Tutoring Appointment Management System
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling Done for SendTutorCancellationConfirmationAsync
        public async Task SendTutorCancellationConfirmationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));
            message.Subject = "Appointment Cancellation Confirmation";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "TutorAppointmentCancellationToTutor.html");
            var html = await File.ReadAllTextAsync(templatePath);
            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = string.Empty;

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = $"{Guid.NewGuid()}@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain"
                         width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var localAppointmentDate = appointmentDate.AddHours(-5);
            var formattedDate = localAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = localAppointmentDate.ToString("h:mm tt");

            var eTutorName = System.Net.WebUtility.HtmlEncode(tutorName);
            var eStudentName = System.Net.WebUtility.HtmlEncode(studentName);
            var eDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var eTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{TutorName}}", eTutorName);
            html = html.Replace("{{StudentName}}", eStudentName);
            html = html.Replace("{{AppointmentDate}}", eDate);
            html = html.Replace("{{AppointmentTime}}", eTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                TAMS Appointment Cancellation Confirmation

                Hello {tutorName},

                This email confirms that you have cancelled your scheduled
                appointment through TAMS.

                Cancelled Appointment Information

                Student: {studentName}
                Appointment Date: {formattedDate}
                Appointment Time: {formattedTime}
                Status: Cancelled
                Cancelled By: You

                Please review your schedule to confirm the updated availability.

                If you did not request this cancellation, please contact
                the TAMS administrator.

                Thank you for using TAMS.

                The BookMyBrain Team
                Tutoring Appointment Management System
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling Done for SendAdminCancelledAppointmentAsync
        public async Task SendAdminCancelledAppointmentAsync(string recipientEmail, string recipientName, string otherPersonName, DateTime appointmentDate, string recipientType, string adminName)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(recipientName, recipientEmail));
            message.Subject = "Appointment Cancelled by Administrator";
            var otherPersonLabel = recipientType.Equals("student", StringComparison.OrdinalIgnoreCase) ? "Tutor" : "Student";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "AppointmentCancellationByAdmin.html");
            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = string.Empty;

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = $"{Guid.NewGuid()}@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain"
                         width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var localAppointmentDate = appointmentDate.AddHours(-5);
            var formattedDate = localAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = localAppointmentDate.ToString("h:mm tt");

            var eRecipientName = System.Net.WebUtility.HtmlEncode(recipientName);
            var eOtherPersonName = System.Net.WebUtility.HtmlEncode(otherPersonName);
            var encodedOtherPersonLabel = System.Net.WebUtility.HtmlEncode(otherPersonLabel);
            var eDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var eTime = System.Net.WebUtility.HtmlEncode(formattedTime);
            var eAdminName = System.Net.WebUtility.HtmlEncode(adminName);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{RecipientName}}", eRecipientName);
            html = html.Replace("{{OtherPersonLabel}}", encodedOtherPersonLabel);
            html = html.Replace("{{OtherPersonName}}", eOtherPersonName);
            html = html.Replace("{{AppointmentDate}}", eDate);
            html = html.Replace("{{AppointmentTime}}", eTime);
            html = html.Replace("{{CancelledByAdminName}}", eAdminName);

            builder.HtmlBody = html;
            builder.TextBody = $"""
                TAMS Appointment Cancellation by Administrator

                Hello {recipientName},

                Your appointment has been cancelled by a TAMS administrator.

                {otherPersonLabel}: {otherPersonName}
                Appointment Date: {formattedDate}
                Appointment Time: {formattedTime}
                Status: Cancelled
                Cancelled By: {adminName}

                Please review your updated schedule. If you have any questions,
                please contact the TAMS administrator.

                Thank you for using TAMS.

                The BookMyBrain Team
                Tutoring Appointment Management System
                """;
            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling Done for PasswordRecoveryCode
        public async Task SendPasswordRecoveryCodeAsync(string studentEmail, string studentName, string recoveryCode)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(studentName, studentEmail));
            message.Subject = "TAMS Password Recovery Code";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "PasswordRecovery.html"); //external HTML email template.
            var html = await File.ReadAllTextAsync(templatePath);
            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var rCode = System.Net.WebUtility.HtmlEncode(recoveryCode);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{RecoveryCode}}", rCode);

            builder.HtmlBody = html;

            // for email clients that cannot display HTML
            builder.TextBody = $"""
                TAMS Password Recovery

                Hello {studentName},

                We received a request to reset your password.

                Your recovery code is: {recoveryCode}

                This code expires in 10 minutes and can only be used once.

                If you did not request a password reset, you can safely
                ignore this email.

                Thank you for using TAMS.

                The BookMyBrain Team
                Tutoring Appointment Management System
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }


        public async Task SendReviewReminderAsync(string studentEmail, string studentName, string tutorName)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
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
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }


        public async Task SendTutorAppointmentCompletionReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int hoursAfterEnd)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(new MailboxAddress(tutorName, tutorEmail));

            message.Subject = "Reminder: Confirm Your Tutoring Appointment";

            var reminderMessage = hoursAfterEnd <= 1 ? "Your tutoring appointment ended at least one hour ago." : "Your tutoring appointment ended at least 24 hours ago.";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                Hello {tutorName},

                {reminderMessage}

                Student: {studentName}
                Appointment Date: {appointmentDate.ToString("MMMM dd, yyyy")}
                Start Time: {appointmentDate.ToString("h:mm tt")}
                End Time: {endDate.ToString("h:mm tt")}

                If the tutoring session took place, please log in to TAMS
                and mark the appointment as complete.

                If it did not take place, please contact 
                an administrator to cancel this appointment for you.

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

        public async Task SendTutorAppointmentAdminFollowUpAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));
            message.Subject = "Action Required: Unconfirmed Appointment";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                    Hello {tutorName},

                    Your tutoring appointment has remained unconfirmed for
                    more than 48 hours after its scheduled end time.

                    Student: {studentName}
                    Appointment Date: {appointmentDate.ToString("MMMM dd, yyyy")}
                    Start Time: {appointmentDate.ToString("h:mm tt")}
                    End Time: {endDate.ToString("h:mm tt")}

                    Please review the appointment in your TAMS dashboard.

                    If the session took place, please mark the appointment 
                    as complete. 
                    
                    If it did not take place, please contact 
                    an administrator to cancel this appointment for you.
                                                    
                    Thank you for using TAMS.
                    """
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        //Styling Done for SendAdminAppointmentFollowUpAsync
        public async Task SendAdminAppointmentFollowUpAsync(string adminEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int appointmentId)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(adminEmail));
            message.Subject = $"Action Required: Appointment #{appointmentId} Needs Review";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "AdminAppointmentFollowUp.html");
            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var formattedDate = appointmentDate.ToString("MMMM d, yyyy");
            var formattedStart = appointmentDate.ToString("h:mm tt");
            var formattedEnd = endDate.ToString("h:mm tt");

            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aStart = System.Net.WebUtility.HtmlEncode(formattedStart);
            var aEnd = System.Net.WebUtility.HtmlEncode(formattedEnd);
            var aId = System.Net.WebUtility.HtmlEncode(appointmentId.ToString());

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{AppointmentId}}", aId);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{StartTime}}", aStart);
            html = html.Replace("{{EndTime}}", aEnd);

            builder.HtmlBody = html;

            // for email clients that cannot display HTML
            builder.TextBody = $"""
                Appointment Requires Admin Review

                The following appointment is still marked as scheduled
                72 hours after its end time.

                Appointment ID: {appointmentId}
                Tutor: {tutorName}
                Student: {studentName}
                Appointment Date: {formattedDate}
                Start Time: {formattedStart}
                End Time: {formattedEnd}

                Please investigate and update the appointment status
                after confirming whether the session took place.

                Thank you for using TAMS.

                The BookMyBrain Team
                Tutoring Appointment Management System
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling donw for SendTutorAppointmentRescheduledAsync
        public async Task SendTutorAppointmentRescheduledAsync(string tutorEmail, string tutorName, string studentName, DateTime newAppointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));
            message.Subject = "Appointment Rescheduled";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "TutorAppointmentReschedule.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var formattedDate = newAppointmentDate.AddHours(-5).ToString("MMMM dd, yyyy");
            var formattedTime = newAppointmentDate.AddHours(-5).ToString("h:mm tt");

            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);
            html = html.Replace("{{Status}}", "Scheduled");

            builder.HtmlBody = html;

            builder.TextBody = $"""
                Hello {tutorName},

                An existing tutoring appointment has been rescheduled.

                Student: {studentName}
                New Appointment Date: {formattedDate}
                New Appointment Time: {formattedTime}
                Status: Scheduled

                Please review your updated schedule in TAMS.

                Thank you for using TAMS.
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling done for SendStudentAppointmentRescheduledAsync
        public async Task SendStudentAppointmentRescheduledAsync(string studentEmail, string studentName, string tutorName, DateTime newAppointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(studentEmail));
            message.Subject = "Appointment Rescheduled";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "StudentAppointmentReschedule.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var formattedDate = newAppointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = newAppointmentDate.ToString("h:mm tt");

            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                Hello {studentName},

                Your tutoring appointment has been successfully rescheduled.

                Tutor: {tutorName}
                New Appointment Date: {formattedDate}
                New Appointment Time: {formattedTime}

                Please contact your tutor if you have any questions.

                Thank you.
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }


        //Styling Done for SendStudentUpcomingAppointmentReminderAsync
        public async Task SendStudentUpcomingAppointmentReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, int hoursBefore, string? tutorNotes)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(studentName, studentEmail));

            message.Subject = hoursBefore == 24 ? "Reminder: Your Tutoring Appointment Is Tomorrow"
                : "Reminder: Your Tutoring Appointment Is in One Hour";

            string reminderText = hoursBefore == 24 ? "Your tutoring appointment is scheduled for tomorrow."
                : "Your tutoring appointment starts in approximately one hour.";

            string reminderTime = hoursBefore == 24 ? "tomorrow" : "approximately one hour";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "StudentAppointmentReminder.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }
            
            var formattedDate = appointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = appointmentDate.ToString("h:mm tt");

            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);
            var rText = System.Net.WebUtility.HtmlEncode(reminderText);
            var rTime = System.Net.WebUtility.HtmlEncode(reminderTime);

            var notesText = string.IsNullOrWhiteSpace(tutorNotes) ? "No notes provided by the tutor." : tutorNotes.Trim();

            var encodedNotes = System.Net.WebUtility.HtmlEncode(notesText)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "<br>");

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);
            html = html.Replace("{{ReminderText}}", rText);
            html = html.Replace("{{ReminderTime}}", rTime);
            html = html.Replace("{{TutorNotes}}", encodedNotes);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                Hello {studentName},

                {reminderText}

                Tutor: {tutorName}
                Appointment Date: {formattedDate}
                Appointment Time: {formattedTime}

                Tutor Notes:
                {notesText}

                Please be ready for your scheduled tutoring session.

                Thank you for using TAMS.
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        //Styling Done for SendTutorUpcomingAppointmentReminderAsync
        public async Task SendTutorUpcomingAppointmentReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, int hoursBefore)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));

            message.Subject = hoursBefore == 24 ? "Reminder: Your Tutoring Appointment Is Tomorrow"
                : "Reminder: Your Tutoring Appointment Is in One Hour";

            string reminderText = hoursBefore == 24 ? "You have a tutoring appointment scheduled for tomorrow."
                : "Your tutoring appointment starts in approximately one hour.";

            string reminderTime = hoursBefore == 24 ? "tomorrow" : "approximately one hour";

            var builder = new BodyBuilder();

            var templatePath = Path.Combine(_environment.ContentRootPath, "EmailTemplates", "TutorAppointmentReminder.html");

            var html = await File.ReadAllTextAsync(templatePath);

            var bannerPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "images", "banner.png");

            string bannerHtml = "";

            if (File.Exists(bannerPath))
            {
                var banner = builder.LinkedResources.Add(bannerPath);
                banner.ContentId = Guid.NewGuid().ToString() + "@tams";

                bannerHtml = $"""
                    <img src="cid:{banner.ContentId}"
                         alt="BookMyBrain" width="560"
                         style="display:block;width:100%;max-width:560px;
                                height:auto;border:0;margin:0 auto;">
                    """;
            }

            var formattedDate = appointmentDate.ToString("MMMM dd, yyyy");
            var formattedTime = appointmentDate.ToString("h:mm tt");

            var tName = System.Net.WebUtility.HtmlEncode(tutorName);
            var sName = System.Net.WebUtility.HtmlEncode(studentName);
            var aDate = System.Net.WebUtility.HtmlEncode(formattedDate);
            var aTime = System.Net.WebUtility.HtmlEncode(formattedTime);
            var rText = System.Net.WebUtility.HtmlEncode(reminderText);
            var rTime = System.Net.WebUtility.HtmlEncode(reminderTime);

            html = html.Replace("{{BannerHtml}}", bannerHtml);
            html = html.Replace("{{TutorName}}", tName);
            html = html.Replace("{{StudentName}}", sName);
            html = html.Replace("{{AppointmentDate}}", aDate);
            html = html.Replace("{{AppointmentTime}}", aTime);
            html = html.Replace("{{ReminderText}}", rText);
            html = html.Replace("{{ReminderTime}}", rTime);

            builder.HtmlBody = html;

            builder.TextBody = $"""
                Hello {tutorName},

                {reminderText}

                Student: {studentName}
                Appointment Date: {formattedDate}
                Appointment Time: {formattedTime}

                Please prepare for your scheduled tutoring session.

                Thank you for using TAMS.
                """;

            message.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

    }
}