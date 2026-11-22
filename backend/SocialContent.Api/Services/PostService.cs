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
/// Posty. Odczyt: CompanyUser widzi posty kont swojej firmy, ManagerUser tylko posty, których jest autorem, ClientUser
/// posty swoich kont (bez edycji). Tworzenie, zmiana i usuwanie: CompanyUser na każdym koncie swojej firmy oraz
/// ManagerUser na koncie, do którego jest przypisany. Manager, któremu odebrano prawa do konta, może tylko przeglądać
/// swoje dawne posty. Autorami nowego posta są od razu wszyscy managerowie przypisani do konta. Edycję jednego posta
/// wykonuje naraz jedna osoba (blokada edycji).
/// Post przeznaczony na kilka platform to kilka wariantów (po jednym Post na konto) ze wspólnym GroupId; w API jest
/// jednym postem (id = GroupId, lista wariantów w polu targets), a operacje dotyczą wszystkich jego wariantów.
/// </summary>
public interface IPostService
{
    Task<List<PostDto>> GetAllAsync(CurrentUser user, FacebookPostType? type, Guid? accountId, Guid? managerId, CancellationToken ct);

    /// <summary>Lista postów z filtrami (status, osoba, tagi, konta, platformy, daty); kategorie łączą się koniunkcją.</summary>
    Task<List<PostDto>> GetAllAsync(CurrentUser user, PostFilterDto filter, CancellationToken ct);
    Task<PostDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task<PostDto> CreateAsync(CurrentUser user, PostWriteDto dto, CancellationToken ct);
    Task<PostDto> UpdateAsync(CurrentUser user, Guid id, PostWriteDto dto, CancellationToken ct);
    Task DeleteAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task<PostLockDto> AcquireLockAsync(CurrentUser user, Guid id, CancellationToken ct);
    Task ReleaseLockAsync(CurrentUser user, Guid id, CancellationToken ct);

    /// <summary>Klient akceptuje post swojego konta: publikacja od razu albo zaplanowana, zależnie od terminu.</summary>
    Task<PostDto> ApproveAsync(CurrentUser user, Guid id, CancellationToken ct);

    /// <summary>
    /// Klient odrzuca post swojego konta (opcjonalnie z uwagami do poprawy); autorzy dostają powiadomienie z uwagami,
    /// mogą poprawić post i wysłać go ponownie.
    /// </summary>
    Task<PostDto> RejectAsync(CurrentUser user, Guid id, CancellationToken ct, string? comment = null);
}

