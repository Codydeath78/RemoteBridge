using System.Diagnostics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Vortice.Direct3D11;
using SharpGen.Runtime;
using Vortice.MediaFoundation;
using static Vortice.MediaFoundation.MediaFactory;

namespace RemoteBridge.WindowsMedia;

public sealed class WindowsGraphicsCaptureService :
    IDisposable
{
    private readonly object
        _sync =
            new();

    private readonly H264StreamSettings
        _settings;

    private readonly FrameRateGate
        _encoderFrameGate;

    private D3D11WgcNv12Converter?
        _nv12Converter;

    private NvidiaH264InputProbeSession?
        _nvidiaInputProbe;

    private long
        _nv12SampleFrames;

    private long
        _mfSampleFrames;

    private long
        _totalMfSamples;

    private bool
        _mediaFoundationStarted;

    private long
        _totalNv12Frames;

    private Direct3D11DeviceBundle?
        _deviceBundle;

    private GraphicsCaptureItem?
        _captureItem;

    private Direct3D11CaptureFramePool?
        _framePool;

    private GraphicsCaptureSession?
        _captureSession;

    private Stopwatch?
        _runningTimer;

    private Stopwatch?
        _sampleTimer;

    private long
        _sampleFrames;

    private long
        _rawSampleFrames;

    private long
        _selectedSampleFrames;

    private long
        _textureSampleFrames;

    private long
        _totalRawFrames;

    private long
        _totalSelectedFrames;

    private long
        _totalFrames;


    private long
        _h264InputSampleFrames;

    private long
        _h264OutputSampleFrames;

    private long
        _h264OutputSampleBytes;

    private long
        _h264DroppedSampleFrames;

    private long
        _totalH264InputFrames;

    private long
        _totalH264OutputFrames;

    private long
        _totalH264OutputBytes;

    private long
        _totalH264DroppedFrames;

    private bool
        _loggedFirstContinuousH264Output;

    private SizeInt32
        _captureSize;

    private bool
        _running;

    private bool
        _disposed;


    public WindowsGraphicsCaptureService()
        : this(
            H264StreamSettings.Default
        )
    {
    }


    public WindowsGraphicsCaptureService(
        H264StreamSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings
        );

        settings.Validate();


        _settings =
            settings;


        _encoderFrameGate =
            new FrameRateGate(
                checked(
                    (int)settings.FramesPerSecond
                )
            );
    }


    public event Action<string>?
        StatusChanged;

    public event Action<WgcCaptureStatistics>?
        StatisticsUpdated;

    public event Action<H264EncodedAccessUnit>?
        H264AccessUnitReady;


    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _running;
            }
        }
    }


    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException
                .ThrowIf(
                    _disposed,
                    this
                );

            if (_running)
            {
                return;
            }

            if (
                !GraphicsCaptureSession
                    .IsSupported()
            )
            {
                throw new PlatformNotSupportedException(
                    "Windows Graphics Capture is not supported on this computer."
                );
            }

            MFStartup()
                .CheckError();

            _mediaFoundationStarted =
                true;

            StatusChanged?.Invoke(
                "Media Foundation started."
            );



            try
            {
                StatusChanged?.Invoke(
                    "Creating D3D11 hardware device..."
                );

                _deviceBundle =
                    Direct3D11DeviceBundle
                        .Create();


                StatusChanged?.Invoke(
                    "Creating NVIDIA H.264 encoder on the WGC D3D11 device..."
                );


                _nvidiaInputProbe =
                    NvidiaH264InputProbeSession
                        .Create(
                            _deviceBundle,
                            _settings
                        );


                StatusChanged?.Invoke(
                    $"NVIDIA encoder ready and requesting input: " +
                    $"{_nvidiaInputProbe.EncoderName}"
                );

                StatusChanged?.Invoke(
                    $"D3D11 ready — " +
                    $"feature level " +
                    $"{_deviceBundle.FeatureLevel}"
                );

                StatusChanged?.Invoke(
                    "Creating primary-monitor capture target..."
                );

                _captureItem =
                    GraphicsCaptureItemFactory
                        .CreateForPrimaryMonitor();

                _captureSize =
                    _captureItem.Size;

                if (
                    _captureSize.Width <= 0 ||
                    _captureSize.Height <= 0
                )
                {
                    throw new InvalidOperationException(
                        "Windows Graphics Capture reported invalid monitor dimensions."
                    );
                }

                StatusChanged?.Invoke(
                    $"Primary monitor: " +
                    $"{_captureSize.Width}×" +
                    $"{_captureSize.Height}"
                );

                _framePool =
                    Direct3D11CaptureFramePool
                        .CreateFreeThreaded(
                            _deviceBundle
                                .WinRtDevice,

                            DirectXPixelFormat
                                .B8G8R8A8UIntNormalized,

                            3,

                            _captureSize
                        );

                _framePool.FrameArrived +=
                    OnFrameArrived;

                _captureSession =
                    _framePool
                        .CreateCaptureSession(
                            _captureItem
                        );

                if (
                    OperatingSystem
                        .IsWindowsVersionAtLeast(
                            10,
                            0,
                            19041
                        )
                )
                {
                    //
                    // The Viewer already has an immediate local
                    // cursor. Do not encode the delayed Host
                    // cursor into the H.264 stream as well.
                    //
                    _captureSession
                        .IsCursorCaptureEnabled =
                            false;
                }



                

                _nv12SampleFrames =
                        0;

                _totalNv12Frames =
                        0;

                _mfSampleFrames =
                        0;

                _totalMfSamples =
                        0;

                _nv12Converter?.Dispose();

                _nv12Converter =
                        null;

                _rawSampleFrames =
                        0;

                _selectedSampleFrames =
                        0;

                _textureSampleFrames =
                        0;

                _totalRawFrames =
                        0;

                _totalSelectedFrames =
                        0;

                _h264InputSampleFrames =
                        0;

                _h264OutputSampleFrames =
                        0;

                _h264OutputSampleBytes =
                        0;

                _h264DroppedSampleFrames =
                        0;

                _totalH264InputFrames =
                        0;

                _totalH264OutputFrames = 
                        0;

                _totalH264OutputBytes =
                        0;

                _totalH264DroppedFrames =
                        0;

                _loggedFirstContinuousH264Output =
                        false;


                _encoderFrameGate.Reset();

                _sampleTimer =
                    Stopwatch.StartNew();

                _runningTimer =
                    Stopwatch.StartNew();

                _running =
                    true;

                _captureSession
                    .StartCapture();

                StatusChanged?.Invoke(
                    "WGC capture started."
                );
            }
            catch
            {
                CleanupLocked();
                throw;
            }
        }
    }


    public void Stop()
    {
        lock (_sync)
        {
            if (!_running)
            {
                return;
            }

            _running =
                false;

            CleanupLocked();

            StatusChanged?.Invoke(
                "WGC capture stopped."
            );
        }
    }


