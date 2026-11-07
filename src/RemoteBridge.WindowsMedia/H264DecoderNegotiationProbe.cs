using SharpGen.Runtime;

using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;


namespace RemoteBridge.WindowsMedia;


public sealed record
    H264DecoderNegotiationProbeResult(
        string? DecoderName,
        bool Activated,
        bool AsyncTransform,
        bool D3D11Aware,
        bool D3DManagerAttached,
        bool H264InputAccepted,
        bool Nv12OutputAccepted,
        string? Error
    );


public static class
    H264DecoderNegotiationProbe
{
    private const uint
        MftEnumFlagSyncMft =
            0x00000001;

    private const uint
        MftEnumFlagAsyncMft =
            0x00000002;

    private const uint
        MftEnumFlagHardware =
            0x00000004;

    private const uint
        MftEnumFlagLocalMft =
            0x00000010;

    private const uint
        MftEnumFlagSortAndFilter =
            0x00000040;


    private const uint Width =
        1920;

    private const uint Height =
        1080;

    private const uint FramesPerSecond =
        60;

    private const uint Progressive =
        2;

    private const uint H264MainProfile =
        77;


    private static readonly Guid
        TransformAsyncGuid =
            new(
                "F81A699A-649A-497D-8C73-29F8FED6AD7A"
            );

    private static readonly Guid
        TransformAsyncUnlockGuid =
            new(
                "E5666D6B-3422-4EB6-A421-DA7DB1F8E207"
            );


    public static
        H264DecoderNegotiationProbeResult
        Run()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new
                PlatformNotSupportedException(
                    "H.264 decoder negotiation requires Windows."
                );
        }


        MFStartup()
            .CheckError();


        try
        {
            using Direct3D11DeviceBundle
                deviceBundle =
                    Direct3D11DeviceBundle
                        .Create();


            uint flags =
                MftEnumFlagSyncMft |
                MftEnumFlagAsyncMft |
                MftEnumFlagHardware |
                MftEnumFlagLocalMft |
                MftEnumFlagSortAndFilter;


            using IMFActivateCollection
                activations =
                    MFTEnumEx(
                        TransformCategoryGuids
                            .VideoDecoder,

                        flags,

                        inputType:
                            null,

                        outputType:
                            null
                    );


            string? lastDecoderName =
                null;

            string? lastError =
                null;


            foreach (
                IMFActivate activation
                in activations
            )
            {
                string decoderName =
                    GetDecoderName(
                        activation
                    );


                if (
                    !LooksLikeH264(
                        decoderName
                    )
                )
                {
                    continue;
                }


                lastDecoderName =
                    decoderName;


                IMFTransform? decoder =
                    null;

                IMFDXGIDeviceManager?
                    deviceManager =
                        null;


                bool activated =
                    false;

                bool isAsync =
                    false;

                bool d3d11Aware =
                    false;

                bool managerAttached =
                    false;

                bool inputAccepted =
                    false;

                bool outputAccepted =
                    false;


                try
                {
                    decoder =
                        activation
                            .ActivateObject<
                                IMFTransform
                            >();


                    activated =
                        true;


                    using IMFAttributes
                        attributes =
                            MediaFoundationTransformInterop
                                .GetAttributes(
                                    decoder
                                );


                    try
                    {
                        isAsync =
                            attributes
                                .GetUInt32(
                                    TransformAsyncGuid
                                ) != 0;
                    }
                    catch
                    {
                        isAsync =
                            false;
                    }


                    if (isAsync)
                    {
                        attributes
                            .Set(
                                TransformAsyncUnlockGuid,
                                1u
                            )
                            .CheckError();
                    }


                    try
                    {
                        d3d11Aware =
                            attributes
                                .GetUInt32(
                                    TransformAttributeKeys
                                        .D3D11Aware
                                ) != 0;
                    }
                    catch
                    {
                        d3d11Aware =
                            false;
                    }


                    if (!d3d11Aware)
                    {
                        throw new
                            InvalidOperationException(
                                "The H.264 decoder is not D3D11-aware."
                            );
                    }


                    deviceManager =
                        MFCreateDXGIDeviceManager();


                    deviceManager
                        .ResetDevice(
                            deviceBundle.NativeDevice
                        )
                        .CheckError();


                    MediaFoundationTransformInterop
                        .SetD3DManager(
                            decoder,
                            deviceManager
                        );


                    managerAttached =
                        true;


                    using IMFMediaType
                        inputType =
                            CreateH264InputType();


                    decoder.SetInputType(
                        0,
                        inputType,
                        0
                    );


                    inputAccepted =
                        true;


                    using IMFMediaType
                        outputType =
                            CreateNv12OutputType();


                    decoder.SetOutputType(
                        0,
                        outputType,
                        0
                    );


                    outputAccepted =
                        true;


                    return
                        new
                            H264DecoderNegotiationProbeResult(
                                DecoderName:
                                    decoderName,

                                Activated:
                                    activated,

                                AsyncTransform:
                                    isAsync,

                                D3D11Aware:
                                    d3d11Aware,

                                D3DManagerAttached:
                                    managerAttached,

                                H264InputAccepted:
                                    inputAccepted,

                                Nv12OutputAccepted:
                                    outputAccepted,

                                Error:
                                    null
                            );
                }
                catch (Exception ex)
                {
                    lastError =
                        ex.Message;
                }
                finally
                {
                    if (activated)
                    {
                        try
                        {
                            activation
                                .ShutdownObject();
                        }
                        catch
                        {
                            //
                            // Probe cleanup only.
                            //
                        }
                    }


                    decoder?.Dispose();

                    deviceManager
                        ?.Dispose();
                }
            }


            return
                new
                    H264DecoderNegotiationProbeResult(
                        DecoderName:
                            lastDecoderName,

                        Activated:
                            false,

                        AsyncTransform:
                            false,

                        D3D11Aware:
                            false,

                        D3DManagerAttached:
                            false,

                        H264InputAccepted:
                            false,

                        Nv12OutputAccepted:
                            false,

                        Error:
                            lastError ??
                            "No compatible H.264 decoder MFT was found."
                    );
        }
        finally
        {
            MFShutdown()
                .CheckError();
        }
    }


    internal static IMFMediaType
        CreateH264InputType()
    {
        IMFMediaType type =
            MFCreateMediaType();


        try
        {
            type
                .Set(
                    MediaTypeAttributeKeys
                        .MajorType,

                    MediaTypeGuids.Video
                )
                .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys
                        .Subtype,

                    VideoFormatGuids.H264
                )
                .CheckError();


            MFSetAttributeSize(
                type,

                MediaTypeAttributeKeys
                    .FrameSize,

                Width,
                Height
            )
            .CheckError();


            MFSetAttributeRatio(
                type,

                MediaTypeAttributeKeys
                    .FrameRate,

                FramesPerSecond,
                1
            )
            .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys
                        .InterlaceMode,

                    Progressive
                )
                .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys
                        .Mpeg2Profile,

                    H264MainProfile
                )
                .CheckError();


            MFSetAttributeRatio(
                type,

                MediaTypeAttributeKeys
                    .PixelAspectRatio,

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


    internal static IMFMediaType
        CreateNv12OutputType()
    {
        IMFMediaType type =
            MFCreateMediaType();


        try
        {
            type
                .Set(
                    MediaTypeAttributeKeys
                        .MajorType,

                    MediaTypeGuids.Video
                )
                .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys
                        .Subtype,

                    VideoFormatGuids.NV12
                )
                .CheckError();


            MFSetAttributeSize(
                type,

                MediaTypeAttributeKeys
                    .FrameSize,

                Width,
                Height
            )
            .CheckError();


            MFSetAttributeRatio(
                type,

                MediaTypeAttributeKeys
                    .FrameRate,

                FramesPerSecond,
                1
            )
            .CheckError();


            type
                .Set(
                    MediaTypeAttributeKeys
                        .InterlaceMode,

                    Progressive
                )
                .CheckError();


            MFSetAttributeRatio(
                type,

                MediaTypeAttributeKeys
                    .PixelAspectRatio,

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


    internal static string GetDecoderName(
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
                "(unnamed video decoder)";
        }
    }


    internal static bool LooksLikeH264(
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