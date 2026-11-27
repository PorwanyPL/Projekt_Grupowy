using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Dtos;

namespace SocialContent.Api.Services;

/// <summary>
/// Czat (v8). Wszystkie reguły uprawnień są tutaj, po stronie serwera (REST i hub używają tego samego serwisu):
///  1. Właściciel firmy wyznacza jednego managera, który może czatować ze WSZYSTKIMI klientami (także niewspółpracującymi).
///  2. Pozostali managerowie czatują tylko z klientami współpracującymi z ich firmą (CompanyClient) oraz z użytkownikami własnej firmy.
///  3. Właściciel firmy czatuje ze wszystkimi użytkownikami poza managerami innych firm.
///  4. Klient czatuje z firmami oraz z managerami, którzy mogą z nim czatować (reguły 1-2 działają symetrycznie); klient z klientem nie.
/// Grupa to rozmowa w obrębie jednej firmy: jej właściciel i managerowie. Zablokowane konta (firma, manager) są pomijane.
/// Uprawnienie jest sprawdzane przy każdym odczycie historii i wysłaniu wiadomości (nie tylko przy tworzeniu rozmowy),
/// więc cofnięcie współpracy albo uprawnienia managera od razu zamyka rozmowę.
/// </summary>
public interface IChatService
{
    Task<ChatManagerDto> GetChatManagerAsync(CurrentUser user, CancellationToken ct);
    Task<ChatManagerDto> SetChatManagerAsync(CurrentUser user, Guid? managerId, CancellationToken ct);
    Task<List<ChatContactDto>> GetContactsAsync(CurrentUser user, CancellationToken ct);

    /// <summary>
    /// Kontakty z filtrem frazy (po stronie serwera, po regułach widoczności czatu: nie znajdzie się nikogo, z kim nie wolno pisać).
    /// Pusta fraza = wszystkie dozwolone kontakty.
    /// </summary>
    Task<List<ChatContactDto>> SearchContactsAsync(CurrentUser user, string? search, ChatContactSearchMode mode, CancellationToken ct);
    Task<List<ConversationDto>> GetConversationsAsync(CurrentUser user, CancellationToken ct);
    Task<ConversationDto> OpenDirectAsync(CurrentUser user, Guid otherId, UserRole otherRole, CancellationToken ct);
    Task<ConversationDto> CreateGroupAsync(CurrentUser user, CreateGroupDto dto, CancellationToken ct);
    Task<List<ChatMessageDto>> GetMessagesAsync(CurrentUser user, Guid conversationId, DateTime? before, int limit, CancellationToken ct);
    Task<ChatMessageDto> SendAsync(CurrentUser user, Guid conversationId, string? text, CancellationToken ct);
    Task MarkReadAsync(CurrentUser user, Guid conversationId, CancellationToken ct);

    /// <summary>Czy para może czatować (reguły 1-4); używane też w testach.</summary>
    Task<bool> CanChatAsync(CurrentUser user, Guid otherId, UserRole otherRole, CancellationToken ct);
}

public class ChatService(AppDbContext db, IRealtimeNotifier realtime, INotificationService notifications) : IChatService
{
    private const int MaxPageSize = 200;

    private sealed record Party(Guid Id, UserRole Role, Guid? CompanyId, string Name);

    // ---------- Użytkownicy ----------

