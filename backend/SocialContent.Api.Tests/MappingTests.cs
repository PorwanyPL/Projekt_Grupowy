namespace SocialContent.Api.Tests;

public class MappingTests
{
    /// <summary>Wszystkie profile z folderu Mapping muszą mieć pełną i poprawną konfigurację.</summary>
    [Fact]
    public void AutoMapperConfiguration_IsValid() => TestDb.MapperConfig.AssertConfigurationIsValid();
}
