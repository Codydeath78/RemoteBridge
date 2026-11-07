using Vortice;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RemoteBridge.WindowsMedia;


internal sealed class D3D11WgcNv12Converter :
    IDisposable
{
    private readonly H264StreamSettings
        _settings;


    private readonly ID3D11Device
        _device;

    private readonly ID3D11DeviceContext
        _deviceContext;

    private readonly ID3D11VideoDevice
        _videoDevice;

    private readonly ID3D11VideoContext
        _videoContext;

    private readonly ID3D11VideoProcessorEnumerator
        _enumerator;

    private readonly ID3D11VideoProcessor
        _processor;

    private readonly ID3D11Texture2D
        _nv12Texture;

    private readonly ID3D11VideoProcessorOutputView
        _outputView;

    private readonly RawRect
        _destinationRectangle;


    private bool _disposed;


    public D3D11WgcNv12Converter(
        Direct3D11DeviceBundle deviceBundle,
        uint sourceWidth,
        uint sourceHeight)
        : this(
            deviceBundle,
            sourceWidth,
            sourceHeight,
            H264StreamSettings.Default
        )
    {
    }


    public D3D11WgcNv12Converter(
        Direct3D11DeviceBundle deviceBundle,
        uint sourceWidth,
        uint sourceHeight,
        H264StreamSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            deviceBundle
        );

        ArgumentNullException.ThrowIfNull(
            settings
        );

        settings.Validate();

        if (
            sourceWidth == 0 ||
            sourceHeight == 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceWidth)
            );
        }


        _settings =
            settings;


        SourceWidth =
            sourceWidth;

        SourceHeight =
            sourceHeight;


        //
        // IMPORTANT:
        //
        // These are borrowed references.
        // Direct3D11DeviceBundle owns them.
        //
        _device =
            deviceBundle.NativeDevice;

        _deviceContext =
            deviceBundle.NativeContext;


        _videoDevice =
            _device
                .QueryInterface<
                    ID3D11VideoDevice
                >();


        _videoContext =
            _deviceContext
                .QueryInterface<
                    ID3D11VideoContext
                >();


        VideoProcessorContentDescription
            contentDescription =
                new()
                {
                    InputFrameFormat =
                        VideoFrameFormat.Progressive,

                    InputFrameRate =
                        new Rational(
                            _settings.FramesPerSecond,
                            1
                        ),

                    InputWidth =
                        sourceWidth,

                    InputHeight =
                        sourceHeight,

                    OutputFrameRate =
                        new Rational(
                            _settings.FramesPerSecond,
                            1
                        ),

                    OutputWidth =
                        _settings.Width,

                    OutputHeight =
                        _settings.Height,

                    Usage =
                        VideoUsage.PlaybackNormal
                };


        _videoDevice
            .CreateVideoProcessorEnumerator(
                contentDescription,
                out ID3D11VideoProcessorEnumerator?
                    enumerator
            )
            .CheckError();


        _enumerator =
            enumerator ??
            throw new InvalidOperationException(
                "D3D11 returned no video processor enumerator."
            );


        _videoDevice
            .CreateVideoProcessor(
                _enumerator,
                0,
                out ID3D11VideoProcessor?
                    processor
            )
            .CheckError();


        _processor =
            processor ??
            throw new InvalidOperationException(
                "D3D11 returned no video processor."
            );


        Texture2DDescription
            outputDescription =
                new()
                {
                    Width =
                        _settings.Width,

                    Height =
                        _settings.Height,

                    MipLevels =
                        1,

                    ArraySize =
                        1,

                    Format =
                        Format.NV12,

                    SampleDescription =
                        new SampleDescription(
                            1,
                            0
                        ),

                    Usage =
                        ResourceUsage.Default,

                    BindFlags =
                        BindFlags.RenderTarget,

                    CPUAccessFlags =
                        CpuAccessFlags.None,

                    MiscFlags =
                        ResourceOptionFlags.None
                };


        _nv12Texture =
            _device
                .CreateTexture2D(
                    outputDescription
                );


        VideoProcessorOutputViewDescription
            outputViewDescription =
                new()
                {
                    ViewDimension =
                        VideoProcessorOutputViewDimension
                            .Texture2D,

                    Texture2D =
                        new Texture2DVideoProcessorOutputView
                        {
                            MipSlice =
                                0
                        }
                };


        _videoDevice
            .CreateVideoProcessorOutputView(
                _nv12Texture,
                _enumerator,
                outputViewDescription,
                out ID3D11VideoProcessorOutputView?
                    outputView
            )
            .CheckError();


        _outputView =
            outputView ??
            throw new InvalidOperationException(
                "D3D11 returned no NV12 output view."
            );


        _destinationRectangle =
            CalculateAspectFit(
                sourceWidth,
                sourceHeight,
                _settings.Width,
                _settings.Height
            );


        _videoContext
            .VideoProcessorSetStreamFrameFormat(
                _processor,
                0,
                VideoFrameFormat.Progressive
            );


        _videoContext
            .VideoProcessorSetStreamDestRect(
                _processor,
                0,
                true,
                _destinationRectangle
            );
    }


    public uint SourceWidth
    {
        get;
    }


    public uint SourceHeight
    {
        get;
    }


    public uint Nv12Width =>
        _settings.Width;


    public uint Nv12Height =>
        _settings.Height;


    //
    // The converter owns this texture.
    // Do NOT dispose it from callers.
    //
    public ID3D11Texture2D Nv12Texture =>
        _nv12Texture;


    public RawRect DestinationRectangle =>
        _destinationRectangle;


    public void Convert(
        ID3D11Texture2D sourceTexture)
    {
        ObjectDisposedException
            .ThrowIf(
                _disposed,
                this
            );

        ArgumentNullException.ThrowIfNull(
            sourceTexture
        );


        Texture2DDescription
            sourceDescription =
                sourceTexture.Description;


        if (
            sourceDescription.Width
                != SourceWidth ||
            sourceDescription.Height
                != SourceHeight
        )
        {
            throw new InvalidOperationException(
                $"WGC texture size changed from " +
                $"{SourceWidth}x{SourceHeight} to " +
                $"{sourceDescription.Width}x" +
                $"{sourceDescription.Height}."
            );
        }


        if (
            sourceDescription.Format
                != Format.B8G8R8A8_UNorm
        )
        {
            throw new InvalidOperationException(
                $"Unexpected WGC texture format: " +
                $"{sourceDescription.Format}."
            );
        }


        VideoProcessorInputViewDescription
            inputViewDescription =
                new()
                {
                    FourCC =
                        0,

                    ViewDimension =
                        VideoProcessorInputViewDimension
                            .Texture2D,

                    Texture2D =
                        new Texture2DVideoProcessorInputView
                        {
                            MipSlice =
                                0,

                            ArraySlice =
                                0
                        }
                };


        _videoDevice
            .CreateVideoProcessorInputView(
                sourceTexture,
                _enumerator,
                inputViewDescription,
                out ID3D11VideoProcessorInputView?
                    inputView
            )
            .CheckError();


        if (inputView is null)
        {
            throw new InvalidOperationException(
                "D3D11 returned no WGC video processor input view."
            );
        }


        using (inputView)
        {
            VideoProcessorStream
                stream =
                    new()
                    {
                        Enable =
                            true,

                        InputSurface =
                            inputView
                    };


            _videoContext
                .VideoProcessorBlt(
                    _processor,
                    _outputView,
                    0,
                    1,
                    [stream]
                )
                .CheckError();
        }


        //
        // For this probe we explicitly submit
        // the commands each converted frame.
        //
        // Once encoding is attached we'll test
        // whether this Flush is still necessary.
        //
        _deviceContext.Flush();
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed =
            true;


        _outputView.Dispose();
        _nv12Texture.Dispose();
        _processor.Dispose();
        _enumerator.Dispose();
        _videoContext.Dispose();
        _videoDevice.Dispose();
    }


    private static RawRect CalculateAspectFit(
        uint sourceWidth,
        uint sourceHeight,
        uint outputWidth,
        uint outputHeight)
    {
        double scale =
            Math.Min(
                (double)outputWidth /
                    sourceWidth,

                (double)outputHeight /
                    sourceHeight
            );


        int fittedWidth =
            MakeEven(
                (int)Math.Round(
                    sourceWidth *
                    scale
                )
            );


        int fittedHeight =
            MakeEven(
                (int)Math.Round(
                    sourceHeight *
                    scale
                )
            );


        fittedWidth =
            Math.Min(
                fittedWidth,
                (int)outputWidth
            );


        fittedHeight =
            Math.Min(
                fittedHeight,
                (int)outputHeight
            );


        int left =
            MakeEven(
                (
                    (int)outputWidth -
                    fittedWidth
                ) / 2
            );


        int top =
            MakeEven(
                (
                    (int)outputHeight -
                    fittedHeight
                ) / 2
            );


        return new RawRect(
            left,
            top,
            left + fittedWidth,
            top + fittedHeight
        );
    }


    private static int MakeEven(
        int value)
    {
        value =
            Math.Max(
                0,
                value
            );

        return value & ~1;
    }
}