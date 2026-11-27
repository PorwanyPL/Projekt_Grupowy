using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Drugi krok logowania: kod z e-maila.</summary>
public class VerifyCodeDto
{
    /// <summary>Identyfikator wyzwania zwrócony przez POST /api/Auth/login.</summary>
    [Required]
    public Guid ChallengeId { get; set; }

    /// <summary>6-cyfrowy kod z wiadomości e-mail.</summary>
    [Required, StringLength(10)]
    public string Code { get; set; } = "";
}
