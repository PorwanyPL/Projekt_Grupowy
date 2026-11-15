namespace SocialContent.Api.Dtos;

/// <summary>Klient (ClientUser) widziany przez firmę (lub jej managera), z którą jest powiązany.</summary>
/// <param name="Alias">Pseudonim nadany klientowi przez firmę; null, gdy firma go nie ustawiła.</param>
/// <param name="DisplayName">Nazwa do wyświetlania w widokach firmy: pseudonim albo imię i nazwisko.</param>
/// <param name="PendingInvitations">Liczba kont klienta czekających na decyzję firmy (zaproszenia).</param>
public record ClientUserDto(
    Guid Id, string FirstName, string LastName, int Age, string Description, string Email,
    string? Alias = null, string DisplayName = "", int PendingInvitations = 0);
