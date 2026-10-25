using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

public interface IPostService
{
    Task<List<PostDto>> GetAllAsync(FacebookPostType? type, CancellationToken ct);
    Task<PostDto> GetAsync(Guid id, CancellationToken ct);
    Task<PostDto> CreateAsync(PostWriteDto dto, CancellationToken ct);
    Task<PostDto> UpdateAsync(Guid id, PostWriteDto dto, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class PostService(AppDbContext db) : IPostService
{
    private IQueryable<Post> Query() =>
        db.Posts.Include(p => p.Files).Include(p => p.ManagerUsers);

    public async Task<List<PostDto>> GetAllAsync(FacebookPostType? type, CancellationToken ct)
    {
        var q = Query().AsNoTracking();
        if (type is not null) q = q.Where(p => p.Visibility == type);
        var items = await q.ToListAsync(ct);
        return items.OrderByDescending(p => p.CreatedAt.Date).Select(p => p.ToDto()).ToList();
    }

    public async Task<PostDto> GetAsync(Guid id, CancellationToken ct) =>
        (await Find(id, ct)).ToDto();

    public async Task<PostDto> CreateAsync(PostWriteDto dto, CancellationToken ct)
    {
        var platform = RequestedPlatform(dto);
        var managers = await ManagerLookup.LoadAsync(db, dto.ManagerUserIds, ct);
        var account = await LoadAccount(dto.TargetAccountId, ct);

        var title = Title.Create(dto.Title);
        var description = Description.Create(dto.Description);
        var pictures = BuildPictures(dto);
        var videos = BuildVideos(dto);
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
        return post.ToDto();
    }

    public async Task<PostDto> UpdateAsync(Guid id, PostWriteDto dto, CancellationToken ct)
    {
        var post = await Find(id, ct);

        var platform = RequestedPlatform(dto);
        if (platform != post.Platform)
            throw new DomainValidationException(post.Platform is null
                ? "Post ogólny nie może zawierać danych platformy; rodzaju posta nie można zmienić po utworzeniu."
                : $"Ten post jest postem platformy {post.Platform}: podaj blok danych tej platformy (i żadnej innej). Rodzaju posta nie można zmienić po utworzeniu.");

        var managers = await ManagerLookup.LoadAsync(db, dto.ManagerUserIds, ct);
        var account = await LoadAccount(dto.TargetAccountId, ct);

        var title = Title.Create(dto.Title);
        var description = Description.Create(dto.Description);
        var pictures = BuildPictures(dto);
        var videos = BuildVideos(dto);
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
        return post.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var post = await Find(id, ct);
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Post> Find(Guid id, CancellationToken ct) =>
        await Query().FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException($"Nie znaleziono posta o id {id}.");

    private async Task<SocialAccount?> LoadAccount(Guid? id, CancellationToken ct)
    {
        if (id is null) return null;
        return await db.SocialAccounts.FirstOrDefaultAsync(a => a.Id == id.Value, ct)
               ?? throw new NotFoundException($"Nie znaleziono konta społecznościowego o id {id}.");
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

    private static ScheduledTime? BuildSchedule(PostWriteDto dto) =>
        dto.ScheduledAt is null ? null : ScheduledTime.Create(dto.ScheduledAt.Value);

    private static WebUrl? ParseUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUrl.Create(value);

    private static ExternalId? ParseExternalId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ExternalId.Create(value);

    private static List<Picture> BuildPictures(PostWriteDto dto) =>
        dto.Pictures.Select(p => Picture.Create(FileName.Create(p.FileName), p.Format, p.SizeKb)).ToList();

    private static List<Video> BuildVideos(PostWriteDto dto) =>
        dto.Videos.Select(v => Video.Create(FileName.Create(v.FileName), v.Format, v.Length)).ToList();
}
