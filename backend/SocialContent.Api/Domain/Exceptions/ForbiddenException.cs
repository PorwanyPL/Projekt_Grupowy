namespace SocialContent.Api.Domain.Exceptions;

/// <summary>Użytkownik jest uwierzytelniony, ale jego rola nie pozwala na daną operację - HTTP 403.</summary>
public class ForbiddenException(string message) : Exception(message);
