using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Domain.ValueObjects;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>
/// Posty. Odczyt: CompanyUser widzi posty kont swojej firmy, ManagerUser tylko posty, których jest autorem.
/// Tworzenie, zmiana i usuwanie: CompanyUser na każdym koncie swojej firmy oraz ManagerUser na koncie, do którego
/// jest przypisany. Manager, któremu odebrano prawa do konta, może tylko przeglądać swoje dawne posty.
/// Autorami nowego posta są od razu wszyscy managerowie przypisani do konta. Edycję jednego posta wykonuje
/// naraz jedna osoba (blokada edycji).
/// </summary>
public interface IPostService
{
    Task<List<PostDto>> GetAllAsync(CurrentUser user, FacebookPostType? type, Guid? accountId, Guid? managerId, CancellationToken ct);
    Task<PostDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task<PostDto> CreateAsync(CurrentUser user, PostWriteDto dto, CancellationToken ct);
    Task<PostDto> UpdateAsync(CurrentUser user, Guid id, PostWriteDto dto, CancellationToken ct);
    Task DeleteAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task<PostLockDto> AcquireLockAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task ReleaseLockAsync(CurrentUser user, Guid id, CancellationToken ct);
}

public class PostService(AppDbContext db, IMapper mapper, IMediaStorage storage, IPostEditLockService locks,
    IPostPublisher publisher) : IPostService
{
    private IQueryable<Post> Query() =>
        db.Posts.Include(p => p.Files).Include(p => p.ManagerUsers)
            .Include(p => p.TargetAccount!).ThenInclude(a => a.ManagerUsers);

    /// <summary>CompanyUser: posty kont swojej firmy. ManagerUser: posty, których jest autorem.</summary>
    private static IQueryable<Post> VisibleTo(CurrentUser user, IQueryable<Post> query) =>
        user.IsCompany
            ? query.Where(p => p.TargetAccount != null && p.TargetAccount.CompanyId == user.Id)
            : query.Where(p => p.ManagerUsers.Any(m => m.Id == user.Id));

    public async Task<List<PostDto>> GetAllAsync(CurrentUser user, FacebookPostType? type, Guid? accountId,
        Guid? managerId, CancellationToken ct)
    {
        if (!user.IsCompany && managerId is not null && managerId != user.Id)
            throw new ForbiddenException("Manager może przeglądać tylko własne posty.");

        var q = VisibleTo(user, Query().AsNoTracking());
        if (type is not null) q = q.Where(p => p.Visibility == type);
        if (accountId is not null) q = q.Where(p => p.TargetAccountId == accountId);
        if (managerId is not null) q = q.Where(p => p.ManagerUsers.Any(m => m.Id == managerId));

        var items = await q.ToListAsync(ct);
        var activeLocks = await ActiveLocksAsync(items.Select(p => p.Id).ToList(), ct);
        return items.OrderByDescending(p => p.CreatedAt.Date)
            .Select(p => ToDto(user, p, activeLocks.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<PostDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var post = await VisibleTo(user, Query().AsNoTracking()).FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw new NotFoundException($"Nie znaleziono posta o id {id}.");
        var activeLocks = await ActiveLocksAsync(new List<Guid> { id }, ct);
        return ToDto(user, post, activeLocks.GetValueOrDefault(id));
    }

    public async Task<PostDto> CreateAsync(CurrentUser user, PostWriteDto dto, CancellationToken ct)
    {
        var platform = RequestedPlatform(dto);
        var (account, managers) = await ResolveAccountAndAuthors(user, dto, ct);

        var title = Title.Create(dto.Title);
        var description = Description.Create(dto.Description);
        var pictures = BuildPictures(dto, account.CompanyId);
        var videos = BuildVideos(dto, account.CompanyId);
        var scheduledAt = BuildSchedule(dto);

        Post post;
        switch (platform)
        {
            case SocialPlatform.Facebook:
            {
                var fb = dto.Facebook!;
                post = FacebookPost.Create(title, description, dto.Visibility, pictures, videos, managers,
                    scheduledAt, account, ExternalId.Create(fb.TargetPageId), ParseUrl(fb.LinkUrl),
                    fb.PrivacySetting, fb.FeedTargeting, fb.PostType);
                break;
            }
            case SocialPlatform.Instagram:
            {
                var ig = dto.Instagram!;
                post = InstagramPost.Create(title, description, dto.Visibility, pictures, videos, managers,
                    scheduledAt, account, ExternalId.Create(ig.InstagramAccountId), ig.MediaType!.Value,
                    ig.UserTags, ParseExternalId(ig.LocationId), ig.ShareToFeed);
                break;
            }
            case SocialPlatform.TikTok:
            {
                var tt = dto.TikTok!;
                post = TikTokPost.Create(title, description, dto.Visibility, pictures, videos, managers,
                    scheduledAt, account, ExternalId.Create(tt.TikTokAccountId), tt.PrivacyLevel!.Value,
                    tt.DisableDuet, tt.DisableStitch, tt.DisableComment, tt.BrandContentToggle, tt.AutoAddMusic);
                break;
            }
            default:
                post = Post.Create(title, description, dto.Visibility, pictures, videos, managers,
                    scheduledAt, account);
                break;
        }

        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);

        // "Publikuj teraz": wynik (Published albo Failed + powiadomienia) widać w statusie zwróconego posta.
        if (dto.PublishNow) await publisher.PublishAsync(post, ct);
        return ToDto(user, post, null);
    }

    public async Task<PostDto> UpdateAsync(CurrentUser user, Guid id, PostWriteDto dto, CancellationToken ct)
    {
        var post = await FindModifiable(user, id, ct);

        var platform = RequestedPlatform(dto);
        if (platform != post.Platform)
            throw new DomainValidationException(post.Platform is null
                ? "Post ogólny nie może zawierać danych platformy; rodzaju posta nie można zmienić po utworzeniu."
                : $"Ten post jest postem platformy {post.Platform}: podaj blok danych tej platformy (i żadnej innej). Rodzaju posta nie można zmienić po utworzeniu.");

        // Serwer wymusza blokadę edycji: gdy post edytuje ktoś inny, zapis kończy się 409 (nic nie jest zmieniane).
        await locks.AcquireAsync(user, id, ct);

        var (account, assigned) = await ResolveAccountAndAuthors(user, dto, ct);
        // Dotychczasowi autorzy zostają (także ci, którym odebrano prawa do konta), a przypisani do konta dochodzą.
        var managers = post.ManagerUsers.Concat(assigned).DistinctBy(m => m.Id).ToList();
        var oldKeys = post.Files.Select(f => f.StorageKey).ToList();

        var title = Title.Create(dto.Title);
        var description = Description.Create(dto.Description);
        var pictures = BuildPictures(dto, account.CompanyId);
        var videos = BuildVideos(dto, account.CompanyId);
        var scheduledAt = BuildSchedule(dto);

        switch (post)
        {
            case FacebookPost fbPost:
            {
                var fb = dto.Facebook!;
                fbPost.Update(title, description, dto.Visibility, pictures, videos, managers, scheduledAt, account,
                    ExternalId.Create(fb.TargetPageId), ParseUrl(fb.LinkUrl), fb.PrivacySetting, fb.FeedTargeting,
                    fb.PostType);
                break;
            }
            case InstagramPost igPost:
            {
                var ig = dto.Instagram!;
                igPost.Update(title, description, dto.Visibility, pictures, videos, managers, scheduledAt, account,
                    ExternalId.Create(ig.InstagramAccountId), ig.MediaType!.Value, ig.UserTags,
                    ParseExternalId(ig.LocationId), ig.ShareToFeed);
                break;
            }
            case TikTokPost ttPost:
            {
                var tt = dto.TikTok!;
                ttPost.Update(title, description, dto.Visibility, pictures, videos, managers, scheduledAt, account,
                    ExternalId.Create(tt.TikTokAccountId), tt.PrivacyLevel!.Value, tt.DisableDuet, tt.DisableStitch,
                    tt.DisableComment, tt.BrandContentToggle, tt.AutoAddMusic);
                break;
            }
            default:
                post.Update(title, description, dto.Visibility, pictures, videos, managers, scheduledAt, account);
                break;
        }

        await db.SaveChangesAsync(ct);

        await locks.ReleaseAsync(user, id, ct); // edycja zakończona zapisem
        await DeleteUnusedFilesAsync(post, oldKeys.Except(post.Files.Select(f => f.StorageKey)), ct);

        if (dto.PublishNow) await publisher.PublishAsync(post, ct);
        return ToDto(user, post, null);
    }

    public async Task DeleteAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var post = await FindModifiable(user, id, ct);
        await locks.AcquireAsync(user, id, ct); // nie usuwamy posta, który ktoś właśnie edytuje

        var keys = post.Files.Select(f => f.StorageKey).ToList();
        var lockRow = await db.PostEditLocks.FirstOrDefaultAsync(l => l.PostId == id, ct);
        if (lockRow is not null) db.PostEditLocks.Remove(lockRow);
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);

        await DeleteUnusedFilesAsync(post, keys, ct);
    }

    public async Task<PostLockDto> AcquireLockAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        await FindModifiable(user, id, ct); // 404/403 zanim ktokolwiek zajmie blokadę
        return await locks.AcquireAsync(user, id, ct);
    }

    public Task ReleaseLockAsync(CurrentUser user, Guid id, CancellationToken ct) =>
        locks.ReleaseAsync(user, id, ct);

    private PostDto ToDto(CurrentUser user, Post post, PostEditLock? activeLock)
    {
        var canModify = PostPermissions.CanModify(user, post);
        return mapper.Map<Post, PostDto>(post) with
        {
            CanEdit = canModify && PostPermissions.IsEditableStatus(post),
            CanDelete = canModify,
            Lock = activeLock is null
                ? null
                : mapper.Map<PostLockDto>(activeLock) with { HeldByMe = activeLock.IsHeldBy(user.Id, user.Role) }
        };
    }

    private async Task<Dictionary<Guid, PostEditLock>> ActiveLocksAsync(List<Guid> postIds, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var found = await db.PostEditLocks.AsNoTracking()
            .Where(l => postIds.Contains(l.PostId) && l.ExpiresAt > now)
            .ToListAsync(ct);
        return found.ToDictionary(l => l.PostId);
    }

    /// <summary>
    /// Post, który użytkownik może zmieniać. Cudze posty zachowują się jak nieistniejące (404); post widoczny,
    /// ale bez prawa zmiany (np. manager bez uprawnień do konta) kończy się 403.
    /// </summary>
    private async Task<Post> FindModifiable(CurrentUser user, Guid id, CancellationToken ct)
    {
        var post = await VisibleTo(user, Query()).FirstOrDefaultAsync(p => p.Id == id, ct)
                   ?? throw new NotFoundException($"Nie znaleziono posta o id {id}.");
        if (!PostPermissions.CanModify(user, post))
            throw new ForbiddenException(
                "Nie masz już uprawnień do konta tego posta - możesz go tylko przeglądać, bez edycji i usuwania.");
        return post;
    }

    /// <summary>Usuwa z dysku pliki, do których nie odwołuje się już żaden inny post.</summary>
    private async Task DeleteUnusedFilesAsync(Post post, IEnumerable<string> keys, CancellationToken ct)
    {
        var companyId = post.TargetAccount?.CompanyId;
        if (companyId is null) return;
        foreach (var key in keys)
        {
            var stillUsed = await db.FileTypes.AnyAsync(f => f.StorageKey == key && f.PostId != post.Id, ct);
            if (!stillUsed) storage.Delete(companyId.Value, key);
        }
    }

    /// <summary>
    /// Reguła tworzenia postów: tylko z poziomu konta społecznościowego. CompanyUser może tworzyć na każdym koncie
    /// swojej firmy (bez względu na przypisanych managerów), ManagerUser - tylko na koncie, do którego jest przypisany.
    /// Autorami są od razu wszyscy managerowie przypisani do konta (współautorzy).
    /// </summary>
    private async Task<(SocialAccount Account, List<ManagerUser> Authors)> ResolveAccountAndAuthors(
        CurrentUser user, PostWriteDto dto, CancellationToken ct)
    {
        if (dto.TargetAccountId is null)
            throw new DomainValidationException("Post można utworzyć tylko w ramach konta społecznościowego - podaj targetAccountId.");

        var account = await db.SocialAccounts.Include(a => a.ManagerUsers)
            .FirstOrDefaultAsync(a => a.Id == dto.TargetAccountId, ct);

        if (user.IsCompany)
        {
            // Konto innej firmy zachowuje się jak nieistniejące.
            if (account is null || account.CompanyId != user.Id)
                throw new NotFoundException("Nie znaleziono konta społecznościowego tej firmy.");
        }
        else if (account is null || account.ManagerUsers.All(m => m.Id != user.Id))
        {
            throw new ForbiddenException("Posty może tworzyć tylko manager przypisany do wybranego konta społecznościowego.");
        }

        return (account, account.ManagerUsers.ToList());
    }

    /// <summary>Który blok platformy podano w żądaniu (null = post ogólny); więcej niż jeden to błąd.</summary>
    private static SocialPlatform? RequestedPlatform(PostWriteDto dto)
    {
        var count = (dto.Facebook is null ? 0 : 1) + (dto.Instagram is null ? 0 : 1) + (dto.TikTok is null ? 0 : 1);
        if (count > 1)
            throw new DomainValidationException("Podaj dane tylko jednej platformy: facebook, instagram albo tikTok.");
        if (dto.Facebook is not null) return SocialPlatform.Facebook;
        if (dto.Instagram is not null) return SocialPlatform.Instagram;
        if (dto.TikTok is not null) return SocialPlatform.TikTok;
        return null;
    }

    private static ScheduledTime? BuildSchedule(PostWriteDto dto)
    {
        if (dto.PublishNow && dto.ScheduledAt is not null)
            throw new DomainValidationException("Wybierz albo publikację od razu (publishNow), albo termin (scheduledAt).");
        return dto.ScheduledAt is null ? null : ScheduledTime.Create(dto.ScheduledAt.Value);
    }

    private static WebUrl? ParseUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUrl.Create(value);

    private static ExternalId? ParseExternalId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ExternalId.Create(value);

    /// <summary>
    /// Media pochodzą z plików przesłanych wcześniej przez POST /api/Media do magazynu firmy konta.
    /// Format i rozmiar bierzemy z dysku, nie od klienta; plik musi pasować rodzajem (zdjęcie/wideo).
    /// </summary>
    private List<Picture> BuildPictures(PostWriteDto dto, Guid companyId) =>
        dto.Pictures.Select(p =>
        {
            var file = FindUploaded(companyId, p.StorageKey, MediaKind.Picture);
            return Picture.Create(FileName.Create(p.FileName), file.Format, file.SizeKb, file.StorageKey);
        }).ToList();

    private List<Video> BuildVideos(PostWriteDto dto, Guid companyId) =>
        dto.Videos.Select(v =>
        {
            var file = FindUploaded(companyId, v.StorageKey, MediaKind.Video);
            return Video.Create(FileName.Create(v.FileName), file.Format, file.SizeKb, file.StorageKey);
        }).ToList();

    private StoredMedia FindUploaded(Guid companyId, string storageKey, MediaKind expected)
    {
        var file = storage.Find(companyId, Validate.StorageKey(storageKey))
                   ?? throw new DomainValidationException("Nie znaleziono przesłanego pliku - prześlij go ponownie.");
        if (file.Kind != expected)
            throw new DomainValidationException(expected == MediaKind.Picture
                ? "Do zdjęć można dodać tylko pliki graficzne (png, jpg, jpeg, gif, webp)."
                : "Do wideo można dodać tylko pliki mp4, mp3 lub flac.");
        return file;
    }
}
