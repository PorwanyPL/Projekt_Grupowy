using Microsoft.AspNetCore.SignalR;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Dtos;
using SocialContent.Api.Hubs;

namespace SocialContent.Api.Services;

/// <summary>Odbiorca zdarzenia czasu rzeczywistego (użytkownik jednej z trzech ról).</summary>
public readonly record struct ChatRecipient(Guid UserId, UserRole Role);

/// <summary>
/// Jedyny kanał dostarczania zdarzeń do podłączonych klientów (SignalR): nowe wiadomości czatu (ReceiveMessage)
/// i powiadomienia o postach (ReceiveNotification). Błąd dostarczenia nigdy nie cofa zapisu w bazie - dane i tak są w bazie,
/// a klient odczyta je przy następnym odświeżeniu.
/// </summary>
public interface IRealtimeNotifier
{
    Task MessageAsync(ChatMessageDto message, IEnumerable<ChatRecipient> recipients, CancellationToken ct = default);
    Task NotificationAsync(NotificationDto notification, Guid recipientId, UserRole role, CancellationToken ct = default);
}

/// <summary>Grupy SignalR: każdy użytkownik ma własną grupę, do której należą wszystkie jego połączenia (kilka urządzeń).</summary>
public static class ChatGroups
{
    public static string Of(Guid userId, UserRole role) => $"user:{role}:{userId:N}";
}

public class SignalRRealtimeNotifier(IHubContext<ChatHub> hub, ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public async Task MessageAsync(ChatMessageDto message, IEnumerable<ChatRecipient> recipients, CancellationToken ct = default)
    {
        foreach (var r in recipients.Distinct())
        {
            try { await hub.Clients.Group(ChatGroups.Of(r.UserId, r.Role)).SendAsync("ReceiveMessage", message, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Nie udało się dostarczyć wiadomości czatu do {Role} {Id}.", r.Role, r.UserId); }
        }
    }

    public async Task NotificationAsync(NotificationDto notification, Guid recipientId, UserRole role, CancellationToken ct = default)
    {
        try { await hub.Clients.Group(ChatGroups.Of(recipientId, role)).SendAsync("ReceiveNotification", notification, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Nie udało się dostarczyć powiadomienia do {Role} {Id}.", role, recipientId); }
    }
}

/// <summary>Bez połączeń (testy i narzędzia): nic nie wysyła.</summary>
public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task MessageAsync(ChatMessageDto message, IEnumerable<ChatRecipient> recipients, CancellationToken ct = default) => Task.CompletedTask;
    public Task NotificationAsync(NotificationDto notification, Guid recipientId, UserRole role, CancellationToken ct = default) => Task.CompletedTask;
}
