using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RemoteBridge.WindowsMedia;


public sealed record D3D11Nv12ResourceProbeResult(
    bool VideoDeviceReady,
    bool VideoContextReady,
    bool ProcessorReady,
    bool BgraTextureReady,
    bool Nv12TextureReady,
    bool InputViewReady,
    bool OutputViewReady,
    int Width,
    int Height
);


public static class D3D11Nv12ResourceProbe
{
    private const uint Width =
        1920;

    private const uint Height =
        1080;

    private const uint FramesPerSecond =
        60;


    public static D3D11Nv12ResourceProbeResult Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "D3D11 NV12 resource probing requires Windows."
            );
        }


        using Direct3D11DeviceBundle deviceBundle =
            Direct3D11DeviceBundle.Create();


        Console.WriteLine(
            "[GPU] Querying ID3D11VideoDevice..."
        );

        using ID3D11VideoDevice videoDevice =
            deviceBundle
                .NativeDevice
                .QueryInterface<
                    ID3D11VideoDevice
                >();


        Console.WriteLine(
            "[GPU] Querying ID3D11VideoContext..."
        );

        using ID3D11VideoContext videoContext =
            deviceBundle
                .NativeContext
                .QueryInterface<
                    ID3D11VideoContext
                >();


        Console.WriteLine(
            "[GPU] Creating video processor enumerator..."
        );


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
                "[GPU] Creating video processor..."
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
                Console.WriteLine(
                    "[GPU] Creating 1920x1080 BGRA input texture..."
                );


                Texture2DDescription
                    inputDescription =
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
                                ResourceUsage
                                    .Default,

                            BindFlags =
                                BindFlags
                                    .None,

                            CPUAccessFlags =
                                CpuAccessFlags
                                    .None,

                            MiscFlags =
                                ResourceOptionFlags
                                    .None
                        };


                using ID3D11Texture2D
                    bgraTexture =
                        deviceBundle
                            .NativeDevice
                            .CreateTexture2D(
                                inputDescription
                            );


                Console.WriteLine(
                    "[GPU] BGRA texture created."
                );


                Console.WriteLine(
                    "[GPU] Creating 1920x1080 NV12 output texture..."
                );


                Texture2DDescription
                    outputDescription =
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
                                ResourceUsage
                                    .Default,

                            BindFlags =
                                BindFlags
                                    .RenderTarget,

                            CPUAccessFlags =
                                CpuAccessFlags
                                    .None,

                            MiscFlags =
                                ResourceOptionFlags
                                    .None
                        };


                using ID3D11Texture2D
                    nv12Texture =
                        deviceBundle
                            .NativeDevice
                            .CreateTexture2D(
                                outputDescription
                            );


                Console.WriteLine(
                    "[GPU] NV12 texture created."
                );


                VideoProcessorInputViewDescription
                    inputViewDescription =
                        new()
                        {
                            // Zero tells D3D11 to use
                            // the texture's DXGI format.
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


                Console.WriteLine(
                    "[GPU] Creating BGRA processor input view..."
                );


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
                        "D3D11 returned no video processor input view."
                    );
                }


                using (inputView)
                {
                    Console.WriteLine(
                        "[GPU] BGRA input view created."
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


                    Console.WriteLine(
                        "[GPU] Creating NV12 processor output view..."
                    );


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
                            "D3D11 returned no video processor output view."
                        );
                    }


                    using (outputView)
                    {
                        Console.WriteLine(
                            "[GPU] NV12 output view created."
                        );


                        return
                            new D3D11Nv12ResourceProbeResult(
                                VideoDeviceReady:
                                    true,

                                VideoContextReady:
                                    true,

                                ProcessorReady:
                                    true,

                                BgraTextureReady:
                                    true,

                                Nv12TextureReady:
                                    true,

                                InputViewReady:
                                    true,

                                OutputViewReady:
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