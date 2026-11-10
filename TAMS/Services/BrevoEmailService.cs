
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

        // Get the original email content.
        var html = message.HtmlBody ?? "";
        var plainText = message.TextBody ?? "";

        // Replace embedded CID images with the public banner URL.
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

        // Get the original recipients.
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

        // Email address where dummy-account emails should go.
        var testRecipient =
            _configuration["Brevo:TestRecipientEmail"];

        // Comma-separated list of dummy email addresses.
        var dummyEmails = (
            _configuration["Brevo:DummyEmails"] ?? ""
        )
        .Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Optional comma-separated dummy domains.
        var dummyDomains = (
            _configuration["Brevo:DummyDomains"] ?? ""
        )
        .Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries)
        .Select(domain => domain.TrimStart('@'))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Determine whether a recipient is a designated dummy.
        bool IsDummyEmail(string email)
        {
            if (dummyEmails.Contains(email))
                return true;

            var atIndex = email.LastIndexOf('@');

            if (atIndex < 0)
                return false;

            var domain = email.Substring(atIndex + 1);

            return dummyDomains.Contains(domain);
        }

        // Redirect only designated dummy recipients.
        var recipients = originalRecipients
            .Select(recipient =>
            {
                bool isDummy = IsDummyEmail(recipient.email);

                if (isDummy &&
                    string.IsNullOrWhiteSpace(testRecipient))
                {
                    throw new InvalidOperationException(
                        "A dummy email was detected, but " +
                        "Brevo:TestRecipientEmail is not configured.");
                }

                return new
                {
                    email = isDummy
                        ? testRecipient!
                        : recipient.email,

                    name = isDummy
                        ? "TAMS Test Inbox"
                        : recipient.name,

                    originalEmail = recipient.email,
                    redirected = isDummy
                };
            })
            .ToArray();

        // Send a separate email for each original recipient.
        // This prevents one person's email content from being
        // shared with other recipients.
        foreach (var recipient in recipients)
        {
            var recipientHtml = html;
            var recipientText = plainText;

            if (recipient.redirected)
            {
                var notice =
                    "TAMS TEST MODE - Original recipient: " +
                    recipient.originalEmail;

                recipientHtml =
                    "<p><strong>TAMS TEST MODE</strong></p>" +
                    "<p>Original recipient: " +
                    WebUtility.HtmlEncode(recipient.originalEmail) +
                    "</p><hr/>" +
                    recipientHtml;

                recipientText =
                    notice + Environment.NewLine +
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
                        email = recipient.email,
                        name = recipient.name
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
