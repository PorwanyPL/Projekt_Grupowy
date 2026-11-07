using System.Globalization;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record FirstName
{
    public string Value { get; }
    private FirstName(string value) => Value = value;
    public static FirstName Create(string? value) => new(Validate.Name(value, "Imię"));
    public override string ToString() => Value;
}
