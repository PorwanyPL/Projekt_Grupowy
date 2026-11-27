using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Włączenie lub wyłączenie weryfikacji dwuetapowej (wymaga potwierdzenia hasłem).</summary>
public class TwoFactorSettingDto
{
    /// <summary>True = włącz, false = wyłącz.</summary>
    public bool Enabled { get; set; }

    /// <summary>Aktualne hasło użytkownika.</summary>
    [Required, StringLength(128)]
    public string Password { get; set; } = "";
}
