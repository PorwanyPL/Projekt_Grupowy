using System.Globalization;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record FileName
{
    private static readonly char[] Invalid = Path.GetInvalidFileNameChars();
    public string Value { get; }
    private FileName(string value) => Value = value;
    public static FileName Create(string? value)
    {
        var v = Validate.Text(value, "Nazwa pliku", 150);
        if (v.IndexOfAny(Invalid) >= 0)
            throw new DomainValidationException("Nazwa pliku zawiera niedozwolone znaki.");
        return new FileName(v);
    }
    public override string ToString() => Value;
}
