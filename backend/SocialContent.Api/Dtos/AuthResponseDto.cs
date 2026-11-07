namespace SocialContent.Api.Dtos;

/// <summary>Wynik rejestracji lub logowania.</summary>
/// <param name="AccessToken">Token JWT; przekazuj w nagłówku Authorization: Bearer.</param>
/// <param name="TokenType">Zawsze "Bearer".</param>
/// <param name="ExpiresAt">Moment wygaśnięcia tokenu (UTC).</param>
public record AuthResponseDto(string AccessToken, string TokenType, DateTime ExpiresAt, UserProfileDto User);
