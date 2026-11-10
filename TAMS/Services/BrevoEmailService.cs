//This sends email using Brevo's transactional email API over HTTPS, rather than an SMTP connection.
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace TAMS.Services
{
    public class BrevoEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public BrevoEmailService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task SendEmailAsync(
            string recipientEmail,
            string subject,
            string htmlBody)
        {
            var apiKey = _configuration["Brevo:ApiKey"]
                ?? throw new InvalidOperationException(
                    "Brevo API key is not configured.");

            var senderEmail = _configuration["Brevo:SenderEmail"]
                ?? throw new InvalidOperationException(
                    "Brevo sender email is not configured.");

            var senderName =
                _configuration["Brevo:SenderName"] ?? "TAMS";

            var email = new
            {
                sender = new
                {
                    name = senderName,
                    email = senderEmail
                },
                to = new[]
                {
                    new
                    {
                        email = recipientEmail
                    }
                },
                subject = subject,
                htmlContent = htmlBody
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.brevo.com/v3/smtp/email");

            request.Headers.Add("api-key", apiKey);
            request.Content = JsonContent.Create(email);

            using var response =
                await _httpClient.SendAsync(request);

            response.EnsureSuccessStatusCode();
        }
    }
}

