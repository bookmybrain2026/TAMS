
using System.Net.Mail;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace TAMS.Services;

public enum EmailVerificationResult
{
    Deliverable,
    Undeliverable,
    Unknown
}

public sealed class EmailVerificationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<EmailVerificationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<EmailVerificationResult> VerifyAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        // Reject clearly malformed addresses without using API credits.
        if (string.IsNullOrWhiteSpace(email))
            return EmailVerificationResult.Undeliverable;

        email = email.Trim();

        try
        {
            var parsed = new MailAddress(email);

            if (!string.Equals(
                parsed.Address,
                email,
                StringComparison.OrdinalIgnoreCase))
            {
                return EmailVerificationResult.Undeliverable;
            }
        }
        catch (FormatException)
        {
            return EmailVerificationResult.Undeliverable;
        }

        // Reserved example domains are never real mail destinations.
        var domain = email.Split('@').Last();

        if (domain.Equals("example.com", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("example.net", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("example.org", StringComparison.OrdinalIgnoreCase) ||
            domain.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith(".example", StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return EmailVerificationResult.Undeliverable;
        }

        var apiKey = _configuration["EmailVerification:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "Email verification API key is not configured.");

            return EmailVerificationResult.Unknown;
        }

        try
        {
            var url =
                "https://api.emailvalidation.io/v1/info?email=" +
                Uri.EscapeDataString(email) +
                "&timeout=10";

            using var request =
                new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Add("apikey", apiKey);

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Email verification API returned HTTP {StatusCode}.",
                    (int)response.StatusCode);

                return EmailVerificationResult.Unknown;
            }

            using var stream = await response.Content.ReadAsStreamAsync(
                cancellationToken);

            using var json = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

            if (!json.RootElement.TryGetProperty(
                "status",
                out var statusElement))
            {
                return EmailVerificationResult.Unknown;
            }

            var status = statusElement.GetString();

            // Only a definite invalid result is treated as undeliverable.
            return status?.ToLowerInvariant() switch
            {
                "valid" => EmailVerificationResult.Deliverable,
                "invalid" => EmailVerificationResult.Undeliverable,

                // Catch-all, disposable, risky, and unknown
                // are not proof that a mailbox does not exist.
                _ => EmailVerificationResult.Unknown
            };
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Email verification request timed out.");
            return EmailVerificationResult.Unknown;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "Email verification service was unavailable.");

            return EmailVerificationResult.Unknown;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Email verification returned an unexpected response.");

            return EmailVerificationResult.Unknown;
        }
    }
}
