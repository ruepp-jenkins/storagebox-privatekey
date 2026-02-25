namespace StorageBoxKeyTool.Api.Domain;

public sealed record StorageBoxTarget(string BaseUsername, int SubId)
{
    public const int StorageBoxSshPort = 23;

    public string Login => $"{BaseUsername}-sub{SubId}";

    public string Host => $"{Login}.your-storagebox.de";

    public int Port => StorageBoxSshPort;
}
