using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.Entities;

/// <summary>
/// Rozmowa czatu (v8). Rozmowa 1 na 1 ma <see cref="DirectKey"/> (para uczestników w stałej kolejności), więc dwóch użytkowników
/// ma najwyżej jedną rozmowę. Grupa jest zawsze w obrębie jednej firmy (<see cref="CompanyId"/>) i ma nazwę.
/// Uczestnicy to osoby z trzech tabel użytkowników, dlatego identyfikuje je para (UserId, UserRole), jak w powiadomieniach.
/// </summary>
public class Conversation
{
    public const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public ConversationKind Kind { get; private set; }
    public string? Name { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string? DirectKey { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public List<ConversationParticipant> Participants { get; private set; } = new();
    public List<ChatMessage> Messages { get; private set; } = new();

    private Conversation() { }

    /// <summary>Klucz pary uczestników niezależny od kolejności.</summary>
    public static string KeyFor((Guid Id, UserRole Role) a, (Guid Id, UserRole Role) b)
    {
        var x = $"{a.Role}:{a.Id:N}";
        var y = $"{b.Role}:{b.Id:N}";
        return string.CompareOrdinal(x, y) <= 0 ? $"{x}|{y}" : $"{y}|{x}";
    }

    public static Conversation CreateDirect((Guid Id, UserRole Role) a, (Guid Id, UserRole Role) b)
    {
        if (a == b) throw new DomainValidationException("Nie można rozmawiać z samym sobą.");
        var c = new Conversation
        {
            Id = Guid.NewGuid(), Kind = ConversationKind.Direct, DirectKey = KeyFor(a, b), CreatedAt = DateTime.UtcNow
        };
        c.Participants.Add(ConversationParticipant.Create(c.Id, a.Id, a.Role));
        c.Participants.Add(ConversationParticipant.Create(c.Id, b.Id, b.Role));
        return c;
    }

    public static Conversation CreateGroup(Guid companyId, string name, IEnumerable<(Guid Id, UserRole Role)> members)
    {
        var c = new Conversation
        {
            Id = Guid.NewGuid(), Kind = ConversationKind.CompanyGroup, CompanyId = companyId,
            Name = Validate.Text(name, "Nazwa grupy", MaxNameLength), CreatedAt = DateTime.UtcNow
        };
        foreach (var m in members.Distinct())
            c.Participants.Add(ConversationParticipant.Create(c.Id, m.Id, m.Role));
        if (c.Participants.Count < 2)
            throw new DomainValidationException("Grupa musi mieć co najmniej dwóch uczestników.");
        return c;
    }
}

/// <summary>Uczestnik rozmowy wraz ze znacznikiem ostatnio przeczytanej wiadomości (licznik nieprzeczytanych).</summary>
public class ConversationParticipant
{
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public UserRole UserRole { get; private set; }
    public DateTime? LastReadAt { get; private set; }

    private ConversationParticipant() { }

    public static ConversationParticipant Create(Guid conversationId, Guid userId, UserRole role) =>
        new() { ConversationId = conversationId, UserId = userId, UserRole = role };

    public void MarkRead(DateTime at) => LastReadAt = at;
}

/// <summary>Wiadomość czatu. Nazwa nadawcy jest zapisana w chwili wysłania (historia nie zależy od późniejszych zmian danych).</summary>
public class ChatMessage
{
    public const int MaxTextLength = 2000;

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public UserRole SenderRole { get; private set; }
    public string SenderName { get; private set; } = null!;
    public string Text { get; private set; } = null!;
    public DateTime SentAt { get; private set; }

    private ChatMessage() { }

    public static ChatMessage Create(Guid conversationId, Guid senderId, UserRole senderRole, string senderName,
        string? text, DateTime? sentAt = null) =>
        new()
        {
            Id = Guid.NewGuid(), ConversationId = conversationId, SenderId = senderId, SenderRole = senderRole,
            SenderName = senderName.Length > 200 ? senderName[..200] : senderName,
            Text = Validate.Text(text, "Wiadomość", MaxTextLength), SentAt = sentAt ?? DateTime.UtcNow
        };
}
