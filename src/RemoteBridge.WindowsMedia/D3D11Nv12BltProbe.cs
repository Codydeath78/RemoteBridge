using Vortice;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace RemoteBridge.WindowsMedia;


public sealed record D3D11Nv12BltProbeResult(
    bool ProcessorReady,
    bool InputTextureReady,
    bool OutputTextureReady,
    bool InputViewReady,
    bool OutputViewReady,
    bool BlitSucceeded,
    int Width,
    int Height
);


public static class D3D11Nv12BltProbe
{
    private const uint Width =
        1920;

    private const uint Height =
        1080;

    private const uint FramesPerSecond =
        60;


    public static D3D11Nv12BltProbeResult Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "D3D11 video-processor conversion requires Windows."
            );
        }


        using Direct3D11DeviceBundle deviceBundle =
            Direct3D11DeviceBundle.Create();


        Console.WriteLine(
            "[GPU-BLT] Querying video device/context..."
        );


        using ID3D11VideoDevice videoDevice =
            deviceBundle
                .NativeDevice
                .QueryInterface<
                    ID3D11VideoDevice
                >();


        using ID3D11VideoContext videoContext =
            deviceBundle
                .NativeContext
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
                            FramesPerSecond,
                            1
                        ),

                    InputWidth =
                        Width,

                    InputHeight =
                        Height,

                    OutputFrameRate =
                        new Rational(
                            FramesPerSecond,
                            1
                        ),

                    OutputWidth =
                        Width,

                    OutputHeight =
                        Height,

                    Usage =
                        VideoUsage.PlaybackNormal
                };


        Console.WriteLine(
            "[GPU-BLT] Creating processor enumerator..."
        );


        videoDevice
            .CreateVideoProcessorEnumerator(
                contentDescription,
                out ID3D11VideoProcessorEnumerator?
                    enumerator
            )
            .CheckError();


        if (enumerator is null)
        {
            throw new InvalidOperationException(
                "D3D11 returned no video processor enumerator."
            );
        }


        using (enumerator)
        {
            Console.WriteLine(
                "[GPU-BLT] Creating processor..."
            );


            videoDevice
                .CreateVideoProcessor(
                    enumerator,
                    0,
                    out ID3D11VideoProcessor?
                        processor
                )
                .CheckError();


            if (processor is null)
            {
                throw new InvalidOperationException(
                    "D3D11 returned no video processor."
                );
            }


            using (processor)
            {
                Texture2DDescription
                    bgraDescription =
                        new()
                        {
                            Width =
                                Width,

                            Height =
                                Height,

                            MipLevels =
                                1,

                            ArraySize =
                                1,

                            Format =
                                Format
                                    .B8G8R8A8_UNorm,

                            SampleDescription =
                                new SampleDescription(
                                    1,
                                    0
                                ),

                            Usage =
                                ResourceUsage.Default,

                            BindFlags =
                                BindFlags.None,

                            CPUAccessFlags =
                                CpuAccessFlags.None,

                            MiscFlags =
                                ResourceOptionFlags.None
                        };


                Console.WriteLine(
                    "[GPU-BLT] Creating BGRA input texture..."
                );


                using ID3D11Texture2D bgraTexture =
                    deviceBundle
                        .NativeDevice
                        .CreateTexture2D(
                            bgraDescription
                        );


                Texture2DDescription
                    nv12Description =
                        new()
                        {
                            Width =
                                Width,

                            Height =
                                Height,

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


                Console.WriteLine(
                    "[GPU-BLT] Creating NV12 output texture..."
                );


                using ID3D11Texture2D nv12Texture =
                    deviceBundle
                        .NativeDevice
                        .CreateTexture2D(
                            nv12Description
                        );


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


                videoDevice
                    .CreateVideoProcessorInputView(
                        bgraTexture,
                        enumerator,
                        inputViewDescription,
                        out ID3D11VideoProcessorInputView?
                            inputView
                    )
                    .CheckError();


                if (inputView is null)
                {
                    throw new InvalidOperationException(
                        "D3D11 returned no BGRA video processor input view."
                    );
                }


                using (inputView)
                {
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


                    videoDevice
                        .CreateVideoProcessorOutputView(
                            nv12Texture,
                            enumerator,
                            outputViewDescription,
                            out ID3D11VideoProcessorOutputView?
                                outputView
                        )
                        .CheckError();


                    if (outputView is null)
                    {
                        throw new InvalidOperationException(
                            "D3D11 returned no NV12 video processor output view."
                        );
                    }


                    using (outputView)
                    {
                        Console.WriteLine(
                            "[GPU-BLT] Configuring progressive stream..."
                        );


                        videoContext
                            .VideoProcessorSetStreamFrameFormat(
                                processor,
                                0,
                                VideoFrameFormat.Progressive
                            );


                        //
                        // IMPORTANT:
                        //
                        // D3D11's default stream destination
                        // rectangle is empty.
                        //
                        // Without this call VideoProcessorBlt()
                        // can succeed while writing nothing.
                        //
                        RawRect destinationRectangle =
                            new(
                                0,
                                0,
                                (int)Width,
                                (int)Height
                            );


                        Console.WriteLine(
                            "[GPU-BLT] Setting destination rectangle..."
                        );


                        videoContext
                            .VideoProcessorSetStreamDestRect(
                                processor,
                                0,
                                true,
                                destinationRectangle
                            );


                        VideoProcessorStream stream =
                            new()
                            {
                                Enable =
                                    true,

                                InputSurface =
                                    inputView
                            };


                        Console.WriteLine(
                            "[GPU-BLT] Calling VideoProcessorBlt..."
                        );


                        videoContext
                            .VideoProcessorBlt(
                                processor,
                                outputView,
                                0,
                                1,
                                [stream]
                            )
                            .CheckError();


                        //
                        // Submit pending commands to the GPU.
                        //
                        deviceBundle
                            .NativeContext
                            .Flush();


                        Console.WriteLine(
                            "[GPU-BLT] BGRA -> NV12 blit succeeded."
                        );


                        return
                            new D3D11Nv12BltProbeResult(
                                ProcessorReady:
                                    true,

                                InputTextureReady:
                                    true,

                                OutputTextureReady:
                                    true,

                                InputViewReady:
                                    true,

                                OutputViewReady:
                                    true,

                                BlitSucceeded:
                                    true,

                                Width:
                                    (int)Width,

                                Height:
                                    (int)Height
                            );
                    }
                }
            }
        }
    }
}