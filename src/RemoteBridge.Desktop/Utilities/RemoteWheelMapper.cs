namespace RemoteBridge.Desktop.Utilities;

public static class RemoteWheelMapper
{
    public const int WheelDeltaPerNotch =
        120;

    public static double ToWindowsDelta(
        double avaloniaDeltaY)
    {
        return
            avaloniaDeltaY *
            WheelDeltaPerNotch;
    }
}