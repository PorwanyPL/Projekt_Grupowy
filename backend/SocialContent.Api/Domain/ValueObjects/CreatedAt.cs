using System.Globalization;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record CreatedAt
{
    public DateTime Date { get; }
    private CreatedAt(DateTime date) => Date = date;

    public static CreatedAt Create(DateTime date)
    {
        // Data utworzenia nie może pochodzić z przyszłości (tolerancja 5 minut na różnice zegarów).
        if (!Validate.ValidDateFormat(date) || date > DateTime.UtcNow.AddMinutes(5))
            throw new DomainValidationException("Nieprawidłowa data utworzenia.");
        return new CreatedAt(date);
    }

    public static CreatedAt Create(string? value) => Create(Validate.ParseIsoDate(value));
}
