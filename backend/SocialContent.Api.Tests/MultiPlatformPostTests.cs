using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

/// <summary>v7: post na wiele platform (grupa wariantów), tagi, filtry listy i edycja zaplanowanych postów.</summary>
public class MultiPlatformPostTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private PostService Posts(AppDbContext db)
    {
        var locks = new PostEditLockService(db, _db.Mapper);
        var notifications = new NotificationService(db, _db.Mapper);
        var publisher = new PostPublisher(db, notifications);
        return new PostService(db, _db.Mapper, new MediaStorage(Path.Combine(Path.GetTempPath(), "sc-tests-" + Guid.NewGuid())),
            locks, publisher, notifications);
    }

    private static CurrentUser AsCompany(CompanyUser c) => new(c.CompanyId, UserRole.CompanyUser);
    private static CurrentUser AsManager(ManagerUser m) => new(m.Id, UserRole.ManagerUser);
    private static CurrentUser AsClient(ClientUser c) => new(c.Id, UserRole.ClientUser);

    private static PostWriteDto NewPost(string title, DateTime? scheduledAt, params Guid[] accounts) => new()
    {
        Title = title,
        Description = "Treść",
        Visibility = FacebookPostType.Text,
        TargetAccountIds = accounts.ToList(),
        ScheduledAt = scheduledAt,
        Tags = new() { "Kawa", "#promo" }
    };

    private sealed record Setup(CompanyUser Company, ManagerUser Manager, ClientUser Client, Guid Facebook, Guid Instagram, Guid TikTok);

    private async Task<Setup> SetupAsync()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var client = await _db.AddClientAsync();
        var fb = await _db.AddAccountForClientAsync(client.Id, company.CompanyId, "fb", SocialPlatform.Facebook, manager.Id);
        var ig = await _db.AddAccountForClientAsync(client.Id, company.CompanyId, "ig", SocialPlatform.Instagram, manager.Id);
        var tt = await _db.AddAccountForClientAsync(client.Id, company.CompanyId, "tt", SocialPlatform.TikTok, manager.Id);
        return new Setup(company, manager, client, fb, ig, tt);
    }

    [Fact]
    public async Task Create_OnTwoAccounts_IsOneLogicalPostWithTwoVariants()
    {
        var s = await SetupAsync();
        PostDto created;
        await using (var db = _db.NewContext())
            created = await Posts(db).CreateAsync(AsCompany(s.Company), NewPost("Wspólny", DateTime.UtcNow.AddDays(2), s.Facebook, s.Instagram), CancellationToken.None);

        Assert.Equal(2, created.Targets!.Count);
        Assert.Equal(new[] { "kawa", "promo" }, created.Tags!.Select(t => t.ToLowerInvariant()).OrderBy(t => t));

        await using var read = _db.NewContext();
        var rows = await read.Posts.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Single(rows.Select(p => p.GroupId).Distinct());
        Assert.Equal(created.Id, rows[0].GroupId);

        var list = await Posts(read).GetAllAsync(AsCompany(s.Company), new PostFilterDto(), CancellationToken.None);
        var one = Assert.Single(list);
        Assert.Equal(new[] { SocialPlatform.Facebook, SocialPlatform.Instagram },
            one.Targets!.Select(t => t.Platform!.Value).OrderBy(p => p));
        Assert.Equal(PostStatus.Scheduled, one.Status);
        Assert.Equal(created.Id, (await Posts(read).GetAsync(AsCompany(s.Company), created.Id, CancellationToken.None)).Id);
    }

    [Fact]
    public async Task Update_AddsAndRemovesPlatforms_WithinTheSameGroup()
    {
        var s = await SetupAsync();
        var when = DateTime.UtcNow.AddDays(2);
        PostDto created;
        await using (var db = _db.NewContext())
            created = await Posts(db).CreateAsync(AsCompany(s.Company), NewPost("Wspólny", when, s.Facebook, s.Instagram), CancellationToken.None);

        await using (var db = _db.NewContext())
        {
            var dto = NewPost("Wspólny - zmieniony", when, s.Instagram, s.TikTok);
            var updated = await Posts(db).UpdateAsync(AsCompany(s.Company), created.Id, dto, CancellationToken.None);
            Assert.Equal(created.Id, updated.Id);
            Assert.Equal(new[] { SocialPlatform.Instagram, SocialPlatform.TikTok },
                updated.Targets!.Select(t => t.Platform!.Value).OrderBy(p => p));
        }

        await using var read = _db.NewContext();
        var rows = await read.Posts.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, p => Assert.Equal(created.Id, p.GroupId));
        Assert.All(rows, p => Assert.Equal("Wspólny - zmieniony", p.Title.Value));
    }

    [Fact]
    public async Task EditingScheduledPost_InClientApprovalMode_DoesNotReturnToApproval()
    {
        var s = await SetupAsync();
        await using (var db = _db.NewContext())
            await new ClientUserService(db, _db.Mapper)
                .SetApprovalModeAsync(s.Client.Id, s.Company.CompanyId, PostApprovalMode.Client, CancellationToken.None);

        var when = DateTime.UtcNow.AddDays(2);
        PostDto created;
        await using (var db = _db.NewContext())
        {
            created = await Posts(db).CreateAsync(AsCompany(s.Company), NewPost("Do akceptacji", when, s.Facebook), CancellationToken.None);
            Assert.Equal(PostStatus.AwaitingApproval, created.Status);
            await Posts(db).ApproveAsync(AsClient(s.Client), created.Id, CancellationToken.None);
        }

        await using (var db = _db.NewContext())
        {
            var edited = await Posts(db).UpdateAsync(AsCompany(s.Company), created.Id, NewPost("Po edycji", when, s.Facebook), CancellationToken.None);
            Assert.Equal(PostStatus.Scheduled, edited.Status); // edycja zaplanowanego posta nie wymaga ponownej akceptacji
            Assert.Equal("Po edycji", edited.Title);
        }
    }

    [Fact]
    public async Task Filters_CombineWithAnd_AndRespectRoleVisibility()
    {
        var s = await SetupAsync();
        var otherManager = await _db.AddManagerAsync(s.Company.CompanyId, "Piotr", "Inny");
        var onlyOther = await _db.AddAccountForClientAsync(s.Client.Id, s.Company.CompanyId, "other", SocialPlatform.TikTok, otherManager.Id);

        await using (var db = _db.NewContext())
        {
            var posts = Posts(db);
            await posts.CreateAsync(AsCompany(s.Company), NewPost("Szkic", null, s.Facebook), CancellationToken.None);
            await posts.CreateAsync(AsCompany(s.Company), NewPost("Zaplanowany FB+IG", DateTime.UtcNow.AddDays(2), s.Facebook, s.Instagram), CancellationToken.None);
            var noTags = NewPost("Zaplanowany TT", DateTime.UtcNow.AddDays(5), s.TikTok);
            noTags.Tags = new() { "inny" };
            await posts.CreateAsync(AsCompany(s.Company), noTags, CancellationToken.None);
            await posts.CreateAsync(AsCompany(s.Company), NewPost("Cudzy", DateTime.UtcNow.AddDays(3), onlyOther), CancellationToken.None);
        }

        await using var read = _db.NewContext();
        var service = Posts(read);
        var company = AsCompany(s.Company);

        Assert.Equal(4, (await service.GetAllAsync(company, new PostFilterDto(), CancellationToken.None)).Count);

        var drafts = await service.GetAllAsync(company, new PostFilterDto { Status = { PostStatus.Draft } }, CancellationToken.None);
        Assert.Equal("Szkic", Assert.Single(drafts).Title);

        var either = await service.GetAllAsync(company, new PostFilterDto { Status = { PostStatus.Draft, PostStatus.Scheduled } }, CancellationToken.None);
        Assert.Equal(4, either.Count);

        var instagram = await service.GetAllAsync(company, new PostFilterDto { Platform = { SocialPlatform.Instagram } }, CancellationToken.None);
        Assert.Equal("Zaplanowany FB+IG", Assert.Single(instagram).Title);

        var tagged = await service.GetAllAsync(company, new PostFilterDto { Tag = { "KAWA" } }, CancellationToken.None);
        Assert.Equal(3, tagged.Count); // tagi bez względu na wielkość liter; wpis "Zaplanowany TT" ma tylko tag "inny"

        // koniunkcja: zaplanowane + tag + platforma Facebook
        var combined = await service.GetAllAsync(company, new PostFilterDto
        {
            Status = { PostStatus.Scheduled }, Tag = { "kawa" }, Platform = { SocialPlatform.Facebook }
        }, CancellationToken.None);
        Assert.Equal("Zaplanowany FB+IG", Assert.Single(combined).Title);

        var range = await service.GetAllAsync(company, new PostFilterDto
        {
            From = DateTime.UtcNow.AddDays(1), To = DateTime.UtcNow.AddDays(4)
        }, CancellationToken.None);
        Assert.Equal(new[] { "Cudzy", "Zaplanowany FB+IG" }, range.Select(p => p.Title).OrderBy(t => t));

        // widoczność roli: manager widzi tylko posty swoich kont, mimo że filtry są puste
        var asManager = await service.GetAllAsync(AsManager(s.Manager), new PostFilterDto(), CancellationToken.None);
        Assert.DoesNotContain(asManager, p => p.Title == "Cudzy");
        Assert.Equal(3, asManager.Count);

        var byPerson = await service.GetAllAsync(company, new PostFilterDto { ManagerId = otherManager.Id }, CancellationToken.None);
        Assert.Equal("Cudzy", Assert.Single(byPerson).Title);
    }

    [Fact]
    public async Task Delete_RemovesAllVariants_ClientCannotModify_AndCounterpartNamesDependOnRole()
    {
        var s = await SetupAsync();
        await using (var db = _db.NewContext())
            await new ClientUserService(db, _db.Mapper)
                .SetAliasAsync(s.Company.CompanyId, s.Client.Id, new ClientAliasDto { Alias = "Kawiarnia" }, CancellationToken.None);

        PostDto created;
        await using (var db = _db.NewContext())
            created = await Posts(db).CreateAsync(AsCompany(s.Company), NewPost("Wspólny", DateTime.UtcNow.AddDays(2), s.Facebook, s.Instagram), CancellationToken.None);

        await using (var db = _db.NewContext())
        {
            var forCompany = await Posts(db).GetAsync(AsCompany(s.Company), created.Id, CancellationToken.None);
            Assert.Equal("Kawiarnia", forCompany.CounterpartName);
            var forClient = await Posts(db).GetAsync(AsClient(s.Client), created.Id, CancellationToken.None);
            Assert.Equal(s.Company.Name, forClient.CounterpartName);
            Assert.False(forClient.CanEdit);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Posts(db).DeleteAsync(AsClient(s.Client), created.Id, CancellationToken.None));
        }

        await using (var db = _db.NewContext())
            await Posts(db).DeleteAsync(AsCompany(s.Company), created.Id, CancellationToken.None);

        await using var read = _db.NewContext();
        Assert.Empty(await read.Posts.ToListAsync());
    }
}