private void OnFrameArrived(
    Direct3D11CaptureFramePool sender,
    object args)
{
    SizeInt32 contentSize;

    bool selectedForEncoder =
        false;

    bool textureBridgeSucceeded =
        false;

    bool nv12ConversionSucceeded = 
        false;

    bool mfSampleSucceeded =
        false;

    try
    {
        using Direct3D11CaptureFrame
            frame =
                sender
                    .TryGetNextFrame();

        if (frame is null)
        {
            return;
        }

        contentSize =
            frame.ContentSize;

        lock (_sync)
        {
            if (!_running)
            {
                return;
            }

            _rawSampleFrames++;
            _totalRawFrames++;

            selectedForEncoder =
                _encoderFrameGate
                    .ShouldProcess(
                        Stopwatch
                            .GetTimestamp()
                    );

            if (selectedForEncoder)
            {
                _selectedSampleFrames++;
                _totalSelectedFrames++;
            }
        }

        if (selectedForEncoder)
        {
    using ID3D11Texture2D sourceTexture =
        Direct3DSurfaceInterop
            .GetTexture2D(
                frame.Surface
            );

    textureBridgeSucceeded =
        true;


    string? converterStatus =
        null;


    lock (_sync)
    {
        if (!_running)
        {
            return;
        }


        if (_deviceBundle is null)
        {
            throw new InvalidOperationException(
                "The WGC D3D11 device is unavailable."
            );
        }


        if (
            _nv12Converter is null ||
            _nv12Converter.SourceWidth !=
                (uint)contentSize.Width ||
            _nv12Converter.SourceHeight !=
                (uint)contentSize.Height
        )
        {
            _nv12Converter?.Dispose();


            _nv12Converter =
                new D3D11WgcNv12Converter(
                    _deviceBundle,
                    (uint)contentSize.Width,
                    (uint)contentSize.Height,
                    _settings
                );


            var destination =
                _nv12Converter
                    .DestinationRectangle;


            converterStatus =
                $"NV12 converter ready - " +
                $"source " +
                $"{contentSize.Width}x" +
                $"{contentSize.Height}, " +
                $"destination " +
                $"{destination.Right - destination.Left}x" +
                $"{destination.Bottom - destination.Top} " +
                $"at " +
                $"({destination.Left}," +
                $"{destination.Top})";
        }


        _nv12Converter.Convert(
            sourceTexture
        );


        nv12ConversionSucceeded =
            true;

        long sampleTime100Ns =
            _runningTimer?
                .Elapsed
                .Ticks
                ?? 0;


        //
        // One second = 10,000,000
        // Media Foundation time units.
        //
        // 60 FPS = approximately 166,667
        // 100-nanosecond units per frame.
        //
        long sampleDuration100Ns =
            10_000_000L /
            _settings.FramesPerSecond;


        if (_nvidiaInputProbe is not null)
        {
            NvidiaH264ContinuousFrameResult encodeResult =
                _nvidiaInputProbe
                    .ProcessFrame(
                        _nv12Converter
                            .Nv12Texture,

                        sampleTime100Ns,
                        sampleDuration100Ns
                    );


            //
            // A submitted frame means we successfully
            // created and handed an MF sample to NVIDIA.
            //
            mfSampleSucceeded =
                encodeResult
                    .SubmittedInput;


            //
            // If NVIDIA has no available NeedInput token
            // or the GPU snapshot pool is full, drop this
            // source frame instead of blocking capture.
            //
            if (
                encodeResult
                    .DroppedForBackpressure
            )
            {
                _h264DroppedSampleFrames++;

                _totalH264DroppedFrames++;
            }


            if (
                encodeResult
                    .SubmittedInput
            )
            {
                _h264InputSampleFrames++;

                _totalH264InputFrames++;
            }


            //
            // An async encoder call can return zero,
            // one, or occasionally more encoded outputs.
            //
            foreach (
                MediaFoundationEncodedOutput encoded
                in encodeResult.Outputs
            )
            {
                _h264OutputSampleFrames++;

                _totalH264OutputFrames++;


                _h264OutputSampleBytes +=
                    encoded.Bytes.Length;

                _totalH264OutputBytes +=
                    encoded.Bytes.Length;

                H264AccessUnitReady?.Invoke(
                    new H264EncodedAccessUnit(
                        Bytes:
                            encoded.Bytes,

                        Timestamp100Ns:
                            sampleTime100Ns,

                        Width:
                            checked(
                                (ushort)_settings.Width
                            ),

                        Height:
                            checked(
                                (ushort)_settings.Height
                            )
                    )
                );


                //
                // Only print the first continuous
                // H.264 access unit so we don't spam
                // the console at ~60 FPS.
                //
                if (!_loggedFirstContinuousH264Output)
                {
                    _loggedFirstContinuousH264Output =
                        true;


                    int previewLength =
                        Math.Min(
                            16,
                            encoded.Bytes.Length
                        );


                    string preview =
                        previewLength == 0
                            ? "(none)"
                            : Convert.ToHexString(
                                encoded.Bytes
                                    .AsSpan(
                                        0,
                                        previewLength
                                    )
                            );


                    StatusChanged?.Invoke(
                        $"First continuous H.264 output: " +
                        $"{encoded.Bytes.Length:N0} bytes, " +
                        $"{preview}"
                    );
                }
 
            }
        }
    }


    if (converterStatus is not null)
    {
        StatusChanged?.Invoke(
            converterStatus
        );
    }
}
    }
    catch (ObjectDisposedException)
    {
        return;
    }
    catch (Exception ex)
    {
        StatusChanged?.Invoke(
            $"GPU/MF frame pipeline failed: {ex.Message}"
        );

        return;
    }

