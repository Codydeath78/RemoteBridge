using Windows.Graphics.Capture;

namespace RemoteBridge.WindowsMedia;

public static class WindowsGraphicsCaptureProbe
{
    public static bool IsSupported()
    {
        return GraphicsCaptureSession
            .IsSupported();
    }
}