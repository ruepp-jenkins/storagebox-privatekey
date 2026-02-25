namespace StorageBoxKeyTool.Api.Domain;

public sealed record StorageBoxTarget(string Login)
{
    public const int StorageBoxSshPort = 23;

    public string Host => $"{Login}.your-storagebox.de";

    public int Port => StorageBoxSshPort;
}
