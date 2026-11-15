using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Pseudonim klienta nadawany przez firmę. Pusty lub pominięty usuwa pseudonim.</summary>
public class ClientAliasDto
{
    [StringLength(100)] public string? Alias { get; set; }
}
