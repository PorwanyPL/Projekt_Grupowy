using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Domain.Entities;

/// <summary>Powiadomienie w aplikacji (np. o nieudanej publikacji posta) dla administratora firmy lub managera.</summary>
public class Notification
{
    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public UserRole RecipientRole { get; private set; }
    public Guid? PostId { get; private set; }
    public string Message { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public bool IsRead { get; private set; }

    private Notification() { }

    public static Notification Create(Guid recipientId, UserRole recipientRole, Guid? postId, string message) =>
        new()
        {
            Id = Guid.NewGuid(), RecipientId = recipientId, RecipientRole = recipientRole, PostId = postId,
            Message = message.Length > 500 ? message[..500] : message, CreatedAt = DateTime.UtcNow
        };

    public void MarkRead() => IsRead = true;
}
