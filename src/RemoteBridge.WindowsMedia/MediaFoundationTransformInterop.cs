using SharpGen.Runtime;

using Vortice.MediaFoundation;

namespace RemoteBridge.WindowsMedia;

internal static unsafe class MediaFoundationTransformInterop
{
    // IMFTransform vtable:
    //
    // IUnknown:
    // 0 QueryInterface
    // 1 AddRef
    // 2 Release
    //
    // IMFTransform:
    // 3  GetStreamLimits
    // 4  GetStreamCount
    // 5  GetStreamIDs
    // 6  GetInputStreamInfo
    // 7  GetOutputStreamInfo
    // 8  GetAttributes
    // ...
    // 23 ProcessMessage

    private const int GetAttributesVtableIndex =
        8;

    private const int ProcessMessageVtableIndex =
        23;


    private const int MftMessageSetD3DManager =
        0x00000002;


    public static IMFAttributes GetAttributes(
        IMFTransform transform)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );

        nint transformPointer =
            transform.NativePointer;

        if (transformPointer == nint.Zero)
        {
            throw new InvalidOperationException(
                "The IMFTransform native pointer is null."
            );
        }


        nint* vtable =
            *(nint**)transformPointer;


        var getAttributes =
            (delegate* unmanaged[Stdcall]<
                nint,
                nint*,
                int>)
            vtable[
                GetAttributesVtableIndex
            ];


        nint attributesPointer =
            nint.Zero;


        int hr =
            getAttributes(
                transformPointer,
                &attributesPointer
            );


        Result result =
            hr;

        result.CheckError();


        if (attributesPointer == nint.Zero)
        {
            throw new InvalidOperationException(
                "IMFTransform::GetAttributes returned a null IMFAttributes pointer."
            );
        }


        return new IMFAttributes(
            attributesPointer
        );
    }


    public static void SetD3DManager(
        IMFTransform transform,
        IMFDXGIDeviceManager deviceManager)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );

        ArgumentNullException.ThrowIfNull(
            deviceManager
        );


        nint transformPointer =
            transform.NativePointer;

        if (transformPointer == nint.Zero)
        {
            throw new InvalidOperationException(
                "The IMFTransform native pointer is null."
            );
        }


        nint managerPointer =
            deviceManager.NativePointer;

        if (managerPointer == nint.Zero)
        {
            throw new InvalidOperationException(
                "The IMFDXGIDeviceManager native pointer is null."
            );
        }


        nint* vtable =
            *(nint**)transformPointer;


        var processMessage =
            (delegate* unmanaged[Stdcall]<
                nint,
                int,
                nuint,
                int>)
            vtable[
                ProcessMessageVtableIndex
            ];


        int hr =
            processMessage(
                transformPointer,
                MftMessageSetD3DManager,
                unchecked(
                    (nuint)managerPointer
                )
            );


        Result result =
            hr;

        result.CheckError();
    }
}