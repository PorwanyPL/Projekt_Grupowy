using System.Globalization;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record Description
{
    public string Value { get; }
    public int Length => Value.Length;
    private Description(string value) => Value = value;
    public static Description Create(string? value) => new(Validate.Text(value, "Opis", 2000));
    public override string ToString() => Value;
}
