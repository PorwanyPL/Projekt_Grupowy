using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

/// <summary>v9: wyszukiwanie rozmówcy (3 tryby, z regułami czatu), cel powiadomień, powiadomienia czatu, odrzucenie posta z uwagami.</summary>
public class V9Tests : IDisposable
{
    private readonly TestDb _db = new();

    public void Dispose() => _db.Dispose();

    private ChatService Chat(AppDbContext db) => new(db, new NullRealtimeNotifier(), new NotificationService(db, _db.Mapper));

    private PostService Posts(AppDbContext db)
    {
        var notifications = new NotificationService(db, _db.Mapper);
        return new PostService(db, _db.Mapper, new MediaStorage(Path.Combine(Path.GetTempPath(), "sc-tests-" + Guid.NewGuid())),
            new PostEditLockService(db, _db.Mapper), new PostPublisher(db, notifications), notifications);
    }

    private static CurrentUser AsCompany(CompanyUser c) => new(c.CompanyId, UserRole.CompanyUser);
    private static CurrentUser AsManager(ManagerUser m) => new(m.Id, UserRole.ManagerUser);
    private static CurrentUser AsClient(ClientUser c) => new(c.Id, UserRole.ClientUser);

    /// <summary>Firma A (ds. czatu: Anna Nowak; drugi manager: Bartek Kowalski), klient współpracujący (ka@) i obcy (obcy@).</summary>
    private sealed record World(CompanyUser Company, ManagerUser Anna, ManagerUser Bartek, ClientUser Cooperating, ClientUser Stranger, Guid AccountId);

    private async Task<World> BuildAsync()
    {
        var company = await _db.AddCompanyAsync("Firma A", "a@example.com");
        var anna = await _db.AddManagerAsync(company.CompanyId, "Anna", "Nowak");
        var bartek = await _db.AddManagerAsync(company.CompanyId, "Bartek", "Kowalski");
        var cooperating = await _db.AddClientAsync("ka@example.com");
        var stranger = await _db.AddClientAsync("obcy@example.com");
        var account = await _db.AddAccountForClientAsync(cooperating.Id, company.CompanyId, "acc-a", SocialPlatform.Facebook, anna.Id);
        await using var db = _db.NewContext();
        await Chat(db).SetChatManagerAsync(AsCompany(company), anna.Id, CancellationToken.None);
        return new World(company, anna, bartek, cooperating, stranger, account);
    }

    private async Task<List<ChatContactDto>> SearchAsync(CurrentUser user, string? phrase, ChatContactSearchMode mode)
    {
        await using var db = _db.NewContext();
        return await Chat(db).SearchContactsAsync(user, phrase, mode, CancellationToken.None);
    }

    // ---------- 1. Wyszukiwanie rozmówcy ----------

    [Fact]
    public async Task Search_Prefix_MatchesNameOrAnyWordOfName_CaseInsensitive()
    {
        var w = await BuildAsync();

        var byLastName = await SearchAsync(AsCompany(w.Company), "kow", ChatContactSearchMode.Prefix);
        Assert.Equal(w.Bartek.Id, Assert.Single(byLastName).UserId);

        var byFirstName = await SearchAsync(AsCompany(w.Company), "ANN", ChatContactSearchMode.Prefix);
        Assert.Equal(w.Anna.Id, Assert.Single(byFirstName).UserId);

        Assert.Empty(await SearchAsync(AsCompany(w.Company), "owak", ChatContactSearchMode.Prefix)); // fragment w środku to nie prefiks
    }

    [Fact]
    public async Task Search_Contains_FindsFragmentAnywhereInName()
    {
        var w = await BuildAsync();

        var ids = (await SearchAsync(AsCompany(w.Company), "ow", ChatContactSearchMode.Contains)).Select(c => c.UserId).ToList();
        Assert.Contains(w.Anna.Id, ids);   // Nowak
        Assert.Contains(w.Bartek.Id, ids); // Kowalski
        Assert.DoesNotContain(w.Cooperating.Id, ids);
    }

    [Fact]
    public async Task Search_Email_MatchesAddress_AndReturnsEmail()
    {
        var w = await BuildAsync();

        var found = Assert.Single(await SearchAsync(AsCompany(w.Company), "obcy@", ChatContactSearchMode.Email));
        Assert.Equal(w.Stranger.Id, found.UserId);
        Assert.Equal("obcy@example.com", found.Email);

        Assert.Empty(await SearchAsync(AsCompany(w.Company), "Obcy", ChatContactSearchMode.Prefix)); // imię klienta to Karol, a nie fragment e-maila
    }

