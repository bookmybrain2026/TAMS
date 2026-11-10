
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MimeKit;

namespace TAMS.Services;

public sealed class BrevoEmailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly EmailVerificationService _verification;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(
        HttpClient httpClient,
        IConfiguration configuration,
        EmailVerificationService verification,
        ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _verification = verification;
        _logger = logger;
    }

    public async Task SendMimeMessageAsync(MimeMessage message)
    {
        var apiKey = _configuration["Brevo:ApiKey"];
        var senderEmail = _configuration["Brevo:SenderEmail"];
        var senderName = _configuration["Brevo:SenderName"] ?? "TAMS";
        var bannerUrl = _configuration["Brevo:BannerUrl"];
        var testRecipient = _configuration["Brevo:TestRecipientEmail"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Missing Brevo:ApiKey.");

        if (string.IsNullOrWhiteSpace(senderEmail))
            throw new InvalidOperationException(
                "Missing Brevo:SenderEmail.");

        // BOTH conditions must be true before redirection:
        // 1. Render is configured for testing.
        // 2. This particular email was explicitly approved
        //    for test redirection by the application.
        bool testModeEnabled =
            _configuration.GetValue<bool>("Brevo:EnableTestRedirect");

        bool messageAllowsRedirect =
            string.Equals(
                message.Headers["X-TAMS-Allow-Test-Redirect"],
                "true",
                StringComparison.OrdinalIgnoreCase);

        bool canRedirect = testModeEnabled && messageAllowsRedirect;

        var html = message.HtmlBody ?? "";
        var plainText = message.TextBody ?? "";

        // Convert CID banner images to public HTTPS images.
        html = Regex.Replace(
            html,
            "<img\\b[^>]*\\bsrc\\s*=\\s*[\"']cid:[^\"']+[\"'][^>]*>",
            match =>
            {
                if (Uri.TryCreate(
                    bannerUrl,
                    UriKind.Absolute,
                    out var url) &&
                    url.Scheme == Uri.UriSchemeHttps)
                {
                    return Regex.Replace(
                        match.Value,
                        "cid:[^\"']+",
                        WebUtility.HtmlEncode(bannerUrl));
                }

                return "";
            },
            RegexOptions.IgnoreCase);

        var recipients = message.To.Mailboxes.ToArray();

        if (recipients.Length == 0)
            throw new InvalidOperationException(
                "The email has no recipients.");

        // Check every recipient independently.
        foreach (var recipient in recipients)
        {
            string originalEmail = recipient.Address;

            var result = await _verification.VerifyAsync(originalEmail);

            string destinationEmail = originalEmail;
            string destinationName = recipient.Name;

            string recipientHtml = html;
            string recipientText = plainText;

            if (result == EmailVerificationResult.Undeliverable)
            {
                if (!canRedirect)
                {
                    // Important: never send a recovery code or
                    // another unapproved message to the admin.
                    _logger.LogWarning(
                        "Suppressed email to an undeliverable recipient.");

                    continue;
                }

                if (string.IsNullOrWhiteSpace(testRecipient))
                {
                    throw new InvalidOperationException(
                        "Test redirection is enabled, but " +
                        "Brevo:TestRecipientEmail is missing.");
                }

                destinationEmail = testRecipient;
                destinationName = "TAMS Test Inbox";

                recipientHtml =
                    "<p><strong>TAMS TEST MODE</strong></p>" +
                    "<p>Original recipient: " +
                    WebUtility.HtmlEncode(originalEmail) +
                    "</p><hr/>" +
                    recipientHtml;

                recipientText =
                    "TAMS TEST MODE - Original recipient: " +
                    originalEmail +
                    Environment.NewLine +
                    Environment.NewLine +
                    recipientText;
            }
            else if (result == EmailVerificationResult.Unknown)
            {
                // Unknown does not mean fake.
                // Leave the recipient unchanged.
                _logger.LogWarning(
                    "Email verification was inconclusive; " +
                    "using the original recipient.");
            }

            var payload = new
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
                        email = destinationEmail,
                        name = destinationName
                    }
                },

                subject = message.Subject,
                htmlContent = recipientHtml,
                textContent = recipientText
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.brevo.com/v3/smtp/email");

            request.Headers.Add("api-key", apiKey);
            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync();

                throw new HttpRequestException(
                    $"Brevo rejected email: " +
                    $"HTTP {(int)response.StatusCode}. {details}");
            }
        }
    }
}
