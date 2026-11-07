using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.ValueObjects;

namespace SocialContent.Api.Services;

/// <summary>
/// Publikacja postów. Prototyp nie ma integracji z API Facebooka, Instagrama ani TikToka: publikacja sprawdza
/// lokalnie połączenie z kontem (Validate.VerifyConnection) i, gdy jest poprawne, oznacza post jako opublikowany
/// z symulowanym identyfikatorem zewnętrznym. Gdy połączenie jest niepoprawne, post dostaje status Failed,
/// a administrator firmy i managerowie posta dostają powiadomienie.
/// </summary>
public interface IPostPublisher
{
    /// <summary>Publikuje post (Draft/Scheduled/Failed -> Published albo Failed). Wywołujący zapisał już post w bazie.</summary>
    Task PublishAsync(Post post, CancellationToken ct);

    /// <summary>Publikuje zaplanowane posty, których termin już minął; zwraca liczbę przetworzonych.</summary>
    Task<int> PublishDueAsync(CancellationToken ct);
}

public class PostPublisher(AppDbContext db, INotificationService notifications) : IPostPublisher
{
    public async Task PublishAsync(Post post, CancellationToken ct)
    {
        // Edycja posta w statusie Failed przywraca go do Draft/Scheduled (Post.Apply), więc ponowna próba jest możliwa.
        post.MarkPublishing();

        var account = post.TargetAccount;
        string? error;
        if (account is null)
            error = "Post nie ma konta docelowego.";
        else if (!Validate.VerifyConnection(account, out error))
            account.SetConnectionError(error);
        else
            account.SetConnectionError(null);

        if (error is null)
        {
            post.MarkPublished(ExternalId.Create($"sim-{Guid.NewGuid():N}"), ScheduledTime.Create(DateTime.UtcNow));
        }
        else
        {
            post.MarkFailed(error);
            notifications.AddPublishFailed(post, error);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> PublishDueAsync(CancellationToken ct)
    {
        // ScheduledAt jest value objectem z konwerterem, więc terminy porównujemy po stronie aplikacji.
        var scheduled = await db.Posts
            .Include(p => p.Files).Include(p => p.ManagerUsers).Include(p => p.TargetAccount)
            .Where(p => p.Status == PostStatus.Scheduled)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var due = scheduled.Where(p => p.ScheduledAt is not null && p.ScheduledAt.Date <= now).ToList();
        foreach (var post in due)
            await PublishAsync(post, ct);
        return due.Count;
    }
}
