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

    Task<List<NotificationDto>> GetAsync(CurrentUser user, CancellationToken ct);
    Task MarkReadAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task MarkAllReadAsync(CurrentUser user, CancellationToken ct);
}

public class NotificationService(AppDbContext db, IMapper mapper) : INotificationService
{
    public void AddPublishFailed(Post post, string error)
    {
        var message = $"Publikacja posta \"{post.Title.Value}\" nie powiodła się: {error}";

        if (post.TargetAccount is not null)
            db.Notifications.Add(Notification.Create(post.TargetAccount.CompanyId, UserRole.CompanyUser, post.Id, message));

        foreach (var manager in post.ManagerUsers.DistinctBy(m => m.Id))
            db.Notifications.Add(Notification.Create(manager.Id, UserRole.ManagerUser, post.Id, message));
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
