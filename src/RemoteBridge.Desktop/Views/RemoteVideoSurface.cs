#if WINDOWS

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Platform;

using RemoteBridge.WindowsMedia;


namespace RemoteBridge.Desktop.Views;


public sealed class RemoteVideoSurface :
    NativeControlHost
{
    private const uint
        WsChild =
            0x40000000;

    private const uint
        SsNotify =
            0x00000100;

    private const uint
        WmMouseActivate =
            0x0021;

    private const int
        MaNoActivate =
            3;

    private const int GwlpWndProc =
        -4;

    private const uint
        WmNcHitTest =
            0x0084;

    private const int
        HtClient =
            1;
    private const uint WmMouseMove =
        0x0200;

    private const uint WmLeftButtonDown =
        0x0201;

    private const uint WmLeftButtonUp =
        0x0202;

    private const uint WmMouseWheel =
        0x020A;


    private NativeWindowProc?
        _nativeWindowProc;

    private IntPtr
        _originalWindowProc;


    private int
        _videoWidth =
            1920;

    private int
        _videoHeight =
            1080;


    public event Action<double, double>?
        PointerMovedNormalized;

    public event Action?
        LeftButtonPressed;

    public event Action?
        LeftButtonReleased;

    public event Action<int>?
        MouseWheel;

    private const uint
        WsVisible =
            0x10000000;

    private const uint
        WsClipSiblings =
            0x04000000;

    private const uint
        WsClipChildren =
            0x02000000;

    private const uint
        SsBlackRect =
            0x00000004;


    private ViewerMediaPipeline?
        _pipeline;


    public event Action<Exception>?
        PipelineFaulted;


    public bool IsReady =>
        Volatile.Read(
            ref _pipeline
        ) is not null;


    public string?
        DecoderName =>
            Volatile.Read(
                ref _pipeline
            )
            ?.DecoderName;


    public long AccessUnitsReceived =>
        Volatile.Read(
            ref _pipeline
        )
        ?.AccessUnitsReceived
        ?? 0;


    public long FramesDecoded =>
        Volatile.Read(
            ref _pipeline
        )
        ?.FramesDecoded
        ?? 0;


    public long FramesPresented =>
        Volatile.Read(
            ref _pipeline
        )
        ?.FramesPresented
        ?? 0;


    public ValueTask SubmitH264Async(
        ReadOnlyMemory<byte>
            h264AccessUnit,
        long timestamp100Ns,
        CancellationToken
            cancellationToken =
                default)
    {
        ViewerMediaPipeline
            pipeline =
                Volatile.Read(
                    ref _pipeline
                ) ??
                throw new
                    InvalidOperationException(
                        "The remote video surface has not created its native HWND yet."
                    );


        return pipeline
            .SubmitAsync(
                h264AccessUnit,
                timestamp100Ns,
                cancellationToken
            );
    }


    public ValueTask SubmitOwnedH264Async(
        byte[] h264AccessUnit,
        long timestamp100Ns,
        CancellationToken
            cancellationToken =
                default)
    {
        ViewerMediaPipeline
            pipeline =
                Volatile.Read(
                    ref _pipeline
                ) ??
                throw new
                    InvalidOperationException(
                        "The remote video surface has not created its native HWND yet."
                    );


        return pipeline
            .SubmitOwnedAsync(
                h264AccessUnit,
                timestamp100Ns,
                cancellationToken
            );
    }

    public void SetVideoSize(
        int width,
        int height)
    {
        if (
            width <= 0 ||
            height <= 0
        )
        {
            return;
        }


        Volatile.Write(
            ref _videoWidth,
            width
        );


        Volatile.Write(
            ref _videoHeight,
            height
        );
    }



    private void InstallInputWindowProcedure(
    IntPtr hwnd)
{
    _nativeWindowProc =
        NativeChildWindowProc;


    IntPtr newProc =
        Marshal.GetFunctionPointerForDelegate(
            _nativeWindowProc
        );


    _originalWindowProc =
        SetWindowLongPtrCompat(
            hwnd,
            GwlpWndProc,
            newProc
        );


    if (
        _originalWindowProc ==
        IntPtr.Zero
    )
    {
        int error =
            Marshal.GetLastWin32Error();


        if (error != 0)
        {
            throw new Win32Exception(
                error,
                "Unable to install the RemoteBridge video input window procedure."
            );
        }
    }
}


private void RemoveInputWindowProcedure(
    IntPtr hwnd)
{
    if (
        hwnd != IntPtr.Zero &&
        _originalWindowProc !=
            IntPtr.Zero
    )
    {
        SetWindowLongPtrCompat(
            hwnd,
            GwlpWndProc,
            _originalWindowProc
        );
    }


    _originalWindowProc =
        IntPtr.Zero;


    _nativeWindowProc =
        null;
}


private IntPtr NativeChildWindowProc(
    IntPtr hwnd,
    uint message,
    IntPtr wParam,
    IntPtr lParam)
{
    //
    // A Win32 STATIC control can otherwise behave
    // transparently during mouse hit testing.
    //
    if (
        message ==
        WmNcHitTest
    )
    {
        return new IntPtr(
            HtClient
        );
    }

    if (
        message ==
        WmMouseActivate
    )
    {
        //
        // Mouse interaction belongs to this native
        // video HWND, but keyboard focus belongs to
        // the Avalonia RemoteInputSurface.
        //
        return new IntPtr(
            MaNoActivate
        );
    }




    switch (message)
    {
        case WmMouseMove:
        {
            PublishMousePosition(
                hwnd,
                lParam
            );

            break;
        }


        case WmLeftButtonDown:
{
    //
    // IMPORTANT:
    //
    // Do NOT call SetFocus(hwnd) here.
    //
    // The native GPU HWND handles mouse input,
    // but keyboard focus belongs to the Avalonia
    // RemoteInputSurface.
    //
    SetCapture(
        hwnd
    );


    if (
        PublishMousePosition(
            hwnd,
            lParam
        )
    )
    {
        try
        {
            LeftButtonPressed
                ?.Invoke();
        }
        catch
        {
        }
    }


    return IntPtr.Zero;
}


        case WmLeftButtonUp:
        {
            PublishMousePosition(
                hwnd,
                lParam
            );


            //
            // Always release the remote button so
            // the host cannot get stuck in a drag.
            //
            try
            {
                LeftButtonReleased
                    ?.Invoke();
            }
            catch
            {
            }


            ReleaseCapture();


            return IntPtr.Zero;
        }


        case WmMouseWheel:
        {
            int delta =
                unchecked(
                    (short)(
                        (
                            wParam.ToInt64() >>
                            16
                        ) &
                        0xFFFF
                    )
                );


            if (delta != 0)
            {
                try
                {
                    MouseWheel
                        ?.Invoke(
                            delta
                        );
                }
                catch
                {
                }
            }


            return IntPtr.Zero;
        }
    }


    if (
        _originalWindowProc !=
        IntPtr.Zero
    )
    {
        return CallWindowProc(
            _originalWindowProc,
            hwnd,
            message,
            wParam,
            lParam
        );
    }


    return DefWindowProc(
        hwnd,
        message,
        wParam,
        lParam
    );
}


private bool PublishMousePosition(
    IntPtr hwnd,
    IntPtr lParam)
{
    int x =
        unchecked(
            (short)(
                lParam.ToInt64() &
                0xFFFF
            )
        );


    int y =
        unchecked(
            (short)(
                (
                    lParam.ToInt64() >>
                    16
                ) &
                0xFFFF
            )
        );


    if (
        !TryNormalizeVideoPoint(
            hwnd,
            x,
            y,
            out double normalizedX,
            out double normalizedY
        )
    )
    {
        return false;
    }


    try
    {
        PointerMovedNormalized
            ?.Invoke(
                normalizedX,
                normalizedY
            );
    }
    catch
    {
    }


    return true;
}


private bool TryNormalizeVideoPoint(
    IntPtr hwnd,
    int mouseX,
    int mouseY,
    out double normalizedX,
    out double normalizedY)
{
    normalizedX =
        0;

    normalizedY =
        0;


    if (
        !GetClientRect(
            hwnd,
            out NativeRect rect
        )
    )
    {
        return false;
    }


    double clientWidth =
        rect.Right -
        rect.Left;


    double clientHeight =
        rect.Bottom -
        rect.Top;


    if (
        clientWidth <= 0 ||
        clientHeight <= 0
    )
    {
        return false;
    }


    int videoWidth =
        Volatile.Read(
            ref _videoWidth
        );


    int videoHeight =
        Volatile.Read(
            ref _videoHeight
        );


    if (
        videoWidth <= 0 ||
        videoHeight <= 0
    )
    {
        return false;
    }


    double videoAspect =
        (double)videoWidth /
        videoHeight;


    double clientAspect =
        clientWidth /
        clientHeight;


    double displayedWidth;
    double displayedHeight;
    double offsetX;
    double offsetY;


    if (
        clientAspect >
        videoAspect
    )
    {
        displayedHeight =
            clientHeight;


        displayedWidth =
            displayedHeight *
            videoAspect;


        offsetX =
            (
                clientWidth -
                displayedWidth
            ) /
            2.0;


        offsetY =
            0;
    }
    else
    {
        displayedWidth =
            clientWidth;


        displayedHeight =
            displayedWidth /
            videoAspect;


        offsetX =
            0;


        offsetY =
            (
                clientHeight -
                displayedHeight
            ) /
            2.0;
    }


    double videoX =
        mouseX -
        offsetX;


    double videoY =
        mouseY -
        offsetY;


    //
    // Ignore clicks/movement inside the
    // black letterbox area.
    //
    if (
        videoX < 0 ||
        videoY < 0 ||
        videoX >
            displayedWidth ||
        videoY >
            displayedHeight
    )
    {
        return false;
    }


    normalizedX =
        Math.Clamp(
            videoX /
            displayedWidth,
            0.0,
            1.0
        );


    normalizedY =
        Math.Clamp(
            videoY /
            displayedHeight,
            0.0,
            1.0
        );


    return true;
}





    protected override IPlatformHandle
        CreateNativeControlCore(
            IPlatformHandle parent)
    {
        if (
            !string.Equals(
                parent.HandleDescriptor,
                "HWND",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new
                InvalidOperationException(
                    "RemoteVideoSurface requires an HWND parent."
                );
        }


        IntPtr moduleHandle =
            GetModuleHandleW(
                null
            );


        if (
            moduleHandle ==
            IntPtr.Zero
        )
        {
            throw new
                Win32Exception(
                    Marshal
                        .GetLastWin32Error()
                );
        }


        uint style =
            WsChild |
            WsVisible |
            WsClipSiblings |
            WsClipChildren |
            SsBlackRect |
            SsNotify;


        IntPtr childWindow =
            CreateWindowExW(
                0,
                "STATIC",
                null,
                style,
                0,
                0,
                1,
                1,
                parent.Handle,
                IntPtr.Zero,
                moduleHandle,
                IntPtr.Zero
            );

        if (
            childWindow ==
            IntPtr.Zero
        )
        {
            throw new
                Win32Exception(
                    Marshal
                        .GetLastWin32Error()
                );
        }


        InstallInputWindowProcedure(
            childWindow
        );


        try
        {
            ViewerMediaPipeline
                pipeline =
                    new(
                        childWindow
                    );


            pipeline.Faulted +=
                OnPipelineFaulted;


            _pipeline =
                pipeline;


            return new PlatformHandle(
                childWindow,
                "HWND"
            );
        }
        catch
        {
            RemoveInputWindowProcedure(
                childWindow
            );


            DestroyWindow(
                childWindow
            );


            throw;
        }
    }


    protected override void
        DestroyNativeControlCore(
            IPlatformHandle control)
    {
        ViewerMediaPipeline?
            pipeline =
                Interlocked.Exchange(
                    ref _pipeline,
                    null
                );


        if (pipeline is not null)
        {
            pipeline.Faulted -=
                OnPipelineFaulted;


            pipeline.Dispose();
        }


        if (
            control.Handle !=
            IntPtr.Zero
        )
        {
            RemoveInputWindowProcedure(
                control.Handle
            );
            
            DestroyWindow(
                control.Handle
            );
        }
    }


    private void OnPipelineFaulted(
        Exception exception)
    {
        try
        {
            PipelineFaulted?.Invoke(
                exception
            );
        }
        catch
        {
            //
            // Diagnostics subscribers must never
            // break the media worker thread.
            //
        }
    }

    [UnmanagedFunctionPointer(
    CallingConvention.Winapi
)]
private delegate IntPtr
    NativeWindowProc(
        IntPtr hwnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam
    );


[StructLayout(
    LayoutKind.Sequential
)]
private struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}


private static IntPtr SetWindowLongPtrCompat(
    IntPtr hwnd,
    int index,
    IntPtr newValue)
{
    if (
        IntPtr.Size ==
        8
    )
    {
        return SetWindowLongPtr64(
            hwnd,
            index,
            newValue
        );
    }


    return new IntPtr(
        SetWindowLong32(
            hwnd,
            index,
            newValue.ToInt32()
        )
    );
}


[DllImport(
    "user32.dll",
    EntryPoint = "SetWindowLongPtrW",
    SetLastError = true
)]
private static extern IntPtr
    SetWindowLongPtr64(
        IntPtr hwnd,
        int index,
        IntPtr newValue
    );


[DllImport(
    "user32.dll",
    EntryPoint = "SetWindowLongW",
    SetLastError = true
)]
private static extern int
    SetWindowLong32(
        IntPtr hwnd,
        int index,
        int newValue
    );


[DllImport(
    "user32.dll"
)]
private static extern IntPtr
    CallWindowProc(
        IntPtr previousWindowProc,
        IntPtr hwnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam
    );


[DllImport(
    "user32.dll"
)]
private static extern IntPtr
    DefWindowProc(
        IntPtr hwnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam
    );


[DllImport(
    "user32.dll"
)]
private static extern IntPtr
    SetFocus(
        IntPtr hwnd
    );


[DllImport(
    "user32.dll"
)]
private static extern IntPtr
    SetCapture(
        IntPtr hwnd
    );


[DllImport(
    "user32.dll"
)]
[return: MarshalAs(
    UnmanagedType.Bool
)]
private static extern bool
    ReleaseCapture();


[DllImport(
    "user32.dll"
)]
[return: MarshalAs(
    UnmanagedType.Bool
)]
private static extern bool
    GetClientRect(
        IntPtr hwnd,
        out NativeRect rect
    );







    [DllImport(
        "user32.dll",
        EntryPoint =
            "CreateWindowExW",
        CharSet =
            CharSet.Unicode,
        SetLastError =
            true
    )]
    private static extern IntPtr
        CreateWindowExW(
            uint dwExStyle,
            string lpClassName,
            string? lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int width,
            int height,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam
        );


    [DllImport(
        "user32.dll",
        SetLastError =
            true
    )]
    [return:
        MarshalAs(
            UnmanagedType.Bool
        )]
    private static extern bool
        DestroyWindow(
            IntPtr hWnd
        );


    [DllImport(
        "kernel32.dll",
        EntryPoint =
            "GetModuleHandleW",
        CharSet =
            CharSet.Unicode,
        SetLastError =
            true
    )]
    private static extern IntPtr
        GetModuleHandleW(
            string? lpModuleName
        );
}


#else


using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;


namespace RemoteBridge.Desktop.Views;


public sealed class RemoteVideoSurface :
    Control
{
    public bool IsReady =>
        false;


    public string?
        DecoderName =>
            null;


    public long AccessUnitsReceived =>
        0;


    public long FramesDecoded =>
        0;


    public long FramesPresented =>
        0;


    public ValueTask SubmitH264Async(
        ReadOnlyMemory<byte>
            h264AccessUnit,
        long timestamp100Ns,
        CancellationToken
            cancellationToken =
                default)
    {
        throw new
            PlatformNotSupportedException(
                "RemoteBridge GPU video presentation is currently implemented only for Windows."
            );
    }


    public ValueTask SubmitOwnedH264Async(
        byte[] h264AccessUnit,
        long timestamp100Ns,
        CancellationToken
            cancellationToken =
                default)
    {
        throw new
            PlatformNotSupportedException(
                "RemoteBridge GPU video presentation is currently implemented only for Windows."
            );
    }
}


#endif