public class PostService(AppDbContext db, IMapper mapper, IMediaStorage storage, IPostEditLockService locks,
    IPostPublisher publisher, INotificationService notifications) : IPostService
{
    private IQueryable<Post> Query() =>
        db.Posts.Include(p => p.Files).Include(p => p.ManagerUsers)
            .Include(p => p.TargetAccount!).ThenInclude(a => a.ManagerUsers);

    /// <summary>CompanyUser: posty kont swojej firmy. ManagerUser: posty, których jest autorem. ClientUser: posty swoich kont.</summary>
    private static IQueryable<Post> VisibleTo(CurrentUser user, IQueryable<Post> query) =>
        user.Role switch
        {
            UserRole.CompanyUser => query.Where(p => p.TargetAccount != null && p.TargetAccount.CompanyId == user.Id),
            UserRole.ClientUser => query.Where(p => p.TargetAccount != null && p.TargetAccount.OwnerClientId == user.Id),
            _ => query.Where(p => p.ManagerUsers.Any(m => m.Id == user.Id))
        };

    public Task<List<PostDto>> GetAllAsync(CurrentUser user, FacebookPostType? type, Guid? accountId,
        Guid? managerId, CancellationToken ct) =>
        GetAllAsync(user, new PostFilterDto { Type = type, AccountId = accountId, ManagerId = managerId }, ct);

    public async Task<List<PostDto>> GetAllAsync(CurrentUser user, PostFilterDto filter, CancellationToken ct)
    {
        if (user.IsManager && filter.ManagerId is not null && filter.ManagerId != user.Id)
            throw new ForbiddenException("Manager może przeglądać tylko własne posty.");

        // Zakres widoczności wynika z roli (VisibleTo); filtry działają dopiero na tym, co użytkownik i tak widzi.
        var variants = await VisibleTo(user, Query().AsNoTracking()).ToListAsync(ct);
        var groups = variants.GroupBy(p => p.GroupId).Select(g => g.ToList()).Where(g => Matches(g, filter)).ToList();

        var activeLocks = await ActiveLocksAsync(groups.Select(g => g[0].GroupId).ToList(), ct);
        var names = await CounterpartsAsync(user, groups.SelectMany(g => g), ct);
        return groups
            .Select(g => ToGroupDto(user, g, activeLocks.GetValueOrDefault(g[0].GroupId), names))
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public async Task<PostDto> GetAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var variants = await VisibleTo(user, Query().AsNoTracking()).Where(p => p.GroupId == id).ToListAsync(ct);
        if (variants.Count == 0) throw new NotFoundException($"Nie znaleziono posta o id {id}.");
        return await ToGroupDtoAsync(user, variants, ct);
    }

    public async Task<PostDto> CreateAsync(CurrentUser user, PostWriteDto dto, CancellationToken ct)
    {
        var targets = await ResolveTargets(user, dto, ct);
        ValidateBlocks(dto, targets);
        var scheduledAt = BuildSchedule(dto);
        var single = targets.Count == 1;

        var posts = new List<Post>();
        foreach (var (account, authors) in targets)
        {
            var post = BuildPost(dto, account, authors, PlatformFor(dto, account, single), scheduledAt);
            post.SetTags(dto.Tags);
            if (posts.Count > 0) post.AssignGroup(posts[0].GroupId); // pierwszy wariant nadaje GroupId całej grupie
            posts.Add(post);
        }

        db.Posts.AddRange(posts);
        await db.SaveChangesAsync(ct);

        await FinishPublicationAsync(posts.Select(p => (p, false)).ToList(), dto.PublishNow, ct);
        return await ToGroupDtoAsync(user, posts, ct);
    }

    public async Task<PostDto> UpdateAsync(CurrentUser user, Guid id, PostWriteDto dto, CancellationToken ct)
    {
        var variants = await FindModifiable(user, id, ct);

        if (dto.PublishNow && variants.Any(v => v.Status == PostStatus.Published))
            throw new DomainValidationException("Ten post jest już opublikowany - można go edytować, ale nie publikować ponownie.");

        // Serwer wymusza blokadę edycji: gdy post edytuje ktoś inny, zapis kończy się 409 (nic nie jest zmieniane).
        await locks.AcquireAsync(user, id, ct);

        var targets = await ResolveTargets(user, dto, ct);
        ValidateBlocks(dto, targets);
        var single = targets.Count == 1;
        var scheduledAt = BuildSchedule(dto);
        var wanted = targets.ToDictionary(t => t.Account.Id);

        var kept = variants.Where(v => v.TargetAccountId is { } a && wanted.ContainsKey(a)).ToList();
        var removed = variants.Except(kept).ToList();
        var allPublished = variants.All(v => v.Status == PostStatus.Published);

        var oldKeys = new List<(Guid CompanyId, string Key)>();
        var finish = new List<(Post Post, bool SkipApproval)>();

        foreach (var variant in kept)
        {
            var (account, assigned) = wanted[variant.TargetAccountId!.Value];
            var platform = PlatformFor(dto, account, single);
            if (platform != variant.Platform)
                throw new DomainValidationException(variant.Platform is null
                    ? "Post ogólny nie może zawierać danych platformy; rodzaju posta nie można zmienić po utworzeniu."
                    : $"Ten post jest postem platformy {variant.Platform}: podaj blok danych tej platformy (i żadnej innej). Rodzaju posta nie można zmienić po utworzeniu.");

            // Dotychczasowi autorzy zostają (także ci, którym odebrano prawa do konta), a przypisani do konta dochodzą.
            var managers = variant.ManagerUsers.Concat(assigned).DistinctBy(m => m.Id).ToList();
            oldKeys.AddRange(variant.Files.Select(f => (account.CompanyId, f.StorageKey)));

            // Opublikowany wariant w grupie z niepublikowanymi nie dostaje terminu (post opublikowany nie ma terminu).
            var schedule = variant.Status == PostStatus.Published && !allPublished ? null : scheduledAt;
            // Edycja posta, który był już zaplanowany albo opublikowany, nie wraca do akceptacji klienta.
            var skipApproval = variant.Status is PostStatus.Scheduled or PostStatus.Published;

            ApplyUpdate(variant, dto, account, managers, schedule);
            variant.SetTags(dto.Tags);
            finish.Add((variant, skipApproval));
        }

        // Dodane platformy: nowe warianty w tej samej grupie.
        var created = new List<Post>();
        foreach (var (account, authors) in targets.Where(t => kept.All(k => k.TargetAccountId != t.Account.Id)))
        {
            var post = BuildPost(dto, account, authors, PlatformFor(dto, account, single), scheduledAt);
            post.SetTags(dto.Tags);
            post.AssignGroup(id);
            created.Add(post);
            finish.Add((post, false));
        }
        db.Posts.AddRange(created);

        // Usunięte platformy: warianty znikają (media z dysku sprzątamy po zapisie).
        foreach (var variant in removed)
        {
            if (variant.TargetAccount is not null)
                oldKeys.AddRange(variant.Files.Select(f => (variant.TargetAccount!.CompanyId, f.StorageKey)));
        }
        db.Posts.RemoveRange(removed);

        await db.SaveChangesAsync(ct);

        await locks.ReleaseAsync(user, id, ct); // edycja zakończona zapisem
        await DeleteUnusedFilesAsync(oldKeys, ct);

        await FinishPublicationAsync(finish, dto.PublishNow, ct);
        return await ToGroupDtoAsync(user, kept.Concat(created).ToList(), ct);
    }

    private void ApplyUpdate(Post post, PostWriteDto dto, SocialAccount account, List<ManagerUser> managers, ScheduledTime? scheduledAt)
    {
        var title = Title.Create(dto.Title);
        var description = Description.Create(dto.Description);
        var pictures = BuildPictures(dto, account.CompanyId);
        var videos = BuildVideos(dto, account.CompanyId);

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
    }

    private Post BuildPost(PostWriteDto dto, SocialAccount account, List<ManagerUser> managers, SocialPlatform? platform,
        ScheduledTime? scheduledAt)
    {
        var title = Title.Create(dto.Title);
        var description = Description.Create(dto.Description);
        // Każdy wariant ma własne wiersze plików (te same klucze w magazynie), bo encja należy do jednego posta.
        var pictures = BuildPictures(dto, account.CompanyId);
        var videos = BuildVideos(dto, account.CompanyId);

        switch (platform)
        {
            case SocialPlatform.Facebook:
            {
                var fb = dto.Facebook!;
                return FacebookPost.Create(title, description, dto.Visibility, pictures, videos, managers,
                    scheduledAt, account, ExternalId.Create(fb.TargetPageId), ParseUrl(fb.LinkUrl),
                    fb.PrivacySetting, fb.FeedTargeting, fb.PostType);
            }
            case SocialPlatform.Instagram:
            {
                var ig = dto.Instagram!;
                return InstagramPost.Create(title, description, dto.Visibility, pictures, videos, managers,
                    scheduledAt, account, ExternalId.Create(ig.InstagramAccountId), ig.MediaType!.Value,
                    ig.UserTags, ParseExternalId(ig.LocationId), ig.ShareToFeed);
            }
            case SocialPlatform.TikTok:
            {
                var tt = dto.TikTok!;
                return TikTokPost.Create(title, description, dto.Visibility, pictures, videos, managers,
                    scheduledAt, account, ExternalId.Create(tt.TikTokAccountId), tt.PrivacyLevel!.Value,
                    tt.DisableDuet, tt.DisableStitch, tt.DisableComment, tt.BrandContentToggle, tt.AutoAddMusic);
            }
            default:
                return Post.Create(title, description, dto.Visibility, pictures, videos, managers, scheduledAt, account);
        }
    }

    /// <summary>
    /// Po zapisie wariantów: jeśli klient akceptuje posty sam (PostApprovalMode.Client) i post ma być opublikowany (teraz albo w terminie),
    /// trafia do akceptacji klienta (status AwaitingApproval + jedno powiadomienie na klienta); w przeciwnym razie "Publikuj teraz"
    /// publikuje od razu. Wyjątek: edycja posta, który był już zaplanowany lub opublikowany, nie wymaga ponownej akceptacji.
    /// </summary>
    private async Task FinishPublicationAsync(List<(Post Post, bool SkipApproval)> items, bool publishNow, CancellationToken ct)
    {
        var toPublish = new List<Post>();
        var dirty = false;
        foreach (var (post, skipApproval) in items)
        {
            var account = post.TargetAccount!;
            var wantsPublication = publishNow || post.Status == PostStatus.Scheduled;
            if (wantsPublication && !skipApproval && post.Status is PostStatus.Draft or PostStatus.Scheduled
                && await ApprovalModeAsync(account, ct) == PostApprovalMode.Client)
            {
                post.SubmitForApproval();
                notifications.AddApprovalRequested(post, account.OwnerClientId);
                dirty = true;
                continue;
            }
            // "Publikuj teraz": wynik (Published albo Failed + powiadomienia) widać w statusie zwróconego posta.
            if (publishNow) toPublish.Add(post);
        }

        if (dirty) await db.SaveChangesAsync(ct);
        foreach (var post in toPublish) await publisher.PublishAsync(post, ct);
    }

    private async Task<PostApprovalMode> ApprovalModeAsync(SocialAccount account, CancellationToken ct) =>
        await db.CompanyClients.AsNoTracking()
            .Where(l => l.CompanyId == account.CompanyId && l.ClientId == account.OwnerClientId)
            .Select(l => (PostApprovalMode?)l.ApprovalMode).FirstOrDefaultAsync(ct)
        ?? PostApprovalMode.Manager;

    public async Task<PostDto> ApproveAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var (variants, deciding) = await FindForDecision(user, id, ct);
        foreach (var post in deciding)
        {
            post.Approve(DateTime.UtcNow);
            notifications.AddApprovalDecision(post, approved: true);
        }
        await db.SaveChangesAsync(ct);

        // Bez terminu (albo z terminem, który minął w czasie oczekiwania) publikujemy od razu; inaczej zrobi to scheduler.
        foreach (var post in deciding.Where(p => p.Status == PostStatus.Draft))
            await publisher.PublishAsync(post, ct);
        return await ToGroupDtoAsync(user, variants, ct);
    }

    public async Task<PostDto> RejectAsync(CurrentUser user, Guid id, CancellationToken ct, string? comment = null)
    {
        var (variants, deciding) = await FindForDecision(user, id, ct);
        foreach (var post in deciding)
        {
            post.Reject(comment);
            notifications.AddApprovalDecision(post, approved: false, comment);
        }
        await db.SaveChangesAsync(ct);
        return await ToGroupDtoAsync(user, variants, ct);
    }

    private async Task<(List<Post> All, List<Post> Deciding)> FindForDecision(CurrentUser user, Guid id, CancellationToken ct)
    {
        var variants = await VisibleTo(user, Query()).Where(p => p.GroupId == id).ToListAsync(ct);
        if (variants.Count == 0) throw new NotFoundException($"Nie znaleziono posta o id {id}.");
        var deciding = variants.Where(p => PostPermissions.CanDecide(user, p)).ToList();
        if (deciding.Count == 0)
            throw new ConflictException("Ten post nie oczekuje na Twoją akceptację.");
        return (variants, deciding);
    }

    public async Task DeleteAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        var variants = await FindModifiable(user, id, ct);
        await locks.AcquireAsync(user, id, ct); // nie usuwamy posta, który ktoś właśnie edytuje

        var keys = variants.Where(v => v.TargetAccount is not null)
            .SelectMany(v => v.Files.Select(f => (v.TargetAccount!.CompanyId, f.StorageKey))).ToList();
        var lockRow = await db.PostEditLocks.FirstOrDefaultAsync(l => l.PostId == id, ct);
        if (lockRow is not null) db.PostEditLocks.Remove(lockRow);
        db.Posts.RemoveRange(variants);
        await db.SaveChangesAsync(ct);

        await DeleteUnusedFilesAsync(keys, ct);
    }

    public async Task<PostLockDto> AcquireLockAsync(CurrentUser user, Guid id, CancellationToken ct)
    {
        await FindModifiable(user, id, ct); // 404/403 zanim ktokolwiek zajmie blokadę
        return await locks.AcquireAsync(user, id, ct);
    }

    public Task ReleaseLockAsync(CurrentUser user, Guid id, CancellationToken ct) =>
        locks.ReleaseAsync(user, id, ct);

    // ---------- filtrowanie i składanie DTO logicznego posta (grupy wariantów) ----------

    private static readonly PostStatus[] StatusPriority =
    {
        PostStatus.Publishing, PostStatus.Failed, PostStatus.AwaitingApproval, PostStatus.Rejected,
        PostStatus.Scheduled, PostStatus.Draft, PostStatus.Published
    };

    /// <summary>Status zbiorczy grupy: wygrywa najpilniejszy status wariantu.</summary>
    private static PostStatus AggregateStatus(IEnumerable<Post> variants)
    {
        var set = variants.Select(v => v.Status).ToHashSet();
        return StatusPriority.First(set.Contains);
    }

    private static SocialPlatform? PlatformOf(Post p) => p.Platform ?? p.TargetAccount?.Platform;

    /// <summary>Data posta w kalendarzu i filtrze zakresu: termin, a bez niego czas publikacji, a bez niego data utworzenia.</summary>
    private static DateTime EffectiveDate(List<Post> variants)
    {
        var rep = Representative(variants);
        if (rep.ScheduledAt is not null) return rep.ScheduledAt.Date;
        var published = variants.Where(v => v.PublishedAt is not null).Select(v => v.PublishedAt!.Date).ToList();
        return published.Count > 0 ? published.Max() : rep.CreatedAt.Date;
    }

    private static Post Representative(List<Post> variants) =>
        variants.OrderBy(v => v.CreatedAt.Date).ThenBy(v => v.Id).First();

    private static bool Matches(List<Post> g, PostFilterDto f)
    {
        var rep = Representative(g);
        if (f.Type is not null && rep.Visibility != f.Type) return false;
        if (f.AccountId is not null && g.All(p => p.TargetAccountId != f.AccountId)) return false;
        if (f.ManagerId is not null && !g.Any(p => p.ManagerUsers.Any(m => m.Id == f.ManagerId))) return false;
        if (f.Status.Count > 0 && !f.Status.Contains(AggregateStatus(g))) return false;
        if (f.Tag.Count > 0)
        {
            var tags = rep.Tags;
            if (!f.Tag.Any(t => tags.Contains((t ?? "").Trim(), StringComparer.OrdinalIgnoreCase))) return false;
        }
        if (f.AccountIds.Count > 0 && !g.Any(p => p.TargetAccountId is { } a && f.AccountIds.Contains(a))) return false;
        if (f.Platform.Count > 0 && !g.Any(p => PlatformOf(p) is { } pl && f.Platform.Contains(pl))) return false;
        if (f.From is not null || f.To is not null)
        {
            var date = EffectiveDate(g);
            if (f.From is not null && date < f.From) return false;
            if (f.To is not null && date > f.To) return false;
        }
        return true;
    }

    /// <summary>
    /// Nazwa "drugiej strony" przy koncie: dla firmy i managera klient (pseudonim nadany przez firmę, a bez niego imię
    /// i nazwisko), dla klienta - firma obsługująca konto. Klucz: id konta.
    /// </summary>
    private async Task<Dictionary<Guid, string>> CounterpartsAsync(CurrentUser user, IEnumerable<Post> posts, CancellationToken ct)
    {
        var accounts = posts.Select(p => p.TargetAccount).Where(a => a is not null).Select(a => a!)
            .DistinctBy(a => a.Id).ToList();
        if (accounts.Count == 0) return new();

        var companyIds = accounts.Select(a => a.CompanyId).Distinct().ToList();
        if (user.IsClient)
        {
            var companies = await db.CompanyUsers.AsNoTracking().Where(c => companyIds.Contains(c.CompanyId)).ToListAsync(ct);
            return accounts.ToDictionary(a => a.Id,
                a => companies.FirstOrDefault(c => c.CompanyId == a.CompanyId)?.Name ?? "");
        }

        var ownerIds = accounts.Select(a => a.OwnerClientId).Distinct().ToList();
        var owners = await db.ClientUsers.AsNoTracking().Where(c => ownerIds.Contains(c.Id)).ToListAsync(ct);
        var links = await db.CompanyClients.AsNoTracking()
            .Where(l => companyIds.Contains(l.CompanyId) && ownerIds.Contains(l.ClientId)).ToListAsync(ct);
        return accounts.ToDictionary(a => a.Id, a =>
        {
            var alias = links.FirstOrDefault(l => l.CompanyId == a.CompanyId && l.ClientId == a.OwnerClientId)?.Alias;
            if (!string.IsNullOrWhiteSpace(alias)) return alias;
            var owner = owners.FirstOrDefault(o => o.Id == a.OwnerClientId);
            return owner is null ? "" : $"{owner.FirstName.Value} {owner.LastName.Value}";
        });
    }

    private async Task<PostDto> ToGroupDtoAsync(CurrentUser user, List<Post> variants, CancellationToken ct)
    {
        var groupId = variants[0].GroupId;
        var activeLocks = await ActiveLocksAsync(new List<Guid> { groupId }, ct);
        var names = await CounterpartsAsync(user, variants, ct);
        return ToGroupDto(user, variants, activeLocks.GetValueOrDefault(groupId), names);
    }

    private PostDto ToGroupDto(CurrentUser user, List<Post> variants, PostEditLock? activeLock, Dictionary<Guid, string> names)
    {
        var rep = Representative(variants);
        var ordered = variants.OrderBy(v => v.CreatedAt.Date).ThenBy(v => v.Id).ToList();
        var canModify = ordered.All(v => PostPermissions.CanModify(user, v));
        var managers = ordered.SelectMany(v => v.ManagerUsers).DistinctBy(m => m.Id).ToList();
        var published = ordered.Where(v => v.PublishedAt is not null).Select(v => v.PublishedAt!.Date).ToList();
        var counterpart = string.Join(", ", ordered
            .Select(v => v.TargetAccountId is { } a ? names.GetValueOrDefault(a, "") : "")
            .Where(n => n.Length > 0).Distinct());

        return mapper.Map<Post, PostDto>(rep) with
        {
            Status = AggregateStatus(ordered),
            ManagerUsers = mapper.Map<List<ManagerUserDto>>(managers),
            PublishedAt = published.Count > 0 ? DateTime.SpecifyKind(published.Max(), DateTimeKind.Utc) : null,
            ErrorMessage = ordered.FirstOrDefault(v => v.Status == PostStatus.Failed)?.ErrorMessage ?? rep.ErrorMessage,
            RejectionComment = ordered.FirstOrDefault(v => v.Status == PostStatus.Rejected)?.RejectionComment,
            Targets = ordered.Select(v => new PostTargetDto(v.Id, v.TargetAccountId, v.TargetAccount?.AccountName ?? "",
                PlatformOf(v), v.Status, v.ErrorMessage)).ToList(),
            CounterpartName = counterpart.Length == 0 ? null : counterpart,
            CanEdit = canModify && ordered.All(PostPermissions.IsEditableStatus),
            CanDelete = canModify,
            CanApprove = ordered.Any(v => PostPermissions.CanDecide(user, v)),
            Lock = activeLock is null
                ? null
                : mapper.Map<PostLockDto>(activeLock) with { HeldByMe = activeLock.IsHeldBy(user.Id, user.Role) }
        };
    }

    private async Task<Dictionary<Guid, PostEditLock>> ActiveLocksAsync(List<Guid> groupIds, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var found = await db.PostEditLocks.AsNoTracking()
            .Where(l => groupIds.Contains(l.PostId) && l.ExpiresAt > now)
            .ToListAsync(ct);
        return found.ToDictionary(l => l.PostId);
    }

    /// <summary>
    /// Warianty posta (po GroupId), które użytkownik może zmieniać. Cudze posty zachowują się jak nieistniejące (404);
    /// post widoczny, ale bez prawa zmiany (np. manager bez uprawnień do konta) kończy się 403.
    /// </summary>
    private async Task<List<Post>> FindModifiable(CurrentUser user, Guid id, CancellationToken ct)
    {
        var variants = await VisibleTo(user, Query()).Where(p => p.GroupId == id).ToListAsync(ct);
        if (variants.Count == 0) throw new NotFoundException($"Nie znaleziono posta o id {id}.");
        if (variants.Any(v => !PostPermissions.CanModify(user, v)))
            throw new ForbiddenException(user.IsClient
                ? "Klient może posty tylko przeglądać oraz akceptować lub odrzucać - bez edycji i usuwania."
                : "Nie masz już uprawnień do konta tego posta - możesz go tylko przeglądać, bez edycji i usuwania.");
        return variants;
    }

    /// <summary>Usuwa z dysku pliki (firma, klucz), do których nie odwołuje się już żaden wiersz FileTypes (wołane po zapisie).</summary>
    private async Task DeleteUnusedFilesAsync(IEnumerable<(Guid CompanyId, string Key)> keys, CancellationToken ct)
    {
        foreach (var (companyId, key) in keys.Distinct())
        {
            var stillUsed = await db.FileTypes.AnyAsync(f => f.StorageKey == key, ct);
            if (!stillUsed) storage.Delete(companyId, key);
        }
    }

    private static List<Guid> RequestedAccountIds(PostWriteDto dto) =>
        dto.TargetAccountIds.Concat(dto.TargetAccountId is { } one ? new[] { one } : Array.Empty<Guid>())
            .Distinct().ToList();

    private async Task<List<(SocialAccount Account, List<ManagerUser> Authors)>> ResolveTargets(
        CurrentUser user, PostWriteDto dto, CancellationToken ct)
    {
        var ids = RequestedAccountIds(dto);
        if (ids.Count == 0)
            throw new DomainValidationException(
                "Post można utworzyć tylko w ramach konta społecznościowego - podaj targetAccountId albo targetAccountIds.");

        var result = new List<(SocialAccount, List<ManagerUser>)>();
        foreach (var id in ids) result.Add(await ResolveAccountAndAuthors(user, id, ct));
        return result;
    }

    /// <summary>
    /// Reguła tworzenia postów: tylko z poziomu konta społecznościowego. CompanyUser może tworzyć na każdym koncie
    /// swojej firmy (bez względu na przypisanych managerów), ManagerUser - tylko na koncie, do którego jest przypisany.
    /// Autorami są od razu wszyscy managerowie przypisani do konta (współautorzy).
    /// </summary>
    private async Task<(SocialAccount Account, List<ManagerUser> Authors)> ResolveAccountAndAuthors(
        CurrentUser user, Guid accountId, CancellationToken ct)
    {
        var account = await db.SocialAccounts.Include(a => a.ManagerUsers)
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

        if (user.IsCompany)
        {
            // Konto innej firmy zachowuje się jak nieistniejące.
            if (account is null || account.CompanyId != user.Id)
                throw new NotFoundException("Nie znaleziono konta społecznościowego tej firmy.");
            if (account.ManagementStatus != ManagementStatus.Accepted)
                throw new ConflictException("Konto czeka na przyjęcie zaproszenia - posty można tworzyć dopiero po jego przyjęciu.");
        }
        else if (account is null || account.ManagerUsers.All(m => m.Id != user.Id))
        {
            throw new ForbiddenException("Posty może tworzyć tylko manager przypisany do wybranego konta społecznościowego.");
        }

        return (account, account.ManagerUsers.ToList());
    }

    /// <summary>Platformy, dla których podano blok danych (facebook, instagram, tikTok).</summary>
    private static List<SocialPlatform> BlockPlatforms(PostWriteDto dto)
    {
        var list = new List<SocialPlatform>();
        if (dto.Facebook is not null) list.Add(SocialPlatform.Facebook);
        if (dto.Instagram is not null) list.Add(SocialPlatform.Instagram);
        if (dto.TikTok is not null) list.Add(SocialPlatform.TikTok);
        return list;
    }

    /// <summary>
    /// Jedno konto: tak jak dotąd - podaj dokładnie jeden blok (post platformy) albo żaden (post ogólny); niezgodność z kontem
    /// zgłasza walidacja posta. Wiele kont: blok dotyczy kont swojej platformy, a każdy blok musi pasować do któregoś konta.
    /// </summary>
    private static void ValidateBlocks(PostWriteDto dto, List<(SocialAccount Account, List<ManagerUser> Authors)> targets)
    {
        var blocks = BlockPlatforms(dto);
        if (targets.Count == 1)
        {
            if (blocks.Count > 1)
                throw new DomainValidationException("Podaj dane tylko jednej platformy: facebook, instagram albo tikTok.");
            return;
        }
        foreach (var block in blocks.Where(b => targets.All(t => t.Account.Platform != b)))
            throw new DomainValidationException($"Dane platformy {block} nie pasują do żadnego z wybranych kont.");
    }

    /// <summary>Rodzaj wariantu (null = post ogólny) dla danego konta.</summary>
    private static SocialPlatform? PlatformFor(PostWriteDto dto, SocialAccount account, bool single)
    {
        var blocks = BlockPlatforms(dto);
        if (single) return blocks.Count == 0 ? null : blocks[0];
        return blocks.Contains(account.Platform) ? account.Platform : null;
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
