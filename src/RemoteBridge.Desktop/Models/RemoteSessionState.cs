namespace RemoteBridge.Desktop.Models;

public enum RemoteRole
{
    None,
    Host,
    Viewer
}

public enum RemoteSessionState
{
    Idle,
    Hosting,
    Joining,
    AwaitingApproval,
    Connected
}