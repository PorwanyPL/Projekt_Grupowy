using System.Globalization;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record Age
{
    public int Value { get; }
    private Age(int value) => Value = value;
    public static Age Create(int value)
    {
        if (value is < 18 or > 100)
            throw new DomainValidationException("Wiek musi mieścić się w przedziale 18-100.");
        return new Age(value);
    }
}
