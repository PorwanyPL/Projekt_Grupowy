using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Zmiana danych logowania managera przez firmę.</summary>
public class ManagerCredentialsUpdateDto
{
    /// <summary>Adres e-mail logowania; unikalny w całym systemie, wielkość liter nie ma znaczenia.</summary>
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";

    /// <summary>Nowe hasło (8-128 znaków, mała i wielka litera, cyfra). Pominięte lub puste = hasło bez zmian.</summary>
    [StringLength(128)] public string? Password { get; set; }
}
