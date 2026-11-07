using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

public class SocialAccountAndPostRulesTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private SocialAccountService Accounts(AppDbContext db) => new(db, _db.Mapper);
    private PostService Posts(AppDbContext db)
    {
        var locks = new PostEditLockService(db, _db.Mapper);
        var publisher = new PostPublisher(db, new NotificationService(db, _db.Mapper));
        return new PostService(db, _db.Mapper, new MediaStorage(Path.Combine(Path.GetTempPath(), "sc-tests-" + Guid.NewGuid())),
            locks, publisher);
    }

    private static PostWriteDto NewPost(Guid? accountId) => new()
    {
        Title = "Nowy post",
        Description = "Treść posta",
        Visibility = FacebookPostType.Text,
        TargetAccountId = accountId
    };

    [Fact]
    public async Task AssignManagers_ReplacesList_AndAllowsManyAccountsPerManager()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId, "Anna", "Aaa");
        var bartek = await _db.AddManagerAsync(company.CompanyId, "Bartek", "Bbb");
        var first = await _db.AddSocialAccountAsync(company.CompanyId, "acc-1", managerIds: anna.Id);
        var second = await _db.AddSocialAccountAsync(company.CompanyId, "acc-2", managerIds: anna.Id);

        await using (var db = _db.NewContext())
            await Accounts(db).AssignManagersAsync(company.CompanyId, first,
                new AssignManagersDto { ManagerUserIds = { bartek.Id } }, CancellationToken.None);

        await using var check = _db.NewContext();
        var accounts = await check.SocialAccounts.Include(a => a.ManagerUsers).ToListAsync();
        Assert.Equal(new[] { bartek.Id }, accounts.Single(a => a.Id == first).ManagerUsers.Select(m => m.Id));
        Assert.Equal(new[] { anna.Id }, accounts.Single(a => a.Id == second).ManagerUsers.Select(m => m.Id));
    }

    [Fact]
    public async Task AssignManagers_ManagerFromAnotherCompany_IsRejected()
    {
        var company = await _db.AddCompanyAsync();
        var other = await _db.AddCompanyAsync("Inna", "inna@example.com");
        var foreign = await _db.AddManagerAsync(other.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => Accounts(db).AssignManagersAsync(company.CompanyId, account,
            new AssignManagersDto { ManagerUserIds = { foreign.Id } }, CancellationToken.None));
    }

    [Fact]
    public async Task GetAll_CompanySeesAllOwnAccounts_ManagerOnlyAssignedOnes()
    {
        var company = await _db.AddCompanyAsync();
        var other = await _db.AddCompanyAsync("Inna", "inna@example.com");
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var assigned = await _db.AddSocialAccountAsync(company.CompanyId, "acc-1", managerIds: manager.Id);
        await _db.AddSocialAccountAsync(company.CompanyId, "acc-2");
        await _db.AddSocialAccountAsync(other.CompanyId, "acc-3");

        await using var db = _db.NewContext();
        var forCompany = await Accounts(db).GetAllAsync(new CurrentUser(company.CompanyId, UserRole.CompanyUser), CancellationToken.None);
        var forManager = await Accounts(db).GetAllAsync(new CurrentUser(manager.Id, UserRole.ManagerUser), CancellationToken.None);

        Assert.Equal(2, forCompany.Count);
        Assert.Equal(assigned, Assert.Single(forManager).Id);
    }

    [Fact]
    public async Task CreatePost_AssignedManager_Succeeds_AndAllAssignedManagersAreAuthors()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var coManager = await _db.AddManagerAsync(company.CompanyId, "Bartek", "Bbb");
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: new[] { manager.Id, coManager.Id });

        await using (var db = _db.NewContext())
        {
            var created = await Posts(db).CreateAsync(new CurrentUser(manager.Id, UserRole.ManagerUser), NewPost(account), CancellationToken.None);
            Assert.Equal("Nowy post", created.Title);
            Assert.True(created.CanEdit);
        }

        await using var check = _db.NewContext();
        var post = await check.Posts.Include(p => p.ManagerUsers).SingleAsync();
        Assert.Equal(account, post.TargetAccountId);
        Assert.Equal(new[] { manager.Id, coManager.Id }.Order(), post.ManagerUsers.Select(m => m.Id).Order());
    }

    [Fact]
    public async Task CreatePost_Company_CanPostOnAnyOwnAccount_ButNotOnForeignOne()
    {
        var company = await _db.AddCompanyAsync();
        var other = await _db.AddCompanyAsync("Inna", "inna@example.com");
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var own = await _db.AddSocialAccountAsync(company.CompanyId, "acc-1", managerIds: manager.Id);
        var unassigned = await _db.AddSocialAccountAsync(company.CompanyId, "acc-2");
        var foreign = await _db.AddSocialAccountAsync(other.CompanyId, "acc-3");
        var asCompany = new CurrentUser(company.CompanyId, UserRole.CompanyUser);

        await using var db = _db.NewContext();
        await Posts(db).CreateAsync(asCompany, NewPost(own), CancellationToken.None);
        await Posts(db).CreateAsync(asCompany, NewPost(unassigned), CancellationToken.None);
        await Assert.ThrowsAsync<NotFoundException>(() => Posts(db).CreateAsync(asCompany, NewPost(foreign), CancellationToken.None));

        await using var check = _db.NewContext();
        var withAuthors = await check.Posts.Include(p => p.ManagerUsers).ToListAsync();
        Assert.Equal(manager.Id, Assert.Single(withAuthors.Single(p => p.TargetAccountId == own).ManagerUsers).Id);
        Assert.Empty(withAuthors.Single(p => p.TargetAccountId == unassigned).ManagerUsers);
    }

    [Fact]
    public async Task Manager_AfterLosingAccess_CanOnlyViewOwnPosts()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: anna.Id);
        var postId = await _db.AddPostWithManagerAsync(anna.Id, account);
        var asAnna = new CurrentUser(anna.Id, UserRole.ManagerUser);

        await using (var db = _db.NewContext())
            await Accounts(db).AssignManagersAsync(company.CompanyId, account, new AssignManagersDto(), CancellationToken.None);

        await using var db2 = _db.NewContext();
        var visible = await Posts(db2).GetAsync(asAnna, postId, CancellationToken.None);
        Assert.False(visible.CanEdit);
        Assert.False(visible.CanDelete);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Posts(db2).UpdateAsync(asAnna, postId, NewPost(account), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => Posts(db2).DeleteAsync(asAnna, postId, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Posts(db2).CreateAsync(asAnna, NewPost(account), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePost_WhenSomeoneElseEdits_ReturnsConflictWithHolderName()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId, "Anna", "Aaa");
        var bartek = await _db.AddManagerAsync(company.CompanyId, "Bartek", "Bbb");
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: new[] { anna.Id, bartek.Id });
        var postId = await _db.AddPostWithManagerAsync(anna.Id, account);
        var asAnna = new CurrentUser(anna.Id, UserRole.ManagerUser);
        var asBartek = new CurrentUser(bartek.Id, UserRole.ManagerUser);

        await using (var db = _db.NewContext())
            await Posts(db).AcquireLockAsync(asAnna, postId, CancellationToken.None);

        await using var db2 = _db.NewContext();
        // Bartek nie jest autorem tego posta (dodaje go dopiero zapis), więc musi go widzieć jako firma.
        var asCompany = new CurrentUser(company.CompanyId, UserRole.CompanyUser);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Posts(db2).UpdateAsync(asCompany, postId, NewPost(account), CancellationToken.None));
        Assert.Contains("Anna", ex.Message);

        // Po zwolnieniu blokady przez Annę zapis firmy przechodzi.
        await using var db3 = _db.NewContext();
        await Posts(db3).ReleaseLockAsync(asAnna, postId, CancellationToken.None);
        var updated = await Posts(db3).UpdateAsync(asCompany, postId, NewPost(account), CancellationToken.None);
        Assert.Equal("Nowy post", updated.Title);
        _ = asBartek;
    }

    [Fact]
    public async Task ExpiredLock_DoesNotBlockAnotherUser()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: anna.Id);
        var postId = await _db.AddPostWithManagerAsync(anna.Id, account);
        var asCompany = new CurrentUser(company.CompanyId, UserRole.CompanyUser);

        await using (var db = _db.NewContext())
        {
            var lockRow = PostEditLock.Create(postId, anna.Id, UserRole.ManagerUser, "Anna Nowak",
                DateTime.UtcNow.AddMinutes(-10), TimeSpan.FromMinutes(2));
            db.PostEditLocks.Add(lockRow);
            await db.SaveChangesAsync();
        }

        await using var db2 = _db.NewContext();
        var lockDto = await Posts(db2).AcquireLockAsync(asCompany, postId, CancellationToken.None);
        Assert.True(lockDto.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task PublishNow_OnAccountWithBrokenConnection_FailsAndNotifiesCompanyAndAuthors()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: anna.Id);
        await using (var db = _db.NewContext())
        {
            var acc = await db.SocialAccounts.FirstAsync();
            db.Entry(acc).Property(a => a.IsActive).CurrentValue = false; // konto nieaktywne = połączenie niepoprawne
            await db.SaveChangesAsync();
        }

        var dto = NewPost(account);
        dto.PublishNow = true;
        await using (var db = _db.NewContext())
        {
            var created = await Posts(db).CreateAsync(new CurrentUser(anna.Id, UserRole.ManagerUser), dto, CancellationToken.None);
            Assert.Equal(PostStatus.Failed, created.Status);
        }

        await using var check = _db.NewContext();
        var recipients = await check.Notifications.Select(n => n.RecipientId).ToListAsync();
        Assert.Contains(company.CompanyId, recipients);
        Assert.Contains(anna.Id, recipients);
    }

    [Fact]
    public async Task PublishNow_OnHealthyAccount_PublishesAndPublishNowWithScheduleIsRejected()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: anna.Id);
        var asAnna = new CurrentUser(anna.Id, UserRole.ManagerUser);

        var ok = NewPost(account);
        ok.PublishNow = true;
        await using var db = _db.NewContext();
        Assert.Equal(PostStatus.Published, (await Posts(db).CreateAsync(asAnna, ok, CancellationToken.None)).Status);

        var both = NewPost(account);
        both.PublishNow = true;
        both.ScheduledAt = DateTime.UtcNow.AddHours(1);
        await Assert.ThrowsAsync<DomainValidationException>(() => Posts(db).CreateAsync(asAnna, both, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePost_ScheduledInThePast_IsRejected()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: anna.Id);
        var dto = NewPost(account);
        dto.ScheduledAt = DateTime.UtcNow.AddHours(-1);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Posts(db).CreateAsync(new CurrentUser(anna.Id, UserRole.ManagerUser), dto, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePost_ManagerNotAssignedToAccount_IsForbidden()
    {
        var company = await _db.AddCompanyAsync();
        var assigned = await _db.AddManagerAsync(company.CompanyId, "Anna", "Aaa");
        var outsider = await _db.AddManagerAsync(company.CompanyId, "Bartek", "Bbb");
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: assigned.Id);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Posts(db).CreateAsync(new CurrentUser(outsider.Id, UserRole.ManagerUser), NewPost(account), CancellationToken.None));
    }

    [Fact]
    public async Task CreatePost_WithoutTargetAccount_IsRejected()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Posts(db).CreateAsync(new CurrentUser(manager.Id, UserRole.ManagerUser), NewPost(null), CancellationToken.None));
    }

    [Fact]
    public async Task GetPosts_ManagerSeesOnlyOwn_AndCannotAskForSomeoneElse_CompanyCanFilterByManager()
    {
        var company = await _db.AddCompanyAsync();
        var anna = await _db.AddManagerAsync(company.CompanyId, "Anna", "Aaa");
        var bartek = await _db.AddManagerAsync(company.CompanyId, "Bartek", "Bbb");
        var account = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: new[] { anna.Id, bartek.Id });
        var annaPost = await _db.AddPostWithManagerAsync(anna.Id, account);
        await _db.AddPostWithManagerAsync(bartek.Id, account);

        await using var db = _db.NewContext();
        var asAnna = new CurrentUser(anna.Id, UserRole.ManagerUser);
        var own = await Posts(db).GetAllAsync(asAnna, null, null, null, CancellationToken.None);
        Assert.Equal(annaPost, Assert.Single(own).Id);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Posts(db).GetAllAsync(asAnna, null, null, bartek.Id, CancellationToken.None));

        var asCompany = new CurrentUser(company.CompanyId, UserRole.CompanyUser);
        Assert.Equal(2, (await Posts(db).GetAllAsync(asCompany, null, null, null, CancellationToken.None)).Count);
        Assert.Equal(annaPost, Assert.Single(await Posts(db).GetAllAsync(asCompany, null, null, anna.Id, CancellationToken.None)).Id);
    }
}
