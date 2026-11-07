using SharpGen.Runtime;

using Vortice.MediaFoundation;

using System.Collections.Generic;

using static Vortice.MediaFoundation.MediaFactory;


namespace RemoteBridge.WindowsMedia;


public sealed class H264DecoderSession :
    IDisposable
{
    private const uint
        MftEnumFlagSyncMft =
            0x00000001;

    private const int
        MfENotAccepting =
            unchecked(
                (int)0xC00D36B0
            );

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


    private const int
        MfETransformStreamChange =
            unchecked(
                (int)0xC00D6D61
            );


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

    private static readonly Guid
        MfLowLatencyGuid =
            new(
                "9C27891A-ED7A-40E1-88E8-B22727A024EE"
            );


    private readonly Direct3D11DeviceBundle
        _deviceBundle;

    private readonly IMFActivate
        _activation;

    private readonly IMFTransform
        _decoder;

    private readonly IMFDXGIDeviceManager
        _deviceManager;


    private bool
        _disposed;

    private bool
        _mediaFoundationStarted;


    private long
        _inputFrames;

    private long
        _outputFrames;


    private H264DecoderSession(
        Direct3D11DeviceBundle deviceBundle,
        IMFActivate activation,
        IMFTransform decoder,
        IMFDXGIDeviceManager deviceManager,
        string decoderName)
    {
        _deviceBundle =
            deviceBundle;

        _activation =
            activation;

        _decoder =
            decoder;

        _deviceManager =
            deviceManager;

        DecoderName =
            decoderName;

        _mediaFoundationStarted =
            true;
    }


    public string DecoderName
    {
        get;
    }


    public long InputFrames =>
        Interlocked.Read(
            ref _inputFrames
        );


    public long OutputFrames =>
        Interlocked.Read(
            ref _outputFrames
        );


    public static H264DecoderSession Create()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new
                PlatformNotSupportedException(
                    "H.264 decoding requires Windows."
                );
        }


        MFStartup()
            .CheckError();


        Direct3D11DeviceBundle?
            deviceBundle =
                null;


        try
        {
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


            foreach (
                IMFActivate activation
                in activations
            )
            {
                string name =
                    H264DecoderNegotiationProbe
                        .GetDecoderName(
                            activation
                        );


                if (
                    !H264DecoderNegotiationProbe
                        .LooksLikeH264(
                            name
                        )
                )
                {
                    continue;
                }


                IMFTransform?
                    decoder =
                        null;

                IMFDXGIDeviceManager?
                    deviceManager =
                        null;


                try
                {
                    decoder =
                        activation
                            .ActivateObject<
                                IMFTransform
                            >();


                    using IMFAttributes
                        attributes =
                            MediaFoundationTransformInterop
                                .GetAttributes(
                                    decoder
                                );


                    bool isAsync;


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
                        attributes
                            .Set(
                                MfLowLatencyGuid,
                                1u
                            )
                            .CheckError();
                    }
                    catch
                    {
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
                        decoder.Dispose();

                        decoder =
                            null;

                        continue;
                    }


                    deviceManager =
                        MFCreateDXGIDeviceManager();


                    deviceManager
                        .ResetDevice(
                            deviceBundle
                                .NativeDevice
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
                                "The H.264 decoder cannot provide D3D11 output samples."
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


                    return new H264DecoderSession(
                        deviceBundle,
                        activation,
                        decoder,
                        deviceManager,
                        name
                    );
                }
                catch
                {
                    decoder?.Dispose();

                    deviceManager?.Dispose();


                    try
                    {
                        activation
                            .ShutdownObject();
                    }
                    catch
                    {
                    }
                }
            }


            throw new
                InvalidOperationException(
                    "No compatible D3D11 H.264 decoder was found."
                );
        }
        catch
        {
            deviceBundle?.Dispose();


            MFShutdown()
                .CheckError();


            throw;
        }
    }


    public bool TrySubmitInput(
    byte[] h264Bytes,
    long timestamp100Ns)
{
    ObjectDisposedException.ThrowIf(
        _disposed,
        this
    );


    ArgumentNullException.ThrowIfNull(
        h264Bytes
    );


    if (h264Bytes.Length == 0)
    {
        return true;
    }


    using IMFSample
        inputSample =
            MediaFoundationByteSampleFactory
                .Create(
                    h264Bytes,
                    timestamp100Ns,
                    FrameDuration100Ns
                );


    try
    {
        MediaFoundationAsyncTransformInterop
            .ProcessInput(
                _decoder,
                0,
                inputSample
            );
    }
    catch (Exception ex)
        when (
            ex.HResult ==
            MfENotAccepting
        )
    {
        //
        // The decoder has pending output.
        // Do NOT lose this input AU.
        //
        return false;
    }


    Interlocked.Increment(
        ref _inputFrames
    );


    return true;
}


