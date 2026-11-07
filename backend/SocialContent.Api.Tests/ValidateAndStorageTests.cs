using SocialContent.Api.Domain;
using SocialContent.Api.Domain.Entities;
using SocialContent.Api.Domain.Enums;
using SocialContent.Api.Domain.Exceptions;
using SocialContent.Api.Services;

namespace SocialContent.Api.Tests;

public class ValidateAndStorageTests
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

    [Fact]
    public void MediaUpload_AcceptsPngWithMatchingSignature()
    {
        Assert.Equal(Format.Png, Validate.MediaUpload("foto.PNG", Png.Length, Png));
    }

    [Fact]
    public void MediaUpload_RejectsWrongExtensionSignatureAndSize()
    {
        Assert.Throws<DomainValidationException>(() => Validate.MediaUpload("plik.exe", 10, Png));
        Assert.Throws<DomainValidationException>(() => Validate.MediaUpload("foto.jpg", Png.Length, Png)); // PNG udający JPG
        Assert.Throws<DomainValidationException>(() => Validate.MediaUpload("foto.png", 0, Png));
        Assert.Throws<DomainValidationException>(() => Validate.MediaUpload("foto.png", Validate.MaxImageBytes + 1, Png));
    }

    [Fact]
    public void NormalizeTags_TrimsAndRemovesEmptyAndDuplicates()
    {
        var tags = Validate.NormalizeTags(new[] { " Moda ", "moda", "Bio", " " });
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void StorageKey_RejectsPathTraversal()
    {
        Assert.Throws<DomainValidationException>(() => Validate.StorageKey("../../etc/passwd"));
        Assert.False(Validate.IsStorageKey("..\\x.png"));
    }

    [Fact]
    public async Task MediaStorage_SavesPerCompany_AndFindsAndDeletes()
    {
        var root = Path.Combine(Path.GetTempPath(), "sc-media-" + Guid.NewGuid());
        try
        {
            var storage = new MediaStorage(root);
            var company = Guid.NewGuid();
            await using var stream = new MemoryStream(Png);
            var saved = await storage.SaveAsync(company, stream, "foto.png", Png.Length, CancellationToken.None);

            Assert.Equal(MediaKind.Picture, saved.Kind);
            Assert.NotNull(storage.Find(company, saved.StorageKey));
            Assert.Null(storage.Find(Guid.NewGuid(), saved.StorageKey)); // inna firma nie widzi pliku

            storage.Delete(company, saved.StorageKey);
            Assert.Null(storage.Find(company, saved.StorageKey));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void VerifyConnection_ReportsExpiredToken()
    {
        var account = SocialAccount.Create(Guid.NewGuid(), "a", SocialPlatform.Facebook, "ext", "Konto", "token",
            null, DateTime.UtcNow.AddDays(-1), true, new List<ManagerUser>());
        Assert.False(Validate.VerifyConnection(account, out var error));
        Assert.Contains("wygasł", error);
    }
}
