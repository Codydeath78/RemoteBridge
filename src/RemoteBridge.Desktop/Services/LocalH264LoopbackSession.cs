#if WINDOWS

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using RemoteBridge.WindowsMedia;


namespace RemoteBridge.Desktop.Services;


internal sealed class LocalH264LoopbackSession :
    IAsyncDisposable
{
    private readonly WindowsGraphicsCaptureService
        _capture;


    private readonly Func<
        byte[],
        long,
        CancellationToken,
        ValueTask
    >
        _submitAsync;


    private readonly Channel<
        H264EncodedAccessUnit
    >
        _accessUnits;


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
        _accessUnitsReceived;

    private long
        _accessUnitsSubmitted;

    private long
        _bytesReceived;


    public LocalH264LoopbackSession(
        Func<
            byte[],
            long,
            CancellationToken,
            ValueTask
        > submitAsync)
    {
        ArgumentNullException.ThrowIfNull(
            submitAsync
        );


        _submitAsync =
            submitAsync;


        _capture =
            new
                WindowsGraphicsCaptureService();


        //
        // For this first product-loop validation we
        // deliberately do NOT drop encoded H.264
        // access units.
        //
        // The encoder output must stay in order
        // because P-frames may depend on earlier
        // access units.
        //
        _accessUnits =
            Channel
                .CreateUnbounded<
                    H264EncodedAccessUnit
                >(
                    new
                        UnboundedChannelOptions
                        {
                            SingleReader =
                                true,

                            SingleWriter =
                                true,

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


    public long AccessUnitsReceived =>
        Interlocked.Read(
            ref _accessUnitsReceived
        );


    public long AccessUnitsSubmitted =>
        Interlocked.Read(
            ref _accessUnitsSubmitted
        );


    public long BytesReceived =>
        Interlocked.Read(
            ref _bytesReceived
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
            throw new
                ObjectDisposedException(
                    nameof(
                        LocalH264LoopbackSession
                    )
                );
        }


        StatusChanged?.Invoke(
            "Starting local H.264 product loop..."
        );


        _worker =
            Task.Run(
                ProcessAccessUnitsAsync
            );


        try
        {
            _capture.Start();


            StatusChanged?.Invoke(
                "Local WGC → H.264 → decoder → GPU presenter loop started."
            );
        }
        catch
        {
            _accessUnits
                .Writer
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
                $"Capture: {message}"
            );
        }
        catch
        {
            //
            // Diagnostics listeners must not
            // interfere with capture.
            //
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
    // Take ownership of our own compressed
    // H.264 byte array.
    //
    // This copies only the compressed H.264
    // access unit, not decoded video pixels.
    //
    byte[]
        ownedBytes =
            (byte[])
                accessUnit
                    .Bytes
                    .Clone();


    H264EncodedAccessUnit
        ownedAccessUnit =
            new(
                ownedBytes,

                accessUnit
                    .Timestamp100Ns,

                accessUnit
                    .Width,

                accessUnit
                    .Height
            );


    if (
        !_accessUnits
            .Writer
            .TryWrite(
                ownedAccessUnit
            )
    )
    {
        return;
    }


    Interlocked.Increment(
        ref _accessUnitsReceived
    );


    Interlocked.Add(
        ref _bytesReceived,
        ownedBytes.Length
    );
}


    private async Task
        ProcessAccessUnitsAsync()
    {
        try
        {
            await foreach (
                H264EncodedAccessUnit
                    accessUnit
                in _accessUnits
                    .Reader
                    .ReadAllAsync(
                        _shutdown.Token
                    )
                    .ConfigureAwait(
                        false
                    )
            )
            {
                await _submitAsync(
                        accessUnit.Bytes,
                        accessUnit.Timestamp100Ns,
                        _shutdown.Token
                    )
                    .ConfigureAwait(
                        false
                    );


                long submitted =
                    Interlocked.Increment(
                        ref _accessUnitsSubmitted
                    );


                if (
                    submitted ==
                        1
                )
                {
                    StatusChanged?.Invoke(
                        "First encoded H.264 access unit reached the Viewer pipeline."
                    );
                }


                if (
                    submitted %
                        120 ==
                    0
                )
                {
                    StatusChanged?.Invoke(
                        $"Viewer pipeline received {submitted:N0} H.264 access units."
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
            //
            // Normal shutdown.
            //
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


        StatusChanged?.Invoke(
            "Stopping local H.264 product loop..."
        );


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
            //
            // Shutdown remains best-effort.
            //
        }


        _accessUnits
            .Writer
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
                    .ConfigureAwait(
                        false
                    );
            }
            catch (
                OperationCanceledException
            )
            {
            }
        }


        StatusChanged?.Invoke(
            "Local H.264 product loop stopped."
        );
    }


    public async ValueTask DisposeAsync()
    {
        await StopAsync()
            .ConfigureAwait(
                false
            );


        _capture.Dispose();

        _shutdown.Dispose();


        GC.SuppressFinalize(
            this
        );
    }
}


#endif