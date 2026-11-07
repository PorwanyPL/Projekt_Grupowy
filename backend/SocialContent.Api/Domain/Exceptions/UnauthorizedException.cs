namespace SocialContent.Api.Domain.Exceptions;

/// <summary>Brak lub nieprawidłowe poświadczenia - HTTP 401.</summary>
public class UnauthorizedException(string message) : Exception(message);
