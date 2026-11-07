namespace RemoteBridge.WindowsMedia;


public sealed record H264DecoderInputFrame(
    uint SequenceNumber,
    long Timestamp100Ns,
    ushort VisibleWidth,
    ushort VisibleHeight,
    bool IsKeyFrame,
    byte[] H264Bytes
);