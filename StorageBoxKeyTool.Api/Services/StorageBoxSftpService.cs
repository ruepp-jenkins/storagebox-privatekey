using System.Net.Sockets;
using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using StorageBoxKeyTool.Api.Contracts;
using StorageBoxKeyTool.Api.Domain;

namespace StorageBoxKeyTool.Api.Services;

public sealed class StorageBoxSftpService
{
    private const string SshDirectoryPath = ".ssh";
    private const string AuthorizedKeysPath = ".ssh/authorized_keys";
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public CheckKeyResponse CheckRemoteKey(CheckKeyRequest request)
    {
        var target = StorageBoxInputValidator.ValidateTarget(request.Username);
        var password = StorageBoxInputValidator.ValidatePassword(request.Password);
        var normalizedPublicKey = PublicKeyUtility.NormalizeOpenSshPublicKey(request.PublicKey);
        var incomingFingerprint = PublicKeyUtility.ComputeSha256Fingerprint(normalizedPublicKey);
        var desiredContent = PublicKeyUtility.ToAuthorizedKeysContent(normalizedPublicKey);

        using var client = CreateClient(target, password);
        ConnectClient(client);

        if (!client.Exists(AuthorizedKeysPath))
        {
            return new CheckKeyResponse(
                RemoteKeyState.Missing,
                target.Login,
                target.Host,
                target.Port,
                AuthorizedKeysPath,
                incomingFingerprint,
                null,
                false
            );
        }

        var existingContent = DownloadTextFile(client, AuthorizedKeysPath);
        var normalizedExisting = PublicKeyUtility.NormalizeAuthorizedKeysContent(existingContent);
        var normalizedDesired = PublicKeyUtility.NormalizeAuthorizedKeysContent(desiredContent);

        if (string.Equals(normalizedExisting, normalizedDesired, StringComparison.Ordinal))
        {
            return new CheckKeyResponse(
                RemoteKeyState.Identical,
                target.Login,
                target.Host,
                target.Port,
                AuthorizedKeysPath,
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
            AuthorizedKeysPath,
            incomingFingerprint,
            PublicKeyUtility.TryComputePrimaryFingerprint(existingContent),
            true
        );
    }

    public UploadKeyResponse UploadRemoteKey(UploadKeyRequest request)
    {
        var target = StorageBoxInputValidator.ValidateTarget(request.Username);
        var password = StorageBoxInputValidator.ValidatePassword(request.Password);
        var normalizedPublicKey = PublicKeyUtility.NormalizeOpenSshPublicKey(request.PublicKey);
        var fingerprint = PublicKeyUtility.ComputeSha256Fingerprint(normalizedPublicKey);
        var desiredContent = PublicKeyUtility.ToAuthorizedKeysContent(normalizedPublicKey);
        var normalizedDesired = PublicKeyUtility.NormalizeAuthorizedKeysContent(desiredContent);

        using var client = CreateClient(target, password);
        ConnectClient(client);

        EnsureSshDirectory(client);

        var existingContent = client.Exists(AuthorizedKeysPath)
            ? DownloadTextFile(client, AuthorizedKeysPath)
            : null;

        if (existingContent is not null)
        {
            var normalizedExisting = PublicKeyUtility.NormalizeAuthorizedKeysContent(existingContent);
            if (string.Equals(normalizedExisting, normalizedDesired, StringComparison.Ordinal))
            {
                return new UploadKeyResponse(
                    true,
                    false,
                    "Remote authorized_keys already matches the provided key.",
                    AuthorizedKeysPath,
                    fingerprint
                );
            }

            if (!request.Overwrite)
            {
                throw new StorageBoxConflictException("Remote authorized_keys exists and differs from the provided key. Explicit overwrite confirmation is required.");
            }

            client.DeleteFile(AuthorizedKeysPath);
        }

        UploadTextFile(client, AuthorizedKeysPath, desiredContent);
        SetPermissions(client, AuthorizedKeysPath, "644");

        var verificationContent = DownloadTextFile(client, AuthorizedKeysPath);
        var normalizedVerification = PublicKeyUtility.NormalizeAuthorizedKeysContent(verificationContent);
        if (!string.Equals(normalizedVerification, normalizedDesired, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Upload verification failed. The remote file content does not match the expected public key.");
        }

        return new UploadKeyResponse(
            true,
            true,
            "SSH public key uploaded successfully.",
            AuthorizedKeysPath,
            fingerprint
        );
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

    private static void EnsureSshDirectory(SftpClient client)
    {
        if (!client.Exists(SshDirectoryPath))
        {
            client.CreateDirectory(SshDirectoryPath);
        }
    }

    private static void SetPermissions(SftpClient client, string path, string octalPermission)
    {
        var mode = Convert.ToInt16(octalPermission, 8);
        client.ChangePermissions(path, mode);
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