public H264DecodedFrame?
    TryTakePendingOutput()
{
    ObjectDisposedException.ThrowIf(
        _disposed,
        this
    );


    return TryTakeOutput();
}

        public H264DecodedFrame?
        Decode(
            byte[] h264Bytes,
            long timestamp100Ns)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        ArgumentNullException.ThrowIfNull(
            h264Bytes
        );


        if (h264Bytes.Length == 0)
        {
            return null;
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
                _decoder,
                0,
                inputSample
            );


        Interlocked.Increment(
            ref _inputFrames
        );


        return
            TryTakeOutput();
    }


    public IReadOnlyList<H264DecodedFrame>
    DecodeAll(
        byte[] h264Bytes,
        long timestamp100Ns)
{
    ObjectDisposedException.ThrowIf(
        _disposed,
        this
    );


    ArgumentNullException.ThrowIfNull(
        h264Bytes
    );


    var frames =
        new List<H264DecodedFrame>(
            capacity: 2
        );


    if (h264Bytes.Length == 0)
    {
        return frames;
    }


    try
    {
        //
        // IMPORTANT:
        //
        // A synchronous Media Foundation decoder
        // may still have output waiting from a
        // previous access unit.
        //
        // Drain everything available BEFORE
        // submitting another compressed frame.
        //
        DrainAvailableOutput(
            frames
        );


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
                _decoder,
                0,
                inputSample
            );


        Interlocked.Increment(
            ref _inputFrames
        );


        //
        // One input access unit can result in
        // zero, one, or more immediately
        // available output samples.
        //
        // Do not stop after the first one.
        //
        DrainAvailableOutput(
            frames
        );


        return frames;
    }
    catch
    {
        //
        // Ownership has not been transferred
        // to the caller when DecodeAll fails.
        //
        foreach (
            H264DecodedFrame frame
            in frames
        )
        {
            frame.Dispose();
        }


        throw;
    }
}


private void DrainAvailableOutput(
    List<H264DecodedFrame> frames)
{
    while (true)
    {
        H264DecodedFrame?
            frame =
                TryTakeOutput();


        if (frame is null)
        {
            break;
        }


        frames.Add(
            frame
        );
    }
}

        private H264DecodedFrame?
        TryTakeOutput()
    {
        while (true)
        {
            try
            {
                bool produced =
                    MediaFoundationDecoderOutputInterop
                        .TryProcessProvidedOutput(
                            _decoder,

                            out IMFSample?
                                outputSample,

                            out uint
                                processStatus,

                            out uint
                                bufferStatus
                        );


                if (!produced)
                {
                    outputSample?.Dispose();

                    return null;
                }


                if (outputSample is null)
                {
                    throw new
                        InvalidOperationException(
                            "The decoder reported output without returning an IMFSample."
                        );
                }


                try
                {
                    MediaFoundationDecodedTextureInfo
                        textureInfo =
                            MediaFoundationDecoderOutputInterop
                                .GetTextureInfo(
                                    outputSample
                                );


                    Interlocked.Increment(
                        ref _outputFrames
                    );


                    return new H264DecodedFrame(
                        outputSample,
                        textureInfo.Width,
                        textureInfo.Height,
                        textureInfo.Format,
                        textureInfo.ArraySize,
                        textureInfo.SubresourceIndex,
                        processStatus,
                        bufferStatus
                    );
                }
                catch
                {
                    outputSample.Dispose();

                    throw;
                }
            }
            catch (Exception ex)
                when (
                    ex.HResult ==
                    MfETransformStreamChange
                )
            {
                RenegotiateOutputType();

                //
                // Retry ProcessOutput immediately.
                //
            }
        }
    }

        private void RenegotiateOutputType()
    {
        using IMFMediaType
            newOutputType =
                MediaFoundationDecoderTypeInterop
                    .GetOutputAvailableType(
                        _decoder,
                        streamId: 0,
                        typeIndex: 0
                    );


        _decoder.SetOutputType(
            0,
            newOutputType,
            0
        );
    }

        public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed =
            true;


        try
        {
            _activation
                .ShutdownObject();
        }
        catch
        {
            //
            // Shutdown cleanup must remain
            // best-effort.
            //
        }


        _decoder.Dispose();

        _deviceManager.Dispose();

        _activation.Dispose();

        _deviceBundle.Dispose();


        if (_mediaFoundationStarted)
        {
            _mediaFoundationStarted =
                false;


            try
            {
                MFShutdown()
                    .CheckError();
            }
            catch
            {
            }
        }


        GC.SuppressFinalize(
            this
        );
    }
}