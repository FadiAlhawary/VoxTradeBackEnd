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

    public async Task SendTwoFaCodeAsync(string toEmail, string username, string code)
    {
        var smtpHost = _configuration["Smtp:Host"];
        var smtpUser = _configuration["Smtp:Username"];
        var smtpPass = _configuration["Smtp:Password"];
        var fromEmail = _configuration["Smtp:FromEmail"];
        var fromName = _configuration["Smtp:FromName"] ?? "VoxTrade";

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("SMTP host and from email must be configured.");

        var hasUsername = !string.IsNullOrWhiteSpace(smtpUser);
        var hasPassword = !string.IsNullOrWhiteSpace(smtpPass);

        var smtpPort = int.TryParse(_configuration["Smtp:Port"], out var parsedPort) ? parsedPort : 587;
        var enableSsl = !string.Equals(_configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (hasUsername && hasPassword)
            client.Credentials = new NetworkCredential(smtpUser, smtpPass);

        var body = $@"
<div style='font-family:sans-serif;max-width:480px;margin:0 auto;background:#0f0f13;border:1px solid rgba(255,255,255,0.08);border-radius:12px;overflow:hidden'>
  <div style='background:linear-gradient(135deg,#6366f1,#8b5cf6);padding:28px 32px'>
    <h2 style='color:#fff;margin:0;font-size:20px;font-weight:700'>VoxTrade</h2>
    <p style='color:rgba(255,255,255,0.7);margin:4px 0 0;font-size:13px'>Two-Factor Authentication</p>
  </div>
  <div style='padding:28px 32px'>
    <p style='color:rgba(255,255,255,0.6);font-size:13px;margin:0 0 20px'>Hi {username}, use the code below to verify your identity. It expires in <strong style='color:#fff'>10 minutes</strong>.</p>
    <div style='background:rgba(99,102,241,0.08);border:1px solid rgba(99,102,241,0.25);border-radius:8px;padding:20px;text-align:center;margin-bottom:20px'>
      <span style='color:#818cf8;font-size:36px;font-weight:800;letter-spacing:12px;font-family:monospace'>{code}</span>
    </div>
    <p style='color:rgba(255,255,255,0.3);font-size:11px;margin:0'>If you didn't request this, you can safely ignore it. Never share this code with anyone.</p>
  </div>
</div>";

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = $"{code} — VoxTrade verification code",
            IsBodyHtml = true,
            Body = body
        };

        message.To.Add(toEmail);
        await client.SendMailAsync(message);
    }
}
