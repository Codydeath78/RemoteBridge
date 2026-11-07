using SharpGen.Runtime;

using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;

namespace RemoteBridge.WindowsMedia;

public sealed record HardwareVideoEncoderProbeResult(
    string D3D11FeatureLevel,
    bool DxgiDeviceManagerReady,
    IReadOnlyList<string> EncoderNames
);

public static class HardwareVideoEncoderProbe
{
    private const uint MftEnumFlagHardware =
        0x00000004;

    private const uint MftEnumFlagSortAndFilter =
        0x00000040;


    public static HardwareVideoEncoderProbeResult Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Media Foundation hardware encoder probing requires Windows."
            );
        }

        Result startupResult =
            MFStartup();

        startupResult.CheckError();

        try
        {
            using Direct3D11DeviceBundle
                deviceBundle =
                    Direct3D11DeviceBundle.Create();

            using IMFDXGIDeviceManager
                deviceManager =
                    MFCreateDXGIDeviceManager();

            deviceManager
                .ResetDevice(
                    deviceBundle.NativeDevice
                )
                .CheckError();

            uint flags =
                MftEnumFlagHardware |
                MftEnumFlagSortAndFilter;

            using IMFActivateCollection
                encoderActivations =
                    MFTEnumEx(
                        TransformCategoryGuids
                            .VideoEncoder,

                        flags,

                        inputType: null,
                        outputType: null
                    );

            List<string>
                encoderNames =
                    new();

            foreach (
                IMFActivate activation
                in encoderActivations
            )
            {
                string name;

                try
                {
                    name =
                        activation
                            .GetString(
                                TransformAttributeKeys
                                    .MftFriendlyNameAttribute
                            );
                }
                catch
                {
                    name =
                        "(unnamed hardware video encoder)";
                }

                if (
                    !string.IsNullOrWhiteSpace(
                        name
                    )
                )
                {
                    encoderNames.Add(
                        name
                    );
                }
            }

            encoderNames.Sort(
                StringComparer
                    .OrdinalIgnoreCase
            );

            return new HardwareVideoEncoderProbeResult(
                deviceBundle
                    .FeatureLevel
                    .ToString(),

                true,

                encoderNames
            );
        }
        finally
        {
            MFShutdown()
                .CheckError();
        }
    }
}