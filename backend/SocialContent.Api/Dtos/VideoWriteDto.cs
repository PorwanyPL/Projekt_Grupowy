using System.ComponentModel.DataAnnotations;

namespace SocialContent.Api.Dtos;

/// <summary>Wideo dodawane do posta: odwołanie do pliku przesłanego wcześniej przez POST /api/Media. Format i rozmiar ustala serwer.</summary>
public class VideoWriteDto
{
    /// <summary>Klucz pliku zwrócony przez POST /api/Media (storageKey).</summary>
    [Required, StringLength(60)] public string StorageKey { get; set; } = "";

    /// <summary>Nazwa pliku pokazywana użytkownikom (zwykle oryginalna nazwa z przesłania).</summary>
    [Required, StringLength(150, MinimumLength = 1)] public string FileName { get; set; } = "";
}
