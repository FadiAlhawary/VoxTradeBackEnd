using System.Net;
using System.Net.Mail;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var smtpHost = _configuration["Smtp:Host"];
        var smtpUser = _configuration["Smtp:Username"];
        var smtpPass = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"];
        var fromName = _configuration["Smtp:FromName"] ?? "VoxTrade";

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("SMTP host and from email must be configured.");
        }

        var hasUsername = !string.IsNullOrWhiteSpace(smtpUser);
        var hasPassword = !string.IsNullOrWhiteSpace(smtpPass);

        if (hasUsername != hasPassword)
        {
            throw new InvalidOperationException("SMTP username and password must both be provided when authentication is enabled.");
        }

        var smtpPort = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !string.Equals(_configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (hasUsername && hasPassword)
        {
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Reset your VoxTrade password",
            IsBodyHtml = true,
            Body = $"<p>You requested a password reset for your VoxTrade account.</p><p>Click the link below to set a new password (valid for 15 minutes):</p><p><a href=\"{resetLink}\">Reset Password</a></p><p>If you did not request this, you can ignore this email.</p>"
        };

        message.To.Add(toEmail);
        await client.SendMailAsync(message);
    }
}
