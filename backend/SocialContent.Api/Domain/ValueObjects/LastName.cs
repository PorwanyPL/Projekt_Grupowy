using System.Globalization;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record LastName
{
    public string Value { get; }
    private LastName(string value) => Value = value;
    public static LastName Create(string? value) => new(Validate.Name(value, "Nazwisko"));
    public override string ToString() => Value;
}
