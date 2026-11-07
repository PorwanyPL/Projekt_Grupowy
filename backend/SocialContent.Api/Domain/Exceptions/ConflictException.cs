namespace SocialContent.Api.Domain.Exceptions;

/// <summary>Konflikt z istniejącymi danymi (np. zajęty e-mail, duplikat konta) - HTTP 409.</summary>
public class ConflictException(string message) : Exception(message);
