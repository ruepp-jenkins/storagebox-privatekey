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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
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
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield break;
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

public sealed record UiSecurityConfigResponse(
    bool SuppressNonLocalHostWarning
);
