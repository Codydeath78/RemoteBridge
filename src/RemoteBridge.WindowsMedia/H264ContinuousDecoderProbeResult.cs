namespace RemoteBridge.WindowsMedia;


public sealed record H264ContinuousDecoderProbeResult(
    string DecoderName,
    int InputFrames,
    int OutputFrames,
    int InitialFramesBeforeOutput,
    uint TextureWidth,
    uint TextureHeight,
    int TextureFormat,
    uint TextureArraySize,
    int StreamChanges,
    bool AllOutputsDxgiBacked,
    string? Error
);