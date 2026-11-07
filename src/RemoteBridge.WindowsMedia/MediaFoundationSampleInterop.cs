using SharpGen.Runtime;

using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


internal static unsafe class MediaFoundationSampleInterop
{
    //
    // IMFSample inherits IMFAttributes.
    //
    // IUnknown occupies vtable slots 0-2.
    // IMFAttributes occupies slots 3-32.
    //
    // IMFSample:
    //
    // 33 GetSampleFlags
    // 34 SetSampleFlags
    // 35 GetSampleTime
    // 36 SetSampleTime
    // 37 GetSampleDuration
    // 38 SetSampleDuration
    //
    private const int SetSampleTimeVtableIndex =
        36;

    private const int SetSampleDurationVtableIndex =
        38;


    public static void SetSampleTime(
        IMFSample sample,
        long sampleTime100Ns)
    {
        ArgumentNullException.ThrowIfNull(
            sample
        );


        nint samplePointer =
            sample.NativePointer;


        if (samplePointer == nint.Zero)
        {
            throw new InvalidOperationException(
                "The IMFSample native pointer is null."
            );
        }


        nint* vtable =
            *(nint**)samplePointer;


        var setSampleTime =
            (delegate* unmanaged[Stdcall]<
                nint,
                long,
                int>)
            vtable[
                SetSampleTimeVtableIndex
            ];


        int hr =
            setSampleTime(
                samplePointer,
                sampleTime100Ns
            );


        Result result =
            hr;

        result.CheckError();
    }


    public static void SetSampleDuration(
        IMFSample sample,
        long sampleDuration100Ns)
    {
        ArgumentNullException.ThrowIfNull(
            sample
        );


        nint samplePointer =
            sample.NativePointer;


        if (samplePointer == nint.Zero)
        {
            throw new InvalidOperationException(
                "The IMFSample native pointer is null."
            );
        }


        nint* vtable =
            *(nint**)samplePointer;


        var setSampleDuration =
            (delegate* unmanaged[Stdcall]<
                nint,
                long,
                int>)
            vtable[
                SetSampleDurationVtableIndex
            ];


        int hr =
            setSampleDuration(
                samplePointer,
                sampleDuration100Ns
            );


        Result result =
            hr;

        result.CheckError();
    }
}