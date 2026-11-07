namespace RemoteBridge.WindowsMedia;


public sealed record H264StreamSettings(
    uint Width,
    uint Height,
    uint FramesPerSecond,
    uint Bitrate)
{
    public static H264StreamSettings Default { get; } =
        new(
            Width: 1920,
            Height: 1080,
            FramesPerSecond: 60,
            Bitrate: 12_000_000
        );


    public void Validate()
    {
        if (
            Width == 0 ||
            Height == 0 ||
            FramesPerSecond == 0 ||
            Bitrate == 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(H264StreamSettings),
                "H.264 stream dimensions, frame rate, and bitrate must be greater than zero."
            );
        }


        if (
            (Width & 1) != 0 ||
            (Height & 1) != 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(H264StreamSettings),
                "H.264 NV12 stream dimensions must be even."
            );
        }


        if (
            Width > ushort.MaxValue ||
            Height > ushort.MaxValue
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(H264StreamSettings),
                "H.264 stream dimensions exceed the packet metadata range."
            );
        }
    }
}