namespace SocialContent.Api.Dtos;

/// <summary>Klient (ClientUser) widziany przez firmę, z którą jest powiązany.</summary>
public record ClientUserDto(Guid Id, string FirstName, string LastName, int Age, string Description, string Email);
