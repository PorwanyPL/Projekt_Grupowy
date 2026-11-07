namespace SocialContent.Api.Dtos;

public record NotificationDto(Guid Id, Guid? PostId, string Message, DateTime CreatedAt, bool IsRead);
