using System.Globalization;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record Title
{
    public string Value { get; }
    private Title(string value) => Value = value;
    public static Title Create(string? value) => new(Validate.Text(value, "Tytuł", 120));
    public override string ToString() => Value;
}
