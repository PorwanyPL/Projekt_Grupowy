using System.Globalization;
using SocialContent.Api.Domain.Exceptions;

namespace SocialContent.Api.Domain.ValueObjects;

public sealed record WebUrl
{
    public string Url { get; }
    private WebUrl(string url) => Url = url;
    public static WebUrl Create(string? value)
    {
        var v = Validate.Text(value, "Adres URL", 2000);
        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainValidationException("Adres URL musi być bezwzględnym adresem http lub https.");
        return new WebUrl(v);
    }
    public override string ToString() => Url;
}
