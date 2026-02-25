using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace StorageBoxKeyTool.Api.Domain;

internal static partial class PublicKeyUtility
{
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
        if (string.IsNullOrWhiteSpace(authorizedKeysContent))
        {
            return null;
        }

        var lines = authorizedKeysContent
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            try
            {
                var normalized = NormalizeOpenSshPublicKey(line);
                return ComputeSha256Fingerprint(normalized);
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

    [GeneratedRegex("^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(256|384|521))$", RegexOptions.CultureInvariant)]
    private static partial Regex AlgorithmRegex();
}
