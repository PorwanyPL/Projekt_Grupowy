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
        var managers = await LoadManagers(dto.ManagerUserIds, ct);
        var post = Post.Create(
            Title.Create(dto.Title),
            Description.Create(dto.Description),
            dto.Visibility,
            BuildPictures(dto),
            BuildVideos(dto),
            managers);

        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        return post.ToDto();
    }

    public async Task<PostDto> UpdateAsync(Guid id, PostWriteDto dto, CancellationToken ct)
    {
        var post = await Find(id, ct);
        var managers = await LoadManagers(dto.ManagerUserIds, ct);

        post.Update(
            Title.Create(dto.Title),
            Description.Create(dto.Description),
            dto.Visibility,
            BuildPictures(dto),
            BuildVideos(dto),
            managers);

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

    private async Task<List<ManagerUser>> LoadManagers(List<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return new List<ManagerUser>();

        var found = await db.ManagerUsers.Where(m => distinct.Contains(m.Id)).ToListAsync(ct);
        var missing = distinct.Except(found.Select(m => m.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Nie znaleziono managerów: {string.Join(", ", missing)}.");
        return found;
    }

    private static List<Picture> BuildPictures(PostWriteDto dto) =>
        dto.Pictures.Select(p => Picture.Create(FileName.Create(p.FileName), p.Format, p.SizeKb)).ToList();

    private static List<Video> BuildVideos(PostWriteDto dto) =>
        dto.Videos.Select(v => Video.Create(FileName.Create(v.FileName), v.Format, v.Length)).ToList();
}
