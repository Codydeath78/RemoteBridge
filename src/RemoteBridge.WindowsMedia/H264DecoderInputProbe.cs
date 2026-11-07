using SharpGen.Runtime;

using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;


namespace RemoteBridge.WindowsMedia;


public sealed record
    H264DecoderInputProbeResult(
        string? DecoderName,
        int InputBytes,
        bool AsyncTransform,
        bool D3D11Aware,
        bool D3DManagerAttached,
        bool ProcessInputAccepted,
        string? Error
    );


public static class
    H264DecoderInputProbe
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


    private const long
        FrameDuration100Ns =
            10_000_000L / 60;


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
        H264DecoderInputProbeResult
        Run(
            byte[] h264Bytes,
            long timestamp100Ns)
    {
        ArgumentNullException.ThrowIfNull(
            h264Bytes
        );


        if (h264Bytes.Length == 0)
        {
            throw new ArgumentException(
                "The H.264 access unit cannot be empty.",
                nameof(h264Bytes)
            );
        }


        if (!OperatingSystem.IsWindows())
        {
            throw new
                PlatformNotSupportedException(
                    "H.264 decoder input probing requires Windows."
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


            string? lastName =
                null;

            string? lastError =
                null;


            foreach (
                IMFActivate activation
                in activations
            )
            {
                string decoderName =
                    H264DecoderNegotiationProbe
                        .GetDecoderName(
                            activation
                        );


                if (
                    !H264DecoderNegotiationProbe
                        .LooksLikeH264(
                            decoderName
                        )
                )
                {
                    continue;
                }


                lastName =
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
                                "The selected H.264 decoder is not D3D11-aware."
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
                            H264DecoderNegotiationProbe
                                .CreateH264InputType();


                    decoder.SetInputType(
                        0,
                        inputType,
                        0
                    );


                    using IMFMediaType
                        outputType =
                            H264DecoderNegotiationProbe
                                .CreateNv12OutputType();


                    decoder.SetOutputType(
                        0,
                        outputType,
                        0
                    );


                    MediaFoundationAsyncTransformInterop
                        .BeginStreaming(
                            decoder
                        );


                    MediaFoundationAsyncTransformInterop
                        .StartOfStream(
                            decoder
                        );


                    //
                    // Asynchronous MFTs request input
                    // through METransformNeedInput.
                    //
                    // Synchronous decoder MFTs do not
                    // require that event handshake.
                    //
                    if (isAsync)
                    {
                        MediaFoundationAsyncTransformInterop
                            .WaitForNeedInput(
                                decoder,
                                TimeSpan.FromSeconds(
                                    2
                                )
                            );
                    }


                    using IMFSample
                        inputSample =
                            MediaFoundationByteSampleFactory
                                .Create(
                                    h264Bytes,
                                    timestamp100Ns,
                                    FrameDuration100Ns
                                );


                    MediaFoundationAsyncTransformInterop
                        .ProcessInput(
                            decoder,
                            0,
                            inputSample
                        );


                    return
                        new
                            H264DecoderInputProbeResult(
                                DecoderName:
                                    decoderName,

                                InputBytes:
                                    h264Bytes.Length,

                                AsyncTransform:
                                    isAsync,

                                D3D11Aware:
                                    d3d11Aware,

                                D3DManagerAttached:
                                    managerAttached,

                                ProcessInputAccepted:
                                    true,

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
                    H264DecoderInputProbeResult(
                        DecoderName:
                            lastName,

                        InputBytes:
                            h264Bytes.Length,

                        AsyncTransform:
                            false,

                        D3D11Aware:
                            false,

                        D3DManagerAttached:
                            false,

                        ProcessInputAccepted:
                            false,

                        Error:
                            lastError ??
                            "No compatible H.264 decoder accepted the access unit."
                    );
        }
        finally
        {
            MFShutdown()
                .CheckError();
        }
    }
}