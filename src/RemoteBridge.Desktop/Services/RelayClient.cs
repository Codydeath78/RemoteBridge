using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RemoteBridge.Protocol;

namespace RemoteBridge.Desktop.Services;

public sealed class RelayClient
{
    private readonly Uri _relayUri;

    private readonly SemaphoreSlim
        _sendLock =
            new(1, 1);

    public RelayClient()
    {
        _relayUri =
            LoadRelayUri();
    }

    private ClientWebSocket? _socket;

    private CancellationTokenSource?
        _receiveCancellation;

    private Task? _receiveLoopTask;

    private static readonly
        JsonSerializerOptions
        JsonOptions =
            new()
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy
                        .CamelCase,

                PropertyNameCaseInsensitive =
                    true
            };
    

    private static RemoteBridgeMessage
    DeserializeProtocolMessage(
        byte[] payload)
{
    string json =
        Encoding.UTF8.GetString(
            payload
        );

    RemoteBridgeMessage? message =
        JsonSerializer
            .Deserialize<
                RemoteBridgeMessage
            >(
                json,
                JsonOptions
            );

    return message
        ?? throw new
            InvalidOperationException(
                "Relay returned an empty response."
            );
}

private static Uri LoadRelayUri()
{
    string configPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "relay-url.txt"
        );

    // Development on the Mac:
    // if no configuration file exists,
    // use the local relay.
    if (!File.Exists(configPath))
    {
        return new Uri(
            "ws://localhost:5080/ws"
        );
    }

    string value =
        File.ReadAllText(
            configPath
        )
        .Trim();

    if (
        !Uri.TryCreate(
            value,
            UriKind.Absolute,
            out Uri? uri
        )
    )
    {
        throw new
            InvalidOperationException(
                $"Invalid relay URL in {configPath}"
            );
    }

    if (
        uri.Scheme != "ws" &&
        uri.Scheme != "wss"
    )
    {
        throw new
            InvalidOperationException(
                "RemoteBridge relay URL must use ws:// or wss://."
            );
    }

    return uri;
}

    public event Action<string>?
        ViewerRequested;



    public event Action?
        RemoteMouseLeftDownReceived;

    public event Action?
        RemoteMouseLeftUpReceived;

    public event Action?
        SessionEnded;

    public event Action?
        SessionApproved;

    public event Action<string>?
        SessionRejected;

    public event Action<string>?
        ErrorReceived;


    public event Action<
        string,
        string,
        string
    >?
        SessionDataReceived;

    public event Action<byte[]>?
        ScreenFrameReceived;


    public event Action<string>?
        PeerDisconnected;

    public event Action<double, double>?
        RemoteMouseMoveReceived;

    public event Action<RemoteKey>?
        RemoteKeyDownReceived;

    public event Action<RemoteKey>?
        RemoteKeyUpReceived;

    public event Action<int>?
        RemoteMouseWheelReceived;



    public bool IsConnected =>
        _socket?.State ==
        WebSocketState.Open;

    

    private async Task
    SendBinaryPayloadAsync(
        BinaryPayloadType payloadType,
        byte[] data,
        CancellationToken cancellationToken =
            default)
{
    if (
        data.Length == 0
    )
    {
        throw new
            ArgumentException(
                "Binary payload cannot be empty.",
                nameof(data)
            );
    }

    if (
        data.Length >
        (4 * 1024 * 1024) - 1
    )
    {
        throw new
            ArgumentException(
                "Binary payload exceeds the maximum size.",
                nameof(data)
            );
    }

    if (
        _socket is null ||
        _socket.State !=
            WebSocketState.Open
    )
    {
        throw new
            InvalidOperationException(
                "RemoteBridge is not connected to the relay."
            );
    }

    byte[] packet =
        new byte[
            data.Length + 1
        ];

    packet[0] =
        (byte)payloadType;

    Buffer.BlockCopy(
        data,
        0,
        packet,
        1,
        data.Length
    );

    await _sendLock.WaitAsync(
        cancellationToken
    );

    try
    {
        await _socket.SendAsync(
            new ArraySegment<byte>(
                packet
            ),
            WebSocketMessageType.Binary,
            true,
            cancellationToken
        );
    }
    finally
    {
        _sendLock.Release();
    }
}


    public async Task<string>
        RegisterHostAsync(
            CancellationToken
                cancellationToken =
                    default)
    {
        await ConnectFreshAsync(
            cancellationToken
        );

        await SendMessageAsync(
            new RemoteBridgeMessage
            {
                Type =
                    MessageTypes
                        .RegisterHost,

                DeviceName =
                    Environment
                        .MachineName
            },
            cancellationToken
        );

        ReceivedSocketMessage incoming =
    await ReceiveSocketMessageAsync(
        _socket!,
        cancellationToken
    );

if (
    incoming.MessageType !=
    WebSocketMessageType.Text
)
{
    throw new
        InvalidOperationException(
            "Expected a protocol response from relay."
        );
}

RemoteBridgeMessage response =
    DeserializeProtocolMessage(
        incoming.Payload
    );

        if (
            response.Type ==
            MessageTypes.Error
        )
        {
            throw new
                InvalidOperationException(
                    response.Message ??
                    "Relay returned an error."
                );
        }

        if (
            response.Type !=
            MessageTypes
                .HostRegistered
        )
        {
            throw new
                InvalidOperationException(
                    $"Unexpected relay response: " +
                    $"{response.Type}"
                );
        }

        if (
            string.IsNullOrWhiteSpace(
                response.SessionCode
            )
        )
        {
            throw new
                InvalidOperationException(
                    "Relay did not return a session code."
                );
        }

        StartReceiveLoop();

        return response.SessionCode;
    }

    public async Task
    SendSessionDataAsync(
        string data)
{
    if (
        string.IsNullOrWhiteSpace(
            data
        )
    )
    {
        throw new
            ArgumentException(
                "Session data cannot be empty.",
                nameof(data)
            );
    }

    await SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes
                    .SessionData,

            Data =
                data
        },
        CancellationToken.None
    );
}


