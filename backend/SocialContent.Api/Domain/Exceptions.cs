namespace SocialContent.Api.Domain;

public class DomainValidationException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);

/// <summary>Konflikt z istniejącymi danymi (np. zajęty e-mail, duplikat konta) - HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Brak lub nieprawidłowe poświadczenia - HTTP 401.</summary>
public class UnauthorizedException(string message) : Exception(message);
