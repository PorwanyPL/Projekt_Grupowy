namespace SocialContent.Api.Services;

/// <summary>Wysyłka wiadomości e-mail (np. kodu weryfikacji dwuetapowej).</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

/// <summary>
/// Implementacja deweloperska: nie wysyła nic, tylko zapisuje treść wiadomości w logu serwera.
/// Na produkcji należy ją zastąpić implementacją SMTP / dostawcy poczty (rejestracja w Program.cs) - kody w logach to ryzyko.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        logger.LogWarning("[E-MAIL DEV] Do: {To} | Temat: {Subject} | Treść: {Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
