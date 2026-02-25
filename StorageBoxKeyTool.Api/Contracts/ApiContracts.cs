namespace StorageBoxKeyTool.Api.Contracts;

public enum RemoteKeyState
{
    Missing = 0,
    Identical = 1,
    Different = 2
}

public sealed record GenerateKeyRequest(
    string Username,
    int SubId,
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
    int SubId,
    string Password,
    string PublicKey
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
    int SubId,
    string Password,
    string PublicKey,
    bool Overwrite
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
