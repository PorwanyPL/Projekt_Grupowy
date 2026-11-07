using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Dane managera przy tworzeniu i pełnej aktualizacji (wszystkie pola wymagane).</summary>
public class ManagerUserWriteDto
{
    /// <summary>Imię: 1-50 znaków, litery, myślnik lub apostrof.</summary>
    [Required, StringLength(50, MinimumLength = 1)] public string FirstName { get; set; } = "";

    /// <summary>Nazwisko: 1-50 znaków, litery, myślnik lub apostrof.</summary>
    [Required, StringLength(50, MinimumLength = 1)] public string LastName { get; set; } = "";

    /// <summary>Wiek w latach, od 18 do 100.</summary>
    [Range(18, 100)] public int Age { get; set; }

    /// <summary>Opis managera, 1-2000 znaków.</summary>
    [Required, StringLength(2000, MinimumLength = 1)] public string Description { get; set; } = "";
}
