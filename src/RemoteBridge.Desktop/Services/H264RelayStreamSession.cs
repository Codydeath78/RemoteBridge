#if WINDOWS

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using RemoteBridge.Protocol;
using RemoteBridge.WindowsMedia;


namespace RemoteBridge.Desktop.Services;


internal sealed class H264RelayStreamSession :
    IAsyncDisposable
{
    private const int QueueCapacity =
        4;


    private readonly RelayClient
        _relayClient;


    private readonly WindowsGraphicsCaptureService
        _capture;


    private readonly Channel<H264EncodedAccessUnit>
        _queue;


    private readonly CancellationTokenSource
        _shutdown =
            new();


    private Task?
        _worker;


    private int
        _started;

    private int
        _stopped;


    private long
        _accessUnitsQueued;

    private long
        _accessUnitsSent;

    private long
        _bytesSent;

    public H264RelayStreamSession(
        RelayClient relayClient)
        : this(
            relayClient,
            H264StreamSettings.Default
        )
    {
    }



    public H264RelayStreamSession(
        RelayClient relayClient,
        H264StreamSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            relayClient
        );

        ArgumentNullException.ThrowIfNull(
            settings
        );

        settings.Validate();


        _relayClient =
            relayClient;


        _capture =
            new WindowsGraphicsCaptureService(
                settings
            );


        _queue =
            Channel
                .CreateBounded<
                    H264EncodedAccessUnit
                >(
                    new BoundedChannelOptions(
                        QueueCapacity
                    )
                    {
                        FullMode =
                            BoundedChannelFullMode.Wait,

                        SingleReader =
                            true,

                        SingleWriter =
                            false,

                        AllowSynchronousContinuations =
                            false
                    }
                );  


        _capture.StatusChanged +=
            OnCaptureStatusChanged;


        _capture.H264AccessUnitReady +=
            OnH264AccessUnitReady;
    }


    public event Action<string>?
        StatusChanged;


    public event Action<Exception>?
        Faulted;


    public long AccessUnitsSent =>
        Interlocked.Read(
            ref _accessUnitsSent
        );


    public long BytesSent =>
        Interlocked.Read(
            ref _bytesSent
        );


    public void Start()
    {
        if (
            Interlocked.Exchange(
                ref _started,
                1
            ) !=
            0
        )
        {
            return;
        }


        if (
            Volatile.Read(
                ref _stopped
            ) !=
            0
        )
        {
            throw new ObjectDisposedException(
                nameof(
                    H264RelayStreamSession
                )
            );
        }


        _worker =
            Task.Run(
                SendLoopAsync
            );


        try
        {
            _capture.Start();


            StatusChanged?.Invoke(
                "H.264 hardware stream started."
            );
        }
        catch
        {
            _queue.Writer
                .TryComplete();


            _shutdown.Cancel();


            throw;
        }
    }


    private void OnCaptureStatusChanged(
        string message)
    {
        try
        {
            StatusChanged?.Invoke(
                message
            );
        }
        catch
        {
        }
    }


    private void OnH264AccessUnitReady(
        H264EncodedAccessUnit accessUnit)
    {
        if (
            Volatile.Read(
                ref _stopped
            ) !=
            0
        )
        {
            return;
        }


        //
        // Own the compressed bytes before the
        // encoder callback returns.
        //
        byte[] ownedBytes =
            (byte[])
                accessUnit
                    .Bytes
                    .Clone();


        H264EncodedAccessUnit owned =
            new(
                ownedBytes,
                accessUnit.Timestamp100Ns,
                accessUnit.Width,
                accessUnit.Height
            );


        try
        {
            //
            // Intentional backpressure.
            //
            // We DO NOT DropOldest here because
            // already-encoded H.264 P-frames may
            // depend on earlier access units.
            //
            // If the network cannot keep up,
            // blocking here naturally slows the
            // producer and stale WGC frames are
            // discarded BEFORE they are encoded.
            //
            _queue.Writer
                .WriteAsync(
                    owned,
                    _shutdown.Token
                )
                .AsTask()
                .GetAwaiter()
                .GetResult();


            Interlocked.Increment(
                ref _accessUnitsQueued
            );
        }
        catch (
            OperationCanceledException
        )
        {
        }
        catch (
            ChannelClosedException
        )
        {
        }
    }


    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (
                H264EncodedAccessUnit accessUnit
                in _queue.Reader
                    .ReadAllAsync(
                        _shutdown.Token
                    )
                    .ConfigureAwait(false)
            )
            {
                byte[] packet =
                    H264VideoPacket.Create(
                        accessUnit.Bytes,
                        accessUnit.Timestamp100Ns,
                        accessUnit.Width,
                        accessUnit.Height
                    );


                //
                // Reuse the existing
                // binary screen-frame transport.
                //
                await _relayClient
                    .SendScreenFrameAsync(
                        packet,
                        _shutdown.Token
                    )
                    .ConfigureAwait(false);


                long sent =
                    Interlocked.Increment(
                        ref _accessUnitsSent
                    );


                Interlocked.Add(
                    ref _bytesSent,
                    accessUnit.Bytes.Length
                );


                if (
                    sent ==
                    1
                )
                {
                    StatusChanged?.Invoke(
                        "First H.264 access unit sent through the Relay."
                    );
                }


                if (
                    sent %
                    120 ==
                    0
                )
                {
                    double megabytes =
                        BytesSent /
                        1024.0 /
                        1024.0;


                    StatusChanged?.Invoke(
                        $"Sent {sent:N0} H.264 access units " +
                        $"({megabytes:F1} MB compressed)."
                    );
                }
            }
        }
        catch (
            OperationCanceledException
        )
        when (
            _shutdown
                .IsCancellationRequested
        )
        {
        }
        catch (Exception ex)
        {
            try
            {
                Faulted?.Invoke(
                    ex
                );
            }
            catch
            {
            }


            _queue.Writer
                .TryComplete(
                    ex
                );
        }
    }


    public async ValueTask StopAsync()
    {
        if (
            Interlocked.Exchange(
                ref _stopped,
                1
            ) !=
            0
        )
        {
            return;
        }


        _capture.H264AccessUnitReady -=
            OnH264AccessUnitReady;


        _capture.StatusChanged -=
            OnCaptureStatusChanged;


        try
        {
            _capture.Stop();
        }
        catch
        {
        }


        _queue.Writer
            .TryComplete();


        _shutdown.Cancel();


        Task?
            worker =
                _worker;


        if (worker is not null)
        {
            try
            {
                await worker
                    .ConfigureAwait(false);
            }
            catch (
                OperationCanceledException
            )
            {
            }
        }
    }


    public async ValueTask DisposeAsync()
    {
        await StopAsync()
            .ConfigureAwait(false);


        _capture.Dispose();

        _shutdown.Dispose();


        GC.SuppressFinalize(
            this
        );
    }
}


#endif