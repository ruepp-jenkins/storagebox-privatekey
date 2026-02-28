using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using StorageBoxKeyTool.Api.Contracts;
using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Api.Services;

public sealed class StorageBoxSftpService
{
    private const string SshDirectoryRelativePath = ".ssh";
    private const string AuthorizedKeysRelativePath = ".ssh/authorized_keys";
    private const string RcloneDirectoryRelativePath = ".config/rclone";
    private const string RcloneConfigRelativePath = ".config/rclone/rclone.conf";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public CheckKeyResponse CheckRemoteKey(CheckKeyRequest request)
    {
        var target = StorageBoxInputValidator.ValidateTarget(request.Username);
        var password = StorageBoxInputValidator.ValidatePassword(request.Password);
        var rootDirectory = StorageBoxInputValidator.ValidateRootDirectory(request.RootDirectory);
        var normalizedPublicKey = PublicKeyUtility.NormalizeOpenSshPublicKey(request.PublicKey);
        var incomingFingerprint = PublicKeyUtility.ComputeSha256Fingerprint(normalizedPublicKey);
        var authorizedKeysPath = BuildRemotePath(rootDirectory, AuthorizedKeysRelativePath);

        using var client = CreateClient(target, password);
        ConnectClient(client);
        EnsureRootDirectoryExists(client, rootDirectory);

        if (!client.Exists(authorizedKeysPath))
        {
            return new CheckKeyResponse(
                RemoteKeyState.Missing,
                target.Login,
                target.Host,
                target.Port,
                authorizedKeysPath,
                incomingFingerprint,
                null,
                false
            );
        }

        var existingContent = DownloadTextFile(client, authorizedKeysPath);
        if (PublicKeyUtility.ContainsPublicKey(existingContent, normalizedPublicKey))
        {
            return new CheckKeyResponse(
                RemoteKeyState.Identical,
                target.Login,
                target.Host,
                target.Port,
                authorizedKeysPath,
                incomingFingerprint,
                incomingFingerprint,
                false
            );
        }

        return new CheckKeyResponse(
            RemoteKeyState.Different,
            target.Login,
            target.Host,
            target.Port,
            authorizedKeysPath,
            incomingFingerprint,
            PublicKeyUtility.TryComputePrimaryFingerprint(existingContent),
            true
        );
    }

    public UploadKeyResponse UploadRemoteKey(UploadKeyRequest request)
    {
        var target = StorageBoxInputValidator.ValidateTarget(request.Username);
        var password = StorageBoxInputValidator.ValidatePassword(request.Password);
        var rootDirectory = StorageBoxInputValidator.ValidateRootDirectory(request.RootDirectory);
        var normalizedPublicKey = PublicKeyUtility.NormalizeOpenSshPublicKey(request.PublicKey);
        var fingerprint = PublicKeyUtility.ComputeSha256Fingerprint(normalizedPublicKey);
        var authorizedKeysPath = BuildRemotePath(rootDirectory, AuthorizedKeysRelativePath);

        using var client = CreateClient(target, password);
        ConnectClient(client);
        EnsureRootDirectoryExists(client, rootDirectory);
        EnsureDirectory(client, rootDirectory, SshDirectoryRelativePath);

        var existingContent = client.Exists(authorizedKeysPath)
            ? DownloadTextFile(client, authorizedKeysPath)
            : null;

        if (existingContent is not null && PublicKeyUtility.ContainsPublicKey(existingContent, normalizedPublicKey))
        {
            return new UploadKeyResponse(
                true,
                false,
                BuildUploadResponseMessage(changed: false),
                authorizedKeysPath,
                fingerprint
            );
        }

        if (existingContent is not null && !request.Overwrite)
        {
            throw new StorageBoxConflictException("Remote authorized_keys exists and differs from the provided key. Explicit overwrite confirmation is required.");
        }

        var mergedContent = PublicKeyUtility.MergeAuthorizedKeysContent(
            existingContent,
            normalizedPublicKey,
            target.Login,
            target.Host,
            backrestResticCompatible: false,
            rootDirectory
        );

        BackupAuthorizedKeysFile(client, authorizedKeysPath);
        UploadTextFile(client, authorizedKeysPath, mergedContent);

        var verificationContent = DownloadTextFile(client, authorizedKeysPath);
        if (!PublicKeyUtility.ContainsPublicKey(verificationContent, normalizedPublicKey))
        {
            throw new InvalidOperationException("Upload verification failed. The remote authorized_keys file does not contain the expected key.");
        }

        return new UploadKeyResponse(
            true,
            true,
            BuildUploadResponseMessage(changed: true),
            authorizedKeysPath,
            fingerprint
        );
    }