    [Fact]
    public async Task Search_RespectsChatRules_NobodyFoundOutsideAllowedPairs()
    {
        var w = await BuildAsync();

        // Anna (manager ds. czatu) widzi obcego klienta, Bartek - nie, nawet po dokładnym e-mailu.
        Assert.Single(await SearchAsync(AsManager(w.Anna), "obcy@example.com", ChatContactSearchMode.Email));
        Assert.Empty(await SearchAsync(AsManager(w.Bartek), "obcy@example.com", ChatContactSearchMode.Email));
        Assert.Single(await SearchAsync(AsManager(w.Bartek), "ka@", ChatContactSearchMode.Email));

        // Nazwa obu klientów to "Karol Klient": Bartek znajdzie jednego (współpracującego), Anna obu.
        Assert.Single(await SearchAsync(AsManager(w.Bartek), "Karol", ChatContactSearchMode.Contains));
        Assert.Equal(2, (await SearchAsync(AsManager(w.Anna), "Karol", ChatContactSearchMode.Contains)).Count);

        // Obcy klient widzi managera ds. czatu, ale nie Bartka.
        var strangerSees = (await SearchAsync(AsClient(w.Stranger), "kowalski", ChatContactSearchMode.Contains)).Select(c => c.UserId);
        Assert.DoesNotContain(w.Bartek.Id, strangerSees);
        Assert.Contains(w.Anna.Id, (await SearchAsync(AsClient(w.Stranger), "nowak", ChatContactSearchMode.Contains)).Select(c => c.UserId));
    }

    [Fact]
    public async Task Search_EmptyOrBlankPhrase_ReturnsAllAllowedContacts()
    {
        var w = await BuildAsync();
        await using var db = _db.NewContext();
        var all = await Chat(db).GetContactsAsync(AsCompany(w.Company), CancellationToken.None);

        Assert.Equal(all.Count, (await SearchAsync(AsCompany(w.Company), null, ChatContactSearchMode.Prefix)).Count);
        Assert.Equal(all.Count, (await SearchAsync(AsCompany(w.Company), "   ", ChatContactSearchMode.Email)).Count);
    }

    // ---------- 4. Cel powiadomień i powiadomienia czatu ----------

    [Fact]
    public void Notification_Targets_PostAndChat()
    {
        var group = Guid.NewGuid();
        var post = Notification.Create(Guid.NewGuid(), UserRole.ClientUser, group, "x");
        Assert.Equal(NotificationTarget.Post, post.TargetType);
        Assert.Equal(group, post.TargetId);

        Assert.Equal(NotificationTarget.None, Notification.Create(Guid.NewGuid(), UserRole.ClientUser, null, "x").TargetType);

        var conversation = Guid.NewGuid();
        var chat = Notification.CreateChat(Guid.NewGuid(), UserRole.ManagerUser, conversation, "y");
        Assert.Equal(NotificationTarget.Chat, chat.TargetType);
        Assert.Equal(conversation, chat.TargetId);

        var dto = _db.Mapper.Map<NotificationDto>(chat);
        Assert.Equal(NotificationTarget.Chat, dto.TargetType);
        Assert.Equal(conversation, dto.TargetId);
    }

    [Fact]
    public async Task ChatMessage_CreatesOneUnreadNotificationPerConversation_AndReadClearsIt()
    {
        var w = await BuildAsync();
        Guid conversationId;
        await using (var db = _db.NewContext())
        {
            var conversation = await Chat(db).OpenDirectAsync(AsClient(w.Cooperating), w.Anna.Id, UserRole.ManagerUser, CancellationToken.None);
            conversationId = conversation.Id;
            await Chat(db).SendAsync(AsClient(w.Cooperating), conversationId, "Cześć", CancellationToken.None);
        }
        await using (var db = _db.NewContext())
            await Chat(db).SendAsync(AsClient(w.Cooperating), conversationId, "Jeszcze raz", CancellationToken.None);

        await using (var check = _db.NewContext())
        {
            var forAnna = await check.Notifications.Where(n => n.RecipientId == w.Anna.Id).ToListAsync();
            var n = Assert.Single(forAnna); // druga wiadomość nie dubluje powiadomienia
            Assert.Equal(NotificationTarget.Chat, n.TargetType);
            Assert.Equal(conversationId, n.TargetId);
            Assert.False(n.IsRead);
            Assert.DoesNotContain(await check.Notifications.ToListAsync(), x => x.RecipientId == w.Cooperating.Id); // nadawca nic nie dostaje
        }

        await using (var db = _db.NewContext())
            await Chat(db).MarkReadAsync(AsManager(w.Anna), conversationId, CancellationToken.None);
        await using (var check = _db.NewContext())
            Assert.True((await check.Notifications.SingleAsync(n => n.RecipientId == w.Anna.Id)).IsRead);

        await using (var db = _db.NewContext())
            await Chat(db).SendAsync(AsClient(w.Cooperating), conversationId, "Nowa wiadomość po odczycie", CancellationToken.None);
        await using (var check = _db.NewContext())
            Assert.Equal(2, await check.Notifications.CountAsync(n => n.RecipientId == w.Anna.Id)); // po odczycie powstaje nowe
    }

