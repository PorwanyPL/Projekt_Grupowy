using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

public class ManagerUserServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private ManagerUserService Service(SocialContent.Api.Data.AppDbContext db) => new(db, _db.Mapper, new Pbkdf2PasswordHasher());

    [Fact]
    public async Task Delete_ManagerWithoutLinks_RemovesManagerAndCredentials()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        await _db.AddCredentialAsync(manager.Id, "anna@example.com");

        await using (var db = _db.NewContext())
            await Service(db).DeleteAsync(company.CompanyId, manager.Id, CancellationToken.None);

        await using var check = _db.NewContext();
        Assert.False(await check.ManagerUsers.AnyAsync(m => m.Id == manager.Id));
        Assert.False(await check.ManagerCredentials.AnyAsync(c => c.ManagerUserId == manager.Id));
    }

    [Fact]
    public async Task Delete_ManagerWithPosts_ThrowsConflictAndKeepsEverything()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        var postId = await _db.AddPostWithManagerAsync(manager.Id);

        await using (var db = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                Service(db).DeleteAsync(company.CompanyId, manager.Id, CancellationToken.None));
            Assert.Contains("zablokuj dostęp zamiast usuwać", ex.Message);
        }

        await using var check = _db.NewContext();
        Assert.True(await check.ManagerUsers.AnyAsync(m => m.Id == manager.Id));
        Assert.True(await check.Posts.AnyAsync(p => p.Id == postId && p.ManagerUsers.Any(m => m.Id == manager.Id)));
    }

    [Fact]
    public async Task Delete_ManagerAssignedToSocialAccount_ThrowsConflict()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        await _db.AddSocialAccountAsync(company.CompanyId, managerIds: manager.Id);

        await using var db = _db.NewContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Service(db).DeleteAsync(company.CompanyId, manager.Id, CancellationToken.None));
        Assert.Contains("zablokuj dostęp zamiast usuwać", ex.Message);
    }

    [Fact]
    public async Task Delete_UnknownManager_ThrowsNotFound()
    {
        var company = await _db.AddCompanyAsync();
        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(db).DeleteAsync(company.CompanyId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Get_ManagerOfAnotherCompany_IsTreatedAsNotFound()
    {
        var mine = await _db.AddCompanyAsync();
        var other = await _db.AddCompanyAsync("Inna firma", "inna@example.com");
        var foreign = await _db.AddManagerAsync(other.CompanyId);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(db).GetAsync(mine.CompanyId, foreign.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithEmailAndPassword_CreatesLoginAccountInCompany()
    {
        var company = await _db.AddCompanyAsync();

        await using (var db = _db.NewContext())
        {
            var dto = await Service(db).CreateAsync(company.CompanyId, new ManagerUserCreateDto
            {
                FirstName = "Jan", LastName = "Kowalski", Age = 28, Description = "Nowy manager",
                Email = "Jan@Example.com", Password = "Haslo1234"
            }, CancellationToken.None);
            Assert.True(dto.HasLoginAccount);
        }

        await using var check = _db.NewContext();
        var saved = await check.ManagerUsers.SingleAsync();
        Assert.Equal(company.CompanyId, saved.CompanyId);
        Assert.Equal("jan@example.com", (await check.ManagerCredentials.SingleAsync()).Email);
    }

    [Fact]
    public async Task Create_WithoutEmailOrPassword_IsRejected()
    {
        var company = await _db.AddCompanyAsync();
        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() => Service(db).CreateAsync(company.CompanyId,
            new ManagerUserCreateDto
            {
                FirstName = "Jan", LastName = "Kowalski", Age = 28, Description = "Nowy manager", Email = "jan@example.com"
            }, CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() => Service(db).CreateAsync(company.CompanyId,
            new ManagerUserCreateDto
            {
                FirstName = "Jan", LastName = "Kowalski", Age = 28, Description = "Nowy manager", Password = "Haslo1234"
            }, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateCredentials_ChangesEmailAndOptionallyPassword()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        await _db.AddCredentialAsync(manager.Id, "stary@example.com", "stary-hash");

        await using (var db = _db.NewContext())
        {
            var dto = await Service(db).UpdateCredentialsAsync(company.CompanyId, manager.Id,
                new ManagerCredentialsUpdateDto { Email = "Nowy@Example.com" }, CancellationToken.None);
            Assert.Equal("nowy@example.com", dto.Email);
        }

        await using (var check = _db.NewContext())
        {
            var cred = await check.ManagerCredentials.SingleAsync();
            Assert.Equal("nowy@example.com", cred.Email);
            Assert.Equal("stary-hash", cred.PasswordHash); // brak password = hasło bez zmian
        }

        await using (var db = _db.NewContext())
            await Service(db).UpdateCredentialsAsync(company.CompanyId, manager.Id,
                new ManagerCredentialsUpdateDto { Email = "nowy@example.com", Password = "Nowe12345" }, CancellationToken.None);

        await using var check2 = _db.NewContext();
        Assert.NotEqual("stary-hash", (await check2.ManagerCredentials.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task UpdateCredentials_InvalidOrTakenEmail_IsRejected()
    {
        var company = await _db.AddCompanyAsync(email: "zajety@example.com");
        var manager = await _db.AddManagerAsync(company.CompanyId);
        await _db.AddCredentialAsync(manager.Id, "jan@example.com");

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() => Service(db).UpdateCredentialsAsync(
            company.CompanyId, manager.Id, new ManagerCredentialsUpdateDto { Email = "to-nie-email" }, CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() => Service(db).UpdateCredentialsAsync(
            company.CompanyId, manager.Id, new ManagerCredentialsUpdateDto { Email = "jan@example.com", Password = "slabe" }, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => Service(db).UpdateCredentialsAsync(
            company.CompanyId, manager.Id, new ManagerCredentialsUpdateDto { Email = "zajety@example.com" }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_EmailUsedByAnotherRole_ThrowsConflict()
    {
        var company = await _db.AddCompanyAsync(email: "zajety@example.com");
        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<ConflictException>(() => Service(db).CreateAsync(company.CompanyId,
            new ManagerUserCreateDto
            {
                FirstName = "Jan", LastName = "Kowalski", Age = 28, Description = "Nowy manager",
                Email = "zajety@example.com", Password = "Haslo1234"
            }, CancellationToken.None));
    }

    [Fact]
    public async Task Anonymize_ReplacesPersonalDataRemovesLoginAndKeepsLinks()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId, "Jan", "Kowalski");
        await _db.AddCredentialAsync(manager.Id, "jan@example.com");
        var accountId = await _db.AddSocialAccountAsync(company.CompanyId, managerIds: manager.Id);
        var postId = await _db.AddPostWithManagerAsync(manager.Id, accountId);

        await using (var db = _db.NewContext())
        {
            var dto = await Service(db).AnonymizeAsync(company.CompanyId, manager.Id, CancellationToken.None);
            Assert.Equal("Usunięty", dto.FirstName);
            Assert.Equal("użytkownik", dto.LastName);
            Assert.False(dto.HasLoginAccount);
        }

        await using var check = _db.NewContext();
        var saved = await check.ManagerUsers.Include(m => m.SocialAccounts).FirstAsync(m => m.Id == manager.Id);
        Assert.Equal("Usunięty", saved.FirstName.Value);
        Assert.DoesNotContain("Opis managera", saved.Description.Value);
        Assert.Contains(saved.SocialAccounts, a => a.Id == accountId);
        Assert.False(await check.ManagerCredentials.AnyAsync(c => c.ManagerUserId == manager.Id));
        Assert.True(await check.Posts.AnyAsync(p => p.Id == postId && p.ManagerUsers.Any(m => m.Id == manager.Id)));
    }

    [Fact]
    public async Task SetAccess_BlocksAndUnblocksManagerWithLogin()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);
        await _db.AddCredentialAsync(manager.Id, "anna@example.com");

        await using (var db = _db.NewContext())
            Assert.True((await Service(db).SetAccessAsync(company.CompanyId, manager.Id, true, CancellationToken.None)).IsAccessBlocked);
        await using (var db = _db.NewContext())
            Assert.True((await db.ManagerCredentials.FirstAsync()).IsAccessBlocked);
        await using (var db = _db.NewContext())
            Assert.False((await Service(db).SetAccessAsync(company.CompanyId, manager.Id, false, CancellationToken.None)).IsAccessBlocked);
    }

    [Fact]
    public async Task SetAccess_ManagerWithoutLogin_IsRejected()
    {
        var company = await _db.AddCompanyAsync();
        var manager = await _db.AddManagerAsync(company.CompanyId);

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Service(db).SetAccessAsync(company.CompanyId, manager.Id, true, CancellationToken.None));
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyOwnCompanyAndReportsFlags()
    {
        var company = await _db.AddCompanyAsync();
        var other = await _db.AddCompanyAsync("Inna", "inna@example.com");
        var withLogin = await _db.AddManagerAsync(company.CompanyId, "Anna", "Aaa");
        var blocked = await _db.AddManagerAsync(company.CompanyId, "Bartek", "Bbb");
        var noLogin = await _db.AddManagerAsync(company.CompanyId, "Celina", "Ccc");
        await _db.AddManagerAsync(other.CompanyId, "Obcy", "Manager");
        await _db.AddCredentialAsync(withLogin.Id, "a@example.com");
        await _db.AddCredentialAsync(blocked.Id, "b@example.com");

        await using (var db = _db.NewContext())
            await Service(db).SetAccessAsync(company.CompanyId, blocked.Id, true, CancellationToken.None);

        await using var read = _db.NewContext();
        var all = await Service(read).GetAllAsync(company.CompanyId, CancellationToken.None);

        Assert.Equal(3, all.Count);
        Assert.True(all.Single(m => m.Id == withLogin.Id).HasLoginAccount);
        Assert.False(all.Single(m => m.Id == withLogin.Id).IsAccessBlocked);
        Assert.True(all.Single(m => m.Id == blocked.Id).IsAccessBlocked);
        Assert.False(all.Single(m => m.Id == noLogin.Id).HasLoginAccount);
    }
}
