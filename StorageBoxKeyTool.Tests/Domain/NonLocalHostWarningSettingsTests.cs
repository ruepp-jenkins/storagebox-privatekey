using Microsoft.Extensions.Configuration;
using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Tests.Domain;

public sealed class NonLocalHostWarningSettingsTests
{
    [Fact]
    public void IsSuppressed_WithNoConfiguredValues_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var result = NonLocalHostWarningSettings.IsSuppressed(configuration);

        Assert.False(result);
    }

    [Fact]
    public void IsSuppressed_WithConfigSectionValueTrue_ReturnsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageBox:DisableNonLocalHostWarning"] = "true"
            })
            .Build();

        var result = NonLocalHostWarningSettings.IsSuppressed(configuration);

        Assert.True(result);
    }

    [Fact]
    public void IsSuppressed_WithEnvironmentStyleValueOne_ReturnsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STORAGEBOX_DISABLE_NON_LOCAL_WARNING"] = "1"
            })
            .Build();

        var result = NonLocalHostWarningSettings.IsSuppressed(configuration);

        Assert.True(result);
    }

    [Fact]
    public void IsSuppressed_WithInvalidSectionValueAndEnvironmentFalse_UsesEnvironmentFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageBox:DisableNonLocalHostWarning"] = "maybe",
                ["STORAGEBOX_DISABLE_NON_LOCAL_WARNING"] = "false"
            })
            .Build();

        var result = NonLocalHostWarningSettings.IsSuppressed(configuration);

        Assert.False(result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    [InlineData("0", false)]
    public void TryParseBoolean_WithSupportedValues_ReturnsParsedValue(string raw, bool expected)
    {
        var canParse = NonLocalHostWarningSettings.TryParseBoolean(raw, out var parsed);

        Assert.True(canParse);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void TryParseBoolean_WithUnknownValue_ReturnsFalse()
    {
        var canParse = NonLocalHostWarningSettings.TryParseBoolean("sometimes", out var parsed);

        Assert.False(canParse);
        Assert.False(parsed);
    }
}
