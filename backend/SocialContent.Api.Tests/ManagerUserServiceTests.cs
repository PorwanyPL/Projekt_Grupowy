using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Domain;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

public class ManagerUserServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Delete_ManagerWithoutLinks_RemovesManagerAndCredentials()
    {
        var manager = await _db.AddManagerAsync();
        await _db.AddCredentialAsync(manager.Id, "anna@example.com");

        await using (var db = _db.NewContext())
            await new ManagerUserService(db).DeleteAsync(manager.Id, CancellationToken.None);

        await using var check = _db.NewContext();
        Assert.False(await check.ManagerUsers.AnyAsync(m => m.Id == manager.Id));
        Assert.False(await check.ManagerCredentials.AnyAsync(c => c.ManagerUserId == manager.Id));
    }

    [Fact]
    public async Task Delete_ManagerWithPosts_ThrowsConflictAndKeepsEverything()
    {
        var manager = await _db.AddManagerAsync();
        var postId = await _db.AddPostWithManagerAsync(manager.Id);

        await using (var db = _db.NewContext())
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                new ManagerUserService(db).DeleteAsync(manager.Id, CancellationToken.None));
            Assert.Contains("zablokuj dostęp zamiast usuwać", ex.Message);
        }

        await using var check = _db.NewContext();
        Assert.True(await check.ManagerUsers.AnyAsync(m => m.Id == manager.Id));
        Assert.True(await check.Posts.AnyAsync(p => p.Id == postId && p.ManagerUsers.Any(m => m.Id == manager.Id)));
    }

    [Fact]
    public async Task Delete_ManagerWithSocialAccount_ThrowsConflict()
    {
        var manager = await _db.AddManagerAsync();
        await _db.AddSocialAccountWithManagerAsync(manager.Id);

        await using var db = _db.NewContext();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            new ManagerUserService(db).DeleteAsync(manager.Id, CancellationToken.None));
        Assert.Contains("zablokuj dostęp zamiast usuwać", ex.Message);
    }

    [Fact]
    public async Task Delete_UnknownManager_ThrowsNotFound()
    {
        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ManagerUserService(db).DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Anonymize_ReplacesPersonalDataRemovesLoginAndKeepsLinks()
    {
        var manager = await _db.AddManagerAsync("Jan", "Kowalski");
        await _db.AddCredentialAsync(manager.Id, "jan@example.com");
        var postId = await _db.AddPostWithManagerAsync(manager.Id);
        var accountId = await _db.AddSocialAccountWithManagerAsync(manager.Id);

        await using (var db = _db.NewContext())
        {
            var dto = await new ManagerUserService(db).AnonymizeAsync(manager.Id, CancellationToken.None);
            Assert.Equal("Usunięty", dto.FirstName);
            Assert.Equal("użytkownik", dto.LastName);
            Assert.False(dto.HasLoginAccount);
        }

        await using var check = _db.NewContext();
        var saved = await check.ManagerUsers.FirstAsync(m => m.Id == manager.Id);
        Assert.Equal("Usunięty", saved.FirstName.Value);
        Assert.Equal("użytkownik", saved.LastName.Value);
        Assert.DoesNotContain("Opis managera", saved.Description.Value);
        Assert.Equal(accountId, saved.SocialAccountId);
        Assert.False(await check.ManagerCredentials.AnyAsync(c => c.ManagerUserId == manager.Id));
        Assert.True(await check.Posts.AnyAsync(p => p.Id == postId && p.ManagerUsers.Any(m => m.Id == manager.Id)));
    }

    [Fact]
    public async Task SetAccess_BlocksAndUnblocksManagerWithLogin()
    {
        var manager = await _db.AddManagerAsync();
        await _db.AddCredentialAsync(manager.Id, "anna@example.com");

        await using (var db = _db.NewContext())
        {
            var dto = await new ManagerUserService(db).SetAccessAsync(manager.Id, true, Guid.NewGuid(), CancellationToken.None);
            Assert.True(dto.IsAccessBlocked);
        }
        await using (var db = _db.NewContext())
            Assert.True((await db.ManagerCredentials.FirstAsync()).IsAccessBlocked);

        await using (var db = _db.NewContext())
        {
            var dto = await new ManagerUserService(db).SetAccessAsync(manager.Id, false, Guid.NewGuid(), CancellationToken.None);
            Assert.False(dto.IsAccessBlocked);
        }
    }

    [Fact]
    public async Task SetAccess_BlockingYourself_IsRejected()
    {
        var manager = await _db.AddManagerAsync();
        await _db.AddCredentialAsync(manager.Id, "anna@example.com");

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            new ManagerUserService(db).SetAccessAsync(manager.Id, true, manager.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SetAccess_ManagerWithoutLogin_IsRejected()
    {
        var manager = await _db.AddManagerAsync();

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            new ManagerUserService(db).SetAccessAsync(manager.Id, true, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetAll_ReportsLoginAndBlockFlags()
    {
        var withLogin = await _db.AddManagerAsync("Anna", "Aaa");
        var blocked = await _db.AddManagerAsync("Bartek", "Bbb");
        var noLogin = await _db.AddManagerAsync("Celina", "Ccc");
        await _db.AddCredentialAsync(withLogin.Id, "a@example.com");
        await _db.AddCredentialAsync(blocked.Id, "b@example.com");

        await using (var db = _db.NewContext())
            await new ManagerUserService(db).SetAccessAsync(blocked.Id, true, null, CancellationToken.None);

        await using var read = _db.NewContext();
        var all = await new ManagerUserService(read).GetAllAsync(CancellationToken.None);

        var withLoginDto = all.Single(m => m.Id == withLogin.Id);
        Assert.True(withLoginDto.HasLoginAccount);
        Assert.False(withLoginDto.IsAccessBlocked);
        Assert.True(all.Single(m => m.Id == blocked.Id).IsAccessBlocked);
        Assert.False(all.Single(m => m.Id == noLogin.Id).HasLoginAccount);
    }
}
