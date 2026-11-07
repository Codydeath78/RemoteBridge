namespace RemoteBridge.WindowsMedia;


public sealed record DecodedVideoFrameInfo(
    uint SequenceNumber,
    long Timestamp100Ns,

    ushort VisibleWidth,
    ushort VisibleHeight,

    uint TextureWidth,
    uint TextureHeight,

    int TextureFormat,
    uint TextureArraySize,
    uint TextureSubresourceIndex
);