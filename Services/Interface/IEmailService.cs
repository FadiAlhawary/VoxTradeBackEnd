namespace VoxTrade.Services.Interface;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task SendTwoFaCodeAsync(string toEmail, string username, string code);
}
