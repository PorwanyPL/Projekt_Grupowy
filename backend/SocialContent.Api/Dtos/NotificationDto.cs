using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Powiadomienie. TargetType + TargetId wskazują, dokąd prowadzi kliknięcie (Post: GroupId posta, Chat: id rozmowy, None: nigdzie).</summary>
public record NotificationDto(
    Guid Id,
    Guid? PostId,
    string Message,
    DateTime CreatedAt,
    bool IsRead,
    NotificationTarget TargetType = NotificationTarget.None,
    Guid? TargetId = null);
