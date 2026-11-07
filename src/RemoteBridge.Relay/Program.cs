using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteBridge.Protocol;
using RemoteBridge.Relay;

var builder =
    WebApplication.CreateBuilder(args);

var app =
    builder.Build();

var sessions =
    new ConcurrentDictionary<
        string,
        RemoteSession
    >();

var jsonOptions =
    new JsonSerializerOptions
    {
        PropertyNamingPolicy =
            JsonNamingPolicy.CamelCase,

        PropertyNameCaseInsensitive =
            true
    };

app.UseWebSockets(
    new WebSocketOptions
    {
        KeepAliveInterval =
            TimeSpan.FromSeconds(30)
    }
);

app.MapGet(
    "/health",
    () =>
    {
        return Results.Ok(
            new
            {
                status = "ok",
                service =
                    "RemoteBridge.Relay",
                activeSessions =
                    sessions.Count
            }
        );
    }
);

app.Map(
    "/ws",
    async context =>
    {
        if (
            !context
                .WebSockets
                .IsWebSocketRequest
        )
        {
            context.Response.StatusCode =
                StatusCodes
                    .Status400BadRequest;

            return;
        }

        using WebSocket socket =
            await context
                .WebSockets
                .AcceptWebSocketAsync();

        string? hostedSessionCode =
            null;

        string? joinedSessionCode =
            null;

        try
        {
            while (
                socket.State ==
                    WebSocketState.Open &&
                !context
                    .RequestAborted
                    .IsCancellationRequested
            )
            {
                ReceivedSocketMessage? incoming =
    await ReceiveSocketMessageAsync(
        socket,
        context.RequestAborted
    );

if (incoming is null)
{
    break;
}

if (
    incoming.MessageType ==
    WebSocketMessageType.Binary
)
{
    RemoteSession? binarySession =
        null;

    WebSocket? targetSocket =
        null;

    string senderRole;

    // Host -> Viewer
    if (
        hostedSessionCode is not null &&
        sessions.TryGetValue(
            hostedSessionCode,
            out binarySession
        )
    )
    {
        lock (
            binarySession.SyncRoot
        )
        {
            if (
                binarySession.IsApproved
            )
            {
                targetSocket =
                    binarySession
                        .ViewerSocket;
            }
        }

        senderRole =
            "Host";
    }

    // Viewer -> Host
    else if (
        joinedSessionCode is not null &&
        sessions.TryGetValue(
            joinedSessionCode,
            out binarySession
        )
    )
    {
        lock (
            binarySession.SyncRoot
        )
        {
            if (
                binarySession.IsApproved
            )
            {
                targetSocket =
                    binarySession
                        .HostSocket;
            }
        }

        senderRole =
            "Viewer";
    }
    else
    {
        await SendErrorAsync(
            socket,
            "This connection does not belong to a session.",
            jsonOptions,
            context.RequestAborted
        );

        continue;
    }

    if (
        !binarySession.IsApproved
    )
    {
        await SendErrorAsync(
            socket,
            "The session has not been approved.",
            jsonOptions,
            context.RequestAborted
        );

        continue;
    }

    if (
        targetSocket is null ||
        targetSocket.State !=
            WebSocketState.Open
    )
    {
        await SendErrorAsync(
            socket,
            "The remote device is not connected.",
            jsonOptions,
            context.RequestAborted
        );

        continue;
    }

    await SendBinaryAsync(
        targetSocket,
        incoming.Payload,
        context.RequestAborted
    );

    if (
        incoming.Payload.Length > 0 &&
        incoming.Payload[0] ==
            (byte)BinaryPayloadType.Test
    )
    {
        Console.WriteLine(
            $"BINARY TEST DATA: " +
            $"{senderRole} -> peer " +
            $"[{incoming.Payload.Length} bytes]"
        );
    }

    continue;
}

if (
    incoming.MessageType !=
    WebSocketMessageType.Text
)
{
    continue;
}

string json =
    Encoding.UTF8.GetString(
        incoming.Payload
    );

                RemoteBridgeMessage? message;

                try
                {
                    message =
                        JsonSerializer
                            .Deserialize<
                                RemoteBridgeMessage
                            >(
                                json,
                                jsonOptions
                            );
                }
                catch (
                    JsonException
                )
                {
                    await SendMessageAsync(
                        socket,
                        new RemoteBridgeMessage
                        {
                            Type =
                                MessageTypes.Error,

                            Message =
                                "Invalid JSON message."
                        },
                        jsonOptions,
                        context.RequestAborted
                    );

                    continue;
                }

                if (message is null)
                {
                    continue;
                }

                switch (message.Type)
                {
                    case MessageTypes.RegisterHost:
                    {
                        if (
                            hostedSessionCode
                                is not null ||
                            joinedSessionCode
                                is not null
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "This connection already belongs to a session.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        RemoteSession session;

                        while (true)
                        {
                            string code =
                                GenerateSessionCode();

                            session =
                                new RemoteSession
                                {
                                    SessionCode =
                                        code,

                                    HostSocket =
                                        socket,

                                    HostDeviceName =
                                        message
                                            .DeviceName
                                        ?? "Unknown device"
                                };

                            if (
                                sessions
                                    .TryAdd(
                                        code,
                                        session
                                    )
                            )
                            {
                                hostedSessionCode =
                                    code;

                                break;
                            }
                        }

                        Console.WriteLine(
                            $"HOST REGISTERED: " +
                            $"{session.HostDeviceName}"
                        );

                        Console.WriteLine(
                            $"SESSION: " +
                            $"{session.SessionCode}"
                        );

                        await SendMessageAsync(
                            socket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .HostRegistered,

                                SessionCode =
                                    session
                                        .SessionCode
                            },
                            jsonOptions,
                            context.RequestAborted
                        );

                        break;
                    }


                    case MessageTypes.JoinSession:
                    {
                        if (
                            hostedSessionCode
                                is not null ||
                            joinedSessionCode
                                is not null
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "This connection already belongs to a session.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        string code =
                            SessionCodeUtility
                                .Normalize(
                                    message.SessionCode
                                );

                        if (
                            code.Length != 9 ||
                            !sessions.TryGetValue(
                                code,
                                out RemoteSession?
                                    session
                            )
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "Session not found.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        bool viewerAlreadyConnected;

                        lock (
                            session.SyncRoot
                        )
                        {
                            viewerAlreadyConnected =
                                session
                                    .ViewerSocket
                                    is not null &&
                                session
                                    .ViewerSocket
                                    .State ==
                                    WebSocketState
                                        .Open;

                            if (
                                !viewerAlreadyConnected
                            )
                            {
                                session.ViewerSocket = socket;

                                session.ViewerDeviceName = message.DeviceName
                                ?? "Unknown device";

                                session.IsApproved =
                                    false;

                                joinedSessionCode =
                                    code;
                            }
                        }

                        if (
                            viewerAlreadyConnected
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "This session already has a viewer.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        Console.WriteLine(
                            $"VIEWER REQUESTED SESSION " +
                            $"{code}: " +
                            $"{session.ViewerDeviceName}"
                        );

                        // Tell viewer that the code
                        // was valid.
                        await SendMessageAsync(
                            socket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .JoinPending,

                                SessionCode =
                                    code,

                                Message =
                                    "Waiting for host approval."
                            },
                            jsonOptions,
                            context.RequestAborted
                        );

                        // Notify host.
                        await SendMessageAsync(
                            session.HostSocket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .ViewerRequested,

                                SessionCode =
                                    code,

                                DeviceName =
                                    session
                                        .ViewerDeviceName
                            },
                            jsonOptions,
                            context.RequestAborted
                        );

                        break;
                    }


                    case MessageTypes.ApproveViewer:
                    {
                        if (
                            hostedSessionCode
                                is null ||
                            !sessions.TryGetValue(
                                hostedSessionCode,
                                out RemoteSession?
                                    session
                            ) ||
                            !ReferenceEquals(
                                session.HostSocket,
                                socket
                            )
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "Only the host can approve a viewer.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        WebSocket? viewerSocket;

                        lock (
                                session.SyncRoot
                            )
                                {
                                    viewerSocket =
                                        session.ViewerSocket;

                                if (
                                    viewerSocket is not null &&
                                    viewerSocket.State ==
                                        WebSocketState.Open
                                    )
                                    {
                                        session.IsApproved =
                                            true;
                                    }
                                }

                        if (
                            viewerSocket is null ||
                            viewerSocket.State !=
                                WebSocketState.Open
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "There is no viewer waiting for approval.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        Console.WriteLine(
                            $"SESSION APPROVED: " +
                            $"{hostedSessionCode}"
                        );

                        await SendMessageAsync(
                            viewerSocket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .SessionApproved,

                                SessionCode =
                                    hostedSessionCode,

                                Message =
                                    "The host approved your connection."
                            },
                            jsonOptions,
                            context.RequestAborted
                        );

                        await SendMessageAsync(
                            socket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .SessionApproved,

                                SessionCode =
                                    hostedSessionCode
                            },
                            jsonOptions,
                            context.RequestAborted
                        );

                        break;
                    }


                    case MessageTypes.RejectViewer:
                    {
                        if (
                            hostedSessionCode
                                is null ||
                            !sessions.TryGetValue(
                                hostedSessionCode,
                                out RemoteSession?
                                    session
                            ) ||
                            !ReferenceEquals(
                                session.HostSocket,
                                socket
                            )
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "Only the host can reject a viewer.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        WebSocket? viewerSocket;

                        lock (
                            session.SyncRoot
                        )
                        {
                            viewerSocket =
                                session.ViewerSocket;

                            session.ViewerSocket =
                                null;

                            session.ViewerDeviceName =
                                null;

                            session.IsApproved = 
                                false;
                        }

                        if (
                            viewerSocket is null ||
                            viewerSocket.State !=
                                WebSocketState.Open
                        )
                        {
                            await SendErrorAsync(
                                socket,
                                "There is no viewer waiting for approval.",
                                jsonOptions,
                                context
                                    .RequestAborted
                            );

                            break;
                        }

                        Console.WriteLine(
                            $"SESSION REJECTED: " +
                            $"{hostedSessionCode}"
                        );

                        await SendMessageAsync(
                            viewerSocket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .SessionRejected,

                                SessionCode =
                                    hostedSessionCode,

                                Message =
                                    "The host rejected the connection."
                            },
                            jsonOptions,
                            context.RequestAborted
                        );

                        await SendMessageAsync(
                            socket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .SessionRejected,

                                SessionCode =
                                    hostedSessionCode
                            },
                            jsonOptions,
                            context.RequestAborted
                        );

                        break;
                    }


                    case MessageTypes.SessionData:
{
    if (
        string.IsNullOrWhiteSpace(
            message.Data
        )
    )
    {
        await SendErrorAsync(
            socket,
            "Session data cannot be empty.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    // Temporary development limit.
    // Later binary screen frames will use
    // a separate transport path.
    if (
        message.Data.Length >
        1024
    )
    {
        await SendErrorAsync(
            socket,
            "Session data is too large.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    RemoteSession? session =
        null;

    WebSocket? targetSocket =
        null;

    string senderRole;
    string senderDeviceName;

    // HOST → VIEWER
    if (
        hostedSessionCode
            is not null &&
        sessions.TryGetValue(
            hostedSessionCode,
            out session
        )
    )
    {
        lock (
            session.SyncRoot
        )
        {
            if (
                !session.IsApproved
            )
            {
                targetSocket =
                    null;
            }
            else
            {
                targetSocket =
                    session.ViewerSocket;
            }
        }

        senderRole =
            "Host";

        senderDeviceName =
            session.HostDeviceName;
    }

    // VIEWER → HOST
    else if (
        joinedSessionCode
            is not null &&
        sessions.TryGetValue(
            joinedSessionCode,
            out session
        )
    )
    {
        lock (
            session.SyncRoot
        )
        {
            if (
                !session.IsApproved
            )
            {
                targetSocket =
                    null;
            }
            else
            {
                targetSocket =
                    session.HostSocket;
            }
        }

        senderRole =
            "Viewer";

        senderDeviceName =
            session.ViewerDeviceName
            ?? "Unknown device";
    }
    else
    {
        await SendErrorAsync(
            socket,
            "This connection does not belong to a session.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        !session.IsApproved
    )
    {
        await SendErrorAsync(
            socket,
            "The session has not been approved.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        targetSocket is null ||
        targetSocket.State !=
            WebSocketState.Open
    )
    {
        await SendErrorAsync(
            socket,
            "The remote device is no longer connected.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    try
    {
        await SendMessageAsync(
            targetSocket,
            new RemoteBridgeMessage
            {
                Type =
                    MessageTypes
                        .SessionData,

                SessionCode =
                    session.SessionCode,

                DeviceName =
                    senderDeviceName,

                SenderRole =
                    senderRole,

                Data =
                    message.Data
            },
            jsonOptions,
            context.RequestAborted
        );

        Console.WriteLine(
            $"SESSION DATA: " +
            $"{senderRole} -> peer " +
            $"[{session.SessionCode}]"
        );
    }
    catch (
        WebSocketException
    )
    {
        await SendErrorAsync(
            socket,
            "Unable to deliver data because the remote device disconnected.",
            jsonOptions,
            context.RequestAborted
        );
    }

    break;
}


case MessageTypes.MouseMove:
{
    if (
        joinedSessionCode is null ||
        !sessions.TryGetValue(
            joinedSessionCode,
            out RemoteSession? session
        )
    )
    {
        await SendErrorAsync(
            socket,
            "Mouse movement can only be sent by an active viewer.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        message.X is null ||
        message.Y is null
    )
    {
        await SendErrorAsync(
            socket,
            "Mouse coordinates are missing.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    double x =
        message.X.Value;

    double y =
        message.Y.Value;

    if (
        double.IsNaN(x) ||
        double.IsNaN(y) ||
        double.IsInfinity(x) ||
        double.IsInfinity(y) ||
        x < 0 ||
        x > 1 ||
        y < 0 ||
        y > 1
    )
    {
        await SendErrorAsync(
            socket,
            "Mouse coordinates are invalid.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    bool approved;

    lock (
        session.SyncRoot
    )
    {
        approved =
            session.IsApproved;
    }

    if (!approved)
    {
        await SendErrorAsync(
            socket,
            "The session has not been approved.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        session.HostSocket.State !=
        WebSocketState.Open
    )
    {
        await SendErrorAsync(
            socket,
            "The host is no longer connected.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    await SendMessageAsync(
        session.HostSocket,
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.MouseMove,

            SessionCode =
                joinedSessionCode,

            X = x,
            Y = y
        },
        jsonOptions,
        context.RequestAborted
    );

    break;
}

case MessageTypes.MouseLeftDown:
case MessageTypes.MouseLeftUp:
{
    if (
        joinedSessionCode is null ||
        !sessions.TryGetValue(
            joinedSessionCode,
            out RemoteSession? session
        )
    )
    {
        await SendErrorAsync(
            socket,
            "Mouse input can only be sent by an active viewer.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    bool approved;

    lock (
        session.SyncRoot
    )
    {
        approved =
            session.IsApproved;
    }

    if (!approved)
    {
        await SendErrorAsync(
            socket,
            "The session has not been approved.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        session.HostSocket.State !=
        WebSocketState.Open
    )
    {
        await SendErrorAsync(
            socket,
            "The host is no longer connected.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    await SendMessageAsync(
        session.HostSocket,
        new RemoteBridgeMessage
        {
            Type =
                message.Type,

            SessionCode =
                joinedSessionCode
        },
        jsonOptions,
        context.RequestAborted
    );

    break;
}


case MessageTypes.EndSession:
{
    if (
        hostedSessionCode is null ||
        !sessions.TryGetValue(
            hostedSessionCode,
            out RemoteSession? session
        ) ||
        !ReferenceEquals(
            session.HostSocket,
            socket
        )
    )
    {
        await SendErrorAsync(
            socket,
            "Only the host can end the remote session.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    WebSocket? viewerSocket;

    lock (
        session.SyncRoot
    )
    {
        viewerSocket =
            session.ViewerSocket;

        session.ViewerSocket =
            null;

        session.ViewerDeviceName =
            null;

        session.IsApproved =
            false;
    }

    if (
        viewerSocket is not null &&
        viewerSocket.State ==
            WebSocketState.Open
    )
    {
        await SendMessageAsync(
            viewerSocket,
            new RemoteBridgeMessage
            {
                Type =
                    MessageTypes
                        .PeerDisconnected,

                SessionCode =
                    hostedSessionCode,

                Message =
                    "The host ended the remote session."
            },
            jsonOptions,
            context.RequestAborted
        );
    }

    await SendMessageAsync(
        socket,
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.EndSession,

            SessionCode =
                hostedSessionCode,

            Message =
                "Remote session ended."
        },
        jsonOptions,
        context.RequestAborted
    );

    Console.WriteLine(
        $"REMOTE SESSION ENDED: " +
        $"{hostedSessionCode}"
    );

    break;
}


case MessageTypes.KeyDown:
case MessageTypes.KeyUp:
{
    if (
        joinedSessionCode is null ||
        !sessions.TryGetValue(
            joinedSessionCode,
            out RemoteSession? session
        )
    )
    {
        await SendErrorAsync(
            socket,
            "Keyboard input can only be sent by an active viewer.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        message.Key is null ||
        message.Key == RemoteKey.None ||
        !Enum.IsDefined(
            typeof(RemoteKey),
            message.Key.Value
        )
    )
    {
        await SendErrorAsync(
            socket,
            "Invalid remote key.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    bool approved;

    lock (session.SyncRoot)
    {
        approved =
            session.IsApproved;
    }

    if (!approved)
    {
        await SendErrorAsync(
            socket,
            "The session has not been approved.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        session.HostSocket.State !=
        WebSocketState.Open
    )
    {
        await SendErrorAsync(
            socket,
            "The host is no longer connected.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    await SendMessageAsync(
        session.HostSocket,
        new RemoteBridgeMessage
        {
            Type =
                message.Type,

            SessionCode =
                joinedSessionCode,

            Key =
                message.Key
        },
        jsonOptions,
        context.RequestAborted
    );

    break;
}

case MessageTypes.MouseWheel:
{
    if (
        joinedSessionCode is null ||
        !sessions.TryGetValue(
            joinedSessionCode,
            out RemoteSession? session
        )
    )
    {
        await SendErrorAsync(
            socket,
            "Mouse wheel input can only be sent by an active viewer.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        message.WheelDelta is null ||
        message.WheelDelta == 0
    )
    {
        await SendErrorAsync(
            socket,
            "Invalid mouse wheel delta.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    int wheelDelta =
        message.WheelDelta.Value;

    // Protect the Host from absurd input values.
    if (
        wheelDelta < -1200 ||
        wheelDelta > 1200
    )
    {
        await SendErrorAsync(
            socket,
            "Mouse wheel delta is outside the allowed range.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    bool approved;

    lock (session.SyncRoot)
    {
        approved =
            session.IsApproved;
    }

    if (!approved)
    {
        await SendErrorAsync(
            socket,
            "The session has not been approved.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    if (
        session.HostSocket.State !=
        WebSocketState.Open
    )
    {
        await SendErrorAsync(
            socket,
            "The host is no longer connected.",
            jsonOptions,
            context.RequestAborted
        );

        break;
    }

    await SendMessageAsync(
        session.HostSocket,
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.MouseWheel,

            SessionCode =
                joinedSessionCode,

            WheelDelta =
                wheelDelta
        },
        jsonOptions,
        context.RequestAborted
    );

    break;
}


                    default:
                    {
                        await SendErrorAsync(
                            socket,
                            $"Unsupported message type: " +
                            $"{message.Type}",
                            jsonOptions,
                            context
                                .RequestAborted
                        );

                        break;
                    }
                }
            }
        }
        catch (
            OperationCanceledException
        )
        {
            // Connection was cancelled.
        }
        catch (
            WebSocketException ex
        )
        {
            Console.WriteLine(
                $"WEBSOCKET DISCONNECTED: " +
                $"{ex.Message}"
            );
        }
        finally
        {
            // Host disconnected.
            if (
                hostedSessionCode
                    is not null &&
                sessions.TryRemove(
                    hostedSessionCode,
                    out RemoteSession?
                        hostedSession
                )
            )
            {
                Console.WriteLine(
                    $"HOST SESSION REMOVED: " +
                    $"{hostedSessionCode}"
                );

                WebSocket? viewerSocket;

                lock (
                    hostedSession.SyncRoot
                )
                {
                    viewerSocket =
                        hostedSession
                            .ViewerSocket;
                }

                if (
                    viewerSocket is not null &&
                    viewerSocket.State ==
                        WebSocketState.Open
                )
                {
                    try
                    {
                        await SendMessageAsync(
                            viewerSocket,
                            new RemoteBridgeMessage
                            {
                                Type =
                                    MessageTypes
                                        .PeerDisconnected,

                                SessionCode =
                                    hostedSessionCode,

                                Message =
                                    "The host disconnected."
                            },
                            jsonOptions,
                            CancellationToken.None
                        );
                    }
                    catch
                    {
                        // Viewer may already
                        // be disconnected.
                    }
                }
            }

            // Viewer disconnected.
            if (
    joinedSessionCode is not null &&
    sessions.TryGetValue(
        joinedSessionCode,
        out RemoteSession? joinedSession
    )
)
{
    bool viewerWasAttached =
        false;

    lock (
        joinedSession.SyncRoot
    )
    {
        if (
            ReferenceEquals(
                joinedSession.ViewerSocket,
                socket
            )
        )
        {
            viewerWasAttached =
                true;

            joinedSession.ViewerSocket =
                null;

            joinedSession.ViewerDeviceName =
                null;

            joinedSession.IsApproved =
                false;
        }
    }

    if (viewerWasAttached)
    {
        Console.WriteLine(
            $"VIEWER LEFT SESSION: " +
            $"{joinedSessionCode}"
        );

        if (
            joinedSession.HostSocket.State ==
            WebSocketState.Open
        )
        {
            try
            {
                await SendMessageAsync(
                    joinedSession.HostSocket,
                    new RemoteBridgeMessage
                    {
                        Type =
                            MessageTypes
                                .PeerDisconnected,

                        SessionCode =
                            joinedSessionCode,

                        Message =
                            "The viewer disconnected."
                    },
                    jsonOptions,
                    CancellationToken.None
                );
            }
            catch
            {
                // Host may have disconnected
                // at nearly the same time.
            }
        }
    }
}

    // Remove the per-socket send lock
    // after all connection cleanup.
    WebSocketSendCoordinator
        .Remove(
            socket
        );

        }
    }
);

app.Run();


static string GenerateSessionCode()
{
    int number =
        RandomNumberGenerator.GetInt32(
            0,
            1_000_000_000
        );

    return number.ToString(
        "D9"
    );
}

static async Task SendErrorAsync(
    WebSocket socket,
    string message,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    await SendMessageAsync(
        socket,
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.Error,

            Message =
                message
        },
        jsonOptions,
        cancellationToken
    );
}


static async Task SendMessageAsync(
    WebSocket socket,
    RemoteBridgeMessage message,
    JsonSerializerOptions jsonOptions,
    CancellationToken cancellationToken)
{
    string json =
        JsonSerializer.Serialize(
            message,
            jsonOptions
        );

    byte[] bytes =
        Encoding.UTF8.GetBytes(
            json
        );

    await WebSocketSendCoordinator
    .SendAsync(
        socket,
        new ArraySegment<byte>(
            bytes
        ),
        WebSocketMessageType.Text,
        true,
        cancellationToken
    );
}

static async Task<ReceivedSocketMessage?>
    ReceiveSocketMessageAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
{
    byte[] buffer =
        new byte[8192];

    using var stream =
        new MemoryStream();

    WebSocketMessageType? messageType =
        null;

    while (true)
    {
        WebSocketReceiveResult result =
            await socket.ReceiveAsync(
                new ArraySegment<byte>(
                    buffer
                ),
                cancellationToken
            );

        if (
            result.MessageType ==
            WebSocketMessageType.Close
        )
        {
            return null;
        }

        if (messageType is null)
        {
            messageType =
                result.MessageType;
        }
        else if (
            result.MessageType !=
            messageType.Value
        )
        {
            throw new
                InvalidOperationException(
                    "A fragmented WebSocket message changed message type."
                );
        }

        long maximumSize =
            messageType ==
            WebSocketMessageType.Binary
                ? 4 * 1024 * 1024
                : 64 * 1024;

        if (
            stream.Length +
            result.Count >
            maximumSize
        )
        {
            throw new
                InvalidOperationException(
                    "WebSocket message exceeded the allowed size."
                );
        }

        stream.Write(
            buffer,
            0,
            result.Count
        );

        if (result.EndOfMessage)
        {
            break;
        }
    }

    return new ReceivedSocketMessage(
        messageType!.Value,
        stream.ToArray()
    );
}

static async Task SendBinaryAsync(
    WebSocket socket,
    byte[] payload,
    CancellationToken cancellationToken)
{
    await WebSocketSendCoordinator
    .SendAsync(
        socket,
        new ArraySegment<byte>(
            payload
        ),
        WebSocketMessageType.Binary,
        true,
        cancellationToken
    );
}

sealed record ReceivedSocketMessage(
    WebSocketMessageType MessageType,
    byte[] Payload
);