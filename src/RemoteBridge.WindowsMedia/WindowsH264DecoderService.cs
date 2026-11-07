using System.Threading.Channels;


namespace RemoteBridge.WindowsMedia;


public sealed class WindowsH264DecoderService :
    IH264DecoderService
{
    private const int QueueCapacity =
        120;


    private readonly object
        _sync =
            new();


    private Channel<H264DecoderInputFrame>?
        _channel;


    private CancellationTokenSource?
        _cancellation;


    private Task?
        _decoderTask;


    private long
        _queuedFrames;


    private long
        _decodedFrames;


    private long
        _droppedUntilKeyFrame;


    private bool
        _needsKeyFrame =
            true;


    private bool
        _disposed;


    public event Action<DecodedVideoFrameInfo>?
        FrameDecoded;


    public event Action<string>?
        StatusChanged;


    public event Action<Exception>?
        DecoderFailed;


    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return
                    _decoderTask is not null &&
                    !_decoderTask.IsCompleted;
            }
        }
    }


    public long QueuedFrames =>
        Interlocked.Read(
            ref _queuedFrames
        );


    public long DecodedFrames =>
        Interlocked.Read(
            ref _decodedFrames
        );


    public long DroppedUntilKeyFrame =>
        Interlocked.Read(
            ref _droppedUntilKeyFrame
        );


    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException
                .ThrowIf(
                    _disposed,
                    this
                );


            if (
                _decoderTask is not null &&
                !_decoderTask.IsCompleted
            )
            {
                return;
            }


            _needsKeyFrame =
                true;


            Interlocked.Exchange(
                ref _queuedFrames,
                0
            );


            Interlocked.Exchange(
                ref _decodedFrames,
                0
            );


            Interlocked.Exchange(
                ref _droppedUntilKeyFrame,
                0
            );


            _channel =
                Channel.CreateBounded<
                    H264DecoderInputFrame
                >(
                    new BoundedChannelOptions(
                        QueueCapacity
                    )
                    {
                        SingleReader =
                            true,

                        SingleWriter =
                            false,

                        FullMode =
                            BoundedChannelFullMode
                                .Wait
                    }
                );


            _cancellation =
                new CancellationTokenSource();


            _decoderTask =
                RunDecoderLoopAsync(
                    _channel.Reader,
                    _cancellation.Token
                );
        }
    }


    public bool TryQueue(
        H264DecoderInputFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame
        );


        Channel<H264DecoderInputFrame>?
            channel;


        lock (_sync)
        {
            if (
                _disposed ||
                _channel is null ||
                _cancellation is null ||
                _cancellation
                    .IsCancellationRequested
            )
            {
                return false;
            }


            channel =
                _channel;
        }


        //
        // Never begin decoding in the middle
        // of an H.264 prediction chain.
        //
        if (_needsKeyFrame)
        {
            if (!frame.IsKeyFrame)
            {
                Interlocked.Increment(
                    ref _droppedUntilKeyFrame
                );

                return true;
            }


            _needsKeyFrame =
                false;
        }


        bool queued =
            channel.Writer
                .TryWrite(
                    frame
                );


        if (!queued)
        {
            //
            // We lost encoded ordering.
            //
            // Do not continue decoding random
            // dependent P-frames.
            //
            _needsKeyFrame =
                true;

            return false;
        }


        Interlocked.Increment(
            ref _queuedFrames
        );


        return true;
    }


    private async Task RunDecoderLoopAsync(
        ChannelReader<H264DecoderInputFrame>
            reader,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (
                H264DecoderInputFrame frame
                in reader.ReadAllAsync(
                    cancellationToken
                )
            )
            {
                //
                // Actual Media Foundation decoder
                // session is added in Step 431.
                //
                await Task.Yield();
            }
        }
        catch (OperationCanceledException)
        {
            //
            // Normal shutdown.
            //
        }
        catch (Exception ex)
        {
            DecoderFailed?.Invoke(
                ex
            );
        }
    }


    public async Task StopAsync()
    {
        CancellationTokenSource?
            cancellation;

        Channel<H264DecoderInputFrame>?
            channel;

        Task?
            decoderTask;


        lock (_sync)
        {
            cancellation =
                _cancellation;

            channel =
                _channel;

            decoderTask =
                _decoderTask;


            _cancellation =
                null;

            _channel =
                null;

            _decoderTask =
                null;


            _needsKeyFrame =
                true;
        }


        channel?.Writer
            .TryComplete();


        cancellation?.Cancel();


        if (decoderTask is not null)
        {
            try
            {
                await decoderTask;
            }
            catch (OperationCanceledException)
            {
                //
                // Expected.
                //
            }
        }


        cancellation?.Dispose();
    }


    public async ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }


            _disposed =
                true;
        }


        await StopAsync();
    }
}