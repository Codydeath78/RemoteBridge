using System.Threading.Channels;


namespace RemoteBridge.WindowsMedia;


public sealed class ViewerMediaPipeline :
    IDisposable,
    IAsyncDisposable
{
    private const int
        DefaultQueueCapacity =
            4;


    private readonly
        H264DecoderSession
        _decoder;


    private readonly
        D3D11VideoPresenter
        _presenter;


    private readonly Channel<
        EncodedAccessUnit
    >
        _queue;


    private readonly
        CancellationTokenSource
        _shutdown =
            new();


    private readonly Task
        _worker;


    private int
        _disposeStarted;

    private const int
        MaxConsecutivePresenterFailures =
            8;


    private int
        _consecutivePresenterFailures;


    private long
        _accessUnitsReceived;

    private long
        _framesDecoded;

    private long
        _framesPresented;


    private Exception?
        _terminalError;


    public ViewerMediaPipeline(
        IntPtr windowHandle,
        int queueCapacity =
            DefaultQueueCapacity)
    {
        if (
            queueCapacity <=
            0
        )
        {
            throw new
                ArgumentOutOfRangeException(
                    nameof(queueCapacity)
                );
        }


        H264DecoderSession?
            decoder =
                null;


        D3D11VideoPresenter?
            presenter =
                null;


        try
        {
            decoder =
                H264DecoderSession
                    .Create();


            presenter =
                new
                    D3D11VideoPresenter(
                        windowHandle
                    );


            _decoder =
                decoder;


            _presenter =
                presenter;


            _queue =
                Channel
                    .CreateBounded<
                        EncodedAccessUnit
                    >(
                        new
                            BoundedChannelOptions(
                                queueCapacity
                            )
                            {
                                //
                                // IMPORTANT:
                                //
                                // Never DropOldest here.
                                //
                                // H.264 P-frames can depend on earlier
                                // encoded frames. Losing arbitrary encoded
                                // access units can break the decoder until
                                // the next IDR/keyframe.
                                //
                                FullMode =
                                    BoundedChannelFullMode
                                        .Wait,

                                SingleReader =
                                    true,

                                SingleWriter =
                                    false,

                                AllowSynchronousContinuations =
                                    false
                            }
                    );


            _worker =
                Task.Run(
                    ProcessLoopAsync
                );
        }
        catch
        {
            presenter?.Dispose();

            decoder?.Dispose();

            throw;
        }
    }


    public event Action<Exception>?
        Faulted;


    public string DecoderName =>
        _decoder.DecoderName;


    public long AccessUnitsReceived =>
        Interlocked.Read(
            ref _accessUnitsReceived
        );


    public long FramesDecoded =>
        Interlocked.Read(
            ref _framesDecoded
        );


    public long FramesPresented =>
        Interlocked.Read(
            ref _framesPresented
        );


    public long DecoderInputFrames =>
        _decoder.InputFrames;


    public long DecoderOutputFrames =>
        _decoder.OutputFrames;


    public Exception?
        TerminalError =>
            Volatile.Read(
                ref _terminalError
            );


    ///
    /// Queues one complete H.264 access unit.
    ///
    /// This overload copies the supplied memory so that the
    /// network receive buffer may be safely reused immediately
    /// after this method returns.
    ///
    public async ValueTask SubmitAsync(
        ReadOnlyMemory<byte>
            h264AccessUnit,
        long timestamp100Ns,
        CancellationToken
            cancellationToken =
                default)
    {
        ThrowIfDisposed();


        if (
            h264AccessUnit.IsEmpty
        )
        {
            return;
        }


        byte[]
            ownedBytes =
                h264AccessUnit
                    .ToArray();


        await SubmitOwnedAsync(
                ownedBytes,
                timestamp100Ns,
                cancellationToken
            )
            .ConfigureAwait(
                false
            );
    }


    ///
    /// Queues one complete H.264 access unit without copying it.
    ///
    /// Ownership of h264AccessUnit transfers to this pipeline.
    /// The caller must not modify or reuse the array after this
    /// method is called.
    ///
    public async ValueTask SubmitOwnedAsync(
        byte[] h264AccessUnit,
        long timestamp100Ns,
        CancellationToken
            cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            h264AccessUnit
        );


        ThrowIfDisposed();


        if (
            h264AccessUnit.Length ==
            0
        )
        {
            return;
        }


        EncodedAccessUnit
            accessUnit =
                new(
                    h264AccessUnit,
                    timestamp100Ns
                );


        try
        {
            await _queue
                .Writer
                .WriteAsync(
                    accessUnit,
                    cancellationToken
                )
                .ConfigureAwait(
                    false
                );
        }
        catch (ChannelClosedException ex)
{
    ThrowIfDisposed();


    Exception?
        terminalError =
            TerminalError ??
            ex.InnerException;


    if (terminalError is not null)
    {
        throw new InvalidOperationException(
            $"Viewer media pipeline is closed because its worker failed: " +
            $"{terminalError.Message}",
            terminalError
        );
    }


    throw;
}


        Interlocked.Increment(
            ref _accessUnitsReceived
        );
    }

