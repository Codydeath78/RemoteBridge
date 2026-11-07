namespace RemoteBridge.Protocol;

public static class MessageTypes
{
    public const string RegisterHost =
        "register_host";

    public const string HostRegistered =
        "host_registered";

    public const string JoinSession =
        "join_session";

    public const string JoinPending =
        "join_pending";

    public const string ViewerRequested =
        "viewer_requested";

    public const string ApproveViewer =
        "approve_viewer";

    public const string RejectViewer =
        "reject_viewer";

    public const string SessionApproved =
        "session_approved";

    public const string SessionRejected =
        "session_rejected";

    public const string Error =
        "error";

    public const string SessionData =
        "session_data";

    public const string PeerDisconnected =
        "peer_disconnected";

    public const string MouseMove =
        "mouse_move";

    public const string MouseLeftDown =
    "mouse_left_down";

    public const string MouseLeftUp =
        "mouse_left_up";

    public const string EndSession =
        "end_session";

    public const string KeyDown =
        "key_down";

    public const string KeyUp =
        "key_up";
    public const string MouseWheel =
    "mouse_wheel";
}