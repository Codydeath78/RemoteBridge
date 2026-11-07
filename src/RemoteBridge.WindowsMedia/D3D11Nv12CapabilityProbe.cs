using SharpGen.Runtime;

using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RemoteBridge.WindowsMedia;


public sealed record D3D11Nv12CapabilityProbeResult(
    string FeatureLevel,
    bool VideoDeviceAvailable,
    bool BgraInputSupported,
    bool Nv12OutputSupported,
    bool ConversionPathSupported
);


public static class D3D11Nv12CapabilityProbe
{
    private const uint Width =
        1920;

    private const uint Height =
        1080;

    private const uint FramesPerSecond =
        60;


    // D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_INPUT
    private const uint FormatSupportInput =
        0x00000001;

    // D3D11_VIDEO_PROCESSOR_FORMAT_SUPPORT_OUTPUT
    private const uint FormatSupportOutput =
        0x00000002;


    public static D3D11Nv12CapabilityProbeResult Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The D3D11 video processor probe requires Windows."
            );
        }


        using Direct3D11DeviceBundle
            deviceBundle =
                Direct3D11DeviceBundle
                    .Create();


        using ID3D11VideoDevice
            videoDevice =
                deviceBundle
                    .NativeDevice
                    .QueryInterface<
                        ID3D11VideoDevice
                    >();


        VideoProcessorContentDescription
            description =
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
                description,
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
            enumerator
                .CheckVideoProcessorFormat(
                    Format.B8G8R8A8_UNorm,
                    out var bgraSupport
                )
                .CheckError();


            enumerator
                .CheckVideoProcessorFormat(
                    Format.NV12,
                    out var nv12Support
                )
                .CheckError();


            bool bgraInputSupported =
                (
                    (uint)bgraSupport &
                    FormatSupportInput
                ) != 0;


            bool nv12OutputSupported =
                (
                    (uint)nv12Support &
                    FormatSupportOutput
                ) != 0;


            return
                new D3D11Nv12CapabilityProbeResult(
                    deviceBundle
                        .FeatureLevel
                        .ToString(),

                    VideoDeviceAvailable:
                        true,

                    BgraInputSupported:
                        bgraInputSupported,

                    Nv12OutputSupported:
                        nv12OutputSupported,

                    ConversionPathSupported:
                        bgraInputSupported &&
                        nv12OutputSupported
                );
        }
    }
}