    bool recreate =
        false;

    WgcCaptureStatistics?
        statistics =
            null;

    lock (_sync)
    {
        if (!_running)
        {
            return;
        }

        if (textureBridgeSucceeded)
        {
            _textureSampleFrames++;
        }

        if (nv12ConversionSucceeded)
        {
            _nv12SampleFrames++;

            _totalNv12Frames++;
        }

        if (mfSampleSucceeded)
        {
            _mfSampleFrames++;

            _totalMfSamples++;
        }

        if (
            contentSize.Width > 0 &&
            contentSize.Height > 0 &&
            (
                contentSize.Width !=
                    _captureSize.Width ||
                contentSize.Height !=
                    _captureSize.Height
            )
        )
        {
            _captureSize =
                contentSize;

            recreate =
                true;
        }

        if (
    _sampleTimer is not null &&
    _runningTimer is not null &&
    _sampleTimer.Elapsed >=
        TimeSpan.FromSeconds(1)
)
{
    double seconds =
        Math.Max(
            0.001,
            _sampleTimer
                .Elapsed
                .TotalSeconds
        );


    double nv12FramesPerSecond =
        _nv12SampleFrames /
        seconds;

    double mfSampleFramesPerSecond =
        _mfSampleFrames /
        seconds;

    double h264InputFramesPerSecond =
        _h264InputSampleFrames /
        seconds;


    double h264OutputFramesPerSecond =
        _h264OutputSampleFrames /
        seconds;


    double h264MegabitsPerSecond =
        (
            _h264OutputSampleBytes *
            8.0
        ) /
        1_000_000.0 /
        seconds;


    statistics =
    new WgcCaptureStatistics(
        Width:
            _captureSize.Width,

        Height:
            _captureSize.Height,

        RawFramesPerSecond:
            _rawSampleFrames /
            seconds,

        SelectedFramesPerSecond:
            _selectedSampleFrames /
            seconds,

        TextureFramesPerSecond:
            _textureSampleFrames /
            seconds,

        Nv12FramesPerSecond:
            nv12FramesPerSecond,

        MfSampleFramesPerSecond:
            mfSampleFramesPerSecond,

        TotalRawFrames:
            _totalRawFrames,

        TotalSelectedFrames:
            _totalSelectedFrames,

        TotalNv12Frames:
            _totalNv12Frames,

        TotalMfSamples:
            _totalMfSamples,

        RunningTime:
            _runningTimer.Elapsed,
        

        H264InputFramesPerSecond:
            h264InputFramesPerSecond,

        H264OutputFramesPerSecond:
            h264OutputFramesPerSecond,

        H264MegabitsPerSecond:
            h264MegabitsPerSecond,

        H264DroppedFrames:
            _h264DroppedSampleFrames,

        TotalH264InputFrames:
            _totalH264InputFrames,

        TotalH264OutputFrames:
            _totalH264OutputFrames,

        TotalH264OutputBytes:
            _totalH264OutputBytes,

        TotalH264DroppedFrames:
        _totalH264DroppedFrames

    );


    _rawSampleFrames =
        0;

    _selectedSampleFrames =
        0;

    _textureSampleFrames =
        0;

    _nv12SampleFrames =
        0;

    _mfSampleFrames =
        0;


    _h264InputSampleFrames =
        0;

    _h264OutputSampleFrames =
        0;

    _h264OutputSampleBytes =
        0;

    _h264DroppedSampleFrames =
        0;


    _sampleTimer.Restart();
}
    }

