using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Domain.Entities;

/// <summary>Powiadomienie w aplikacji (post, decyzja klienta, nowa wiadomość czatu) dla firmy, managera lub klienta; może prowadzić do celu (post, rozmowa).</summary>
public class Notification
{
    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public UserRole RecipientRole { get; private set; }
    public Guid? PostId { get; private set; }

    /// <summary>Typ celu powiadomienia (v9): dokąd prowadzi kliknięcie. Dla powiadomień o postach: Post (TargetId = GroupId posta).</summary>
    public NotificationTarget TargetType { get; private set; }

    /// <summary>Identyfikator celu: GroupId posta albo identyfikator rozmowy (zależnie od <see cref="TargetType"/>).</summary>
    public Guid? TargetId { get; private set; }
    public string Message { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public bool IsRead { get; private set; }

    private Notification() { }

    /// <summary>Powiadomienie o poście (postId = GroupId posta): kliknięcie prowadzi do posta. Bez postId nie ma celu.</summary>
    public static Notification Create(Guid recipientId, UserRole recipientRole, Guid? postId, string message) =>
        new()
        {
            Id = Guid.NewGuid(), RecipientId = recipientId, RecipientRole = recipientRole, PostId = postId,
            TargetType = postId is null ? NotificationTarget.None : NotificationTarget.Post, TargetId = postId,
            Message = Trim(message), CreatedAt = DateTime.UtcNow
        };

    /// <summary>Powiadomienie o nowej wiadomości czatu: kliknięcie prowadzi do rozmowy.</summary>
    public static Notification CreateChat(Guid recipientId, UserRole recipientRole, Guid conversationId, string message) =>
        new()
        {
            Id = Guid.NewGuid(), RecipientId = recipientId, RecipientRole = recipientRole,
            TargetType = NotificationTarget.Chat, TargetId = conversationId,
            Message = Trim(message), CreatedAt = DateTime.UtcNow
        };

    private static string Trim(string message) => message.Length > 500 ? message[..500] : message;

    public void MarkRead() => IsRead = true;
}
