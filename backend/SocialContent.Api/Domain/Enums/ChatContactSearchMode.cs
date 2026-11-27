namespace SocialContent.Api.Domain.Enums;

/// <summary>Sposób dopasowania frazy przy wyszukiwaniu rozmówcy w czacie (bez względu na wielkość liter).</summary>
public enum ChatContactSearchMode
{
    /// <summary>Fraza występuje w dowolnym miejscu nazwy (fragment nazwy).</summary>
    Contains,

    /// <summary>Fraza występuje w adresie e-mail.</summary>
    Email
}
