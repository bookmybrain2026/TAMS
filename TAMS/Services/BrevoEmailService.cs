
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

        // Replace embedded CID banner images with a public HTTPS URL.
        var html = message.HtmlBody ?? "";

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

        var recipients = message.To.Mailboxes
            .Select(mailbox => new
            {
                email = mailbox.Address,
                name = mailbox.Name
            })
            .ToArray();

        if (recipients.Length == 0)
            throw new InvalidOperationException(
                "The email has no recipients.");

        var payload = new
        {
            sender = new
            {
                name = senderName,
                email = senderEmail
            },
            to = recipients,
            subject = message.Subject,
            htmlContent = html,
            textContent = message.TextBody ?? ""
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
                $"Brevo rejected email: HTTP {(int)response.StatusCode}. {details}");
        }
    }
}
