using Microsoft.Extensions.Configuration;

namespace StorageBoxKeyTool.Api.Domain;

internal static class NonLocalHostWarningSettings
{
    private const string ConfigKey = "StorageBox:DisableNonLocalHostWarning";
    private const string EnvironmentKey = "STORAGEBOX_DISABLE_NON_LOCAL_WARNING";

    public static bool IsSuppressed(IConfiguration configuration)
    {
        if (TryParseBoolean(configuration[ConfigKey], out var suppressFromSection))
        {
            return suppressFromSection;
        }

        if (TryParseBoolean(configuration[EnvironmentKey], out var suppressFromEnvironment))
        {
            return suppressFromEnvironment;
        }

        return false;
    }

    internal static bool TryParseBoolean(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "1":
            case "true":
            case "yes":
            case "on":
                value = true;
                return true;
            case "0":
            case "false":
            case "no":
            case "off":
                value = false;
                return true;
            default:
                return false;
        }
    }
}
