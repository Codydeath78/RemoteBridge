using SharpGen.Runtime;

using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;

namespace RemoteBridge.WindowsMedia;

public sealed record H264EncoderActivationAttempt(
    string EncoderName,
    bool Activated,
    string? Error
);

public sealed record H264EncoderActivationProbeResult(
    string? SelectedEncoderName,
    IReadOnlyList<H264EncoderActivationAttempt> Attempts
);

public static class H264EncoderActivationProbe
{
    private const uint MftEnumFlagHardware =
        0x00000004;

    private const uint MftEnumFlagSortAndFilter =
        0x00000040;


    public static H264EncoderActivationProbeResult Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "H.264 hardware encoder activation requires Windows."
            );
        }

        Result startupResult =
            MFStartup();

        startupResult.CheckError();

        try
        {
            uint flags =
                MftEnumFlagHardware |
                MftEnumFlagSortAndFilter;

            using IMFActivateCollection activations =
                MFTEnumEx(
                    TransformCategoryGuids.VideoEncoder,
                    flags,
                    inputType: null,
                    outputType: null
                );

            List<H264EncoderActivationAttempt> attempts =
                new();

            string? selectedEncoderName =
                null;

            foreach (
                IMFActivate activation
                in activations
            )
            {
                string encoderName;

                try
                {
                    encoderName =
                        activation.GetString(
                            TransformAttributeKeys
                                .MftFriendlyNameAttribute
                        );
                }
                catch
                {
                    encoderName =
                        "(unnamed hardware encoder)";
                }

                if (!LooksLikeH264Encoder(
                        encoderName))
                {
                    continue;
                }

                try
                {
                    using IMFTransform encoder =
                        activation
                            .ActivateObject<
                                IMFTransform
                            >();

                    attempts.Add(
                        new H264EncoderActivationAttempt(
                            encoderName,
                            Activated: true,
                            Error: null
                        )
                    );

                    selectedEncoderName ??=
                        encoderName;
                }
                catch (Exception ex)
                {
                    attempts.Add(
                        new H264EncoderActivationAttempt(
                            encoderName,
                            Activated: false,
                            Error: ex.Message
                        )
                    );
                }
            }

            return new H264EncoderActivationProbeResult(
                selectedEncoderName,
                attempts
            );
        }
        finally
        {
            MFShutdown()
                .CheckError();
        }
    }


    private static bool LooksLikeH264Encoder(
        string name)
    {
        return
            name.Contains(
                "H.264",
                StringComparison.OrdinalIgnoreCase
            ) ||
            name.Contains(
                "H264",
                StringComparison.OrdinalIgnoreCase
            ) ||
            name.Contains(
                "AVC",
                StringComparison.OrdinalIgnoreCase
            );
    }
}