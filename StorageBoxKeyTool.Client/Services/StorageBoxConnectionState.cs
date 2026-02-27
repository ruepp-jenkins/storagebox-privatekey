namespace StorageBoxKeyTool.Client.Services;

public sealed class StorageBoxConnectionState
{
    private string _username = string.Empty;
    private string _password = string.Empty;

    public string Username
    {
        get => _username;
        set
        {
            if (string.Equals(_username, value, StringComparison.Ordinal))
            {
                return;
            }

            _username = value;
            Changed?.Invoke();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (string.Equals(_password, value, StringComparison.Ordinal))
            {
                return;
            }

            _password = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}
