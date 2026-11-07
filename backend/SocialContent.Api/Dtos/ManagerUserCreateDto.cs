using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Dane nowego managera tworzonego przez firmę. Konto logowania (e-mail i hasło) powstaje zawsze razem z managerem.</summary>
public class ManagerUserCreateDto : ManagerUserWriteDto
{
    /// <summary>Adres e-mail będący identyfikatorem logowania; unikalny w całym systemie, wielkość liter nie ma znaczenia.</summary>
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";

    /// <summary>Hasło: 8-128 znaków, co najmniej jedna mała litera, wielka litera i cyfra.</summary>
    [Required, StringLength(128)] public string Password { get; set; } = "";
}
