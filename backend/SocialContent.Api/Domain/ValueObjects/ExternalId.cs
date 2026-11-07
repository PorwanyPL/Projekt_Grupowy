using System.Globalization;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.ValueObjects;

/// <summary>
/// Identyfikator obiektu po stronie zewnętrznej platformy (strona Facebooka, konto Instagrama/TikToka, lokalizacja, post).
/// Diagram podaje atrybut "Date: DateTime", ale metody Create(string)/Read(): string wskazują na wartość tekstową
/// - przyjęto atrybut Value: string (patrz założenia).
/// </summary>
public sealed record ExternalId
{
    public string Value { get; }
    private ExternalId(string value) => Value = value;
    public static ExternalId Create(string? value)
    {
        var v = Validate.Text(value, "Identyfikator zewnętrzny", 100);
        if (v.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new DomainValidationException("Identyfikator zewnętrzny nie może zawierać białych znaków.");
        return new ExternalId(v);
    }
    public override string ToString() => Value;
}