    if (
        recreate &&
        _deviceBundle is not null
    )
    {
        try
        {
            sender.Recreate(
                _deviceBundle
                    .WinRtDevice,

                DirectXPixelFormat
                    .B8G8R8A8UIntNormalized,

                3,

                contentSize
            );
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    if (statistics is not null)
    {
        StatisticsUpdated?.Invoke(
            statistics
        );
    }
}


    private void CleanupLocked()
    {
        if (_framePool is not null)
        {
            _framePool.FrameArrived -=
                OnFrameArrived;
        }

        _nvidiaInputProbe?.Dispose();

        _nvidiaInputProbe =
            null;


        _nv12Converter?.Dispose();

        _nv12Converter =
            null;

        _captureSession?.Dispose();
        _captureSession =
            null;

        _framePool?.Dispose();
        _framePool =
            null;

        _captureItem =
            null;

        _deviceBundle?.Dispose();
        _deviceBundle =
            null;

        _sampleTimer?.Stop();
        _sampleTimer =
            null;

        _runningTimer?.Stop();
        _runningTimer =
            null;

        _sampleFrames =
            0;


        if (_mediaFoundationStarted)
        {
            _mediaFoundationStarted =
                false;

            try
            {
                MFShutdown()
                    .CheckError();
            }
            catch
            {
                //
                // Cleanup must not mask the
                // original shutdown reason.
                //
            }
        }



    }


    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _running =
                false;

            CleanupLocked();

            _disposed =
                true;
        }

        GC.SuppressFinalize(
            this
        );
    }
}