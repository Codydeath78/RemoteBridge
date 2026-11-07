using System.ComponentModel;
using System.Runtime.InteropServices;

using SharpGen.Runtime;
using Vortice;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

using static Vortice.DXGI.DXGI;


namespace RemoteBridge.WindowsMedia;


public sealed class D3D11VideoPresenter :
    IDisposable
{
    private static readonly Format
        SwapChainFormat =
            Format.B8G8R8A8_UNorm;


    private readonly object
        _sync =
            new();


    private readonly IntPtr
        _windowHandle;


    private ID3D11Device?
        _device;

    private ID3D11DeviceContext?
        _deviceContext;

    private ID3D11VideoDevice?
        _videoDevice;

    private ID3D11VideoContext?
        _videoContext;


    private IDXGIFactory2?
        _factory;

    private IDXGISwapChain1?
        _swapChain;


    private ID3D11Texture2D?
        _backBuffer;

    private ID3D11RenderTargetView?
        _backBufferRenderTarget;


    private ID3D11VideoProcessorEnumerator?
        _processorEnumerator;

    private ID3D11VideoProcessor?
        _processor;

    private ID3D11VideoProcessorOutputView?
        _processorOutputView;


    private uint
        _sourceWidth;

    private uint
        _sourceHeight;


    private uint
        _targetWidth;

    private uint
        _targetHeight;


    private uint
        _outputFrameNumber;


    private bool
        _disposed;


    public D3D11VideoPresenter(
        IntPtr windowHandle)
    {
        if (
            windowHandle ==
            IntPtr.Zero
        )
        {
            throw new
                ArgumentException(
                    "A valid HWND is required.",
                    nameof(windowHandle)
                );
        }


        if (!OperatingSystem.IsWindows())
        {
            throw new
                PlatformNotSupportedException(
                    "The D3D11 video presenter requires Windows."
                );
        }


        _windowHandle =
            windowHandle;
    }


    public uint SourceWidth
    {
        get
        {
            lock (_sync)
            {
                return _sourceWidth;
            }
        }
    }


    public uint SourceHeight
    {
        get
        {
            lock (_sync)
            {
                return _sourceHeight;
            }
        }
    }


    public uint TargetWidth
    {
        get
        {
            lock (_sync)
            {
                return _targetWidth;
            }
        }
    }


    public uint TargetHeight
    {
        get
        {
            lock (_sync)
            {
                return _targetHeight;
            }
        }
    }


    public bool Present(
        H264DecodedFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame
        );


        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this
            );


            if (
                !TryGetClientSize(
                    out uint targetWidth,
                    out uint targetHeight
                )
            )
            {
                //
                // The native Viewer surface can temporarily
                // have zero size while minimized or while
                // Avalonia is performing layout.
                //
                return false;
            }


            using ID3D11Texture2D
                sourceTexture =
                    DecodedFrameTextureInterop
                        .GetTexture2D(
                            frame,
                            out uint
                                subresourceIndex
                        );


            Texture2DDescription
                sourceDescription =
                    sourceTexture.Description;


            if (
                sourceDescription.Format !=
                Format.NV12
            )
            {
                throw new
                    InvalidOperationException(
                        $"The Viewer renderer expects NV12 decoder output, " +
                        $"but received {sourceDescription.Format}."
                    );
            }


            if (
                sourceDescription.Width ==
                    0 ||
                sourceDescription.Height ==
                    0
            )
            {
                throw new
                    InvalidOperationException(
                        "The decoder returned a zero-sized D3D11 texture."
                    );
            }


            EnsureDevice(
                sourceTexture
            );


            EnsureSwapChain(
                targetWidth,
                targetHeight
            );


            EnsureVideoProcessor(
                sourceDescription.Width,
                sourceDescription.Height,
                targetWidth,
                targetHeight
            );


            ID3D11VideoDevice
                videoDevice =
                    _videoDevice ??
                    throw new
                        InvalidOperationException(
                            "The D3D11 video device is unavailable."
                        );


            ID3D11VideoContext
                videoContext =
                    _videoContext ??
                    throw new
                        InvalidOperationException(
                            "The D3D11 video context is unavailable."
                        );


            ID3D11VideoProcessorEnumerator
                processorEnumerator =
                    _processorEnumerator ??
                    throw new
                        InvalidOperationException(
                            "The video processor enumerator is unavailable."
                        );


            ID3D11VideoProcessor
                processor =
                    _processor ??
                    throw new
                        InvalidOperationException(
                            "The video processor is unavailable."
                        );


            ID3D11VideoProcessorOutputView
                outputView =
                    _processorOutputView ??
                    throw new
                        InvalidOperationException(
                            "The video processor output view is unavailable."
                        );


            uint mipLevels =
                sourceDescription.MipLevels;


            if (mipLevels == 0)
            {
                mipLevels =
                    1;
            }


            uint mipSlice =
                subresourceIndex %
                mipLevels;


            uint arraySlice =
                subresourceIndex /
                mipLevels;


            if (
                arraySlice >=
                sourceDescription.ArraySize
            )
            {
                throw new
                    InvalidOperationException(
                        $"Decoder subresource {subresourceIndex} maps to " +
                        $"array slice {arraySlice}, but the texture only " +
                        $"contains {sourceDescription.ArraySize} slice(s)."
                    );
            }


            VideoProcessorInputViewDescription
                inputViewDescription =
                    new()
                    {
                        //
                        // Zero means use the DXGI format
                        // already attached to the texture:
                        // DXGI_FORMAT_NV12.
                        //
                        FourCC =
                            0,

                        ViewDimension =
                            VideoProcessorInputViewDimension
                                .Texture2D,

                        Texture2D =
                            new
                                Texture2DVideoProcessorInputView
                            {
                                MipSlice =
                                    mipSlice,

                                ArraySlice =
                                    arraySlice
                            }
                    };


            videoDevice
                .CreateVideoProcessorInputView(
                    sourceTexture,
                    processorEnumerator,
                    inputViewDescription,
                    out ID3D11VideoProcessorInputView
                        inputView
                )
                .CheckError();


            using (inputView)
            {
                RawRect
                    sourceRectangle =
                        new(
                            0,
                            0,
                            checked(
                                (int)sourceDescription
                                    .Width
                            ),
                            checked(
                                (int)sourceDescription
                                    .Height
                            )
                        );


                RawRect
                    targetRectangle =
                        CalculateLetterboxRectangle(
                            sourceDescription.Width,
                            sourceDescription.Height,
                            targetWidth,
                            targetHeight
                        );


                RawRect
                    fullOutputRectangle =
                        new(
                            0,
                            0,
                            checked(
                                (int)targetWidth
                            ),
                            checked(
                                (int)targetHeight
                            )
                        );


                videoContext
                    .VideoProcessorSetStreamFrameFormat(
                        processor,
                        0,
                        VideoFrameFormat
                            .Progressive
                    );


                videoContext
                    .VideoProcessorSetStreamAutoProcessingMode(
                        processor,
                        0,
                        false
                    );


                videoContext
                    .VideoProcessorSetStreamSourceRect(
                        processor,
                        0,
                        true,
                        sourceRectangle
                    );


                videoContext
                    .VideoProcessorSetStreamDestRect(
                        processor,
                        0,
                        true,
                        targetRectangle
                    );


                videoContext
                    .VideoProcessorSetOutputTargetRect(
                        processor,
                        true,
                        fullOutputRectangle
                    );


                //
                // Clear the full swap-chain buffer first.
                //
                // If the Viewer window and remote desktop
                // have different aspect ratios, this leaves
                // clean black letterbox/pillarbox bars.
                //
                ID3D11DeviceContext
                    deviceContext =
                        _deviceContext ??
                        throw new
                            InvalidOperationException(
                                "The D3D11 device context is unavailable."
                            );


                ID3D11RenderTargetView
                    renderTarget =
                        _backBufferRenderTarget ??
                        throw new
                            InvalidOperationException(
                                "The swap-chain render target is unavailable."
                            );


                deviceContext
                    .ClearRenderTargetView(
                        renderTarget,
                        new Color4(
                            0.0f,
                            0.0f,
                            0.0f,
                            1.0f
                        )
                    );


                VideoProcessorStream
                    stream =
                        new()
                        {
                            Enable =
                                true,

                            InputSurface =
                                inputView
                        };


                VideoProcessorStream[]
                    streams =
                        [
                            stream
                        ];


                videoContext
                    .VideoProcessorBlt(
                        processor,
                        outputView,
                        _outputFrameNumber++,
                        1,
                        streams
                    )
                    .CheckError();


                IDXGISwapChain1
                    swapChain =
                        _swapChain ??
                        throw new
                            InvalidOperationException(
                                "The DXGI swap chain is unavailable."
                            );


                //
                // SyncInterval = 0 keeps the decoding worker
                // from intentionally blocking for a complete
                // display refresh.
                //
                // The DWM still performs final desktop
                // composition.
                //
                swapChain
                    .Present(
                        0,
                        PresentFlags.None
                    )
                    .CheckError();
            }


            return true;
        }
    }


    private void EnsureDevice(
        ID3D11Texture2D sourceTexture)
    {
        if (_device is not null)
        {
            return;
        }

//
// sourceTexture.Device is a persisted Vortice wrapper
// associated with the decoded texture.
//
// Do not retain that wrapper directly because the
// decoded texture is temporary and is disposed after
// Present().
//
// QueryInterface gives the presenter its own COM
// reference whose lifetime is controlled by this
// presenter.
//
ID3D11Device sourceDevice =
    sourceTexture.Device;


_device =
    sourceDevice
        .QueryInterface<
            ID3D11Device
        >();


//
// The ImmediateContext property is also persisted.
//
// Take another independent COM reference so the
// presenter owns the context for its entire lifetime.
//
ID3D11DeviceContext sourceContext =
    _device.ImmediateContext;


_deviceContext =
    sourceContext
        .QueryInterface<
            ID3D11DeviceContext
        >();


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


        _factory =
            CreateDXGIFactory2<
                IDXGIFactory2
            >(
                debug:
                    false
            );


        //
        // Keep DXGI from building a deep presentation queue.
        // A remote-desktop Viewer wants the newest visible
        // frame rather than several already-obsolete frames.
        //
        try
        {
            using IDXGIDevice1
                dxgiDevice =
                    _device
                        .QueryInterface<
                            IDXGIDevice1
                        >();


            dxgiDevice
                .SetMaximumFrameLatency(
                    1
                );
        }
        catch
        {
            //
            // Frame-latency limiting is an optimization.
            // It is not required for rendering correctness.
            //
        }
    }


    private void EnsureSwapChain(
        uint width,
        uint height)
    {
        if (
            _device is null ||
            _factory is null
        )
        {
            throw new
                InvalidOperationException(
                    "The D3D11 device has not been initialized."
                );
        }


        if (_swapChain is null)
        {
            SwapChainDescription1
                description =
                    new()
                    {
                        Width =
                            width,

                        Height =
                            height,

                        Format =
                            SwapChainFormat,

                        Stereo =
                            false,

                        SampleDescription =
                            new SampleDescription(
                                1,
                                0
                            ),

                        BufferUsage =
                            Usage.RenderTargetOutput,

                        BufferCount =
                            2,

                        Scaling =
                            Scaling.Stretch,

                        SwapEffect =
                            SwapEffect.FlipDiscard,

                        AlphaMode =
                            AlphaMode.Ignore,

                        Flags =
                            SwapChainFlags.None
                    };


            using IDXGIDevice
                dxgiDevice =
                    _device
                        .QueryInterface<
                            IDXGIDevice
                        >();


            _swapChain =
                _factory
                    .CreateSwapChainForHwnd(
                        dxgiDevice,
                        _windowHandle,
                        description
                    );


            _targetWidth =
                width;

            _targetHeight =
                height;


            CreateBackBufferResources();

            return;
        }


        if (
            _targetWidth ==
                width &&
            _targetHeight ==
                height
        )
        {
            return;
        }


        //
        // Video-processor output views hold a reference
        // to the old swap-chain backbuffer, so release
        // those resources before ResizeBuffers().
        //
        ReleaseVideoProcessorResources();


        _backBufferRenderTarget
            ?.Dispose();

        _backBufferRenderTarget =
            null;


        _backBuffer
            ?.Dispose();

        _backBuffer =
            null;


        _swapChain
            .ResizeBuffers(
                2,
                width,
                height,
                SwapChainFormat,
                SwapChainFlags.None
            )
            .CheckError();


        _targetWidth =
            width;

        _targetHeight =
            height;


        CreateBackBufferResources();
    }


    private void CreateBackBufferResources()
    {
        if (
            _swapChain is null ||
            _device is null
        )
        {
            throw new
                InvalidOperationException(
                    "The swap chain is not initialized."
                );
        }


        _backBuffer =
            _swapChain
                .GetBuffer<
                    ID3D11Texture2D
                >(
                    0
                );


        _backBufferRenderTarget =
            _device
                .CreateRenderTargetView(
                    _backBuffer
                );
    }


    private void EnsureVideoProcessor(
        uint sourceWidth,
        uint sourceHeight,
        uint targetWidth,
        uint targetHeight)
    {
        if (
            _processor is not null &&
            _processorEnumerator is not null &&
            _processorOutputView is not null &&
            _sourceWidth ==
                sourceWidth &&
            _sourceHeight ==
                sourceHeight &&
            _targetWidth ==
                targetWidth &&
            _targetHeight ==
                targetHeight
        )
        {
            return;
        }


        ReleaseVideoProcessorResources();


        ID3D11VideoDevice
            videoDevice =
                _videoDevice ??
                throw new
                    InvalidOperationException(
                        "The D3D11 video device is unavailable."
                    );


        ID3D11Texture2D
            backBuffer =
                _backBuffer ??
                throw new
                    InvalidOperationException(
                        "The swap-chain backbuffer is unavailable."
                    );


        VideoProcessorContentDescription
            description =
                new()
                {
                    Usage =
                        VideoUsage.PlaybackNormal,

                    InputFrameFormat =
                        VideoFrameFormat.Progressive,

                    InputWidth =
                        sourceWidth,

                    InputHeight =
                        sourceHeight,

                    OutputWidth =
                        targetWidth,

                    OutputHeight =
                        targetHeight,

                    InputFrameRate =
                        new Rational(
                            60,
                            1
                        ),

                    OutputFrameRate =
                        new Rational(
                            60,
                            1
                        )
                };


        videoDevice
            .CreateVideoProcessorEnumerator(
                description,
                out _processorEnumerator
            )
            .CheckError();


        videoDevice
            .CreateVideoProcessor(
                _processorEnumerator,
                0,
                out _processor
            )
            .CheckError();


        VideoProcessorOutputViewDescription
            outputDescription =
                new()
                {
                    ViewDimension =
                        VideoProcessorOutputViewDimension
                            .Texture2D,

                    Texture2D =
                        new
                            Texture2DVideoProcessorOutputView
                        {
                            MipSlice =
                                0
                        }
                };


        videoDevice
            .CreateVideoProcessorOutputView(
                backBuffer,
                _processorEnumerator,
                outputDescription,
                out _processorOutputView
            )
            .CheckError();


        _sourceWidth =
            sourceWidth;

        _sourceHeight =
            sourceHeight;
    }


    private bool TryGetClientSize(
        out uint width,
        out uint height)
    {
        width =
            0;

        height =
            0;


        if (
            !GetClientRect(
                _windowHandle,
                out NativeRect rectangle
            )
        )
        {
            throw new
                Win32Exception(
                    Marshal
                        .GetLastWin32Error()
                );
        }


        int clientWidth =
            rectangle.Right -
            rectangle.Left;


        int clientHeight =
            rectangle.Bottom -
            rectangle.Top;


        if (
            clientWidth <=
                0 ||
            clientHeight <=
                0
        )
        {
            return false;
        }


        width =
            checked(
                (uint)clientWidth
            );


        height =
            checked(
                (uint)clientHeight
            );


        return true;
    }


    private static RawRect
        CalculateLetterboxRectangle(
            uint sourceWidth,
            uint sourceHeight,
            uint targetWidth,
            uint targetHeight)
    {
        double sourceAspect =
            sourceWidth /
            (double)sourceHeight;


        double targetAspect =
            targetWidth /
            (double)targetHeight;


        int destinationWidth;
        int destinationHeight;
        int left;
        int top;


        if (
            targetAspect >
            sourceAspect
        )
        {
            destinationHeight =
                checked(
                    (int)targetHeight
                );


            destinationWidth =
                Math.Max(
                    1,
                    (int)Math.Round(
                        destinationHeight *
                        sourceAspect
                    )
                );


            left =
                (
                    checked(
                        (int)targetWidth
                    ) -
                    destinationWidth
                ) /
                2;


            top =
                0;
        }
        else
        {
            destinationWidth =
                checked(
                    (int)targetWidth
                );


            destinationHeight =
                Math.Max(
                    1,
                    (int)Math.Round(
                        destinationWidth /
                        sourceAspect
                    )
                );


            left =
                0;


            top =
                (
                    checked(
                        (int)targetHeight
                    ) -
                    destinationHeight
                ) /
                2;
        }


        return new RawRect(
            left,
            top,
            left +
                destinationWidth,
            top +
                destinationHeight
        );
    }


    private void ReleaseVideoProcessorResources()
    {
        _processorOutputView
            ?.Dispose();

        _processorOutputView =
            null;


        _processor
            ?.Dispose();

        _processor =
            null;


        _processorEnumerator
            ?.Dispose();

        _processorEnumerator =
            null;


        _sourceWidth =
            0;

        _sourceHeight =
            0;
    }


    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }


            _disposed =
                true;


            ReleaseVideoProcessorResources();


            _backBufferRenderTarget
                ?.Dispose();

            _backBufferRenderTarget =
                null;


            _backBuffer
                ?.Dispose();

            _backBuffer =
                null;


            _swapChain
                ?.Dispose();

            _swapChain =
                null;


            _factory
                ?.Dispose();

            _factory =
                null;


            _videoContext
                ?.Dispose();

            _videoContext =
                null;


            _videoDevice
                ?.Dispose();

            _videoDevice =
                null;


            _deviceContext
                ?.Dispose();

            _deviceContext =
                null;


            _device
                ?.Dispose();

            _device =
                null;


            GC.SuppressFinalize(
                this
            );
        }
    }


    [StructLayout(
        LayoutKind.Sequential
    )]
    private struct NativeRect
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }


    [DllImport(
        "user32.dll",
        SetLastError =
            true
    )]
    [return:
        MarshalAs(
            UnmanagedType.Bool
        )]
    private static extern bool
        GetClientRect(
            IntPtr hWnd,
            out NativeRect lpRect
        );
}