namespace SocialContent.Api.Domain.Enums;

/// <summary>Sposób dopasowania frazy przy wyszukiwaniu rozmówcy w czacie (bez względu na wielkość liter).</summary>
public enum ChatContactSearchMode
{
    /// <summary>Nazwa albo dowolny jej człon (imię, nazwisko, słowo nazwy firmy) zaczyna się od frazy.</summary>
    Prefix,

    /// <summary>Fraza występuje w dowolnym miejscu nazwy.</summary>
    Contains,

    /// <summary>Fraza występuje w adresie e-mail.</summary>
    Email
}
