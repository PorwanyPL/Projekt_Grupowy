using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Powiązanie istniejącego klienta z firmą po adresie e-mail, którego klient użył przy rejestracji.</summary>
public class LinkClientDto
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
}