public async Task SendMouseMoveAsync(
    double x,
    double y,
    CancellationToken cancellationToken =
        default)
{
    x =
        Math.Clamp(
            x,
            0.0,
            1.0
        );

    y =
        Math.Clamp(
            y,
            0.0,
            1.0
        );

    await SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.MouseMove,

            X = x,
            Y = y
        },
        cancellationToken
    );
}


    public async Task
        JoinSessionAsync(
            string sessionCode,
            CancellationToken
                cancellationToken =
                    default)
    {
        await ConnectFreshAsync(
            cancellationToken
        );

        await SendMessageAsync(
            new RemoteBridgeMessage
            {
                Type =
                    MessageTypes
                        .JoinSession,

                SessionCode =
                    sessionCode,

                DeviceName =
                    Environment
                        .MachineName
            },
            cancellationToken
        );

        ReceivedSocketMessage incoming =
    await ReceiveSocketMessageAsync(
        _socket!,
        cancellationToken
    );

if (
    incoming.MessageType !=
    WebSocketMessageType.Text
)
{
    throw new
        InvalidOperationException(
            "Expected a protocol response from relay."
        );
}

RemoteBridgeMessage response =
    DeserializeProtocolMessage(
        incoming.Payload
    );

        if (
            response.Type ==
            MessageTypes.Error
        )
        {
            throw new
                InvalidOperationException(
                    response.Message ??
                    "Unable to join session."
                );
        }

        if (
            response.Type !=
            MessageTypes.JoinPending
        )
        {
            throw new
                InvalidOperationException(
                    $"Unexpected relay response: " +
                    $"{response.Type}"
                );
        }

        StartReceiveLoop();
    }


    public async Task
        ApproveViewerAsync()
    {
        await SendMessageAsync(
            new RemoteBridgeMessage
            {
                Type =
                    MessageTypes
                        .ApproveViewer
            },
            CancellationToken.None
        );
    }


    public async Task
        RejectViewerAsync()
    {
        await SendMessageAsync(
            new RemoteBridgeMessage
            {
                Type =
                    MessageTypes
                        .RejectViewer
            },
            CancellationToken.None
        );
    }


    private async Task
        ConnectFreshAsync(
            CancellationToken
                cancellationToken)
    {
        await DisconnectAsync();

        _socket =
            new ClientWebSocket();

        await _socket.ConnectAsync(
            _relayUri,
            cancellationToken
        );
    }


    private void StartReceiveLoop()
    {
        if (_socket is null)
        {
            return;
        }

        _receiveCancellation =
            new CancellationTokenSource();

        ClientWebSocket socket =
            _socket;

        CancellationToken token =
            _receiveCancellation.Token;

        _receiveLoopTask =
            Task.Run(
                () =>
                    ReceiveLoopAsync(
                        socket,
                        token
                    )
            );
    }


    private async Task
        ReceiveLoopAsync(
            ClientWebSocket socket,
            CancellationToken
                cancellationToken)
    {
        try
        {
            while (
                socket.State ==
                    WebSocketState.Open &&
                !cancellationToken
                    .IsCancellationRequested
            )
            {

                ReceivedSocketMessage incoming =
    await ReceiveSocketMessageAsync(
        socket,
        cancellationToken
    );

if (
    incoming.MessageType ==
    WebSocketMessageType.Binary
)
{
    if (
        incoming.Payload.Length <
        1
    )
    {
        ErrorReceived?.Invoke(
            "Received an invalid empty binary packet."
        );

        continue;
    }

    BinaryPayloadType payloadType =
        (BinaryPayloadType)
        incoming.Payload[0];

    byte[] payload =
        incoming.Payload[1..];

    switch (payloadType)
{
    case BinaryPayloadType.ScreenFrameJpeg:
    {
        ScreenFrameReceived
            ?.Invoke(
                payload
            );

        break;
    }

    default:
    {
        ErrorReceived?.Invoke(
            $"Unknown binary payload type: " +
            $"{incoming.Payload[0]}"
        );

        break;
    }
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

RemoteBridgeMessage message =
    DeserializeProtocolMessage(
        incoming.Payload
    );

                switch (message.Type)
                {
                    case MessageTypes
                        .ViewerRequested:
                    {
                        ViewerRequested?.Invoke(
                            message.DeviceName
                            ?? "Unknown device"
                        );

                        break;
                    }

                    case MessageTypes
                        .SessionApproved:
                    {
                        SessionApproved
                            ?.Invoke();

                        break;
                    }


                    case MessageTypes
                        .PeerDisconnected:
                    {
                        PeerDisconnected?.Invoke(
                        message.Message ??
                        "The remote device disconnected."
                        );

                        break;
                    }


                    case MessageTypes.MouseLeftDown:
                    {
                        RemoteMouseLeftDownReceived
                            ?.Invoke();

                        break;
                    }


                    case MessageTypes.MouseLeftUp:
                    {
                        RemoteMouseLeftUpReceived
                            ?.Invoke();

                        break;
                    }


                    case MessageTypes.EndSession:
                    {
                    SessionEnded
                        ?.Invoke();

                    break;
                    }


                    case MessageTypes.MouseMove:
                    {
                        if (
                            message.X is not null &&
                            message.Y is not null
                            )
                            {
                                RemoteMouseMoveReceived
                                    ?.Invoke(
                                        message.X.Value,
                                        message.Y.Value
                                    );
                            }

                            break;
                    }

                    case MessageTypes.KeyDown:
                    {
                        if (message.Key is not null)
                        {
                            RemoteKeyDownReceived
                                ?.Invoke(
                                    message.Key.Value
                                );
                        }

                        break;
                    }


                    case MessageTypes.KeyUp:
                    {
                        if (message.Key is not null)
                        {
                            RemoteKeyUpReceived
                                ?.Invoke(
                                    message.Key.Value
                                );
                        }

                        break;
                    }

                    case MessageTypes.MouseWheel:
                    {
                        if (
                            message.WheelDelta is not null &&
                            message.WheelDelta.Value != 0
                            )
                            {
                                RemoteMouseWheelReceived
                                    ?.Invoke(
                                        message.WheelDelta.Value
                                    );
                            }

                            break;
                    }

                    case MessageTypes
                        .SessionRejected:
                    {
                        SessionRejected?.Invoke(
                            message.Message ??
                            "Connection rejected."
                        );

                        break;
                    }

                    case MessageTypes.Error:
                    {
                        ErrorReceived?.Invoke(
                            message.Message ??
                            "Unknown relay error."
                        );

                        break;
                    }

                    case MessageTypes
                        .SessionData:
                    {
                        SessionDataReceived
                            ?.Invoke(
                                message.SenderRole
                                    ?? "Remote",

                                message.DeviceName
                                    ?? "Unknown device",

                                message.Data
                                    ?? string.Empty
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
            // Normal shutdown.
        }
        catch (
            WebSocketException ex
        )
        {
            ErrorReceived?.Invoke(
                $"Connection lost: " +
                $"{ex.Message}"
            );
        }
        catch (
            Exception ex
        )
        {
            ErrorReceived?.Invoke(
                ex.Message
            );
        }
    }


    private async Task
    SendMessageAsync(
        RemoteBridgeMessage message,
        CancellationToken cancellationToken)
{
    if (
        _socket is null ||
        _socket.State !=
            WebSocketState.Open
    )
    {
        throw new
            InvalidOperationException(
                "RemoteBridge is not connected to the relay."
            );
    }

    string json =
        JsonSerializer.Serialize(
            message,
            JsonOptions
        );

    byte[] bytes =
        Encoding.UTF8.GetBytes(
            json
        );

    await _sendLock.WaitAsync(
        cancellationToken
    );

    try
    {
        await _socket.SendAsync(
            new ArraySegment<byte>(
                bytes
            ),
            WebSocketMessageType.Text,
            true,
            cancellationToken
        );
    }
    finally
    {
        _sendLock.Release();
    }
}


    private static async Task<
    ReceivedSocketMessage
>
    ReceiveSocketMessageAsync(
        ClientWebSocket socket,
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
            throw new
                WebSocketException(
                    "Relay closed the connection."
                );
        }

        if (messageType is null)
        {
            messageType =
                result.MessageType;
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
                    "Incoming WebSocket message exceeded the allowed size."
                );
        }

        stream.Write(
            buffer,
            0,
            result.Count
        );

        if (
            result.EndOfMessage
        )
        {
            break;
        }
    }

    return new ReceivedSocketMessage(
        messageType!.Value,
        stream.ToArray()
    );
}


    public async Task
        DisconnectAsync()
    {
        _receiveCancellation
            ?.Cancel();

        ClientWebSocket? socket =
            _socket;

        _socket = null;

        if (socket is null)
        {
            return;
        }

        try
        {
            if (
                socket.State ==
                WebSocketState.Open
            )
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus
                        .NormalClosure,
                    "Client disconnecting.",
                    CancellationToken.None
                );
            }
        }
        catch
        {
            // Ignore shutdown errors
            // during development.
        }
        finally
        {
            socket.Dispose();

            _receiveCancellation
                ?.Dispose();

            _receiveCancellation =
                null;

            _receiveLoopTask =
                null;
        }
    }

public Task SendMouseWheelAsync(
    int wheelDelta,
    CancellationToken cancellationToken =
        default)
{
    if (wheelDelta == 0)
    {
        return Task.CompletedTask;
    }

    wheelDelta =
        Math.Clamp(
            wheelDelta,
            -1200,
            1200
        );

    return SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.MouseWheel,

            WheelDelta =
                wheelDelta
        },
        cancellationToken
    );
}

