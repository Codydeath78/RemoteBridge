using SharpGen.Runtime;
using Vortice.Direct3D11;
using Vortice.MediaFoundation;

using static Vortice.MediaFoundation.MediaFactory;


namespace RemoteBridge.WindowsMedia;


internal sealed record NvidiaH264ContinuousFrameResult(
    bool SubmittedInput,
    bool DroppedForBackpressure,
    IReadOnlyList<MediaFoundationEncodedOutput> Outputs
);


internal sealed class NvidiaH264InputProbeSession :
    IDisposable
{
    private const uint
        MftEnumFlagHardware =
            0x00000004;

    private const uint
        MftEnumFlagSortAndFilter =
            0x00000040;


    private readonly H264StreamSettings
        _settings;


    private const uint H264MainProfile =
        77;

    private const uint Progressive =
        2;


    private const uint
        MftOutputDataBufferIncomplete =
            0x01000000;


    private readonly D3D11Nv12SnapshotPool
        _snapshotPool;

    private readonly Queue<int>
        _inFlightSnapshotSlots =
            new();


    private int
        _needInputTokens =
            1;


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
        LowLatencyGuid =
            new(
                "9C27891A-ED7A-40E1-88E8-B22727A024EE"
            );


    private readonly IMFActivate
        _activation;

    private readonly IMFTransform
        _encoder;

    private readonly IMFDXGIDeviceManager
        _deviceManager;


    private bool _needInputAvailable;

    private bool _firstInputSubmitted;

    private bool _disposed;

    private bool _firstOutputReady;


    private NvidiaH264InputProbeSession(
        IMFActivate activation,
        IMFTransform encoder,
        IMFDXGIDeviceManager deviceManager,
        Direct3D11DeviceBundle deviceBundle,
        string encoderName,
        H264StreamSettings settings)
    {
        _activation =
            activation;

        _encoder =
            encoder;

        _deviceManager =
            deviceManager;

        _settings =
            settings;

        EncoderName =
            encoderName;

        _needInputAvailable =
            true;

        _snapshotPool =
            new D3D11Nv12SnapshotPool(
                deviceBundle,
                settings.Width,
                settings.Height,
                capacity: 8
            );
    }


    public string EncoderName
    {
        get;
    }


    public bool FirstInputSubmitted =>
        _firstInputSubmitted;

    public bool FirstOutputReady =>
        _firstOutputReady;


    public static NvidiaH264InputProbeSession Create(
        Direct3D11DeviceBundle deviceBundle)
    {
        return Create(
            deviceBundle,
            H264StreamSettings.Default
        );
    }


    public static NvidiaH264InputProbeSession Create(
        Direct3D11DeviceBundle deviceBundle,
        H264StreamSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            deviceBundle
        );

        ArgumentNullException.ThrowIfNull(
            settings
        );

        settings.Validate();


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


        foreach (
            IMFActivate activation
            in activations
        )
        {
            string name =
                GetEncoderName(
                    activation
                );


            if (!IsNvidiaH264(name))
            {
                continue;
            }


            IMFTransform? encoder =
                null;

            IMFDXGIDeviceManager? deviceManager =
                null;


            try
            {
                encoder =
                    activation
                        .ActivateObject<
                            IMFTransform
                        >();


                using IMFAttributes attributes =
                    MediaFoundationTransformInterop
                        .GetAttributes(
                            encoder
                        );


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
                    isAsync =
                        false;
                }


                if (!isAsync)
                {
                    throw new InvalidOperationException(
                        "The NVIDIA H.264 encoder is not reporting asynchronous MFT support."
                    );
                }


                attributes
                    .Set(
                        TransformAsyncUnlockGuid,
                        1u
                    )
                    .CheckError();

                    
                attributes
                    .Set(
                        LowLatencyGuid,
                        1u
                    )
                    .CheckError();


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
                    d3d11Aware =
                        false;
                }


                if (!d3d11Aware)
                {
                    throw new InvalidOperationException(
                        "The NVIDIA H.264 encoder is not D3D11-aware."
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
                        encoder,
                        deviceManager
                    );


                using IMFMediaType outputType =
                    CreateOutputType(
                        settings
                    );


                encoder.SetOutputType(
                    0,
                    outputType,
                    0
                );


                using IMFMediaType inputType =
                    CreateInputType(
                        settings
                    );


                encoder.SetInputType(
                    0,
                    inputType,
                    0
                );


                MediaFoundationAsyncTransformInterop
                    .BeginStreaming(
                        encoder
                    );


                MediaFoundationAsyncTransformInterop
                    .StartOfStream(
                        encoder
                    );


                MediaFoundationAsyncTransformInterop
                    .WaitForNeedInput(
                        encoder,
                        TimeSpan.FromSeconds(
                            2
                        )
                    );


                return
                    new NvidiaH264InputProbeSession(
                        activation,
                        encoder,
                        deviceManager,
                        deviceBundle,
                        name,
                        settings
                    );
            }
            catch
            {
                encoder?.Dispose();

                deviceManager?.Dispose();


                try
                {
                    activation
                        .ShutdownObject();
                }
                catch
                {
                    // Probe cleanup only.
                }


                throw;
            }
        }


        throw new InvalidOperationException(
            "No NVIDIA H.264 hardware encoder was found."
        );
    }


    public bool TrySubmitFirstInput(
        IMFSample sample)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        ArgumentNullException.ThrowIfNull(
            sample
        );


        if (_firstInputSubmitted)
        {
            return false;
        }


        if (!_needInputAvailable)
        {
            return false;
        }


        MediaFoundationAsyncTransformInterop
            .ProcessInput(
                _encoder,
                0,
                sample
            );


        _needInputAvailable =
            false;

        _firstInputSubmitted =
            true;


        return true;
    }

    public MediaFoundationOutputStreamInfo
        GetOutputStreamInfo()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        if (!_firstOutputReady)
        {
            throw new InvalidOperationException(
                "NVIDIA output is not ready yet."
            );
        }


        return
            MediaFoundationAsyncTransformInterop
                .GetOutputStreamInfo(
                    _encoder,
                    0
                );
    }


    public NvidiaH264ContinuousFrameResult
        ProcessFrame(
            ID3D11Texture2D nv12Texture,
            long sampleTime100Ns,
            long sampleDuration100Ns)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        ArgumentNullException.ThrowIfNull(
            nv12Texture
        );


        List<MediaFoundationEncodedOutput>
            outputs =
                new();


        PumpAvailableEvents(
            outputs
        );


        if (
            _needInputTokens <=
            0
        )
        {
            return
                new NvidiaH264ContinuousFrameResult(
                    SubmittedInput:
                        false,

                    DroppedForBackpressure:
                        true,

                    Outputs:
                        outputs
                );
        }


        if (
            !_snapshotPool
                .TryRentSnapshot(
                    nv12Texture,
                    out int slot,
                    out ID3D11Texture2D snapshot
                )
        )
        {
            return
                new NvidiaH264ContinuousFrameResult(
                    SubmittedInput:
                        false,

                    DroppedForBackpressure:
                        true,

                    Outputs:
                        outputs
                );
        }


        bool slotTransferred =
            false;


        try
        {
            using IMFSample sample =
                MediaFoundationDxgiSampleFactory
                    .CreateNv12Sample(
                        snapshot,
                        sampleTime100Ns,
                        sampleDuration100Ns
                    );


            MediaFoundationAsyncTransformInterop
                .ProcessInput(
                    _encoder,
                    0,
                    sample
                );


            _needInputTokens--;


            _inFlightSnapshotSlots
                .Enqueue(
                    slot
                );


            slotTransferred =
                true;
        }
        finally
        {
            if (!slotTransferred)
            {
                _snapshotPool.Return(
                    slot
                );
            }
        }


        //
        // NVIDIA may already have generated
        // another async event by this point.
        //
        PumpAvailableEvents(
            outputs
        );


        return
            new NvidiaH264ContinuousFrameResult(
                SubmittedInput:
                    true,

                DroppedForBackpressure:
                    false,

                Outputs:
                    outputs
            );
    }

    public MediaFoundationEncodedOutput
        TakeFirstOutput()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        if (!_firstOutputReady)
        {
            throw new InvalidOperationException(
                "NVIDIA encoded output is not ready yet."
            );
        }


        MediaFoundationOutputStreamInfo
            outputInfo =
                GetOutputStreamInfo();


        if (!outputInfo.ProvidesSamples)
        {
            throw new InvalidOperationException(
                "This probe expects NVIDIA to provide its own output samples."
            );
        }


        MediaFoundationEncodedOutput result =
            MediaFoundationAsyncTransformInterop
                .ProcessProvidedOutput(
                    _encoder,
                    0
                );


        _firstOutputReady =
            false;


        return result;
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
            // Probe cleanup only.
        }


        _encoder.Dispose();

        _snapshotPool.Dispose();

        _deviceManager.Dispose();

        _activation.Dispose();
    }

    public void WaitForFirstOutput(
        TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        if (!_firstInputSubmitted)
        {
            throw new InvalidOperationException(
                "No NVIDIA input sample has been submitted yet."
            );
        }


        if (_firstOutputReady)
        {
            return;
        }


        //
        // The encoder may buffer the first H.264
        // frame while waiting for more input.
        //
        // For this one-frame probe, force the MFT
        // to finish whatever input it already has.
        //
        MediaFoundationAsyncTransformInterop
            .Drain(
                _encoder
            );


        MediaFoundationAsyncTransformInterop
            .WaitForHaveOutput(
                _encoder,
                timeout
            );


        _firstOutputReady =
            true;
    }


    private static IMFMediaType CreateOutputType(
        H264StreamSettings settings)
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
                    settings.Bitrate
                )
                .CheckError();


            MFSetAttributeSize(
                type,
                MediaTypeAttributeKeys.FrameSize,
                settings.Width,
                settings.Height
            )
            .CheckError();


            MFSetAttributeRatio(
                type,
                MediaTypeAttributeKeys.FrameRate,
                settings.FramesPerSecond,
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


    private void PumpAvailableEvents(
    List<MediaFoundationEncodedOutput> outputs)
    {
        const int maxPumpPasses =
            32;


        for (
            int pumpPass = 0;
            pumpPass < maxPumpPasses;
            pumpPass++
        )
        {
            MediaFoundationTransformEventBatch events =
                MediaFoundationAsyncTransformInterop
                    .ReadAvailableEvents(
                        _encoder
                    );


            int totalEvents =
                events.NeedInputCount +
                events.HaveOutputCount +
                events.OtherEventCount;


            //
            // Nothing else is currently queued.
            //
            if (totalEvents == 0)
            {
                return;
            }


            //
            // Each METransformNeedInput represents
            // permission to submit one input sample.
            //
            _needInputTokens +=
                events.NeedInputCount;


            for (
                int eventIndex = 0;
                eventIndex < events.HaveOutputCount;
                eventIndex++
            )
            {
                bool moreOutput;


                do
                {
                    MediaFoundationEncodedOutput output =
                        MediaFoundationAsyncTransformInterop
                            .ProcessProvidedOutput(
                                _encoder,
                                0
                            );


                    outputs.Add(
                        output
                    );


                    moreOutput =
                        (
                            output.BufferStatus &
                            MftOutputDataBufferIncomplete
                        ) != 0;
                }
                while (moreOutput);


                //
                // One completed encoded frame retires
                // one in-flight NV12 snapshot.
                //
                if (
                    _inFlightSnapshotSlots.Count >
                    0
                )
                {
                    int completedSlot =
                        _inFlightSnapshotSlots
                            .Dequeue();


                    _snapshotPool.Return(
                        completedSlot
                    );
                }
            }


            //
            // IMPORTANT:
            //
            // ProcessOutput() can cause the async MFT
            // to enqueue another METransformNeedInput.
            //
            // Loop around and immediately read the
            // event queue again instead of waiting
            // until the next WGC frame arrives.
            //
        }


        throw new InvalidOperationException(
            "The NVIDIA encoder event queue did not settle after 32 pump passes."
        );
    }

    private static IMFMediaType CreateInputType(
        H264StreamSettings settings)
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
                settings.Width,
                settings.Height
            )
            .CheckError();


            MFSetAttributeRatio(
                type,
                MediaTypeAttributeKeys.FrameRate,
                settings.FramesPerSecond,
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
        bool isNvidia =
            encoderName.Contains(
                "NVIDIA",
                StringComparison.OrdinalIgnoreCase
            );


        bool isH264 =
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
            );


        return
            isNvidia &&
            isH264;
    }
}