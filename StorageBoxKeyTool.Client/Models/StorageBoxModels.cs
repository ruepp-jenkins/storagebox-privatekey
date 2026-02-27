using System.ComponentModel.DataAnnotations;

namespace StorageBoxKeyTool.Client.Models;

public enum KeyInputMode
{
    GenerateNewKeyPair = 0,
    UseExistingPublicKey = 1
}

public enum RemoteKeyState
{
    Missing = 0,
    Identical = 1,
    Different = 2
}

public sealed class SftpKeyFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Username is required.")]
    [RegularExpression("^[a-z0-9]{1,32}-sub[0-9]{1,6}$", ErrorMessage = "Use format <base>-sub<id> (for example u123456-sub12).")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Storage Box password is required.")]
    [StringLength(256, ErrorMessage = "Password is too long.")]
    public string Password { get; set; } = string.Empty;

    public KeyInputMode KeyMode { get; set; } = KeyInputMode.GenerateNewKeyPair;

    [Required(ErrorMessage = "Please choose an algorithm.")]
    public string Algorithm { get; set; } = "ed25519";

    public int? KeySize { get; set; }

    [StringLength(256, ErrorMessage = "Passphrase is too long.")]
    public string? KeyPassphrase { get; set; }

    [StringLength(256, ErrorMessage = "Passphrase confirmation is too long.")]
    public string? ConfirmKeyPassphrase { get; set; }

    [StringLength(128, ErrorMessage = "Comment is too long.")]
    public string? KeyComment { get; set; }

    public string? ProvidedPublicKey { get; set; }

    public bool UseCustomRootDirectory { get; set; }

    public string? RootDirectory { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var validationResult in RootDirectoryValidation.Validate(UseCustomRootDirectory, RootDirectory, nameof(RootDirectory)))
        {
            yield return validationResult;
        }

        if (KeyMode == KeyInputMode.UseExistingPublicKey && string.IsNullOrWhiteSpace(ProvidedPublicKey))
        {
            yield return new ValidationResult(
                "Provide a public key via text or file upload.",
                [nameof(ProvidedPublicKey)]
            );
        }

        if (KeyMode == KeyInputMode.GenerateNewKeyPair)
        {
            var algorithm = (Algorithm ?? string.Empty).Trim().ToLowerInvariant();
            switch (algorithm)
            {
                case "ed25519":
                    break;
                case "ecdsa" when KeySize is not (256 or 384 or 521):
                    yield return new ValidationResult(
                        "ECDSA size must be 256, 384 or 521.",
                        [nameof(KeySize)]
                    );
                    break;
                case "rsa" when KeySize is null or < 2048 or > 8192:
                    yield return new ValidationResult(
                        "RSA size must be between 2048 and 8192.",
                        [nameof(KeySize)]
                    );
                    break;
                case "ecdsa":
                case "rsa":
                    break;
                default:
                    yield return new ValidationResult(
                        "Unsupported algorithm.",
                        [nameof(Algorithm)]
                    );
                    break;
            }

            if (!string.Equals(KeyPassphrase, ConfirmKeyPassphrase, StringComparison.Ordinal))
            {
                yield return new ValidationResult(
                    "Passphrase and confirmation do not match.",
                    [nameof(ConfirmKeyPassphrase)]
                );
            }
        }
    }
}

public sealed class BackrestResticFormModel : IValidatableObject
{
    [Required(ErrorMessage = "Username is required.")]
    [RegularExpression("^[a-z0-9]{1,32}-sub[0-9]{1,6}$", ErrorMessage = "Use format <base>-sub<id> (for example u123456-sub12).")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Storage Box password is required.")]
    [StringLength(256, ErrorMessage = "Password is too long.")]
    public string Password { get; set; } = string.Empty;

    public bool UseCustomRootDirectory { get; set; }

    public string? RootDirectory { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var validationResult in RootDirectoryValidation.Validate(UseCustomRootDirectory, RootDirectory, nameof(RootDirectory)))
        {
            yield return validationResult;
        }
    }
}

internal static class RootDirectoryValidation
{
    public static IEnumerable<ValidationResult> Validate(bool useCustomRootDirectory, string? rootDirectory, string memberName)
    {
        if (!useCustomRootDirectory)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            yield return new ValidationResult(
                "Provide a custom root directory.",
                [memberName]
            );
            yield break;
        }

        var normalizedRootDirectory = rootDirectory.Trim();
        if (normalizedRootDirectory.Length > 1)
        {
            normalizedRootDirectory = normalizedRootDirectory.TrimEnd('/');
        }

        if (string.Equals(normalizedRootDirectory, "/home", StringComparison.Ordinal)
            || normalizedRootDirectory.StartsWith("/home/", StringComparison.Ordinal))
        {
            yield break;
        }

        yield return new ValidationResult(
            "Custom root directory must be /home or start with /home/.",
            [memberName]
        );
    }
}

public sealed record GenerateKeyRequest(
    string Username,
    string Algorithm,
    int? KeySize,
    string? Passphrase,
    string? Comment
);

public sealed record GenerateKeyResponse(
    string PublicKey,
    string PrivateKey,
    string Algorithm,
    int? KeySize,
    string Comment,
    string FingerprintSha256
);

public sealed record CheckKeyRequest(
    string Username,
    string Password,
    string PublicKey,
    string RootDirectory
);

public sealed record CheckKeyResponse(
    RemoteKeyState State,
    string Login,
    string Host,
    int Port,
    string DestinationPath,
    string IncomingFingerprintSha256,
    string? ExistingFingerprintSha256,
    bool RequiresOverwrite
);

public sealed record UploadKeyRequest(
    string Username,
    string Password,
    string PublicKey,
    bool Overwrite,
    string RootDirectory
);

public sealed record CheckSshLoginRequest(
    string Username,
    string Password,
    string RootDirectory
);

public sealed record CheckSshLoginResponse(
    bool HasSshPublicKey,
    string Login,
    string Host,
    int Port,
    string DestinationPath,
    string? ExistingFingerprintSha256
);

public sealed record ApplyBackrestResticRequest(
    string Username,
    string Password,
    string RootDirectory
);

public sealed record ApplyBackrestResticResponse(
    bool Success,
    bool Changed,
    string Message,
    string AuthorizedKeysPath,
    string RcloneConfigPath,
    string FingerprintSha256
);

public sealed record UploadKeyResponse(
    bool Success,
    bool Changed,
    string Message,
    string DestinationPath,
    string FingerprintSha256
);

public sealed record ApiErrorResponse(
    string Step,
    string Message,
    string? Detail
);