    // ---------- 5. Odrzucenie posta z uwagami ----------

    private async Task<(World World, Guid PostId)> AwaitingPostAsync()
    {
        var w = await BuildAsync();
        await using var db = _db.NewContext();
        await new ClientUserService(db, _db.Mapper)
            .SetApprovalModeAsync(w.Cooperating.Id, w.Company.CompanyId, PostApprovalMode.Client, CancellationToken.None);
        var created = await Posts(db).CreateAsync(AsManager(w.Anna), new PostWriteDto
        {
            Title = "Post do akceptacji", Description = "Treść", Visibility = FacebookPostType.Text,
            TargetAccountId = w.AccountId, PublishNow = true
        }, CancellationToken.None);
        Assert.Equal(PostStatus.AwaitingApproval, created.Status);
        return (w, created.Id);
    }

    [Fact]
    public async Task Reject_WithComment_ReturnsPostForCorrection_AndNotifiesAuthorsWithComment()
    {
        var (w, postId) = await AwaitingPostAsync();

        PostDto rejected;
        await using (var db = _db.NewContext())
            rejected = await Posts(db).RejectAsync(AsClient(w.Cooperating), postId, CancellationToken.None, "  Zmień zdjęcie i dodaj hasztag.  ");

        Assert.Equal(PostStatus.Rejected, rejected.Status);
        Assert.Equal("Zmień zdjęcie i dodaj hasztag.", rejected.RejectionComment);

        await using var check = _db.NewContext();
        var notifications = await check.Notifications.Where(n => n.Message.Contains("odrzucił")).ToListAsync();
        foreach (var recipient in new[] { w.Company.CompanyId, w.Anna.Id })
        {
            var n = Assert.Single(notifications, x => x.RecipientId == recipient);
            Assert.Contains("Zmień zdjęcie i dodaj hasztag.", n.Message);
            Assert.Equal(NotificationTarget.Post, n.TargetType);
            Assert.Equal(postId, n.TargetId); // klik prowadzi do posta (GroupId)
        }
    }

    [Fact]
    public async Task Reject_WithoutComment_StillWorks_AndTooLongCommentIsRefused()
    {
        var (w, postId) = await AwaitingPostAsync();

        await using (var db = _db.NewContext())
            await Assert.ThrowsAsync<DomainValidationException>(() =>
                Posts(db).RejectAsync(AsClient(w.Cooperating), postId, CancellationToken.None, new string('x', Post.MaxRejectionCommentLength + 1)));

        await using (var db = _db.NewContext())
        {
            var rejected = await Posts(db).RejectAsync(AsClient(w.Cooperating), postId, CancellationToken.None, "   ");
            Assert.Equal(PostStatus.Rejected, rejected.Status);
            Assert.Null(rejected.RejectionComment);
        }
    }

    [Fact]
    public async Task OnlyOwningClientCanReject_AndEditingRejectedPostClearsComment()
    {
        var (w, postId) = await AwaitingPostAsync();

        await using (var db = _db.NewContext())
            await Assert.ThrowsAsync<NotFoundException>(() =>
                Posts(db).RejectAsync(AsClient(w.Stranger), postId, CancellationToken.None, "Obcy"));

        await using (var db = _db.NewContext())
            await Posts(db).RejectAsync(AsClient(w.Cooperating), postId, CancellationToken.None, "Popraw tytuł.");

        await using (var db = _db.NewContext())
        {
            var fixedPost = await Posts(db).UpdateAsync(AsManager(w.Anna), postId, new PostWriteDto
            {
                Title = "Poprawiony tytuł", Description = "Treść", Visibility = FacebookPostType.Text,
                TargetAccountId = w.AccountId, PublishNow = false
            }, CancellationToken.None);
            Assert.Null(fixedPost.RejectionComment);
            Assert.Equal(PostStatus.Draft, fixedPost.Status);
        }
    }
}
