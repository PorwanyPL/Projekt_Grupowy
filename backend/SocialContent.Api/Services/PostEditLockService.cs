using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>
/// Blokada edycji posta: jedna osoba naraz. Blokada ma czas życia (<see cref="PostEditLockService.Ttl"/>); edytor odnawia ją
/// co jakiś czas, a zamknięcie formularza ją zwalnia. Porzucona sesja wygasa sama, więc nikt nie zostaje zablokowany na stałe.
/// Serwer wymusza blokadę przy zapisie i usuwaniu posta (PostService), nie tylko w widoku.
/// </summary>
public interface IPostEditLockService
{
    /// <summary>Przejmuje lub odnawia blokadę; 409, gdy post edytuje ktoś inny.</summary>
    Task<PostLockDto> AcquireAsync(CurrentUser user, Guid postId, CancellationToken ct);

    /// <summary>Zwalnia blokadę, jeśli należy do użytkownika (inaczej nic nie robi).</summary>
    Task ReleaseAsync(CurrentUser user, Guid postId, CancellationToken ct);
}

public class PostEditLockService(AppDbContext db, IMapper mapper) : IPostEditLockService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public async Task<PostLockDto> AcquireAsync(CurrentUser user, Guid postId, CancellationToken ct)
    {
        var post = await db.Posts.Include(p => p.ManagerUsers)
                       .Include(p => p.TargetAccount!).ThenInclude(a => a.ManagerUsers)
                       .FirstOrDefaultAsync(p => p.Id == postId, ct)
                   ?? throw new NotFoundException($"Nie znaleziono posta o id {postId}.");
        if (!PostPermissions.CanView(user, post))
            throw new NotFoundException($"Nie znaleziono posta o id {postId}.");
        if (!PostPermissions.CanModify(user, post))
            throw new ForbiddenException("Nie masz uprawnień do edycji tego posta.");

        var now = DateTime.UtcNow;
        var existing = await db.PostEditLocks.FirstOrDefaultAsync(l => l.PostId == postId, ct);

        if (existing is null)
        {
            existing = PostEditLock.Create(postId, user.Id, user.Role, await DisplayNameAsync(user, ct), now, Ttl);
            db.PostEditLocks.Add(existing);
        }
        else if (existing.IsHeldBy(user.Id, user.Role))
        {
            existing.Renew(now, Ttl);
        }
        else if (existing.IsExpired(now))
        {
            existing.TakeOver(user.Id, user.Role, await DisplayNameAsync(user, ct), now, Ttl);
        }
        else
        {
            throw Busy(existing);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dwie osoby jednocześnie założyły blokadę - klucz główny (PostId) przepuścił tylko jedną.
            throw new ConflictException("Post jest właśnie edytowany przez inną osobę. Spróbuj ponownie za chwilę.");
        }
        return mapper.Map<PostLockDto>(existing) with { HeldByMe = true };
    }

    public async Task ReleaseAsync(CurrentUser user, Guid postId, CancellationToken ct)
    {
        var existing = await db.PostEditLocks.FirstOrDefaultAsync(l => l.PostId == postId, ct);
        if (existing is null || !existing.IsHeldBy(user.Id, user.Role)) return;
        db.PostEditLocks.Remove(existing);
        await db.SaveChangesAsync(ct);
    }

    private static ConflictException Busy(PostEditLock existing) =>
        new($"Ten post jest właśnie edytowany przez: {existing.HolderName}. " +
            "Poczekaj, aż zakończy edycję (blokada wygasa po kilku minutach bezczynności).");

    private async Task<string> DisplayNameAsync(CurrentUser user, CancellationToken ct)
    {
        if (user.IsCompany)
            return await db.CompanyUsers.AsNoTracking().Where(c => c.CompanyId == user.Id)
                .Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "Administrator firmy";

        var manager = await db.ManagerUsers.AsNoTracking().Where(m => m.Id == user.Id)
            .Select(m => new { First = m.FirstName, Last = m.LastName }).FirstOrDefaultAsync(ct);
        return manager is null ? "Manager" : $"{manager.First.Value} {manager.Last.Value}";
    }
}
