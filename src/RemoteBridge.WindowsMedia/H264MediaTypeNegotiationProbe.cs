using SharpGen.Runtime;

using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;

namespace RemoteBridge.WindowsMedia;


public sealed record H264NegotiationAttempt(
    string EncoderName,
    bool OutputTypeAccepted,
    bool InputTypeAccepted,
    string? Error
);


public sealed record H264NegotiationProbeResult(
    string? SelectedEncoderName,
    IReadOnlyList<H264NegotiationAttempt> Attempts
);


public static class H264MediaTypeNegotiationProbe
{
    private const uint MftEnumFlagHardware =
        0x00000004;

    private const uint MftEnumFlagSortAndFilter =
        0x00000040;


    private const uint Width =
        1920;

    private const uint Height =
        1080;

    private const uint FramesPerSecond =
        60;

    private const uint Bitrate =
        12_000_000;


    // eAVEncH264VProfile_Main
    private const uint H264MainProfile =
        77;


    // MFVideoInterlace_Progressive
    private const uint Progressive =
        2;


    private static readonly Guid TransformAsyncGuid =
        new(
            "F81A699A-649A-497D-8C73-29F8FED6AD7A"
        );

    private static readonly Guid TransformAsyncUnlockGuid =
        new(
            "E5666D6B-3422-4EB6-A421-DA7DB1F8E207"
        );


