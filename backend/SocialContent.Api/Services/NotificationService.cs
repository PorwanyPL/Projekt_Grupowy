using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>
/// Powiadomienia w aplikacji (brak wcześniejszego mechanizmu, więc najprostsze rozwiązanie zgodne ze stosem:
/// tabela w bazie odczytywana przez API). Odbiorcy: administrator firmy i managerowie powiązani z postem.
/// </summary>
public interface INotificationService
{
    /// <summary>Dodaje (bez zapisu - zapisuje wywołujący) powiadomienia o nieudanej publikacji posta.</summary>
    void AddPublishFailed(Post post, string error);

    /// <summary>Powiadamia firmę i autorów, że post został opublikowany (bez zapisu).</summary>
    void AddPublished(Post post);

    /// <summary>Powiadamia klienta, że post jego konta czeka na akceptację (bez zapisu).</summary>
    void AddApprovalRequested(Post post, Guid clientId);

    /// <summary>
    /// Powiadamia firmę i autorów o decyzji klienta: zaakceptował albo odrzucił post (bez zapisu).
    /// Przy odrzuceniu komunikat zawiera uwagi klienta z poprawkami (jeśli je wpisał).
    /// </summary>
    void AddApprovalDecision(Post post, bool approved, string? comment = null);

    /// <summary>Powiadamia uczestnika o nowej wiadomości w rozmowie (jedno nieprzeczytane powiadomienie na rozmowę; bez zapisu).</summary>
    void AddChatMessage(Guid recipientId, UserRole recipientRole, Guid conversationId, string message);

    Task<List<NotificationDto>> GetAsync(CurrentUser user, CancellationToken ct);
    Task MarkReadAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task MarkAllReadAsync(CurrentUser user, CancellationToken ct);
}

public class NotificationService(AppDbContext db, IMapper mapper) : INotificationService
{
    /// <summary>Post na wiele platform to kilka wierszy z jednym GroupId, więc identyczne powiadomienie dla tej samej osoby dodajemy raz.</summary>
    private void AddOnce(Notification n)
    {
        var exists = db.Notifications.Local.Any(x => x.RecipientId == n.RecipientId && x.RecipientRole == n.RecipientRole
                                                      && x.PostId == n.PostId && x.Message == n.Message);
        if (!exists) db.Notifications.Add(n);
    }

    public void AddPublishFailed(Post post, string error)
    {
        var message = $"Publikacja posta \"{post.Title.Value}\" nie powiodła się: {error}";

        if (post.TargetAccount is not null)
            AddOnce(Notification.Create(post.TargetAccount.CompanyId, UserRole.CompanyUser, post.GroupId, message));

        foreach (var manager in post.ManagerUsers.DistinctBy(m => m.Id))
            AddOnce(Notification.Create(manager.Id, UserRole.ManagerUser, post.GroupId, message));
    }

    public void AddPublished(Post post)
    {
        var message = $"Post \"{post.Title.Value}\" został opublikowany.";

        if (post.TargetAccount is not null)
            AddOnce(Notification.Create(post.TargetAccount.CompanyId, UserRole.CompanyUser, post.GroupId, message));
        foreach (var manager in post.ManagerUsers.DistinctBy(m => m.Id))
            AddOnce(Notification.Create(manager.Id, UserRole.ManagerUser, post.GroupId, message));
    }

    public void AddApprovalRequested(Post post, Guid clientId) =>
        AddOnce(Notification.Create(clientId, UserRole.ClientUser, post.GroupId,
            $"Post \"{post.Title.Value}\" czeka na Twoją akceptację."));

    public void AddApprovalDecision(Post post, bool approved, string? comment = null)
    {
        var note = comment?.Trim();
        var message = approved
            ? $"Klient zaakceptował post \"{post.Title.Value}\"."
            : string.IsNullOrEmpty(note)
                ? $"Klient odrzucił post \"{post.Title.Value}\". Popraw go i wyślij ponownie."
                : $"Klient odrzucił post \"{post.Title.Value}\" i zwrócił go do poprawy. Uwagi: {note}";

        if (post.TargetAccount is not null)
            AddOnce(Notification.Create(post.TargetAccount.CompanyId, UserRole.CompanyUser, post.GroupId, message));
        foreach (var manager in post.ManagerUsers.DistinctBy(m => m.Id))
            AddOnce(Notification.Create(manager.Id, UserRole.ManagerUser, post.GroupId, message));
    }

    public void AddChatMessage(Guid recipientId, UserRole recipientRole, Guid conversationId, string message)
    {
        // Jedno nieprzeczytane powiadomienie na rozmowę (kolejne wiadomości nie mnożą wpisów w dzwonku).
        var exists = db.Notifications.Local.Any(n => n.RecipientId == recipientId && n.RecipientRole == recipientRole
                                                     && n.TargetType == NotificationTarget.Chat && n.TargetId == conversationId && !n.IsRead)
                     || db.Notifications.Any(n => n.RecipientId == recipientId && n.RecipientRole == recipientRole
                                                  && n.TargetType == NotificationTarget.Chat && n.TargetId == conversationId && !n.IsRead);
        if (!exists) db.Notifications.Add(Notification.CreateChat(recipientId, recipientRole, conversationId, message));
    }

    public async Task<List<NotificationDto>> GetAsync(CurrentUser user, CancellationToken ct)
    {
        var items = await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientId == user.Id && n.RecipientRole == user.Role)
            .ToListAsync(ct);
        return items.OrderByDescending(n => n.CreatedAt).Take(100).Select(mapper.Map<NotificationDto>).ToList();
    }

    public async Task MarkReadAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var item = await db.Notifications.FirstOrDefaultAsync(
                       n => n.Id == id && n.RecipientId == user.Id && n.RecipientRole == user.Role, ct)
                   ?? throw new NotFoundException("Nie znaleziono powiadomienia.");
        item.MarkRead();
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CurrentUser user, CancellationToken ct)
    {
        var unread = await db.Notifications
            .Where(n => n.RecipientId == user.Id && n.RecipientRole == user.Role && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in unread) n.MarkRead();
        await db.SaveChangesAsync(ct);
    }
}
