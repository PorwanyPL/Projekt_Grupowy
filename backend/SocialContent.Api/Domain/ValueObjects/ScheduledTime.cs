using System.Globalization;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.ValueObjects;

/// <summary>Termin publikacji (ScheduledAt) lub faktyczny czas publikacji (PublishedAt). Zawsze w UTC.</summary>
public sealed record ScheduledTime
{
    public DateTime Date { get; }
    private ScheduledTime(DateTime date) => Date = date;

    public static ScheduledTime Create(DateTime date)
    {
        if (!Validate.ValidDateFormat(date))
            throw new DomainValidationException("Nieprawidłowa data (oczekiwano daty od roku 2000).");
        return new ScheduledTime(Validate.ToUtc(date));
    }

    public static ScheduledTime Create(string? value) => Create(Validate.ParseIsoDate(value));
}