    public static H264NegotiationProbeResult Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "H.264 negotiation requires Windows."
            );
        }

        Result startup =
            MFStartup();

        startup.CheckError();

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

            List<H264NegotiationAttempt> attempts =
                new();

            string? selectedEncoder =
                null;


            foreach (
    IMFActivate activation
    in activations
)
{
    string encoderName =
        GetEncoderName(
            activation
        );

    if (!IsNvidiaH264(
            encoderName))
    {
        continue;
    }

    bool outputAccepted =
        false;

    bool inputAccepted =
        false;

    bool activated =
        false;

    try
    {
        Console.WriteLine(
            $"[NEG] Activating: {encoderName}"
        );

        using IMFTransform encoder =
            activation
                .ActivateObject<
                    IMFTransform
                >();

        activated =
            true;
        Console.WriteLine(
    $"[NEG] Activated: {encoderName}"
);

        Console.WriteLine(
    $"[NEG] Reading transform attributes..."
);

using IMFAttributes attributes =
    MediaFoundationTransformInterop
        .GetAttributes(
            encoder
        );


//Unlock asynchronous hardware MFT

bool isAsync =
    false;

try
{
    isAsync =
        attributes.GetUInt32(
            TransformAsyncGuid
        ) != 0;
}
catch
{
    // Missing MF_TRANSFORM_ASYNC means
    // treat the transform as synchronous.
}


Console.WriteLine(
    $"[NEG] Async transform: " +
    $"{(isAsync ? "YES" : "NO")}"
);


if (isAsync)
{
    Console.WriteLine(
        "[NEG] Unlocking async transform..."
    );

    attributes
        .Set(
            TransformAsyncUnlockGuid,
            1u
        )
        .CheckError();

    Console.WriteLine(
        "[NEG] Async transform unlocked."
    );
}


// Check D3D11 awareness

bool d3d11Aware =
    false;

try
{
    d3d11Aware =
    attributes.GetUInt32(
        TransformAttributeKeys
            .D3D11Aware
    ) != 0;
}
catch
{
    // Attribute not present -> not D3D11-aware.
}


Console.WriteLine(
    $"[NEG] D3D11 aware: " +
    $"{(d3d11Aware ? "YES" : "NO")}"
);


// Give hardware encoder our DXGI device manager

if (d3d11Aware)
{
    Console.WriteLine(
        "[NEG] Attaching DXGI device manager..."
    );

    MediaFoundationTransformInterop
    .SetD3DManager(
        encoder,
        deviceManager
    );

    Console.WriteLine(
        "[NEG] DXGI device manager attached."
    );
}
        
        Console.WriteLine(
            $"[NEG] Creating H.264 output type..."
        );

        using IMFMediaType outputType =
            CreateOutputType();

        Console.WriteLine(
            $"[NEG] Calling SetOutputType..."
        );

        encoder.SetOutputType(
            0,
            outputType,
            0
        );

        outputAccepted =
            true;

        Console.WriteLine(
            $"[NEG] H.264 output accepted."
        );

        Console.WriteLine(
            $"[NEG] Creating NV12 input type..."
        );

        using IMFMediaType inputType =
            CreateInputType();

        Console.WriteLine(
            $"[NEG] Calling SetInputType..."
        );

        encoder.SetInputType(
            0,
            inputType,
            0
        );

        inputAccepted =
            true;

        Console.WriteLine(
            $"[NEG] NV12 input accepted."
        );

        Console.WriteLine(
            "[NEG] Sending MFT_MESSAGE_NOTIFY_BEGIN_STREAMING..."
        );


        MediaFoundationAsyncTransformInterop
            .BeginStreaming(
                encoder
        );


        Console.WriteLine(
            "[NEG] BEGIN_STREAMING accepted."
        );


        Console.WriteLine(
            "[NEG] Sending MFT_MESSAGE_NOTIFY_START_OF_STREAM..."
        );


        MediaFoundationAsyncTransformInterop
            .StartOfStream(
                encoder
        );


        Console.WriteLine(
            "[NEG] START_OF_STREAM accepted."
        );


        Console.WriteLine(
            "[NEG] Waiting for METransformNeedInput..."
        );


        MediaFoundationAsyncTransformInterop
            .WaitForNeedInput(
                encoder,
                TimeSpan.FromSeconds(
                    2
                )
        );


        Console.WriteLine(
            "[NEG] METransformNeedInput received."
        );

        attempts.Add(
            new H264NegotiationAttempt(
                encoderName,
                outputAccepted,
                inputAccepted,
                Error: null
            )
        );

        selectedEncoder ??=
            encoderName;
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"[NEG] Managed failure: " +
            $"{encoderName}"
        );

        Console.WriteLine(
            $"[NEG] {ex}"
        );

        attempts.Add(
            new H264NegotiationAttempt(
                encoderName,
                outputAccepted,
                inputAccepted,
                ex.Message
            )
        );
    }
    finally
    {
        if (activated)
        {
            try
            {
                Console.WriteLine(
                    $"[NEG] Shutting down: " +
                    $"{encoderName}"
                );

                activation
                    .ShutdownObject();
            }
            catch
            {
                // Probe cleanup only.
            }
        }
    }
}


            return new H264NegotiationProbeResult(
                selectedEncoder,
                attempts
            );
        }
        finally
        {
            MFShutdown()
                .CheckError();
        }
    }


    private static IMFMediaType CreateOutputType()
    {
        IMFMediaType type =
            MFCreateMediaType();

        try
        {
            type
                .Set(
                    MediaTypeAttributeKeys.MajorType,
                    MediaTypeGuids.Video
                )
                .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys.Subtype,
                    VideoFormatGuids.H264
                )
                .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys.AvgBitrate,
                    Bitrate
                )
                .CheckError();


            MFSetAttributeSize(
                type,
                MediaTypeAttributeKeys.FrameSize,
                Width,
                Height
            )
            .CheckError();


            MFSetAttributeRatio(
                type,
                MediaTypeAttributeKeys.FrameRate,
                FramesPerSecond,
                1
            )
            .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys.InterlaceMode,
                    Progressive
                )
                .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys.Mpeg2Profile,
                    H264MainProfile
                )
                .CheckError();


            MFSetAttributeRatio(
                type,
                MediaTypeAttributeKeys.PixelAspectRatio,
                1,
                1
            )
            .CheckError();


            return type;
        }
        catch
        {
            type.Dispose();

            throw;
        }
    }


    private static IMFMediaType CreateInputType()
    {
        IMFMediaType type =
            MFCreateMediaType();

        try
        {
            type
                .Set(
                    MediaTypeAttributeKeys.MajorType,
                    MediaTypeGuids.Video
                )
                .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys.Subtype,
                    VideoFormatGuids.NV12
                )
                .CheckError();


            MFSetAttributeSize(
                type,
                MediaTypeAttributeKeys.FrameSize,
                Width,
                Height
            )
            .CheckError();


            MFSetAttributeRatio(
                type,
                MediaTypeAttributeKeys.FrameRate,
                FramesPerSecond,
                1
            )
            .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys.InterlaceMode,
                    Progressive
                )
                .CheckError();


            MFSetAttributeRatio(
                type,
                MediaTypeAttributeKeys.PixelAspectRatio,
                1,
                1
            )
            .CheckError();


            return type;
        }
        catch
        {
            type.Dispose();

            throw;
        }
    }


    private static string GetEncoderName(
        IMFActivate activation)
    {
        try
        {
            return activation
                .GetString(
                    TransformAttributeKeys
                        .MftFriendlyNameAttribute
                );
        }
        catch
        {
            return
                "(unnamed hardware encoder)";
        }
    }


    private static bool IsNvidiaH264(
    string encoderName)
{
    return
        encoderName.Contains(
            "NVIDIA",
            StringComparison.OrdinalIgnoreCase
        ) &&
        (
            encoderName.Contains(
                "H.264",
                StringComparison.OrdinalIgnoreCase
            ) ||
            encoderName.Contains(
                "H264",
                StringComparison.OrdinalIgnoreCase
            ) ||
            encoderName.Contains(
                "AVC",
                StringComparison.OrdinalIgnoreCase
            )
        );
}
}