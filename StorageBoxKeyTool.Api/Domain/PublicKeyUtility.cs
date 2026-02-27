using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StorageBoxKeyTool.Api.Domain;

internal static partial class PublicKeyUtility
{
    private const string BeginSsh2PublicKeyMarker = "---- BEGIN SSH2 PUBLIC KEY ----";
    private const string EndSsh2PublicKeyMarker = "---- END SSH2 PUBLIC KEY ----";
    private const string BackrestCommandPrefix = "command=\"rclone serve restic --stdio ";

    public static string NormalizeOpenSshPublicKey(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new ValidationException("Public key is required.");
        }

        var normalizedInput = rawKey.Trim().Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalizedInput.Contains("BEGIN SSH2 PUBLIC KEY", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("RFC4716 public keys are not supported for port 23. Please provide a one-line OpenSSH public key.");
        }

        var firstLine = normalizedInput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            throw new ValidationException("Public key is empty.");
        }

        var parts = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new ValidationException("Public key must contain algorithm and base64 key data.");
        }

        if (!AlgorithmRegex().IsMatch(parts[0]))
        {
            throw new ValidationException("Unsupported key algorithm. Allowed: ssh-ed25519, ecdsa-sha2-nistp256/384/521, ssh-rsa.");
        }

        try
        {
            _ = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            throw new ValidationException("Public key base64 payload is invalid.");
        }

        if (parts.Length == 2)
        {
            return $"{parts[0]} {parts[1]}";
        }

        return $"{parts[0]} {parts[1]} {parts[2].Trim()}";
    }

    public static string ToAuthorizedKeysContent(string normalizedPublicKey)
    {
        return $"{normalizedPublicKey}\n";
    }

    public static string ToBackrestResticAuthorizedKeysContent(
        string normalizedPublicKey,
        string login,
        string host,
        string rootDirectory)
    {
        var (algorithm, payload) = ParseKeyParts(normalizedPublicKey);
        var loginAtHost = $"{login}@{host}";
        var commandLine = BuildBackrestCommandLine(rootDirectory, algorithm, payload, loginAtHost);

        var builder = new StringBuilder();
        builder.AppendLine(BeginSsh2PublicKeyMarker);
        builder.AppendLine($"Comment: \"{loginAtHost}\"");

        foreach (var payloadChunk in WrapText(payload, 70))
        {
            builder.AppendLine(payloadChunk);
        }

        builder.AppendLine(EndSsh2PublicKeyMarker);
        builder.AppendLine(commandLine);

        return builder.ToString();
    }

    public static bool ContainsPublicKey(string? authorizedKeysContent, string normalizedPublicKey)
    {
        if (string.IsNullOrWhiteSpace(authorizedKeysContent))
        {
            return false;
        }

        var (algorithm, payload) = ParseKeyParts(normalizedPublicKey);

        foreach (var line in NormalizeLines(authorizedKeysContent))
        {
            if (!TryExtractNormalizedOpenSshPublicKeyFromAuthorizedLine(line, out var existingKey))
            {
                continue;
            }

            var (existingAlgorithm, existingPayload) = ParseKeyParts(existingKey);
            if (string.Equals(existingAlgorithm, algorithm, StringComparison.Ordinal)
                && string.Equals(existingPayload, payload, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsBackrestResticEntry(string? authorizedKeysContent, string normalizedPublicKey, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(authorizedKeysContent))
        {
            return false;
        }

        var (algorithm, payload) = ParseKeyParts(normalizedPublicKey);

        foreach (var line in NormalizeLines(authorizedKeysContent))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith(BackrestCommandPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var endOfCommand = trimmedLine.IndexOf('"', BackrestCommandPrefix.Length);
            if (endOfCommand < 0)
            {
                continue;
            }

            var configuredRoot = trimmedLine[BackrestCommandPrefix.Length..endOfCommand];
            if (!string.Equals(configuredRoot, rootDirectory, StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryExtractNormalizedOpenSshPublicKeyFromAuthorizedLine(trimmedLine, out var existingKey))
            {
                continue;
            }

            var (existingAlgorithm, existingPayload) = ParseKeyParts(existingKey);
            if (string.Equals(existingAlgorithm, algorithm, StringComparison.Ordinal)
                && string.Equals(existingPayload, payload, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsSsh2PublicKeyBlock(string? authorizedKeysContent, string normalizedPublicKey)
    {
        if (string.IsNullOrWhiteSpace(authorizedKeysContent))
        {
            return false;
        }

        var (_, payload) = ParseKeyParts(normalizedPublicKey);
        var lines = NormalizeLines(authorizedKeysContent);

        for (var index = 0; index < lines.Count; index++)
        {
            if (!string.Equals(lines[index].Trim(), BeginSsh2PublicKeyMarker, StringComparison.Ordinal))
            {
                continue;
            }

            var endIndex = -1;
            for (var innerIndex = index + 1; innerIndex < lines.Count; innerIndex++)
            {
                if (string.Equals(lines[innerIndex].Trim(), EndSsh2PublicKeyMarker, StringComparison.Ordinal))
                {
                    endIndex = innerIndex;
                    break;
                }
            }

            if (endIndex < 0)
            {
                break;
            }

            var payloadLines = lines
                .Skip(index + 1)
                .Take(endIndex - index - 1)
                .Where(line => !line.TrimStart().StartsWith("Comment:", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            var blockPayload = string.Concat(payloadLines);
            if (string.Equals(blockPayload, payload, StringComparison.Ordinal))
            {
                return true;
            }

            index = endIndex;
        }

        return false;
    }

    public static string MergeAuthorizedKeysContent(
        string? existingContent,
        string normalizedPublicKey,
        string login,
        string host,
        bool backrestResticCompatible,
        string rootDirectory)
    {
        if (backrestResticCompatible)
        {
            return MergeBackrestResticContent(existingContent, normalizedPublicKey, login, host, rootDirectory);
        }

        return MergePlainKeyContent(existingContent, normalizedPublicKey);
    }

    public static string NormalizeAuthorizedKeysContent(string? authorizedKeysContent)
    {
        if (string.IsNullOrEmpty(authorizedKeysContent))
        {
            return string.Empty;
        }

        return authorizedKeysContent
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }

    public static string ComputeSha256Fingerprint(string normalizedPublicKey)
    {
        var parts = normalizedPublicKey.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new ValidationException("Invalid public key format for fingerprint calculation.");
        }

        var payload = Convert.FromBase64String(parts[1]);
        var hash = SHA256.HashData(payload);
        var fingerprint = Convert.ToBase64String(hash).TrimEnd('=');

        return $"SHA256:{fingerprint}";
    }

    public static string? TryComputePrimaryFingerprint(string? authorizedKeysContent)
    {
        var normalizedPublicKey = TryExtractPrimaryPublicKey(authorizedKeysContent);
        if (string.IsNullOrWhiteSpace(normalizedPublicKey))
        {
            return null;
        }

        try
        {
            return ComputeSha256Fingerprint(normalizedPublicKey);
        }
        catch (ValidationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string? TryExtractPrimaryPublicKey(string? authorizedKeysContent)
    {
        if (string.IsNullOrWhiteSpace(authorizedKeysContent))
        {
            return null;
        }

        foreach (var line in NormalizeLines(authorizedKeysContent))
        {
            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (!TryExtractNormalizedOpenSshPublicKeyFromAuthorizedLine(line, out var normalized))
            {
                continue;
            }

            try
            {
                return NormalizeOpenSshPublicKey(normalized);
            }
            catch (ValidationException)
            {
                continue;
            }
            catch (FormatException)
            {
                continue;
            }
        }

        return null;
    }

    private static string MergePlainKeyContent(string? existingContent, string normalizedPublicKey)
    {
        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return ToAuthorizedKeysContent(normalizedPublicKey);
        }

        var lines = NormalizeLines(existingContent);
        var keyLineIndex = FindFirstKeyLineIndex(lines);

        if (keyLineIndex >= 0)
        {
            lines[keyLineIndex] = normalizedPublicKey;
        }
        else
        {
            lines.Add(normalizedPublicKey);
        }

        return string.Join('\n', lines) + "\n";
    }

    private static string MergeBackrestResticContent(
        string? existingContent,
        string normalizedPublicKey,
        string login,
        string host,
        string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(existingContent))
        {
            return ToBackrestResticAuthorizedKeysContent(normalizedPublicKey, login, host, rootDirectory);
        }

        var lines = NormalizeLines(existingContent);
        RemoveSsh2PublicKeyBlocks(lines);

        var (algorithm, payload) = ParseKeyParts(normalizedPublicKey);
        var loginAtHost = $"{login}@{host}";
        var commandLine = BuildBackrestCommandLine(rootDirectory, algorithm, payload, loginAtHost);

        var keyLineIndex = FindBackrestKeyLineIndex(lines, host);
        if (keyLineIndex >= 0)
        {
            lines[keyLineIndex] = commandLine;
        }
        else
        {
            lines.Add(commandLine);
            keyLineIndex = lines.Count - 1;
        }

        var ssh2BlockLines = BuildSsh2PublicKeyBlock(payload, loginAtHost);
        lines.InsertRange(keyLineIndex, ssh2BlockLines);

        return string.Join('\n', lines) + "\n";
    }

    private static int FindBackrestKeyLineIndex(IReadOnlyList<string> lines, string host)
    {
        var hostSuffix = $"@{host}";
        var fallbackIndex = -1;

        for (var index = 0; index < lines.Count; index++)
        {
            var trimmedLine = lines[index].Trim();
            if (!TryExtractNormalizedOpenSshPublicKeyFromAuthorizedLine(trimmedLine, out _))
            {
                continue;
            }

            if (trimmedLine.StartsWith(BackrestCommandPrefix, StringComparison.Ordinal))
            {
                return index;
            }

            if (trimmedLine.EndsWith(hostSuffix, StringComparison.Ordinal))
            {
                return index;
            }

            if (fallbackIndex < 0)
            {
                fallbackIndex = index;
            }
        }

        return fallbackIndex;
    }

    private static int FindFirstKeyLineIndex(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (TryExtractNormalizedOpenSshPublicKeyFromAuthorizedLine(lines[index], out _))
            {
                return index;
            }
        }

        return -1;
    }

    private static void RemoveSsh2PublicKeyBlocks(List<string> lines)
    {
        while (true)
        {
            var startIndex = lines.FindIndex(line => string.Equals(line.Trim(), BeginSsh2PublicKeyMarker, StringComparison.Ordinal));
            if (startIndex < 0)
            {
                return;
            }

            var endIndex = -1;
            for (var index = startIndex + 1; index < lines.Count; index++)
            {
                if (string.Equals(lines[index].Trim(), EndSsh2PublicKeyMarker, StringComparison.Ordinal))
                {
                    endIndex = index;
                    break;
                }
            }

            if (endIndex < 0)
            {
                lines.RemoveAt(startIndex);
                continue;
            }

            lines.RemoveRange(startIndex, endIndex - startIndex + 1);
        }
    }

    private static List<string> BuildSsh2PublicKeyBlock(string payload, string comment)
    {
        var lines = new List<string>
        {
            BeginSsh2PublicKeyMarker,
            $"Comment: \"{comment}\""
        };

        lines.AddRange(WrapText(payload, 70));
        lines.Add(EndSsh2PublicKeyMarker);

        return lines;
    }

    private static string BuildBackrestCommandLine(string rootDirectory, string algorithm, string payload, string comment)
    {
        return $"{BackrestCommandPrefix}{rootDirectory}\" {algorithm} {payload} {comment}";
    }

    private static (string Algorithm, string Payload) ParseKeyParts(string normalizedPublicKey)
    {
        var parts = normalizedPublicKey.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new ValidationException("Public key must contain algorithm and base64 key data.");
        }

        return (parts[0], parts[1]);
    }

    private static bool TryExtractNormalizedOpenSshPublicKeyFromAuthorizedLine(string line, out string normalizedPublicKey)
    {
        normalizedPublicKey = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < tokens.Length; index++)
        {
            var algorithm = tokens[index];
            if (!AlgorithmRegex().IsMatch(algorithm))
            {
                continue;
            }

            if (index + 1 >= tokens.Length)
            {
                return false;
            }

            var payload = tokens[index + 1];
            if (!IsValidBase64(payload))
            {
                return false;
            }

            if (tokens.Length == index + 2)
            {
                normalizedPublicKey = $"{algorithm} {payload}";
                return true;
            }

            var comment = string.Join(' ', tokens[(index + 2)..]).Trim();
            normalizedPublicKey = string.IsNullOrWhiteSpace(comment)
                ? $"{algorithm} {payload}"
                : $"{algorithm} {payload} {comment}";
            return true;
        }

        return false;
    }

    private static bool IsValidBase64(string payload)
    {
        try
        {
            _ = Convert.FromBase64String(payload);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static IEnumerable<string> WrapText(string input, int maxLineLength)
    {
        if (string.IsNullOrEmpty(input))
        {
            yield return string.Empty;
            yield break;
        }

        for (var index = 0; index < input.Length; index += maxLineLength)
        {
            var length = Math.Min(maxLineLength, input.Length - index);
            yield return input.Substring(index, length);
        }
    }

    private static List<string> NormalizeLines(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    [GeneratedRegex("^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(256|384|521))$", RegexOptions.CultureInvariant)]
    private static partial Regex AlgorithmRegex();
}
