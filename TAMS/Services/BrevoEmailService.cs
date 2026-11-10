using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MimeKit;

namespace TAMS.Services;

public sealed class BrevoEmailService
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

    // Reserved domains that are clearly not real delivery inboxes.
    private static readonly HashSet<string> DummyDomains =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "example.com",
            "example.org",
            "example.net",
            "test",
            "invalid",
            "localhost",
            "local",
            "example",
            "test.com"
        };

    // Detect only addresses that are clearly dummy or invalid.
    // Do not guess whether Gmail, Yahoo, Outlook, etc. exist.
    private static bool IsObviousDummyEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return true;

        email = email.Trim();

        // Reject obvious malformed addresses.
        if (!Regex.IsMatch(
            email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return true;
        }

        var atIndex = email.LastIndexOf('@');

        if (atIndex < 0)
            return true;

        var domain = email[(atIndex + 1)..];

        // Reserved example domains and obvious test domains.
        if (DummyDomains.Contains(domain))
            return true;

        if (domain.EndsWith(
            ".example",
            StringComparison.OrdinalIgnoreCase))
            return true;

        if (domain.EndsWith(
            ".invalid",
            StringComparison.OrdinalIgnoreCase))
            return true;

        if (domain.EndsWith(
            ".test",
            StringComparison.OrdinalIgnoreCase))
            return true;

        if (domain.EndsWith(
            ".localhost",
            StringComparison.OrdinalIgnoreCase))
            return true;

        // All other addresses remain unchanged.
        return false;
    }

    public async Task SendMimeMessageAsync(MimeMessage message)
    {
        var apiKey = _configuration["Brevo:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Missing Brevo:ApiKey configuration.");

        var senderEmail = _configuration["Brevo:SenderEmail"];

        if (string.IsNullOrWhiteSpace(senderEmail))
            throw new InvalidOperationException(
                "Missing Brevo:SenderEmail configuration.");

        var senderName =
            _configuration["Brevo:SenderName"] ?? "TAMS";

        var bannerUrl = _configuration["Brevo:BannerUrl"];

        var testRecipient =
            _configuration["Brevo:TestRecipientEmail"];

        // Preserve original HTML and plain-text content.
        var html = message.HtmlBody ?? "";
        var plainText = message.TextBody ?? "";

        // Replace CID banner images with the public banner URL.
        html = Regex.Replace(
            html,
            "<img\\b[^>]*\\bsrc\\s*=\\s*[\"']cid:[^\"']+[\"'][^>]*>",
            match =>
            {
                if (Uri.TryCreate(
                    bannerUrl,
                    UriKind.Absolute,
                    out var url)
                    && url.Scheme == Uri.UriSchemeHttps)
                {
                    return Regex.Replace(
                        match.Value,
                        "cid:[^\"']+",
                        WebUtility.HtmlEncode(bannerUrl));
                }

                return "";
            },
            RegexOptions.IgnoreCase);

        var originalRecipients = message.To.Mailboxes
            .Select(mailbox => new
            {
                email = mailbox.Address,
                name = mailbox.Name
            })
            .ToArray();

        if (originalRecipients.Length == 0)
            throw new InvalidOperationException(
                "The email has no recipients.");

        // Process each recipient individually.
        foreach (var recipient in originalRecipients)
        {
            var isDummy = IsObviousDummyEmail(
                recipient.email);

            var destinationEmail = recipient.email;
            var destinationName = recipient.name;

            var recipientHtml = html;
            var recipientText = plainText;

            if (isDummy)
            {
                if (string.IsNullOrWhiteSpace(testRecipient))
                {
                    throw new InvalidOperationException(
                        "An obvious dummy email was detected, " +
                        "but Brevo:TestRecipientEmail is not configured.");
                }

                // Redirect dummy emails to the testing inbox.
                destinationEmail = testRecipient;
                destinationName = "TAMS Test Inbox";

                // Include the original intended recipient.
                var notice =
                    "TAMS TEST MODE - Original recipient: " +
                    recipient.email;

                recipientHtml =
                    "<p><strong>TAMS TEST MODE</strong></p>" +
                    "<p>Original recipient: " +
                    WebUtility.HtmlEncode(recipient.email) +
                    "</p><hr/>" +
                    recipientHtml;

                recipientText =
                    notice +
                    Environment.NewLine +
                    Environment.NewLine +
                    recipientText;
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

            using var response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var details =
                    await response.Content.ReadAsStringAsync();

                throw new HttpRequestException(
                    $"Brevo rejected email: " +
                    $"HTTP {(int)response.StatusCode}. {details}");
            }
        }
    }
}
