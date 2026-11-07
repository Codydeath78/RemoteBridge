using SharpGen.Runtime;

using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;


namespace RemoteBridge.WindowsMedia;


public sealed record
    H264DecoderOutputProbeResult(
        string? DecoderName,
        int InputSamplesSubmitted,
        uint OutputStreamFlags,
        bool ProvidesSamples,
        bool CanProvideSamples,
        bool OutputProduced,
        bool DxgiBacked,
        uint TextureWidth,
        uint TextureHeight,
        int TextureFormat,
        uint TextureArraySize,
        uint TextureSubresourceIndex,
        uint ProcessStatus,
        uint BufferStatus,
        string? Error
    );


public static class
    H264DecoderOutputProbe
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

    private const int
        MfETransformStreamChange =
            unchecked(
                (int)0xC00D6D61
            );

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
        H264DecoderOutputProbeResult
        Run(
            IReadOnlyList<
                H264EncodedAccessUnit
            > accessUnits)
    {
        ArgumentNullException.ThrowIfNull(
            accessUnits
        );


        if (
            accessUnits.Count ==
            0
        )
        {
            throw new ArgumentException(
                "At least one real H.264 access unit is required.",
                nameof(accessUnits)
            );
        }


        if (!OperatingSystem.IsWindows())
        {
            throw new
                PlatformNotSupportedException(
                    "H.264 decoder output probing requires Windows."
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


            uint enumerationFlags =
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

                        enumerationFlags,

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


                lastDecoderName =
                    decoderName;


                IMFTransform? decoder =
                    null;

                IMFDXGIDeviceManager?
                    deviceManager =
                        null;


                bool activated =
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


                    bool isAsync =
                        false;


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


                    bool d3d11Aware;


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


                    MediaFoundationDecoderOutputStreamInfo
                        outputInfo =
                            MediaFoundationDecoderOutputInterop
                                .GetOutputStreamInfo(
                                    decoder
                                );


                    if (
                        !outputInfo.ProvidesSamples &&
                        !outputInfo.CanProvideSamples
                    )
                    {
                        throw new
                            InvalidOperationException(
                                "The D3D11 decoder requires caller-allocated output samples."
                            );
                    }


                    MediaFoundationAsyncTransformInterop
                        .BeginStreaming(
                            decoder
                        );


                    MediaFoundationAsyncTransformInterop
                        .StartOfStream(
                            decoder
                        );


                    int submitted =
                        0;


                    foreach (
                        H264EncodedAccessUnit
                            accessUnit
                        in accessUnits
                    )
                    {
                        using IMFSample
                            inputSample =
                                MediaFoundationByteSampleFactory
                                    .Create(
                                        accessUnit.Bytes,

                                        accessUnit
                                            .Timestamp100Ns,

                                        FrameDuration100Ns
                                    );


                        MediaFoundationAsyncTransformInterop
                            .ProcessInput(
                                decoder,
                                0,
                                inputSample
                            );


                        submitted++;


                        IMFSample?
    outputSample =
        null;

uint processStatus =
    0;

uint bufferStatus =
    0;

bool produced =
    false;


for (
    int outputAttempt = 0;
    outputAttempt < 3;
    outputAttempt++
)
{
    try
    {
        produced =
            MediaFoundationDecoderOutputInterop
                .TryProcessProvidedOutput(
                    decoder,

                    out outputSample,

                    out processStatus,

                    out bufferStatus
                );


        break;
    }
    catch (Exception ex)
        when (
            ex.HResult ==
            MfETransformStreamChange
        )
    {
        //
        // The decoder has now parsed enough
        // H.264 SPS/PPS information to know
        // its real decoded output format.
        //
        // Select one of the decoder's newly
        // advertised output types, then retry
        // ProcessOutput WITHOUT resubmitting
        // this input sample.
        //
        RenegotiateOutputType(
            decoder
        );


        outputInfo =
            MediaFoundationDecoderOutputInterop
                .GetOutputStreamInfo(
                    decoder
                );
    }
}


if (!produced)
{
    outputSample?.Dispose();

    continue;
}


                        using (outputSample)
                        {
                            if (
                                outputSample is null
                            )
                            {
                                throw new
                                    InvalidOperationException(
                                        "Decoder reported output but returned no sample."
                                    );
                            }


                            MediaFoundationDecodedTextureInfo
                                textureInfo =
                                    MediaFoundationDecoderOutputInterop
                                        .GetTextureInfo(
                                            outputSample
                                        );


                            return new
                                H264DecoderOutputProbeResult(
                                    DecoderName:
                                        decoderName,

                                    InputSamplesSubmitted:
                                        submitted,

                                    OutputStreamFlags:
                                        outputInfo.Flags,

                                    ProvidesSamples:
                                        outputInfo.ProvidesSamples,

                                    CanProvideSamples:
                                        outputInfo.CanProvideSamples,

                                    OutputProduced:
                                        true,

                                    DxgiBacked:
                                        true,

                                    TextureWidth:
                                        textureInfo.Width,

                                    TextureHeight:
                                        textureInfo.Height,

                                    TextureFormat:
                                        textureInfo.Format,

                                    TextureArraySize:
                                        textureInfo.ArraySize,

                                    TextureSubresourceIndex:
                                        textureInfo.SubresourceIndex,

                                    ProcessStatus:
                                        processStatus,

                                    BufferStatus:
                                        bufferStatus,

                                    Error:
                                        null
                                );
                        }
                    }


                    lastError =
                        "The decoder accepted all probe inputs but did not produce an output frame.";
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


            return new
                H264DecoderOutputProbeResult(
                    DecoderName:
                        lastDecoderName,

                    InputSamplesSubmitted:
                        0,

                    OutputStreamFlags:
                        0,

                    ProvidesSamples:
                        false,

                    CanProvideSamples:
                        false,

                    OutputProduced:
                        false,

                    DxgiBacked:
                        false,

                    TextureWidth:
                        0,

                    TextureHeight:
                        0,

                    TextureFormat:
                        0,

                    TextureArraySize:
                        0,

                    TextureSubresourceIndex:
                        0,

                    ProcessStatus:
                        0,

                    BufferStatus:
                        0,

                    Error:
                        lastError ??
                        "No compatible H.264 decoder produced output."
                );
        }
        finally
        {
            MFShutdown()
                .CheckError();
        }
    }

    private static void
    RenegotiateOutputType(
        IMFTransform decoder)
{
    using IMFMediaType
        newOutputType =
            MediaFoundationDecoderTypeInterop
                .GetOutputAvailableType(
                    decoder,
                    streamId: 0,
                    typeIndex: 0
                );


    decoder.SetOutputType(
        0,
        newOutputType,
        0
    );
}



}