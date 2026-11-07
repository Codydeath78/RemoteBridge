using SharpGen.Runtime;

using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


internal static unsafe class
    MediaFoundationDecoderTypeInterop
{
    //
    // IMFTransform vtable:
    //
    //  0..2  IUnknown
    //  3     GetStreamLimits
    //  4     GetStreamCount
    //  5     GetStreamIDs
    //  6     GetInputStreamInfo
    //  7     GetOutputStreamInfo
    //  8     GetAttributes
    //  9     GetInputStreamAttributes
    // 10     GetOutputStreamAttributes
    // 11     DeleteInputStream
    // 12     AddInputStreams
    // 13     GetInputAvailableType
    // 14     GetOutputAvailableType
    //
    private const int
        GetOutputAvailableTypeVtableIndex =
            14;


    public static IMFMediaType
        GetOutputAvailableType(
            IMFTransform transform,
            uint streamId = 0,
            uint typeIndex = 0)
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


        var getOutputAvailableType =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                uint,
                nint*,
                int>)
            vtable[
                GetOutputAvailableTypeVtableIndex
            ];


        nint mediaTypePointer =
            nint.Zero;


        int hr =
            getOutputAvailableType(
                transformPointer,
                streamId,
                typeIndex,
                &mediaTypePointer
            );


        Result result =
            hr;


        result.CheckError();


        if (
            mediaTypePointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "GetOutputAvailableType returned a null IMFMediaType."
            );
        }


        //
        // GetOutputAvailableType returns an
        // AddRef'd IMFMediaType. The wrapper
        // owns that reference.
        //
        return new IMFMediaType(
            mediaTypePointer
        );
    }
}