    public CheckSshLoginResponse CheckSshLogin(CheckSshLoginRequest request)
    {
        var target = StorageBoxInputValidator.ValidateTarget(request.Username);
        var password = StorageBoxInputValidator.ValidatePassword(request.Password);
        var rootDirectory = StorageBoxInputValidator.ValidateRootDirectory(request.RootDirectory);
        var authorizedKeysPath = BuildRemotePath(rootDirectory, AuthorizedKeysRelativePath);

        using var client = CreateClient(target, password);
        ConnectClient(client);
        EnsureRootDirectoryExists(client, rootDirectory);

        if (!client.Exists(authorizedKeysPath))
        {
            return new CheckSshLoginResponse(
                false,
                target.Login,
                target.Host,
                target.Port,
                authorizedKeysPath,
                null
            );
        }

        var existingContent = DownloadTextFile(client, authorizedKeysPath);
        var existingPublicKey = PublicKeyUtility.TryExtractPrimaryPublicKey(existingContent);
        if (string.IsNullOrWhiteSpace(existingPublicKey))
        {
            return new CheckSshLoginResponse(
                false,
                target.Login,
                target.Host,
                target.Port,
                authorizedKeysPath,
                null
            );
        }

        return new CheckSshLoginResponse(
            true,
            target.Login,
            target.Host,
            target.Port,
            authorizedKeysPath,
            PublicKeyUtility.ComputeSha256Fingerprint(existingPublicKey)
        );
    }

    public ApplyBackrestResticResponse ApplyBackrestResticCompatibility(ApplyBackrestResticRequest request)
    {
        var target = StorageBoxInputValidator.ValidateTarget(request.Username);
        var password = StorageBoxInputValidator.ValidatePassword(request.Password);
        var rootDirectory = StorageBoxInputValidator.ValidateRootDirectory(request.RootDirectory);
        var authorizedKeysPath = BuildRemotePath(rootDirectory, AuthorizedKeysRelativePath);
        var rcloneConfigPath = BuildRemotePath(rootDirectory, RcloneConfigRelativePath);

        using var client = CreateClient(target, password);
        ConnectClient(client);
        EnsureRootDirectoryExists(client, rootDirectory);

        if (!client.Exists(authorizedKeysPath))
        {
            throw CreateMissingSshLoginException(authorizedKeysPath);
        }

        var existingContent = DownloadTextFile(client, authorizedKeysPath);
        var existingPublicKey = PublicKeyUtility.TryExtractPrimaryPublicKey(existingContent);
        if (string.IsNullOrWhiteSpace(existingPublicKey))
        {
            throw CreateMissingSshLoginException(authorizedKeysPath);
        }

        var sshChanged = false;
        if (!IsBackrestResticConfigured(existingContent, existingPublicKey, rootDirectory))
        {
            var mergedContent = PublicKeyUtility.MergeAuthorizedKeysContent(
                existingContent,
                existingPublicKey,
                target.Login,
                target.Host,
                backrestResticCompatible: true,
                rootDirectory
            );

            BackupAuthorizedKeysFile(client, authorizedKeysPath);
            UploadTextFile(client, authorizedKeysPath, mergedContent);

            var verificationContent = DownloadTextFile(client, authorizedKeysPath);
            if (!IsBackrestResticConfigured(verificationContent, existingPublicKey, rootDirectory))
            {
                throw new InvalidOperationException("Backrest/restic verification failed. The remote authorized_keys file does not contain the expected command-based key entry.");
            }

            sshChanged = true;
        }

        var rcloneConfigCreated = EnsureEmptyBackrestRcloneConfig(client, rootDirectory);

        return new ApplyBackrestResticResponse(
            true,
            sshChanged || rcloneConfigCreated,
            BuildBackrestResponseMessage(sshChanged, rcloneConfigCreated),
            authorizedKeysPath,
            rcloneConfigPath,
            PublicKeyUtility.ComputeSha256Fingerprint(existingPublicKey)
        );
    }

    private static bool IsBackrestResticConfigured(string authorizedKeysContent, string normalizedPublicKey, string rootDirectory)
    {
        return PublicKeyUtility.ContainsBackrestResticEntry(authorizedKeysContent, normalizedPublicKey, rootDirectory)
            && PublicKeyUtility.ContainsSsh2PublicKeyBlock(authorizedKeysContent, normalizedPublicKey);
    }

    private static ValidationException CreateMissingSshLoginException(string authorizedKeysPath)
    {
        return new ValidationException($"No SSH key login is configured at '{authorizedKeysPath}'. Configure it in the sFTP key tab first.");
    }

    private static SftpClient CreateClient(StorageBoxTarget target, string password)
    {
        var connectionInfo = new PasswordConnectionInfo(target.Host, target.Port, target.Login, password)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        return new SftpClient(connectionInfo)
        {
            OperationTimeout = TimeSpan.FromSeconds(30)
        };
    }

