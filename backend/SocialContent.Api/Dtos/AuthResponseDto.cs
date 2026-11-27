namespace SocialContent.Api.Dtos;

/// <summary>Wynik rejestracji lub logowania (także pierwszego kroku logowania z weryfikacją dwuetapową).</summary>
/// <param name="AccessToken">Token JWT; przekazuj w nagłówku Authorization: Bearer. Pusty, gdy wymagany jest kod (TwoFactorRequired).</param>
/// <param name="TokenType">Zawsze "Bearer".</param>
/// <param name="ExpiresAt">Moment wygaśnięcia tokenu (UTC).</param>
/// <param name="User">Profil zalogowanego; null, gdy wymagany jest jeszcze kod.</param>
/// <param name="TwoFactorRequired">True: hasło poprawne, ale trzeba jeszcze wywołać POST /api/Auth/login/verify-code z kodem z e-maila.</param>
/// <param name="ChallengeId">Identyfikator wyzwania do przekazania przy weryfikacji kodu.</param>
/// <param name="MaskedEmail">Zamaskowany adres, na który wysłano kod (np. k***@demo.pl).</param>
public record AuthResponseDto(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAt,
    UserProfileDto? User,
    bool TwoFactorRequired = false,
    Guid? ChallengeId = null,
    string? MaskedEmail = null);
