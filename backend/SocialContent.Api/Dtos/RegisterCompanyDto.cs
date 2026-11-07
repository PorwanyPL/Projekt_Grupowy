using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Rejestracja administratora firmy (CompanyUser): nazwa firmy oraz dane logowania.</summary>
public class RegisterCompanyDto
{
    /// <summary>Nazwa firmy, 1-150 znaków.</summary>
    [Required, StringLength(150, MinimumLength = 1)] public string Name { get; set; } = "";

    /// <summary>Adres e-mail będący identyfikatorem logowania; unikalny w całym systemie, wielkość liter nie ma znaczenia.</summary>
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";

    /// <summary>Hasło: 8-128 znaków, co najmniej jedna mała litera, wielka litera i cyfra.</summary>
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = "";
}
