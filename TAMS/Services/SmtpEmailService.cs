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
        private readonly IWebHostEnvironment _environment;

        public SmtpEmailService(IConfiguration configuration, IWebHostEnvironment environment) //runs when ASP.NET Core creates an EmailService object
        {
            _configuration = configuration; //stores the configuration object in the class field so the email methods can use it
            _environment = environment;
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
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));
            message.Subject = "New Appointment Scheduled";

            message.Body = new TextPart("plain")
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

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }



        public async Task SendStudentAppointmentCancellationAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
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
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendTutorAppointmentCancellationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
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
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }



        public async Task SendStudentAppointmentCancelledByTutorAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
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
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendTutorCancellationConfirmationAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
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
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }



        public async Task SendAdminCancelledAppointmentAsync(string recipientEmail, string recipientName, string otherPersonName, DateTime appointmentDate, string recipientType)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(recipientName, recipientEmail));
            message.Subject = "Appointment Cancelled by Administrator";

            var otherPersonLabel = recipientType.Equals("student", StringComparison.OrdinalIgnoreCase) ? "Tutor" : "Student";

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

        public async Task SendAdminAppointmentFollowUpAsync(string adminEmail, string tutorName, string studentName, DateTime appointmentDate, DateTime endDate, int appointmentId)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(adminEmail));
            message.Subject = $"Action Required: Appointment #{appointmentId} Needs Review";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                    Appointment Requires Admin Review

                    The following appointment is still marked as scheduled
                    72 hours after its end time.

                    Appointment ID: {appointmentId}
                    Tutor: {tutorName}
                    Student: {studentName}
                    Appointment Date: {appointmentDate:MMMM d, yyyy}
                    Start Time: {appointmentDate:h:mm tt}
                    End Time: {endDate:h:mm tt}

                    Please investigate and update the appointment status
                    after confirming whether the session took place.
                    """
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }



        public async Task SendTutorAppointmentRescheduledAsync(string tutorEmail, string tutorName, string studentName, DateTime newAppointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));
            message.Subject = "Appointment Rescheduled";
            
            message.Body = new TextPart("plain")
            {
                Text = $"""
                    Hello {tutorName},

                    An existing tutoring appointment has been rescheduled.

                    Student: {studentName}
                    New Appointment Date: {newAppointmentDate.AddHours(-5):MMMM dd, yyyy}
                    New Appointment Time: {newAppointmentDate.AddHours(-5):h:mm tt}
                    Status: Scheduled

                    Please review your updated schedule in TAMS.

                    Thank you for using TAMS.
                    """
                        };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendStudentAppointmentRescheduledAsync(string studentEmail, string studentName, string tutorName, DateTime newAppointmentDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(studentEmail));
            message.Subject = "Appointment Rescheduled";
            var formattedDate = newAppointmentDate.ToString("dddd, MMMM d, yyyy 'at' h:mm tt");

            message.Body = new TextPart("plain")
            {
                Text =
                    $"Hello {studentName},\n\n" +
                    "Your tutoring appointment has been successfully rescheduled.\n\n" +
                    $"Tutor: {tutorName}\n" +
                    $"New appointment date and time: {formattedDate}\n\n" +
                    "Please contact your tutor if you have any questions.\n\n" +
                    "Thank you."
            };

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }



        public async Task SendStudentUpcomingAppointmentReminderAsync(string studentEmail, string studentName, string tutorName, DateTime appointmentDate, int hoursBefore)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(studentName, studentEmail));

            message.Subject = hoursBefore == 24 ? "Reminder: Your Tutoring Appointment Is Tomorrow"
                : "Reminder: Your Tutoring Appointment Is in One Hour";

            string reminderText = hoursBefore == 24 ? "Your tutoring appointment is scheduled for tomorrow."
                : "Your tutoring appointment starts in approximately one hour.";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                    Hello {studentName},

                    {reminderText}

                    Tutor: {tutorName}
                    Appointment Date: {appointmentDate:MMMM dd, yyyy}
                    Appointment Time: {appointmentDate:h:mm tt}

                    Please be ready for your scheduled tutoring session.

                    Thank you for using TAMS.
                    """
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendTutorUpcomingAppointmentReminderAsync(string tutorEmail, string tutorName, string studentName, DateTime appointmentDate, int hoursBefore)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_configuration["EmailSettings:SenderName"], _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(tutorName, tutorEmail));

            message.Subject = hoursBefore == 24 ? "Reminder: Your Tutoring Appointment Is Tomorrow"
                : "Reminder: Your Tutoring Appointment Is in One Hour";

            string reminderText = hoursBefore == 24 ? "You have a tutoring appointment scheduled for tomorrow."
                : "Your tutoring appointment starts in approximately one hour.";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                    Hello {tutorName},

                    {reminderText}

                    Student: {studentName}
                    Appointment Date: {appointmentDate:MMMM dd, yyyy}
                    Appointment Time: {appointmentDate:h:mm tt}

                    Please prepare for your scheduled tutoring session.

                    Thank you for using TAMS.
                    """
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_configuration["EmailSettings:SmtpServer"], int.Parse(_configuration["EmailSettings:SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }


    }
}