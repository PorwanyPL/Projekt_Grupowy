using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Samodzielna rejestracja klienta (ClientUser): dane osobowe (jak w ManagerUserWriteDto) oraz dane logowania.</summary>
public class RegisterClientDto : ManagerUserWriteDto
{
    /// <summary>Adres e-mail będący identyfikatorem logowania; unikalny w całym systemie, wielkość liter nie ma znaczenia.</summary>
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";

    /// <summary>Hasło: 8-128 znaków, co najmniej jedna mała litera, wielka litera i cyfra.</summary>
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = "";
}
