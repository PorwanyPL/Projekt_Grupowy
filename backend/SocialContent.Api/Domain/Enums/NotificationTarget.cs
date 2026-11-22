namespace SocialContent.Api.Domain.Enums;

/// <summary>Do czego prowadzi powiadomienie po kliknięciu (typ celu; identyfikator jest w Notification.TargetId). Zapisywane jako tekst.</summary>
public enum NotificationTarget
{
    /// <summary>Brak celu - powiadomienie tylko informuje.</summary>
    None,

    /// <summary>Post: TargetId to identyfikator logicznego posta (GroupId).</summary>
    Post,

    /// <summary>Czat: TargetId to identyfikator rozmowy.</summary>
    Chat
}