private async Task ProcessLoopAsync()
{
    try
    {
        await foreach (
            EncodedAccessUnit accessUnit
            in _queue.Reader
                .ReadAllAsync(
                    _shutdown.Token
                )
                .ConfigureAwait(false)
        )
        {
            bool inputSubmitted =
                false;


            while (!inputSubmitted)
            {
                try
                {
                    inputSubmitted =
                        _decoder
                            .TrySubmitInput(
                                accessUnit.Bytes,
                                accessUnit.Timestamp100Ns
                            );
                }
                catch (Exception ex)
                {
                    long inputNumber =
                        _decoder.InputFrames +
                        1;


                    throw new InvalidOperationException(
                        $"H.264 DECODER input stage failed near " +
                        $"input #{inputNumber:N0}. " +
                        $"Access unit: {accessUnit.Bytes.Length:N0} bytes, " +
                        $"timestamp {accessUnit.Timestamp100Ns}.",
                        ex
                    );
                }


                if (!inputSubmitted)
                {
                    //
                    // Media Foundation refused more
                    // input because output is pending.
                    //
                    // Drain everything currently
                    // available, then retry this SAME
                    // encoded access unit.
                    //
                    int drained =
                        DrainDecoderOutput();


                    if (drained == 0)
                    {
                        throw new InvalidOperationException(
                            "The H.264 decoder refused input with " +
                            "MF_E_NOTACCEPTING but produced no pending output."
                        );
                    }
                }
            }


            //
            // Once this input is accepted, drain every
            // decoded frame currently available.
            //
            DrainDecoderOutput();
        }
    }
    catch (OperationCanceledException)
        when (_shutdown.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
        Volatile.Write(
            ref _terminalError,
            ex
        );


        _queue.Writer.TryComplete(
            ex
        );


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

private int DrainDecoderOutput()
{
    int drained =
        0;


    while (true)
    {
        H264DecodedFrame?
            decodedFrame;


        try
        {
            decodedFrame =
                _decoder
                    .TryTakePendingOutput();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"H.264 DECODER output drain failed after " +
                $"{_decoder.InputFrames:N0} accepted inputs.",
                ex
            );
        }


        if (decodedFrame is null)
        {
            break;
        }


        using (decodedFrame)
        {
            PresentDecodedFrame(
                decodedFrame
            );
        }


        drained++;
    }


    return drained;
}


private void PresentDecodedFrame(
    H264DecodedFrame decodedFrame)
{
    long decodedNumber =
        Interlocked.Increment(
            ref _framesDecoded
        );


    bool presented;


    try
    {
        presented =
            _presenter.Present(
                decodedFrame
            );


        //
        // A successful presentation proves that
        // any previous D3D11 failure was transient.
        //
        Interlocked.Exchange(
            ref _consecutivePresenterFailures,
            0
        );
    }
    catch (Exception ex)
    {
        int failureCount =
            Interlocked.Increment(
                ref _consecutivePresenterFailures
            );


        //
        // A fullscreen/resize transition can make
        // one presentation temporarily fail.
        //
        // The H.264 frame has already been decoded,
        // so it is safe to discard this DISPLAY frame
        // and continue with the next decoded frame.
        //
        if (
            failureCount <
            MaxConsecutivePresenterFailures
        )
        {
            return;
        }


        throw new InvalidOperationException(
            $"D3D11 PRESENTER stage failed " +
            $"{failureCount} consecutive times. " +
            $"Last failure was on decoded " +
            $"frame #{decodedNumber:N0}. " +
            $"Texture: " +
            $"{decodedFrame.TextureWidth}x" +
            $"{decodedFrame.TextureHeight}, " +
            $"format={decodedFrame.TextureFormat}, " +
            $"arraySize={decodedFrame.TextureArraySize}, " +
            $"subresource=" +
            $"{decodedFrame.TextureSubresourceIndex}.",
            ex
        );
    }


    if (presented)
    {
        Interlocked.Increment(
            ref _framesPresented
        );
    }
}


    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(
                ref _disposeStarted
            ) !=
            0,
            this
        );
    }


    private bool BeginShutdown()
    {
        if (
            Interlocked.Exchange(
                ref _disposeStarted,
                1
            ) !=
            0
        )
        {
            return false;
        }


        _queue
            .Writer
            .TryComplete();


        _shutdown.Cancel();


        return true;
    }


    public void Dispose()
    {
        if (!BeginShutdown())
        {
            return;
        }


        try
        {
            _worker
                .GetAwaiter()
                .GetResult();
        }
        catch (
            OperationCanceledException
        )
        {
        }
        finally
        {
            DisposeResources();
        }
    }


    public async ValueTask DisposeAsync()
    {
        if (!BeginShutdown())
        {
            return;
        }


        try
        {
            await _worker
                .ConfigureAwait(
                    false
                );
        }
        catch (
            OperationCanceledException
        )
        {
        }
        finally
        {
            DisposeResources();
        }
    }


    private void DisposeResources()
    {
        _presenter.Dispose();

        _decoder.Dispose();

        _shutdown.Dispose();


        GC.SuppressFinalize(
            this
        );
    }


    private readonly record struct
        EncodedAccessUnit(
            byte[] Bytes,
            long Timestamp100Ns
        );
}