    private static void ConnectClient(SftpClient client)
    {
        try
        {
            client.Connect();
        }
        catch (SshAuthenticationException ex)
        {
            throw new InvalidOperationException("Authentication failed. Check username and password.", ex);
        }
        catch (SshConnectionException ex)
        {
            throw new InvalidOperationException("Could not connect to the Storage Box host. Ensure SSH support is enabled and reachable on port 23.", ex);
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("Could not resolve the Storage Box host. Verify username format and network DNS settings.", ex);
        }
        catch (SshOperationTimeoutException ex)
        {
            throw new TimeoutException("Connection timed out while reaching the Storage Box host on port 23.", ex);
        }
    }

    private static void EnsureRootDirectoryExists(SftpClient client, string rootDirectory)
    {
        if (!client.Exists(rootDirectory))
        {
            throw new ValidationException($"Root directory '{rootDirectory}' does not exist on the remote Storage Box.");
        }

        var attributes = client.GetAttributes(rootDirectory);
        if (!attributes.IsDirectory)
        {
            throw new ValidationException($"Root directory '{rootDirectory}' is not a directory on the remote Storage Box.");
        }
    }

    private static void EnsureDirectory(SftpClient client, string rootDirectory, string relativeDirectoryPath)
    {
        var normalizedRoot = rootDirectory.TrimEnd('/');
        var segments = relativeDirectoryPath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var currentPath = normalizedRoot;
        foreach (var segment in segments)
        {
            currentPath = $"{currentPath}/{segment}";

            if (!client.Exists(currentPath))
            {
                client.CreateDirectory(currentPath);
            }
        }
    }

    private static bool EnsureEmptyBackrestRcloneConfig(SftpClient client, string rootDirectory)
    {
        EnsureDirectory(client, rootDirectory, RcloneDirectoryRelativePath);

        var rcloneConfigPath = BuildRemotePath(rootDirectory, RcloneConfigRelativePath);
        if (client.Exists(rcloneConfigPath))
        {
            return false;
        }

        UploadTextFile(client, rcloneConfigPath, string.Empty);
        return true;
    }

    internal static string BuildUploadResponseMessage(bool changed)
    {
        return changed
            ? "SSH public key uploaded successfully."
            : "Remote SSH public key already matches the provided key.";
    }

    internal static string BuildBackrestResponseMessage(bool sshChanged, bool rcloneConfigCreated)
    {
        if (sshChanged && rcloneConfigCreated)
        {
            return "Configured backrest/restic compatibility in authorized_keys and created empty .config/rclone/rclone.conf.";
        }

        if (sshChanged)
        {
            return "Configured backrest/restic compatibility in authorized_keys.";
        }

        if (rcloneConfigCreated)
        {
            return "Backrest/restic compatibility already existed in authorized_keys. Created empty .config/rclone/rclone.conf.";
        }

        return "Backrest/restic compatibility is already configured.";
    }

    private static void BackupAuthorizedKeysFile(SftpClient client, string authorizedKeysPath)
    {
        if (!client.Exists(authorizedKeysPath))
        {
            return;
        }

        var directory = authorizedKeysPath[..authorizedKeysPath.LastIndexOf('/')];
        var existingFiles = client.ListDirectory(directory);
        var fileNames = existingFiles.Select(f => f.Name).ToList();
        var nextNumber = FindNextBackupNumber(fileNames);
        var backupPath = $"{authorizedKeysPath}.backup.{nextNumber}";

        var content = DownloadTextFile(client, authorizedKeysPath);
        UploadTextFile(client, backupPath, content);
    }

    internal static int FindNextBackupNumber(IEnumerable<string> fileNames)
    {
        var maxNumber = 0;
        foreach (var name in fileNames)
        {
            if (name.StartsWith("authorized_keys.backup.") &&
                int.TryParse(name["authorized_keys.backup.".Length..], out var number) &&
                number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return maxNumber + 1;
    }

    private static string BuildRemotePath(string rootDirectory, string relativePath)
    {
        var normalizedRoot = rootDirectory.TrimEnd('/');
        var normalizedRelative = relativePath.TrimStart('/');
        return $"{normalizedRoot}/{normalizedRelative}";
    }

    private static string DownloadTextFile(SftpClient client, string remotePath)
    {
        using var memoryStream = new MemoryStream();
        client.DownloadFile(remotePath, memoryStream);
        return Utf8NoBom.GetString(memoryStream.ToArray());
    }

    private static void UploadTextFile(SftpClient client, string remotePath, string content)
    {
        var payload = Utf8NoBom.GetBytes(content);
        using var memoryStream = new MemoryStream(payload);
        client.UploadFile(memoryStream, remotePath, canOverride: true);
    }
}
