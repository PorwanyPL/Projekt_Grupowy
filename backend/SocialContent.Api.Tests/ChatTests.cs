using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SocialContent.Api.Data;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

/// <summary>v8: reguły uprawnień czatu (1 na 1 i grupy w firmie), historia, nieprzeczytane i powiadomienia na żywo.</summary>
public class ChatTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly RecordingNotifier _notifier = new();

    public void Dispose() => _db.Dispose();

    private sealed class RecordingNotifier : IRealtimeNotifier
    {
        public List<(ChatMessageDto Message, List<ChatRecipient> Recipients)> Messages { get; } = new();
        public List<(NotificationDto Notification, Guid RecipientId, UserRole Role)> Notifications { get; } = new();

        public Task MessageAsync(ChatMessageDto message, IEnumerable<ChatRecipient> recipients, CancellationToken ct = default)
        {
            Messages.Add((message, recipients.ToList()));
            return Task.CompletedTask;
        }

        public Task NotificationAsync(NotificationDto notification, Guid recipientId, UserRole role, CancellationToken ct = default)
        {
            Notifications.Add((notification, recipientId, role));
            return Task.CompletedTask;
        }
    }

    private ChatService Chat(AppDbContext db) => new(db, _notifier, new NotificationService(db, _db.Mapper));

    private static CurrentUser AsCompany(CompanyUser c) => new(c.CompanyId, UserRole.CompanyUser);
    private static CurrentUser AsManager(ManagerUser m) => new(m.Id, UserRole.ManagerUser);
    private static CurrentUser AsClient(ClientUser c) => new(c.Id, UserRole.ClientUser);

    /// <summary>Firma A (właściciel, ds. czatu: Anna, drugi manager: Bartek), firma B (właściciel, manager), klienci:
    /// współpracujący z A, niewspółpracujący, współpracujący z B.</summary>
    private sealed record World(
        CompanyUser CompanyA, ManagerUser Anna, ManagerUser Bartek,
        CompanyUser CompanyB, ManagerUser Beata,
        ClientUser ClientOfA, ClientUser Stranger, ClientUser ClientOfB);

    private async Task<World> BuildAsync(bool annaIsChatManager = true)
    {
        var a = await _db.AddCompanyAsync("Firma A", "a@example.com");
        var anna = await _db.AddManagerAsync(a.CompanyId, "Anna", "Nowak");
        var bartek = await _db.AddManagerAsync(a.CompanyId, "Bartek", "Kowalski");
        var b = await _db.AddCompanyAsync("Firma B", "b@example.com");
        var beata = await _db.AddManagerAsync(b.CompanyId, "Beata", "Inna");
        var clientOfA = await _db.AddClientAsync("ka@example.com");
        var stranger = await _db.AddClientAsync("obcy@example.com");
        var clientOfB = await _db.AddClientAsync("kb@example.com");
        await _db.AddAccountForClientAsync(clientOfA.Id, a.CompanyId, "acc-a", SocialPlatform.Facebook, anna.Id);
        await _db.AddAccountForClientAsync(clientOfB.Id, b.CompanyId, "acc-b", SocialPlatform.Facebook, beata.Id);

        if (annaIsChatManager)
            await using (var db = _db.NewContext())
                await Chat(db).SetChatManagerAsync(new CurrentUser(a.CompanyId, UserRole.CompanyUser), anna.Id, CancellationToken.None);

        return new World(a, anna, bartek, b, beata, clientOfA, stranger, clientOfB);
    }

    private async Task<bool> CanAsync(CurrentUser user, Guid otherId, UserRole otherRole)
    {
        await using var db = _db.NewContext();
        return await Chat(db).CanChatAsync(user, otherId, otherRole, CancellationToken.None);
    }

    // ---------- Reguły 1-4 ----------

    [Fact]
    public async Task ChatManager_CanChatWithEveryClient_OtherManagersOnlyWithCooperatingOnes()
    {
        var w = await BuildAsync();

        // reguła 1: manager ds. czatu - wszyscy klienci, także niewspółpracujący i klient innej firmy
        Assert.True(await CanAsync(AsManager(w.Anna), w.ClientOfA.Id, UserRole.ClientUser));
        Assert.True(await CanAsync(AsManager(w.Anna), w.Stranger.Id, UserRole.ClientUser));
        Assert.True(await CanAsync(AsManager(w.Anna), w.ClientOfB.Id, UserRole.ClientUser));

        // reguła 2: zwykły manager - tylko współpracujący klient
        Assert.True(await CanAsync(AsManager(w.Bartek), w.ClientOfA.Id, UserRole.ClientUser));
        Assert.False(await CanAsync(AsManager(w.Bartek), w.Stranger.Id, UserRole.ClientUser));
        Assert.False(await CanAsync(AsManager(w.Bartek), w.ClientOfB.Id, UserRole.ClientUser));

        // symetrycznie z perspektywy klienta
        Assert.True(await CanAsync(AsClient(w.Stranger), w.Anna.Id, UserRole.ManagerUser));
        Assert.False(await CanAsync(AsClient(w.Stranger), w.Bartek.Id, UserRole.ManagerUser));
    }

    [Fact]
    public async Task WithoutChatManager_NobodyButOwnerReachesNonCooperatingClients_AndOnlyOwnCompanyManagerCanBeChosen()
    {
        var w = await BuildAsync(annaIsChatManager: false);
        Assert.False(await CanAsync(AsManager(w.Anna), w.Stranger.Id, UserRole.ClientUser));
        Assert.True(await CanAsync(AsCompany(w.CompanyA), w.Stranger.Id, UserRole.ClientUser)); // reguła 3

        await using var db = _db.NewContext();
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Chat(db).SetChatManagerAsync(AsCompany(w.CompanyA), w.Beata.Id, CancellationToken.None)); // manager innej firmy
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Chat(db).SetChatManagerAsync(AsManager(w.Anna), w.Anna.Id, CancellationToken.None)); // tylko właściciel

        var chosen = await Chat(db).SetChatManagerAsync(AsCompany(w.CompanyA), w.Bartek.Id, CancellationToken.None);
        Assert.Equal(w.Bartek.Id, chosen.ManagerId); // zmiana na innego - dokładnie jeden manager
        Assert.True(await CanAsync(AsManager(w.Bartek), w.Stranger.Id, UserRole.ClientUser));
        Assert.Null((await Chat(db).SetChatManagerAsync(AsCompany(w.CompanyA), null, CancellationToken.None)).ManagerId);
    }

    [Fact]
    public async Task OwnerChatsWithEveryoneExceptManagersOfOtherCompanies_AndManagersStayInOwnCompany()
    {
        var w = await BuildAsync();
        var owner = AsCompany(w.CompanyA);

        Assert.True(await CanAsync(owner, w.Anna.Id, UserRole.ManagerUser));
        Assert.True(await CanAsync(owner, w.Stranger.Id, UserRole.ClientUser));
        Assert.True(await CanAsync(owner, w.CompanyB.CompanyId, UserRole.CompanyUser));
        Assert.False(await CanAsync(owner, w.Beata.Id, UserRole.ManagerUser)); // manager innej firmy

        Assert.True(await CanAsync(AsManager(w.Bartek), w.Anna.Id, UserRole.ManagerUser));
        Assert.True(await CanAsync(AsManager(w.Bartek), w.CompanyA.CompanyId, UserRole.CompanyUser));
        Assert.False(await CanAsync(AsManager(w.Bartek), w.Beata.Id, UserRole.ManagerUser));
        Assert.False(await CanAsync(AsManager(w.Bartek), w.CompanyB.CompanyId, UserRole.CompanyUser));

        Assert.False(await CanAsync(AsClient(w.ClientOfA), w.Stranger.Id, UserRole.ClientUser)); // klient - klient
        Assert.False(await CanAsync(owner, w.CompanyA.CompanyId, UserRole.CompanyUser)); // sam ze sobą
    }

    [Fact]
    public async Task Server_RejectsOpeningSendingAndReadingOutsideAllowedPair()
    {
        var w = await BuildAsync();

        await using (var db = _db.NewContext())
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).OpenDirectAsync(AsManager(w.Bartek), w.Stranger.Id, UserRole.ClientUser, CancellationToken.None));

        ConversationDto conversation;
        await using (var db = _db.NewContext())
            conversation = await Chat(db).OpenDirectAsync(AsManager(w.Bartek), w.ClientOfA.Id, UserRole.ClientUser, CancellationToken.None);

        // obcy użytkownik nie czyta ani nie pisze w cudzej rozmowie
        await using (var db = _db.NewContext())
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).GetMessagesAsync(AsClient(w.Stranger), conversation.Id, null, 50, CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).SendAsync(AsManager(w.Anna), conversation.Id, "hej", CancellationToken.None));
        }

        // po rozwiązaniu współpracy rozmowa Bartka z klientem przestaje być dostępna dla obu stron
        await using (var db = _db.NewContext())
        {
            var link = await db.CompanyClients.FirstAsync(l => l.ClientId == w.ClientOfA.Id);
            db.CompanyClients.Remove(link);
            await db.SaveChangesAsync();
        }

        await using (var db = _db.NewContext())
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).SendAsync(AsManager(w.Bartek), conversation.Id, "ping", CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).GetMessagesAsync(AsClient(w.ClientOfA), conversation.Id, null, 50, CancellationToken.None));
            Assert.Empty(await Chat(db).GetConversationsAsync(AsManager(w.Bartek), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Contacts_FollowTheSameRules()
    {
        var w = await BuildAsync();
        await using var db = _db.NewContext();
        var chat = Chat(db);

        var bartek = (await chat.GetContactsAsync(AsManager(w.Bartek), CancellationToken.None)).Select(c => c.UserId).ToList();
        Assert.Contains(w.ClientOfA.Id, bartek);
        Assert.DoesNotContain(w.Stranger.Id, bartek);
        Assert.DoesNotContain(w.Beata.Id, bartek);
        Assert.Contains(w.CompanyA.CompanyId, bartek);

        var anna = (await chat.GetContactsAsync(AsManager(w.Anna), CancellationToken.None)).Select(c => c.UserId).ToList();
        Assert.Contains(w.Stranger.Id, anna);
        Assert.Contains(w.ClientOfB.Id, anna);

        var owner = (await chat.GetContactsAsync(AsCompany(w.CompanyA), CancellationToken.None)).Select(c => c.UserId).ToList();
        Assert.Contains(w.Stranger.Id, owner);
        Assert.Contains(w.CompanyB.CompanyId, owner);
        Assert.DoesNotContain(w.Beata.Id, owner);

        var stranger = (await chat.GetContactsAsync(AsClient(w.Stranger), CancellationToken.None)).Select(c => c.UserId).ToList();
        Assert.Contains(w.Anna.Id, stranger);
        Assert.DoesNotContain(w.Bartek.Id, stranger);
        Assert.DoesNotContain(w.ClientOfA.Id, stranger);
    }

    // ---------- Grupy w obrębie firmy ----------

    [Fact]
    public async Task Group_IsLimitedToOneCompany_AndOnlyMembersReadAndWrite()
    {
        var w = await BuildAsync();

        await using (var db = _db.NewContext())
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).CreateGroupAsync(AsClient(w.ClientOfA), new CreateGroupDto { Name = "Klienci" }, CancellationToken.None));
            await Assert.ThrowsAsync<DomainValidationException>(() =>
                Chat(db).CreateGroupAsync(AsCompany(w.CompanyA),
                    new CreateGroupDto { Name = "Mieszana", ManagerIds = { w.Beata.Id } }, CancellationToken.None));
        }

        ConversationDto group;
        await using (var db = _db.NewContext())
            group = await Chat(db).CreateGroupAsync(AsManager(w.Bartek),
                new CreateGroupDto { Name = "Zespół", ManagerIds = { w.Anna.Id } }, CancellationToken.None);

        Assert.Equal(ConversationKind.CompanyGroup, group.Kind);
        Assert.Equal(3, group.Participants.Count); // właściciel, autor i Anna
        Assert.Equal("Zespół", group.Title);

        await using (var db = _db.NewContext())
        {
            var sent = await Chat(db).SendAsync(AsCompany(w.CompanyA), group.Id, "  Witajcie  ", CancellationToken.None);
            Assert.Equal("Witajcie", sent.Text);
            Assert.Equal("Firma A", sent.SenderName);
            // wszyscy uczestnicy dostają zdarzenie na żywo
            Assert.Equal(3, _notifier.Messages.Last().Recipients.Count);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).SendAsync(AsManager(w.Beata), group.Id, "włamanie", CancellationToken.None)); // inna firma
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).GetMessagesAsync(AsClient(w.ClientOfA), group.Id, null, 50, CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Chat(db).GetMessagesAsync(AsCompany(w.CompanyB), group.Id, null, 50, CancellationToken.None));
        }
    }

    // ---------- Historia, nieprzeczytane, walidacja ----------

    [Fact]
    public async Task History_IsStoredInDatabase_PagedAndUnreadCountsFollowReadMarks()
    {
        var w = await BuildAsync();
        ConversationDto conversation;
        await using (var db = _db.NewContext())
            conversation = await Chat(db).OpenDirectAsync(AsCompany(w.CompanyA), w.ClientOfA.Id, UserRole.ClientUser, CancellationToken.None);

        await using (var db = _db.NewContext())
        {
            for (var i = 1; i <= 5; i++)
            {
                await Chat(db).SendAsync(AsCompany(w.CompanyA), conversation.Id, $"wiadomość {i}", CancellationToken.None);
                await Task.Delay(5); // różne znaczniki czasu
            }
        }

        await using (var check = _db.NewContext())
            Assert.Equal(5, await check.ChatMessages.CountAsync()); // zapis w SQLite

        await using (var db = _db.NewContext())
        {
            var chat = Chat(db);
            var list = await chat.GetConversationsAsync(AsClient(w.ClientOfA), CancellationToken.None);
            var item = Assert.Single(list);
            Assert.Equal(5, item.UnreadCount);
            Assert.Equal("Firma A", item.Title);
            Assert.Equal("wiadomość 5", item.LastMessage!.Text);
            Assert.Equal(0, Assert.Single(await chat.GetConversationsAsync(AsCompany(w.CompanyA), CancellationToken.None)).UnreadCount);

            var page = await chat.GetMessagesAsync(AsClient(w.ClientOfA), conversation.Id, null, 3, CancellationToken.None);
            Assert.Equal(new[] { "wiadomość 3", "wiadomość 4", "wiadomość 5" }, page.Select(m => m.Text)); // od najstarszej
            var older = await chat.GetMessagesAsync(AsClient(w.ClientOfA), conversation.Id, page[0].SentAt, 10, CancellationToken.None);
            Assert.Equal(new[] { "wiadomość 1", "wiadomość 2" }, older.Select(m => m.Text));

            await chat.MarkReadAsync(AsClient(w.ClientOfA), conversation.Id, CancellationToken.None);
        }

        await using (var db = _db.NewContext())
            Assert.Equal(0, Assert.Single(await Chat(db).GetConversationsAsync(AsClient(w.ClientOfA), CancellationToken.None)).UnreadCount);
    }

    [Fact]
    public async Task OpenDirect_IsIdempotent_AndMessageTextIsValidated()
    {
        var w = await BuildAsync();
        await using var db = _db.NewContext();
        var chat = Chat(db);

        var first = await chat.OpenDirectAsync(AsCompany(w.CompanyA), w.Anna.Id, UserRole.ManagerUser, CancellationToken.None);
        var again = await chat.OpenDirectAsync(AsManager(w.Anna), w.CompanyA.CompanyId, UserRole.CompanyUser, CancellationToken.None);
        Assert.Equal(first.Id, again.Id); // jedna rozmowa na parę, niezależnie od strony

        await Assert.ThrowsAsync<DomainValidationException>(() => chat.SendAsync(AsCompany(w.CompanyA), first.Id, "   ", CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            chat.SendAsync(AsCompany(w.CompanyA), first.Id, new string('x', ChatMessage.MaxTextLength + 1), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => chat.SendAsync(AsCompany(w.CompanyA), Guid.NewGuid(), "x", CancellationToken.None));
    }

    [Fact]
    public async Task BlockedManager_IsNotAvailableForChat()
    {
        var w = await BuildAsync();
        await _db.AddCredentialAsync(w.Bartek.Id, "bartek@example.com");
        await using (var db = _db.NewContext())
        {
            (await db.ManagerCredentials.FirstAsync(c => c.ManagerUserId == w.Bartek.Id)).SetAccessBlocked(true);
            await db.SaveChangesAsync();
        }

        Assert.False(await CanAsync(AsCompany(w.CompanyA), w.Bartek.Id, UserRole.ManagerUser));
        await using var check = _db.NewContext();
        Assert.DoesNotContain(w.Bartek.Id, (await Chat(check).GetContactsAsync(AsCompany(w.CompanyA), CancellationToken.None)).Select(c => c.UserId));
    }

    // ---------- Powiadomienia na żywo (ten sam kanał co czat) ----------

    [Fact]
    public async Task Notifications_ArePushedOnlyAfterSuccessfulSave()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection().AddSingleton<IRealtimeNotifier>(_notifier).BuildServiceProvider();
        var interceptor = new NotificationPushInterceptor(services, NullLogger<NotificationPushInterceptor>.Instance);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var recipient = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        db.Notifications.Add(Notification.Create(recipient, UserRole.ManagerUser, groupId, "Post został opublikowany."));
        Assert.Empty(_notifier.Notifications); // przed zapisem nic nie wychodzi
        await db.SaveChangesAsync();

        var pushed = Assert.Single(_notifier.Notifications);
        Assert.Equal(recipient, pushed.RecipientId);
        Assert.Equal(UserRole.ManagerUser, pushed.Role);
        Assert.Equal(groupId, pushed.Notification.PostId); // powiadomienia o postach nadal po GroupId
        Assert.Equal(1, await db.Notifications.CountAsync());

        await db.SaveChangesAsync(); // zapis bez nowych powiadomień nie wysyła ich ponownie
        Assert.Single(_notifier.Notifications);
    }
}
