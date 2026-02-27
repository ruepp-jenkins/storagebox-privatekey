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
        var normalizedPublicKey = PublicKeyUtility.NormalizeOpenSshPublicKey(request.PublicKey);
        var incomingFingerprint = PublicKeyUtility.ComputeSha256Fingerprint(normalizedPublicKey);
        var rootDirectory = StorageBoxInputValidator.ValidateRootDirectory(request.RootDirectory);
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
        var isConfigured = IsAuthorizedKeysConfigured(
            existingContent,
            normalizedPublicKey,
            request.BackrestResticCompatible,
            rootDirectory
        );

        if (isConfigured)
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

        var normalizedPublicKey = string.Empty;
        var fingerprint = string.Empty;
        if (request.UploadSshPublicKey)
        {
            normalizedPublicKey = PublicKeyUtility.NormalizeOpenSshPublicKey(request.PublicKey);
            fingerprint = PublicKeyUtility.ComputeSha256Fingerprint(normalizedPublicKey);
        }

        var authorizedKeysPath = BuildRemotePath(rootDirectory, AuthorizedKeysRelativePath);
        var rcloneConfigPath = BuildRemotePath(rootDirectory, RcloneConfigRelativePath);

        using var client = CreateClient(target, password);
        ConnectClient(client);
        EnsureRootDirectoryExists(client, rootDirectory);

        var sshChanged = false;
        if (request.UploadSshPublicKey)
        {
            EnsureDirectory(client, rootDirectory, SshDirectoryRelativePath);

            var existingContent = client.Exists(authorizedKeysPath)
                ? DownloadTextFile(client, authorizedKeysPath)
                : null;

            var alreadyConfigured = IsAuthorizedKeysConfigured(
                existingContent,
                normalizedPublicKey,
                request.BackrestResticCompatible,
                rootDirectory
            );

            if (!alreadyConfigured)
            {
                if (existingContent is not null && !request.Overwrite)
                {
                    throw new StorageBoxConflictException("Remote authorized_keys exists and differs from the provided key. Explicit overwrite confirmation is required.");
                }

                var mergedContent = PublicKeyUtility.MergeAuthorizedKeysContent(
                    existingContent,
                    normalizedPublicKey,
                    target.Login,
                    target.Host,
                    request.BackrestResticCompatible,
                    rootDirectory
                );

                UploadTextFile(client, authorizedKeysPath, mergedContent);

                var verificationContent = DownloadTextFile(client, authorizedKeysPath);
                var verified = IsAuthorizedKeysConfigured(
                    verificationContent,
                    normalizedPublicKey,
                    request.BackrestResticCompatible,
                    rootDirectory
                );

                if (!verified)
                {
                    throw new InvalidOperationException("Upload verification failed. The remote authorized_keys file does not contain the expected key configuration.");
                }

                sshChanged = true;
            }
        }

        var rcloneConfigCreated = request.BackrestResticCompatible && EnsureEmptyBackrestRcloneConfig(client, rootDirectory);
        var changed = sshChanged || rcloneConfigCreated;

        return new UploadKeyResponse(
            true,
            changed,
            BuildUploadResponseMessage(request.UploadSshPublicKey, sshChanged, request.BackrestResticCompatible, rcloneConfigCreated),
            request.UploadSshPublicKey ? authorizedKeysPath : rcloneConfigPath,
            fingerprint
        );
    }

    private static bool IsAuthorizedKeysConfigured(
        string? authorizedKeysContent,
        string normalizedPublicKey,
        bool backrestResticCompatible,
        string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(authorizedKeysContent))
        {
            return false;
        }

        return backrestResticCompatible
            ? PublicKeyUtility.ContainsBackrestResticEntry(authorizedKeysContent, normalizedPublicKey, rootDirectory)
                && PublicKeyUtility.ContainsSsh2PublicKeyBlock(authorizedKeysContent, normalizedPublicKey)
            : PublicKeyUtility.ContainsPublicKey(authorizedKeysContent, normalizedPublicKey);
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

    internal static string BuildUploadResponseMessage(
        bool uploadSshPublicKey,
        bool sshChanged,
        bool backrestResticCompatible,
        bool rcloneConfigCreated)
    {
        if (!uploadSshPublicKey && !backrestResticCompatible)
        {
            return "No remote changes were requested.";
        }

        var baseMessage = uploadSshPublicKey
            ? sshChanged
                ? "SSH public key uploaded successfully."
                : "Remote SSH public key already matches the provided key."
            : "SSH public key upload skipped.";

        if (!backrestResticCompatible)
        {
            return baseMessage;
        }

        if (rcloneConfigCreated)
        {
            return $"{baseMessage} Created empty .config/rclone/rclone.conf.";
        }

        return $"{baseMessage} Backrest/restic compatibility is enabled.";
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
