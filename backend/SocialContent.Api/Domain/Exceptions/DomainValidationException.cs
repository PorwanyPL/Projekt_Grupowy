namespace SocialContent.Api.Domain.Exceptions;

public class DomainValidationException(string message) : Exception(message);
