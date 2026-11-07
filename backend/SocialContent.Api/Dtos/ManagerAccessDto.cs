using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Żądanie zablokowania lub odblokowania dostępu managera.</summary>
public class ManagerAccessDto
{
    /// <summary>true = zablokuj dostęp, false = odblokuj.</summary>
    [Required] public bool? Blocked { get; set; }
}
