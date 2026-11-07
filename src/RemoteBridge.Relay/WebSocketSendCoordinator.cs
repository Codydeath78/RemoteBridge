using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace RemoteBridge.Relay;

public static class WebSocketSendCoordinator
{
    private static readonly
        ConcurrentDictionary<
            WebSocket,
            SemaphoreSlim
        > SendLocks =
            new();


    public static async Task SendAsync(
        WebSocket socket,
        ArraySegment<byte> payload,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        SemaphoreSlim sendLock =
            SendLocks.GetOrAdd(
                socket,
                _ => new SemaphoreSlim(1, 1)
            );

        await sendLock.WaitAsync(
            cancellationToken
        );

        try
        {
            if (
                socket.State !=
                WebSocketState.Open
            )
            {
                throw new WebSocketException(
                    "The WebSocket is not open."
                );
            }

            await socket.SendAsync(
                payload,
                messageType,
                endOfMessage,
                cancellationToken
            );
        }
        finally
        {
            sendLock.Release();
        }
    }


    public static void Remove(
        WebSocket socket)
    {
        // Don't Dispose here because another
        // in-flight sender could still be
        // releasing this semaphore.
        SendLocks.TryRemove(
            socket,
            out _
        );
    }
}