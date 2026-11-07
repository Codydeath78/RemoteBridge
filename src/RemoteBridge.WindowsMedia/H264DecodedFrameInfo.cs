namespace RemoteBridge.WindowsMedia;


public sealed record H264DecodedFrameInfo(
    long OutputIndex,
    uint TextureWidth,
    uint TextureHeight,
    int TextureFormat,
    uint TextureArraySize,
    uint TextureSubresourceIndex,
    uint ProcessStatus,
    uint BufferStatus
);