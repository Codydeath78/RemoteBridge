using System.Diagnostics;
using System.Runtime.InteropServices;

using SharpGen.Runtime;

using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


internal sealed record MediaFoundationOutputStreamInfo(
    uint Flags,
    uint BufferSize,
    uint Alignment,
    bool ProvidesSamples,
    bool CanProvideSamples
);

internal sealed record MediaFoundationEncodedOutput(
    byte[] Bytes,
    uint ProcessStatus,
    uint BufferStatus
);

internal sealed record MediaFoundationTransformEventBatch(
    int NeedInputCount,
    int HaveOutputCount,
    int OtherEventCount
);


internal static unsafe class
    MediaFoundationAsyncTransformInterop
{


    [StructLayout(
        LayoutKind.Sequential
    )]
    private struct MftOutputStreamInfoNative
    {
        public uint Flags;

        public uint BufferSize;

        public uint Alignment;
    }


    [StructLayout(
        LayoutKind.Sequential
    )]
    private struct MftOutputDataBufferNative
    {
        public uint StreamId;

        public nint Sample;

        public uint Status;

        public nint Events;
    }


    private const uint
        MftOutputStreamProvidesSamples =
            0x00000100;

    private const uint
        MftOutputStreamCanProvideSamples =
            0x00000200;


    private const int
        ProcessOutputVtableIndex =
            25;

    private const int
        SampleConvertToContiguousBufferVtableIndex =
            41;

    private const int
        MediaBufferLockVtableIndex =
            3;

    private const int
        MediaBufferUnlockVtableIndex =
            4;




    private const uint
        MftMessageNotifyBeginStreaming =
            0x10000000;

    private const uint
        MftMessageNotifyStartOfStream =
            0x10000003;


    private const uint
        MftMessageCommandDrain =
            0x00000001;


    private const uint
        MfEventFlagNoWait =
            0x00000001;


    private const int
        MeTransformNeedInput =
            601;

    private const int
        MeTransformHaveOutput =
            602;

    private const int
        ProcessInputVtableIndex =
            24;

    private const int
        GetOutputStreamInfoVtableIndex =
            7;


    private const int
        MfENoEventsAvailable =
            unchecked(
                (int)0xC00D3E80
            );


    private static readonly Guid
        ImfMediaEventGeneratorGuid =
            new(
                "2CD0BD52-BCD5-4B89-B62C-EADC0C031E7D"
            );


    //
    // IMFTransform:
    //
    // IUnknown       = 0..2
    // ...
    // ProcessMessage = 23
    //
    private const int
        ProcessMessageVtableIndex =
            23;


    //
    // IMFMediaEventGenerator:
    //
    // IUnknown  = 0..2
    // GetEvent  = 3
    //
    private const int
        GetEventVtableIndex =
            3;


    //
    // IMFMediaEvent inherits IMFAttributes.
    //
    // IUnknown      = 0..2
    // IMFAttributes = 3..32
    //
    // GetType   = 33
    // GetStatus = 35
    //
    private const int
        GetEventTypeVtableIndex =
            33;

    private const int
        GetEventStatusVtableIndex =
            35;


    public static void BeginStreaming(
        IMFTransform transform)
    {
        SendMessage(
            transform,
            MftMessageNotifyBeginStreaming,
            0
        );
    }

    public static MediaFoundationOutputStreamInfo
        GetOutputStreamInfo(
            IMFTransform transform,
            uint streamId)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );


        nint transformPointer =
            transform.NativePointer;


        if (
            transformPointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "The IMFTransform native pointer is null."
            );
        }


        nint* vtable =
            *(nint**)transformPointer;


        var getOutputStreamInfo =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                MftOutputStreamInfoNative*,
                int>)
            vtable[
                GetOutputStreamInfoVtableIndex
            ];


        MftOutputStreamInfoNative
            nativeInfo =
                default;


        int hr =
            getOutputStreamInfo(
                transformPointer,
                streamId,
                &nativeInfo
            );


        Result result =
            hr;

        result.CheckError();


        bool providesSamples =
            (
                nativeInfo.Flags &
                MftOutputStreamProvidesSamples
            ) != 0;


        bool canProvideSamples =
            (
                nativeInfo.Flags &
                MftOutputStreamCanProvideSamples
            ) != 0;


        return
            new MediaFoundationOutputStreamInfo(
                Flags:
                    nativeInfo.Flags,

                BufferSize:
                    nativeInfo.BufferSize,

                Alignment:
                    nativeInfo.Alignment,

                ProvidesSamples:
                    providesSamples,

                CanProvideSamples:
                    canProvideSamples
            );
    }

    public static MediaFoundationTransformEventBatch
        ReadAvailableEvents(
            IMFTransform transform)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );


        nint eventGeneratorPointer =
            nint.Zero;


        Guid iid =
            ImfMediaEventGeneratorGuid;


        int queryResult =
            Marshal.QueryInterface(
                transform.NativePointer,
                in iid,
                out eventGeneratorPointer
            );


        Result queryHr =
            queryResult;

        queryHr.CheckError();


        if (
            eventGeneratorPointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "The NVIDIA encoder did not expose IMFMediaEventGenerator."
            );
        }


        try
        {
            int needInputCount =
                0;

            int haveOutputCount =
                0;

            int otherEventCount =
                0;


            while (true)
            {
                nint eventPointer =
                    nint.Zero;


                int hr =
                    GetEvent(
                        eventGeneratorPointer,
                        MfEventFlagNoWait,
                        out eventPointer
                    );


                if (
                    hr ==
                    MfENoEventsAvailable
                )
                {
                    break;
                }


                Result getEventResult =
                    hr;

                getEventResult
                    .CheckError();


                if (
                    eventPointer ==
                    nint.Zero
                )
                {
                    continue;
                }


                try
                {
                    int eventStatus =
                        GetEventStatus(
                            eventPointer
                        );


                    Result statusResult =
                        eventStatus;

                    statusResult
                        .CheckError();


                    int eventType =
                        GetEventType(
                            eventPointer
                        );


                    if (
                        eventType ==
                        MeTransformNeedInput
                    )
                    {
                        needInputCount++;
                    }
                    else if (
                        eventType ==
                        MeTransformHaveOutput
                    )
                    {
                        haveOutputCount++;
                    }
                    else
                    {
                        otherEventCount++;
                    }
                }
                finally
                {
                    Marshal.Release(
                        eventPointer
                    );
                }
            }


            return
                new MediaFoundationTransformEventBatch(
                    NeedInputCount:
                        needInputCount,

                    HaveOutputCount:
                        haveOutputCount,

                    OtherEventCount:
                        otherEventCount
                );
        }
        finally
        {
            Marshal.Release(
                eventGeneratorPointer
            );
        }
    }


    public static MediaFoundationEncodedOutput
        ProcessProvidedOutput(
            IMFTransform transform,
            uint streamId)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );


        nint transformPointer =
            transform.NativePointer;


        if (
            transformPointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "The IMFTransform native pointer is null."
            );
        }


        nint* vtable =
            *(nint**)transformPointer;


        var processOutput =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                uint,
                MftOutputDataBufferNative*,
                uint*,
                int>)
            vtable[
                ProcessOutputVtableIndex
            ];


        MftOutputDataBufferNative output =
            new()
            {
                StreamId =
                    streamId,

                Sample =
                    nint.Zero,

                Status =
                    0,

                Events =
                    nint.Zero
            };


        uint processStatus =
            0;


        try
        {
            int hr =
                processOutput(
                    transformPointer,
                    0,
                    1,
                    &output,
                    &processStatus
                );


            Result result =
                hr;

            result.CheckError();


            if (
                output.Sample ==
                nint.Zero
            )
            {
                throw new InvalidOperationException(
                    "NVIDIA ProcessOutput succeeded but returned no IMFSample."
                );
            }


            byte[] bytes =
                CopySampleBytes(
                    output.Sample
                );


            return
                new MediaFoundationEncodedOutput(
                    Bytes:
                        bytes,

                    ProcessStatus:
                        processStatus,

                    BufferStatus:
                        output.Status
                );
        }
        finally
        {
            if (
                output.Events !=
                nint.Zero
            )
            {
                Marshal.Release(
                    output.Events
                );
            }


            if (
                output.Sample !=
                nint.Zero
            )
            {
                Marshal.Release(
                    output.Sample
                );
            }
        }
    }

    private static byte[] CopySampleBytes(
        nint samplePointer)
    {
        if (
            samplePointer ==
            nint.Zero
        )
        {
            throw new ArgumentException(
                "The IMFSample pointer is null.",
                nameof(samplePointer)
            );
        }


        nint* sampleVtable =
            *(nint**)samplePointer;


        var convertToContiguousBuffer =
            (delegate* unmanaged[Stdcall]<
                nint,
                nint*,
                int>)
            sampleVtable[
                SampleConvertToContiguousBufferVtableIndex
            ];


        nint bufferPointer =
            nint.Zero;


        int hr =
            convertToContiguousBuffer(
                samplePointer,
                &bufferPointer
            );


        Result result =
            hr;

        result.CheckError();


        if (
            bufferPointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "The encoded IMFSample returned no IMFMediaBuffer."
            );
        }


        try
        {
            return
                CopyMediaBufferBytes(
                    bufferPointer
                );
        }
        finally
        {
            Marshal.Release(
                bufferPointer
            );
        }
    }


    private static byte[] CopyMediaBufferBytes(
        nint bufferPointer)
    {
        nint* vtable =
            *(nint**)bufferPointer;


        var lockBuffer =
            (delegate* unmanaged[Stdcall]<
                nint,
                nint*,
                uint*,
                uint*,
                int>)
            vtable[
                MediaBufferLockVtableIndex
            ];


        var unlockBuffer =
            (delegate* unmanaged[Stdcall]<
                nint,
                int>)
            vtable[
                MediaBufferUnlockVtableIndex
            ];


        nint dataPointer =
            nint.Zero;

        uint maximumLength =
            0;

        uint currentLength =
            0;


        int hr =
            lockBuffer(
                bufferPointer,
                &dataPointer,
                &maximumLength,
                &currentLength
            );


        Result result =
            hr;

        result.CheckError();


        try
        {
            if (
                currentLength ==
                0
            )
            {
                return
                    Array.Empty<byte>();
            }


            if (
                dataPointer ==
                nint.Zero
            )
            {
                throw new InvalidOperationException(
                    "The encoded Media Foundation buffer returned a null data pointer."
                );
            }


            byte[] bytes =
                new byte[
                    checked(
                        (int)currentLength
                    )
                ];


            Marshal.Copy(
                dataPointer,
                bytes,
                0,
                bytes.Length
            );


            return bytes;
        }
        finally
        {
            int unlockHr =
                unlockBuffer(
                    bufferPointer
                );


            Result unlockResult =
                unlockHr;

            unlockResult.CheckError();
        }
    }



    public static void Drain(
        IMFTransform transform)
    {
        SendMessage(
            transform,
            MftMessageCommandDrain,
            0
        );
    }

    public static void ProcessInput(
    IMFTransform transform,
    uint streamId,
    IMFSample sample)
{
    ArgumentNullException.ThrowIfNull(
        transform
    );

    ArgumentNullException.ThrowIfNull(
        sample
    );


    nint transformPointer =
        transform.NativePointer;


    if (
        transformPointer ==
        nint.Zero
    )
    {
        throw new InvalidOperationException(
            "The IMFTransform native pointer is null."
        );
    }


    if (
        sample.NativePointer ==
        nint.Zero
    )
    {
        throw new InvalidOperationException(
            "The IMFSample native pointer is null."
        );
    }


    nint* vtable =
        *(nint**)transformPointer;


    var processInput =
        (delegate* unmanaged[Stdcall]<
            nint,
            uint,
            nint,
            uint,
            int>)
        vtable[
            ProcessInputVtableIndex
        ];


    int hr =
        processInput(
            transformPointer,
            streamId,
            sample.NativePointer,
            0
        );


    Result result =
        hr;

    result.CheckError();
}


    public static void StartOfStream(
        IMFTransform transform)
    {
        SendMessage(
            transform,
            MftMessageNotifyStartOfStream,
            0
        );
    }


    public static void WaitForNeedInput(
        IMFTransform transform,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );


        nint eventGeneratorPointer =
            nint.Zero;


        Guid iid =
            ImfMediaEventGeneratorGuid;


        int queryResult =
            Marshal.QueryInterface(
                transform.NativePointer,
                in iid,
                out eventGeneratorPointer
            );


        Result queryHr =
            queryResult;

        queryHr.CheckError();


        if (
            eventGeneratorPointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "The NVIDIA encoder did not expose IMFMediaEventGenerator."
            );
        }


        try
        {
            Stopwatch timer =
                Stopwatch.StartNew();


            while (
                timer.Elapsed <
                timeout
            )
            {
                nint eventPointer =
                    nint.Zero;


                int hr =
                    GetEvent(
                        eventGeneratorPointer,
                        MfEventFlagNoWait,
                        out eventPointer
                    );


                if (
                    hr ==
                    MfENoEventsAvailable
                )
                {
                    Thread.Sleep(
                        5
                    );

                    continue;
                }


                Result getEventResult =
                    hr;

                getEventResult
                    .CheckError();


                if (
                    eventPointer ==
                    nint.Zero
                )
                {
                    continue;
                }


                try
                {
                    int eventStatus =
                        GetEventStatus(
                            eventPointer
                        );


                    Result statusResult =
                        eventStatus;

                    statusResult
                        .CheckError();


                    int eventType =
                        GetEventType(
                            eventPointer
                        );


                    if (
                        eventType ==
                        MeTransformNeedInput
                    )
                    {
                        return;
                    }


                    //
                    // Ignore unrelated events
                    // during this handshake probe.
                    //
                }
                finally
                {
                    Marshal.Release(
                        eventPointer
                    );
                }
            }


            throw new TimeoutException(
                "Timed out waiting for METransformNeedInput from the NVIDIA H.264 encoder."
            );
        }
        finally
        {
            Marshal.Release(
                eventGeneratorPointer
            );
        }
    }

    public static void WaitForHaveOutput(
    IMFTransform transform,
    TimeSpan timeout)
{
    ArgumentNullException.ThrowIfNull(
        transform
    );


    nint eventGeneratorPointer =
        nint.Zero;


    Guid iid =
        ImfMediaEventGeneratorGuid;


    int queryResult =
        Marshal.QueryInterface(
            transform.NativePointer,
            in iid,
            out eventGeneratorPointer
        );


    Result queryHr =
        queryResult;

    queryHr.CheckError();


    if (
        eventGeneratorPointer ==
        nint.Zero
    )
    {
        throw new InvalidOperationException(
            "The NVIDIA encoder did not expose IMFMediaEventGenerator."
        );
    }


    try
    {
        Stopwatch timer =
            Stopwatch.StartNew();


        while (
            timer.Elapsed <
            timeout
        )
        {
            nint eventPointer =
                nint.Zero;


            int hr =
                GetEvent(
                    eventGeneratorPointer,
                    MfEventFlagNoWait,
                    out eventPointer
                );


            if (
                hr ==
                MfENoEventsAvailable
            )
            {
                Thread.Sleep(
                    5
                );

                continue;
            }


            Result getEventResult =
                hr;

            getEventResult
                .CheckError();


            if (
                eventPointer ==
                nint.Zero
            )
            {
                continue;
            }


            try
            {
                int eventStatus =
                    GetEventStatus(
                        eventPointer
                    );


                Result statusResult =
                    eventStatus;

                statusResult
                    .CheckError();


                int eventType =
                    GetEventType(
                        eventPointer
                    );


                if (
                    eventType ==
                    MeTransformHaveOutput
                )
                {
                    return;
                }


                //
                // For this one-frame probe we
                // ignore unrelated transform
                // events while waiting for
                // encoded output.
                //
            }
            finally
            {
                Marshal.Release(
                    eventPointer
                );
            }
        }


        throw new TimeoutException(
            "Timed out waiting for METransformHaveOutput from the NVIDIA H.264 encoder."
        );
    }
    finally
    {
        Marshal.Release(
            eventGeneratorPointer
        );
    }
}


    private static void SendMessage(
        IMFTransform transform,
        uint message,
        nuint parameter)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );


        nint transformPointer =
            transform.NativePointer;


        if (
            transformPointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "The IMFTransform native pointer is null."
            );
        }


        nint* vtable =
            *(nint**)transformPointer;


        var processMessage =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                nuint,
                int>)
            vtable[
                ProcessMessageVtableIndex
            ];


        int hr =
            processMessage(
                transformPointer,
                message,
                parameter
            );


        Result result =
            hr;

        result.CheckError();
    }


    private static int GetEvent(
        nint eventGeneratorPointer,
        uint flags,
        out nint eventPointer)
    {
        nint* vtable =
            *(nint**)eventGeneratorPointer;


        var getEvent =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                nint*,
                int>)
            vtable[
                GetEventVtableIndex
            ];


        nint resultPointer =
            nint.Zero;


        int hr =
            getEvent(
                eventGeneratorPointer,
                flags,
                &resultPointer
            );


        eventPointer =
            resultPointer;


        return hr;
    }


    private static int GetEventType(
        nint eventPointer)
    {
        nint* vtable =
            *(nint**)eventPointer;


        var getType =
            (delegate* unmanaged[Stdcall]<
                nint,
                int*,
                int>)
            vtable[
                GetEventTypeVtableIndex
            ];


        int eventType =
            0;


        int hr =
            getType(
                eventPointer,
                &eventType
            );


        Result result =
            hr;

        result.CheckError();


        return eventType;
    }


    private static int GetEventStatus(
        nint eventPointer)
    {
        nint* vtable =
            *(nint**)eventPointer;


        var getStatus =
            (delegate* unmanaged[Stdcall]<
                nint,
                int*,
                int>)
            vtable[
                GetEventStatusVtableIndex
            ];


        int eventStatus =
            0;


        int hr =
            getStatus(
                eventPointer,
                &eventStatus
            );


        Result result =
            hr;

        result.CheckError();


        return eventStatus;
    }
}