public Task SendKeyDownAsync(
    RemoteKey key,
    CancellationToken cancellationToken =
        default)
{
    return SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.KeyDown,

            Key =
                key
        },
        cancellationToken
    );
}


public Task SendKeyUpAsync(
    RemoteKey key,
    CancellationToken cancellationToken =
        default)
{
    return SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.KeyUp,

            Key =
                key
        },
        cancellationToken
    );
}

public Task SendMouseLeftDownAsync(
    CancellationToken cancellationToken =
        default)
{
    return SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.MouseLeftDown
        },
        cancellationToken
    );
}


public Task SendMouseLeftUpAsync(
    CancellationToken cancellationToken =
        default)
{
    return SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.MouseLeftUp
        },
        cancellationToken
    );
}


public Task EndSessionAsync(
    CancellationToken cancellationToken =
        default)
{
    return SendMessageAsync(
        new RemoteBridgeMessage
        {
            Type =
                MessageTypes.EndSession
        },
        cancellationToken
    );
}


public Task SendScreenFrameAsync(
    byte[] jpegBytes,
    CancellationToken cancellationToken =
        default)
{
    return SendBinaryPayloadAsync(
        BinaryPayloadType.ScreenFrameJpeg,
        jpegBytes,
        cancellationToken
    );
}

    private sealed record
        ReceivedSocketMessage(
            WebSocketMessageType MessageType,
            byte[] Payload
        );
}