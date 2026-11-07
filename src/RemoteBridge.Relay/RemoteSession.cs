using System.Net.WebSockets;

namespace RemoteBridge.Relay;

public sealed class RemoteSession
{
    public required string SessionCode
    {
        get;
        init;
    }

    public required WebSocket HostSocket
    {
        get;
        init;
    }

    public string HostDeviceName
    {
        get;
        init;
    } = string.Empty;

    public WebSocket? ViewerSocket
    {
        get;
        set;
    }

    public string? ViewerDeviceName
    {
        get;
        set;
    }

    public bool IsApproved
    {
        get;
        set;
    }

    public object SyncRoot { get; } =
        new();
}