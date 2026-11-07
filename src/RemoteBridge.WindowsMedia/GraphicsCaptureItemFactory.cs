using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

using Windows.Graphics.Capture;

using WinRT;

namespace RemoteBridge.WindowsMedia;

public static partial class GraphicsCaptureItemFactory
{
    private const uint
        MonitorDefaultToPrimary =
            1;

    private static readonly Guid
        GraphicsCaptureItemGuid =
            new(
                "79C3F95B-31F7-4EC2-A464-632EF5D30760"
            );


    public static GraphicsCaptureItem
        CreateForPrimaryMonitor()
    {
        nint monitor =
            MonitorFromPoint(
                new Point
                {
                    X = 0,
                    Y = 0
                },
                MonitorDefaultToPrimary
            );

        if (
            monitor ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "Unable to locate the primary Windows monitor."
            );
        }

        IGraphicsCaptureItemInterop
            interop =
                GraphicsCaptureItem
                    .As<
                        IGraphicsCaptureItemInterop
                    >();

        nint itemPointer =
            interop
                .CreateForMonitor(
                    monitor,
                    GraphicsCaptureItemGuid
                );

        if (
            itemPointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "Windows returned a null GraphicsCaptureItem."
            );
        }

        try
        {
            return MarshalInterface<
                GraphicsCaptureItem
            >
            .FromAbi(
                itemPointer
            );
        }
        finally
        {
            Marshal.Release(
                itemPointer
            );
        }
    }


    [StructLayout(
        LayoutKind.Sequential
    )]
    private struct Point
    {
        public int X;
        public int Y;
    }


    [DllImport(
        "user32.dll"
    )]
    private static extern nint
        MonitorFromPoint(
            Point point,
            uint flags);


    [GeneratedComInterface]
    [Guid(
        "3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"
    )]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown
    )]
    internal partial interface
        IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(
            nint window,
            in Guid iid);

        nint CreateForMonitor(
            nint monitor,
            in Guid iid);
    }
}