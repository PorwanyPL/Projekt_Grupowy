using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Enums;

namespace SocialContent.Api.Dtos;

/// <summary>Wybór klienta: kto akceptuje posty przed publikacją na jego kontach zarządzanych przez daną firmę.</summary>
public class ApprovalModeDto
{
    [Required] public PostApprovalMode? Mode { get; set; }
}
