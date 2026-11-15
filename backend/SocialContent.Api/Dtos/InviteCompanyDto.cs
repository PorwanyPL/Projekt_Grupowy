using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Zaproszenie firmy do zarządzania kontem społecznościowym; firmę wskazuje się adresem e-mail jej konta.</summary>
public class InviteCompanyDto
{
    [Required, EmailAddress, StringLength(254)] public string CompanyEmail { get; set; } = "";
}
