using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using StorageBoxKeyTool.Api.Contracts;
using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Api.Services;

public sealed class SshKeyGenerationService
{
    private const int MaxPassphraseLength = 256;

    public async Task<GenerateKeyResponse> GenerateAsync(GenerateKeyRequest request, CancellationToken cancellationToken)
    {
        var target = StorageBoxInputValidator.ValidateTarget(request.Username);
        var options = ValidateOptions(request.Algorithm, request.KeySize);
        var comment = StorageBoxInputValidator.BuildComment(target, request.Comment);
        var passphrase = request.Passphrase ?? string.Empty;

        if (passphrase.Length > MaxPassphraseLength)
        {
            throw new ValidationException($"Key passphrase must not exceed {MaxPassphraseLength} characters.");
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"storagebox-key-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        var keyFilePath = Path.Combine(tempDirectory, "id_storagebox");

        try
        {
            using var process = BuildKeygenProcess(options, comment, keyFilePath);

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to start ssh-keygen. Please ensure OpenSSH client tools are installed.", ex);
            }

            await process.StandardInput.WriteAsync(passphrase + Environment.NewLine + passphrase + Environment.NewLine);
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var message = BuildKeygenErrorMessage(stdout, stderr);
                throw new InvalidOperationException(message);
            }

            var privateKey = await File.ReadAllTextAsync(keyFilePath, cancellationToken);
            var publicKeyRaw = await File.ReadAllTextAsync($"{keyFilePath}.pub", cancellationToken);
            var normalizedPublicKey = PublicKeyUtility.NormalizeOpenSshPublicKey(publicKeyRaw);
            var fingerprint = PublicKeyUtility.ComputeSha256Fingerprint(normalizedPublicKey);

            return new GenerateKeyResponse(
                normalizedPublicKey,
                privateKey,
                options.DisplayAlgorithm,
                options.KeySize,
                comment,
                fingerprint
            );
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Key generation timed out. Please try again.");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static KeyGenerationOptions ValidateOptions(string? algorithmInput, int? keySizeInput)
    {
        var algorithm = (algorithmInput ?? "ed25519").Trim().ToLowerInvariant();

        return algorithm switch
        {
            "ed25519" => new KeyGenerationOptions("ed25519", null, "ed25519"),
            "ecdsa" => ValidateEcdsaOptions(keySizeInput),
            "rsa" => ValidateRsaOptions(keySizeInput),
            _ => throw new ValidationException("Unsupported algorithm. Allowed values: ed25519, ecdsa, rsa.")
        };
    }

    private static KeyGenerationOptions ValidateEcdsaOptions(int? keySizeInput)
    {
        var size = keySizeInput ?? 384;
        if (size is not (256 or 384 or 521))
        {
            throw new ValidationException("ECDSA key size must be 256, 384 or 521.");
        }

        return new KeyGenerationOptions("ecdsa", size, $"ecdsa-{size}");
    }

    private static KeyGenerationOptions ValidateRsaOptions(int? keySizeInput)
    {
        var size = keySizeInput ?? 3072;
        if (size < 2048 || size > 8192)
        {
            throw new ValidationException("RSA key size must be between 2048 and 8192 bits.");
        }

        return new KeyGenerationOptions("rsa", size, $"rsa-{size}");
    }

    private static Process BuildKeygenProcess(KeyGenerationOptions options, string comment, string keyFilePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ssh-keygen",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-q");
        startInfo.ArgumentList.Add("-t");
        startInfo.ArgumentList.Add(options.KeyType);

        if (options.KeySize is not null)
        {
            startInfo.ArgumentList.Add("-b");
            startInfo.ArgumentList.Add(options.KeySize.Value.ToString());
        }

        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(comment);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add(keyFilePath);

        return new Process { StartInfo = startInfo };
    }

    private static string BuildKeygenErrorMessage(string stdout, string stderr)
    {
        var raw = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "ssh-keygen failed without an error message.";
        }

        var oneLine = string.Join(' ', raw
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return $"ssh-keygen failed: {oneLine}";
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Intentionally ignored to avoid leaking sensitive material into logs.
        }
    }

    private sealed record KeyGenerationOptions(string KeyType, int? KeySize, string DisplayAlgorithm);
}