    private async Task<Party?> FindPartyAsync(Guid id, UserRole role, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.CompanyUser:
                var company = await db.CompanyUsers.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CompanyId == id && !c.IsAccessBlocked, ct);
                return company is null ? null : new Party(id, role, id, company.Name);
            case UserRole.ManagerUser:
                var manager = await db.ManagerUsers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
                if (manager is null) return null;
                // Blokada jest w ManagerCredential (brak wiersza = konto bez logowania, nie blokada).
                var blocked = await db.ManagerCredentials.AsNoTracking()
                    .AnyAsync(c => c.ManagerUserId == id && c.IsAccessBlocked, ct);
                return blocked ? null : new Party(id, role, manager.CompanyId, ManagerName(manager));
            case UserRole.ClientUser:
                var client = await db.ClientUsers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
                return client is null ? null : new Party(id, role, null, $"{client.FirstName.Value} {client.LastName.Value}");
            default:
                return null;
        }
    }

    private static string ManagerName(ManagerUser m) => $"{m.FirstName.Value} {m.LastName.Value}";

    private async Task<string> NameAsync(Guid id, UserRole role, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.CompanyUser:
                return await db.CompanyUsers.AsNoTracking().Where(c => c.CompanyId == id).Select(c => c.Name)
                    .FirstOrDefaultAsync(ct) ?? "Firma";
            case UserRole.ManagerUser:
                var m = await db.ManagerUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
                return m is null ? "Manager" : ManagerName(m);
            default:
                var c = await db.ClientUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
                return c is null ? "Klient" : $"{c.FirstName.Value} {c.LastName.Value}";
        }
    }

    // ---------- Reguły uprawnień ----------

    /// <summary>Czy manager (firmy X) może czatować z klientem: jest managerem ds. czatu firmy albo klient współpracuje z firmą.</summary>
    private async Task<bool> ManagerMayChatWithClientAsync(Party manager, Guid clientId, CancellationToken ct)
    {
        var companyId = manager.CompanyId!.Value;
        var chatManagerId = await db.CompanyUsers.AsNoTracking().Where(c => c.CompanyId == companyId)
            .Select(c => c.ChatManagerId).FirstOrDefaultAsync(ct);
        if (chatManagerId == manager.Id) return true;
        return await db.CompanyClients.AsNoTracking().AnyAsync(c => c.CompanyId == companyId && c.ClientId == clientId, ct);
    }

    private async Task<bool> AllowedAsync(Party a, Party b, CancellationToken ct)
    {
        switch (a.Role, b.Role)
        {
            case (UserRole.CompanyUser, UserRole.CompanyUser):
                return true; // właściciel firmy - ze wszystkimi poza managerami innych firm
            case (UserRole.CompanyUser, UserRole.ManagerUser):
                return b.CompanyId == a.Id; // manager innej firmy jest wykluczony
            case (UserRole.ManagerUser, UserRole.CompanyUser):
                return a.CompanyId == b.Id;
            case (UserRole.CompanyUser, UserRole.ClientUser):
            case (UserRole.ClientUser, UserRole.CompanyUser):
                return true;
            case (UserRole.ManagerUser, UserRole.ManagerUser):
                return a.CompanyId == b.CompanyId;
            case (UserRole.ManagerUser, UserRole.ClientUser):
                return await ManagerMayChatWithClientAsync(a, b.Id, ct);
            case (UserRole.ClientUser, UserRole.ManagerUser):
                return await ManagerMayChatWithClientAsync(b, a.Id, ct);
            default:
                return false; // klient - klient
        }
    }

    public async Task<bool> CanChatAsync(CurrentUser user, Guid otherId, UserRole otherRole, CancellationToken ct)
    {
        if (user.Id == otherId && user.Role == otherRole) return false;
        var me = await FindPartyAsync(user.Id, user.Role, ct);
        var other = await FindPartyAsync(otherId, otherRole, ct);
        return me is not null && other is not null && await AllowedAsync(me, other, ct);
    }

    /// <summary>Czy użytkownik należy do firmy (jej właściciel albo manager) - warunek uczestnictwa w grupie.</summary>
    private async Task<bool> IsCompanyMemberAsync(CurrentUser user, Guid companyId, CancellationToken ct)
    {
        var me = await FindPartyAsync(user.Id, user.Role, ct);
        return me is not null && me.Role != UserRole.ClientUser && me.CompanyId == companyId;
    }

    // ---------- Manager ds. czatu ----------

    public async Task<ChatManagerDto> GetChatManagerAsync(CurrentUser user, CancellationToken ct)
    {
        if (!user.IsCompany) throw new ForbiddenException("Manager ds. czatu jest ustawiany przez właściciela firmy.");
        var id = await db.CompanyUsers.AsNoTracking().Where(c => c.CompanyId == user.Id).Select(c => c.ChatManagerId)
            .FirstOrDefaultAsync(ct);
        if (id is null) return new ChatManagerDto(null, null);
        var party = await FindPartyAsync(id.Value, UserRole.ManagerUser, ct);
        return party is null || party.CompanyId != user.Id ? new ChatManagerDto(null, null) : new ChatManagerDto(id, party.Name);
    }

    public async Task<ChatManagerDto> SetChatManagerAsync(CurrentUser user, Guid? managerId, CancellationToken ct)
    {
        if (!user.IsCompany) throw new ForbiddenException("Manager ds. czatu jest ustawiany przez właściciela firmy.");
        if (managerId is not null)
        {
            var party = await FindPartyAsync(managerId.Value, UserRole.ManagerUser, ct);
            if (party is null || party.CompanyId != user.Id)
                throw new DomainValidationException("Wskazany manager nie istnieje, jest zablokowany albo należy do innej firmy.");
        }

        var company = await db.CompanyUsers.FirstOrDefaultAsync(c => c.CompanyId == user.Id, ct)
                      ?? throw new NotFoundException("Nie znaleziono firmy.");
        company.SetChatManager(managerId);
        await db.SaveChangesAsync(ct);
        return await GetChatManagerAsync(user, ct);
    }

    // ---------- Kontakty ----------

    public Task<List<ChatContactDto>> GetContactsAsync(CurrentUser user, CancellationToken ct) =>
        SearchContactsAsync(user, null, ChatContactSearchMode.Contains, ct);

    public async Task<List<ChatContactDto>> SearchContactsAsync(
        CurrentUser user, string? search, ChatContactSearchMode mode, CancellationToken ct)
    {
        var me = await FindPartyAsync(user.Id, user.Role, ct) ?? throw new ForbiddenException("Konto jest niedostępne.");
        var companyNames = await db.CompanyUsers.AsNoTracking().ToDictionaryAsync(c => c.CompanyId, c => c.Name, ct);
        var result = new List<ChatContactDto>();

        // Managerowie niezablokowani (z firmą), na bazie których składamy listy dla każdej roli.
        var managerEmails = await db.ManagerCredentials.AsNoTracking()
            .ToDictionaryAsync(c => c.ManagerUserId, c => c.Email, ct);
        var blockedManagers = db.ManagerCredentials.Where(c => c.IsAccessBlocked).Select(c => c.ManagerUserId);
        var managers = await db.ManagerUsers.AsNoTracking().Where(m => !blockedManagers.Contains(m.Id)).ToListAsync(ct);
        var companies = await db.CompanyUsers.AsNoTracking().Where(c => !c.IsAccessBlocked).ToListAsync(ct);

        ChatContactDto ManagerContact(ManagerUser m) =>
            new(m.Id, UserRole.ManagerUser, ManagerName(m), $"Manager · {companyNames.GetValueOrDefault(m.CompanyId, "firma")}",
                managerEmails.GetValueOrDefault(m.Id));
        ChatContactDto CompanyContact(CompanyUser c) => new(c.CompanyId, UserRole.CompanyUser, c.Name, "Firma", c.Email);
        ChatContactDto ClientContact(ClientUser c) =>
            new(c.Id, UserRole.ClientUser, $"{c.FirstName.Value} {c.LastName.Value}", "Klient", c.Email);

        switch (user.Role)
        {
            case UserRole.CompanyUser:
                result.AddRange(managers.Where(m => m.CompanyId == user.Id).Select(ManagerContact));
                result.AddRange(companies.Where(c => c.CompanyId != user.Id).Select(CompanyContact));
                result.AddRange((await db.ClientUsers.AsNoTracking().ToListAsync(ct)).Select(ClientContact));
                break;

            case UserRole.ManagerUser:
                var company = companies.FirstOrDefault(c => c.CompanyId == me.CompanyId);
                if (company is not null) result.Add(CompanyContact(company));
                result.AddRange(managers.Where(m => m.CompanyId == me.CompanyId && m.Id != user.Id).Select(ManagerContact));
                var allClients = company?.ChatManagerId == user.Id;
                var clientsQuery = db.ClientUsers.AsNoTracking();
                if (!allClients)
                {
                    var linked = db.CompanyClients.Where(c => c.CompanyId == me.CompanyId).Select(c => c.ClientId);
                    clientsQuery = clientsQuery.Where(c => linked.Contains(c.Id));
                }
                result.AddRange((await clientsQuery.ToListAsync(ct)).Select(ClientContact));
                break;

            default:
                result.AddRange(companies.Select(CompanyContact));
                var linkedCompanies = await db.CompanyClients.AsNoTracking().Where(c => c.ClientId == user.Id)
                    .Select(c => c.CompanyId).ToListAsync(ct);
                var chatManagers = companies.Where(c => c.ChatManagerId != null).Select(c => c.ChatManagerId!.Value).ToHashSet();
                result.AddRange(managers.Where(m => linkedCompanies.Contains(m.CompanyId) || chatManagers.Contains(m.Id))
                    .Select(ManagerContact));
                break;
        }

        // Filtr frazy działa dopiero na liście dozwolonych kontaktów, więc nie da się nim "znaleźć" kogoś spoza reguł czatu.
        var phrase = search?.Trim();
        var filtered = string.IsNullOrEmpty(phrase) ? result : result.Where(c => MatchesContact(c, phrase, mode));
        return filtered.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private const int MaxSearchLength = 100;

    private static bool MatchesContact(ChatContactDto contact, string phrase, ChatContactSearchMode mode)
    {
        if (phrase.Length > MaxSearchLength) phrase = phrase[..MaxSearchLength];
        const StringComparison cmp = StringComparison.CurrentCultureIgnoreCase;
        switch (mode)
        {
            case ChatContactSearchMode.Email:
                return contact.Email is not null && contact.Email.Contains(phrase, cmp);
            default:
                return contact.Name.Contains(phrase, cmp);
        }
    }

    // ---------- Rozmowy ----------

    private async Task<(Conversation Conversation, ConversationParticipant Me)> LoadAsync(
        CurrentUser user, Guid conversationId, CancellationToken ct)
    {
        var conversation = await db.Conversations.Include(c => c.Participants)
                               .FirstOrDefaultAsync(c => c.Id == conversationId, ct)
                           ?? throw new NotFoundException("Nie znaleziono rozmowy.");
        var me = conversation.Participants.FirstOrDefault(p => p.UserId == user.Id && p.UserRole == user.Role)
                 ?? throw new ForbiddenException("Nie masz dostępu do tej rozmowy.");

        if (conversation.Kind == ConversationKind.Direct)
        {
            var other = conversation.Participants.First(p => p != me);
            if (!await CanChatAsync(user, other.UserId, other.UserRole, ct))
                throw new ForbiddenException("Nie możesz rozmawiać z tym użytkownikiem.");
        }
        else if (conversation.CompanyId is null || !await IsCompanyMemberAsync(user, conversation.CompanyId.Value, ct))
        {
            throw new ForbiddenException("Nie jesteś już członkiem tej grupy.");
        }

        return (conversation, me);
    }

    private static ChatMessageDto ToDto(ChatMessage m) =>
        new(m.Id, m.ConversationId, m.SenderId, m.SenderRole, m.SenderName, m.Text, m.SentAt);

    private async Task<ConversationDto> ToConversationDtoAsync(Conversation c, CurrentUser user,
        ConversationParticipant me, Dictionary<(Guid, UserRole), string> names, CancellationToken ct)
    {
        var participants = new List<ChatParticipantDto>();
        foreach (var p in c.Participants)
        {
            if (!names.TryGetValue((p.UserId, p.UserRole), out var name))
                names[(p.UserId, p.UserRole)] = name = await NameAsync(p.UserId, p.UserRole, ct);
            participants.Add(new ChatParticipantDto(p.UserId, p.UserRole, name));
        }

        var title = c.Kind == ConversationKind.CompanyGroup
            ? c.Name ?? "Grupa"
            : participants.FirstOrDefault(p => !(p.UserId == user.Id && p.Role == user.Role))?.Name ?? "Rozmowa";

        var last = await db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == c.Id)
            .OrderByDescending(m => m.SentAt).FirstOrDefaultAsync(ct);
        var lastRead = me.LastReadAt;
        var unread = await db.ChatMessages.AsNoTracking().CountAsync(m => m.ConversationId == c.Id
            && !(m.SenderId == user.Id && m.SenderRole == user.Role) && (lastRead == null || m.SentAt > lastRead), ct);

        return new ConversationDto(c.Id, c.Kind, title, participants, last is null ? null : ToDto(last), unread);
    }

    public async Task<List<ConversationDto>> GetConversationsAsync(CurrentUser user, CancellationToken ct)
    {
        var conversations = await db.Conversations.AsNoTracking().Include(c => c.Participants)
            .Where(c => c.Participants.Any(p => p.UserId == user.Id && p.UserRole == user.Role))
            .ToListAsync(ct);

        var names = new Dictionary<(Guid, UserRole), string>();
        var items = new List<ConversationDto>();
        foreach (var c in conversations)
        {
            // Rozmowy, do których użytkownik utracił uprawnienie, nie są pokazywane (i tak nie da się ich otworzyć).
            if (c.Kind == ConversationKind.Direct)
            {
                var other = c.Participants.First(p => !(p.UserId == user.Id && p.UserRole == user.Role));
                if (!await CanChatAsync(user, other.UserId, other.UserRole, ct)) continue;
            }
            else if (c.CompanyId is null || !await IsCompanyMemberAsync(user, c.CompanyId.Value, ct)) continue;

            var me = c.Participants.First(p => p.UserId == user.Id && p.UserRole == user.Role);
            items.Add(await ToConversationDtoAsync(c, user, me, names, ct));
        }

        return items.OrderByDescending(i => i.LastMessage?.SentAt ?? DateTime.MinValue).ThenBy(i => i.Title).ToList();
    }

    public async Task<ConversationDto> OpenDirectAsync(CurrentUser user, Guid otherId, UserRole otherRole, CancellationToken ct)
    {
        if (!await CanChatAsync(user, otherId, otherRole, ct))
            throw new ForbiddenException("Nie możesz rozpocząć rozmowy z tym użytkownikiem.");

        var key = Conversation.KeyFor((user.Id, user.Role), (otherId, otherRole));
        var conversation = await db.Conversations.Include(c => c.Participants).FirstOrDefaultAsync(c => c.DirectKey == key, ct);
        if (conversation is null)
        {
            conversation = Conversation.CreateDirect((user.Id, user.Role), (otherId, otherRole));
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync(ct);
        }

        var me = conversation.Participants.First(p => p.UserId == user.Id && p.UserRole == user.Role);
        return await ToConversationDtoAsync(conversation, user, me, new(), ct);
    }

    public async Task<ConversationDto> CreateGroupAsync(CurrentUser user, CreateGroupDto dto, CancellationToken ct)
    {
        if (user.IsClient) throw new ForbiddenException("Grupy w firmie tworzą właściciel firmy i managerowie.");
        var me = await FindPartyAsync(user.Id, user.Role, ct) ?? throw new ForbiddenException("Konto jest niedostępne.");
        var companyId = me.CompanyId!.Value;

        var members = new List<(Guid, UserRole)> { (companyId, UserRole.CompanyUser) };
        if (user.IsManager) members.Add((user.Id, UserRole.ManagerUser));
        foreach (var managerId in dto.ManagerIds.Distinct())
        {
            var manager = await FindPartyAsync(managerId, UserRole.ManagerUser, ct);
            if (manager is null || manager.CompanyId != companyId)
                throw new DomainValidationException("Do grupy można dodać tylko niezablokowanych managerów własnej firmy.");
            members.Add((managerId, UserRole.ManagerUser));
        }

        var conversation = Conversation.CreateGroup(companyId, dto.Name, members);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(ct);

        var mine = conversation.Participants.First(p => p.UserId == user.Id && p.UserRole == user.Role);
        return await ToConversationDtoAsync(conversation, user, mine, new(), ct);
    }

    // ---------- Wiadomości ----------

    public async Task<List<ChatMessageDto>> GetMessagesAsync(CurrentUser user, Guid conversationId, DateTime? before,
        int limit, CancellationToken ct)
    {
        await LoadAsync(user, conversationId, ct);
        var take = Math.Clamp(limit <= 0 ? 50 : limit, 1, MaxPageSize);
        var query = db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (before is not null) query = query.Where(m => m.SentAt < before.Value);
        var page = await query.OrderByDescending(m => m.SentAt).Take(take).ToListAsync(ct);
        page.Reverse(); // od najstarszej do najnowszej
        return page.Select(ToDto).ToList();
    }

    public async Task<ChatMessageDto> SendAsync(CurrentUser user, Guid conversationId, string? text, CancellationToken ct)
    {
        var (conversation, me) = await LoadAsync(user, conversationId, ct);
        var sender = await FindPartyAsync(user.Id, user.Role, ct) ?? throw new ForbiddenException("Konto jest niedostępne.");

        var message = ChatMessage.Create(conversation.Id, user.Id, user.Role, sender.Name, text);
        db.ChatMessages.Add(message);
        me.MarkRead(message.SentAt); // własna wiadomość nie jest nieprzeczytana

        // Powiadomienie w dzwonku (klik prowadzi do rozmowy): jedno nieprzeczytane na rozmowę i odbiorcę.
        var notice = conversation.Kind == ConversationKind.CompanyGroup
            ? $"Nowa wiadomość w grupie \"{conversation.Name}\" od {sender.Name}."
            : $"Nowa wiadomość od {sender.Name}.";
        foreach (var other in conversation.Participants.Where(p => p != me))
            notifications.AddChatMessage(other.UserId, other.UserRole, conversation.Id, notice);
        await db.SaveChangesAsync(ct);

        var dto = ToDto(message);
        await realtime.MessageAsync(dto, conversation.Participants.Select(p => new ChatRecipient(p.UserId, p.UserRole)), ct);
        return dto;
    }

    public async Task MarkReadAsync(CurrentUser user, Guid conversationId, CancellationToken ct)
    {
        var (_, me) = await LoadAsync(user, conversationId, ct);
        me.MarkRead(DateTime.UtcNow);

        // Przeczytana rozmowa gasi też swoje powiadomienie o nowych wiadomościach.
        var unread = await db.Notifications.Where(n => n.RecipientId == user.Id && n.RecipientRole == user.Role
                                                       && n.TargetType == NotificationTarget.Chat
                                                       && n.TargetId == conversationId && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) n.MarkRead();
        await db.SaveChangesAsync(ct);
    }
}
