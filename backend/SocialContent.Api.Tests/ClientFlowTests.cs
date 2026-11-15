using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

/// <summary>Przepływ v6: klient tworzy konto i zaprasza firmę, firma przyjmuje, tryb akceptacji postów, edycja opublikowanych.</summary>
public class ClientFlowTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private SocialAccountService Accounts(AppDbContext db) => new(db, _db.Mapper);
    private ClientUserService Clients(AppDbContext db) => new(db, _db.Mapper);

    private PostService Posts(AppDbContext db)
    {
        var locks = new PostEditLockService(db, _db.Mapper);
        var notifications = new NotificationService(db, _db.Mapper);
        var publisher = new PostPublisher(db, notifications);
        return new PostService(db, _db.Mapper, new MediaStorage(Path.Combine(Path.GetTempPath(), "sc-tests-" + Guid.NewGuid())),
            locks, publisher, notifications);
    }

    private static SocialAccountWriteDto NewAccount(string companyEmail, string accountId = "klient-fb") => new()
    {
        AccountId = accountId,
        Platform = SocialPlatform.Facebook,
        ExternalPlatformId = "ext-1",
        AccountName = "Profil klienta",
        AccessToken = "token",
        TokenExpiresAt = DateTime.UtcNow.AddDays(30),
        CompanyEmail = companyEmail
    };

    private static PostWriteDto NewPost(Guid account, bool publishNow = false) => new()
    {
        Title = "Post",
        Description = "Treść",
        Visibility = FacebookPostType.Text,
        TargetAccountId = account,
        PublishNow = publishNow
    };

    private static CurrentUser AsClient(ClientUser c) => new(c.Id, UserRole.ClientUser);
    private static CurrentUser AsCompany(CompanyUser c) => new(c.CompanyId, UserRole.CompanyUser);
    private static CurrentUser AsManager(ManagerUser m) => new(m.Id, UserRole.ManagerUser);

    [Fact]
    public async Task ClientCreatesAccount_InvitesCompanyByEmail_CompanySeesItWithClientName()
    {
        var company = await _db.AddCompanyAsync(email: "firma@example.com");
        var client = await _db.AddClientAsync();

        SocialAccountDto created;
        await using (var db = _db.NewContext())
            created = await Accounts(db).CreateAsync(client.Id, NewAccount("FIRMA@example.com"), CancellationToken.None);

        Assert.Equal(ManagementStatus.Invited, created.ManagementStatus);
        Assert.Equal(company.CompanyId, created.CompanyId);
        Assert.Equal(client.Id, created.OwnerClientId);

        await using var read = _db.NewContext();
        var forCompany = Assert.Single(await Accounts(read).GetAllAsync(AsCompany(company), null, null, CancellationToken.None));
        Assert.Equal("Karol Klient", forCompany.OwnerName);
        Assert.Empty(await Accounts(read).GetAllAsync(new CurrentUser(Guid.NewGuid(), UserRole.ClientUser), null, null, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAccount_UnknownCompany_NotFound_DuplicateForSameClient_Conflict()
    {
        await _db.AddCompanyAsync();
        var client = await _db.AddClientAsync();

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Accounts(db).CreateAsync(client.Id, NewAccount("brak@example.com"), CancellationToken.None));

        await Accounts(db).CreateAsync(client.Id, NewAccount("firma@example.com"), CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() =>
            Accounts(db).CreateAsync(client.Id, NewAccount("firma@example.com"), CancellationToken.None));
    }

    [Fact]
    public async Task Accept_CreatesCompanyClientRelation_DeclineHidesAccount_ReinviteWorks()
    {
        var company = await _db.AddCompanyAsync();
        var client = await _db.AddClientAsync();
        var invited = await _db.AddInvitedAccountAsync(client.Id, company.CompanyId);
        var declined = await _db.AddInvitedAccountAsync(client.Id, company.CompanyId, "inv-2");

        await using (var db = _db.NewContext())
        {
            var accepted = await Accounts(db).AcceptInvitationAsync(company.CompanyId, invited, CancellationToken.None);
            Assert.Equal(ManagementStatus.Accepted, accepted.ManagementStatus);
            await Accounts(db).DeclineInvitationAsync(company.CompanyId, declined, CancellationToken.None);
        }

        await using var read = _db.NewContext();
        Assert.True(await read.CompanyClients.AnyAsync(l => l.CompanyId == company.CompanyId && l.ClientId == client.Id));
        var forCompany = await Accounts(read).GetAllAsync(AsCompany(company), null, null, CancellationToken.None);
        Assert.Equal(invited, Assert.Single(forCompany).Id); // odrzucone znika z widoku firmy

        var forClient = await Accounts(read).GetAllAsync(AsClient(client), null, null, CancellationToken.None);
        Assert.Equal(ManagementStatus.Declined, forClient.Single(a => a.Id == declined).ManagementStatus);

        var again = await Accounts(read).InviteAsync(client.Id, declined,
            new InviteCompanyDto { CompanyEmail = company.Email }, CancellationToken.None);
        Assert.Equal(ManagementStatus.Invited, again.ManagementStatus);

        // Przyjętego konta nie da się przekazać innej firmie.
        await Assert.ThrowsAsync<ConflictException>(() => Accounts(read).InviteAsync(client.Id, invited,
            new InviteCompanyDto { CompanyEmail = company.Email }, CancellationToken.None));
    }

    [Fact]
    public async Task OnlyOwnerClient_ModifiesAccount_AndOnlyInvitedCompanyDecides()
    {
        var company = await _db.AddCompanyAsync();
        var other = await _db.AddCompanyAsync("Inna", "inna@example.com");
        var client = await _db.AddClientAsync();
        var stranger = await _db.AddClientAsync("obcy@example.com");
        var account = await _db.AddInvitedAccountAsync(client.Id, company.CompanyId);
        var manager = await _db.AddManagerAsync(company.CompanyId);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() => Accounts(db).DeleteAsync(stranger.Id, account, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Accounts(db).AcceptInvitationAsync(other.CompanyId, account, CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() => Accounts(db).AssignManagersAsync(company.CompanyId, account,
            new AssignManagersDto { ManagerUserIds = { manager.Id } }, CancellationToken.None));
    }

    [Fact]
    public async Task Company_CannotCreatePostsOnNotYetAcceptedAccount()
    {
        var company = await _db.AddCompanyAsync();
        var client = await _db.AddClientAsync();
        var account = await _db.AddInvitedAccountAsync(client.Id, company.CompanyId);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => Posts(db).CreateAsync(AsCompany(company), NewPost(account), CancellationToken.None));
    }

    [Fact]
    public async Task ClientChoosesApprovalMode_PerCompany_AndOnlyForOwnRelation()
    {
        var company = await _db.AddCompanyAsync();
        var client = await _db.AddClientAsync();
        await _db.AddAccountForClientAsync(client.Id, company.CompanyId, "a", SocialPlatform.Facebook);

        await using var db = _db.NewContext();
        var dto = await Clients(db).SetApprovalModeAsync(client.Id, company.CompanyId, PostApprovalMode.Client, CancellationToken.None);
        Assert.Equal(PostApprovalMode.Client, dto.ApprovalMode);
        Assert.Equal(PostApprovalMode.Client, Assert.Single(await Clients(db).GetCompaniesAsync(client.Id, CancellationToken.None)).ApprovalMode);

        var stranger = await _db.AddClientAsync("obcy@example.com");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Clients(db).SetApprovalModeAsync(stranger.Id, company.CompanyId, PostApprovalMode.Client, CancellationToken.None));
    }

    [Fact]
    public async Task ClientApprovalMode_PostWaitsForClient_ThenApproveRejectFlow()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var client = await _db.AddClientAsync();
        var account = await _db.AddAccountForClientAsync(client.Id, company.CompanyId, "a", SocialPlatform.Facebook, manager.Id);
        await using (var db = _db.NewContext())
            await Clients(db).SetApprovalModeAsync(client.Id, company.CompanyId, PostApprovalMode.Client, CancellationToken.None);

        Guid first, second;
        await using (var db = _db.NewContext())
        {
            var created = await Posts(db).CreateAsync(AsManager(manager), NewPost(account, publishNow: true), CancellationToken.None);
            first = created.Id;
            Assert.Equal(PostStatus.AwaitingApproval, created.Status);
            second = (await Posts(db).CreateAsync(AsManager(manager), NewPost(account, publishNow: true), CancellationToken.None)).Id;
        }

        await using (var db = _db.NewContext())
        {
            var seen = await Posts(db).GetAsync(AsClient(client), first, CancellationToken.None);
            Assert.True(seen.CanApprove);
            Assert.False(seen.CanEdit);
            Assert.False(seen.CanDelete);
            Assert.Contains(await db.Notifications.ToListAsync(), n => n.RecipientId == client.Id && n.RecipientRole == UserRole.ClientUser);

            Assert.Equal(PostStatus.Published, (await Posts(db).ApproveAsync(AsClient(client), first, CancellationToken.None)).Status);
            Assert.Equal(PostStatus.Rejected, (await Posts(db).RejectAsync(AsClient(client), second, CancellationToken.None)).Status);
            await Assert.ThrowsAsync<ConflictException>(() => Posts(db).ApproveAsync(AsClient(client), first, CancellationToken.None));
        }

        await using var check = _db.NewContext();
        var recipients = await check.Notifications.Select(n => n.RecipientId).ToListAsync();
        Assert.Contains(company.CompanyId, recipients); // decyzja klienta trafia do firmy
        Assert.Contains(manager.Id, recipients);
    }

    [Fact]
    public async Task ManagerApprovalMode_PublishesWithoutClientConsent()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId, "a", SocialPlatform.Facebook, manager.Id);

        await using var db = _db.NewContext();
        var created = await Posts(db).CreateAsync(AsManager(manager), NewPost(account, publishNow: true), CancellationToken.None);
        Assert.Equal(PostStatus.Published, created.Status);
    }

    [Fact]
    public async Task Client_SeesOnlyOwnAccountsPosts_AndCannotModifyThem()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var client = await _db.AddClientAsync();
        var stranger = await _db.AddClientAsync("obcy@example.com");
        var own = await _db.AddAccountForClientAsync(client.Id, company.CompanyId, "a", SocialPlatform.Facebook, manager.Id);
        var foreign = await _db.AddAccountForClientAsync(stranger.Id, company.CompanyId, "b", SocialPlatform.Facebook, manager.Id);

        Guid ownPost;
        await using (var db = _db.NewContext())
        {
            ownPost = (await Posts(db).CreateAsync(AsManager(manager), NewPost(own), CancellationToken.None)).Id;
            await Posts(db).CreateAsync(AsManager(manager), NewPost(foreign), CancellationToken.None);
        }

        await using var read = _db.NewContext();
        var visible = await Posts(read).GetAllAsync(AsClient(client), null, null, null, CancellationToken.None);
        Assert.Equal(ownPost, Assert.Single(visible).Id);
        Assert.False(visible[0].CanEdit);

        await Assert.ThrowsAsync<ForbiddenException>(() => Posts(read).UpdateAsync(AsClient(client), ownPost, NewPost(own), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => Posts(read).DeleteAsync(AsClient(client), ownPost, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Posts(read).GetAsync(AsClient(stranger), ownPost, CancellationToken.None));
    }

    [Fact]
    public async Task PublishedPost_CanStillBeEdited_ButNotRepublishedOrScheduled()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var account = await _db.AddSocialAccountAsync(company.CompanyId, "a", SocialPlatform.Facebook, manager.Id);
        var asManager = AsManager(manager);

        await using var db = _db.NewContext();
        var published = await Posts(db).CreateAsync(asManager, NewPost(account, publishNow: true), CancellationToken.None);
        Assert.Equal(PostStatus.Published, published.Status);
        Assert.True(published.CanEdit);

        var edit = NewPost(account);
        edit.Title = "Poprawiony tytuł";
        var updated = await Posts(db).UpdateAsync(asManager, published.Id, edit, CancellationToken.None);
        Assert.Equal("Poprawiony tytuł", updated.Title);
        Assert.Equal(PostStatus.Published, updated.Status);

        var republish = NewPost(account, publishNow: true);
        await Assert.ThrowsAsync<DomainValidationException>(() => Posts(db).UpdateAsync(asManager, published.Id, republish, CancellationToken.None));
        var scheduled = NewPost(account);
        scheduled.ScheduledAt = DateTime.UtcNow.AddDays(1);
        await Assert.ThrowsAsync<DomainValidationException>(() => Posts(db).UpdateAsync(asManager, published.Id, scheduled, CancellationToken.None));
    }

    [Fact]
    public async Task CompanyAlias_ShowsInCompanyAndManagerViews_ButNotInClientView()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var client = await _db.AddClientAsync();
        var account = await _db.AddAccountForClientAsync(client.Id, company.CompanyId, "a", SocialPlatform.Facebook, manager.Id);

        await using var db = _db.NewContext();
        await Clients(db).SetAliasAsync(company.CompanyId, client.Id, new ClientAliasDto { Alias = "Kawiarnia" }, CancellationToken.None);

        var companyClients = await Clients(db).GetForCompanyAsync(company.CompanyId, CancellationToken.None);
        Assert.Equal("Kawiarnia", Assert.Single(companyClients).DisplayName);

        var managerClients = await Clients(db).GetForManagerAsync(manager.Id, CancellationToken.None);
        var seenByManager = Assert.Single(managerClients);
        Assert.Equal("Kawiarnia", seenByManager.DisplayName);
        Assert.Equal("", seenByManager.Email); // manager nie dostaje adresu e-mail klienta

        var forManager = Assert.Single(await Accounts(db).GetAllAsync(AsManager(manager), client.Id, null, CancellationToken.None));
        Assert.Equal("Kawiarnia", forManager.OwnerName);
        var forClient = Assert.Single(await Accounts(db).GetAllAsync(AsClient(client), null, company.CompanyId, CancellationToken.None));
        Assert.Equal("Karol Klient", forClient.OwnerName);
        Assert.Equal(account, forClient.Id);
    }

    [Fact]
    public async Task CompanyClients_IncludeOwnersOfPendingInvitations_AndUnlinkIsBlockedWhileManagingAccounts()
    {
        var company = await _db.AddCompanyAsync();
        var inviting = await _db.AddClientAsync("nowy@example.com");
        var managed = await _db.AddClientAsync("stary@example.com");
        await _db.AddInvitedAccountAsync(inviting.Id, company.CompanyId);
        await _db.AddAccountForClientAsync(managed.Id, company.CompanyId, "a", SocialPlatform.Facebook);

        await using var db = _db.NewContext();
        var list = await Clients(db).GetForCompanyAsync(company.CompanyId, CancellationToken.None);
        Assert.Equal(2, list.Count);
        Assert.Equal(1, list.Single(c => c.Id == inviting.Id).PendingInvitations);

        await Assert.ThrowsAsync<ConflictException>(() => Clients(db).UnlinkAsync(company.CompanyId, managed.Id, CancellationToken.None));
    }
}
