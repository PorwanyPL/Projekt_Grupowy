namespace SocialContent.Api.Dtos;

/// <summary>Blokada edycji posta.</summary>
/// <param name="HolderName">Kto edytuje post (imię i nazwisko albo nazwa firmy).</param>
/// <param name="ExpiresAt">Kiedy blokada wygaśnie, jeśli nie zostanie odnowiona (UTC).</param>
/// <param name="HeldByMe">Czy blokadę ma zalogowany użytkownik.</param>
public record PostLockDto(string HolderName, DateTime ExpiresAt, bool HeldByMe = false);
