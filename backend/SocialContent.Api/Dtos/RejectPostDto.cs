using System.ComponentModel.DataAnnotations;
using SocialContent.Api.Domain.Entities;

namespace SocialContent.Api.Dtos;

/// <summary>Odrzucenie posta przez klienta: opcjonalne uwagi z poprawkami.</summary>
public class RejectPostDto
{
    /// <summary>Uwagi dla autorów (co poprawić); do 1000 znaków, może być puste.</summary>
    [StringLength(Post.MaxRejectionCommentLength)]
    public string? Comment { get; set; }
}
