namespace SocialContent.Api.Domain;

public class DomainValidationException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);
