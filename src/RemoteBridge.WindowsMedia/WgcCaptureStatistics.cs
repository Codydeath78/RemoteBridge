namespace RemoteBridge.WindowsMedia;


public sealed record WgcCaptureStatistics(
    int Width,
    int Height,
    double RawFramesPerSecond,
    double SelectedFramesPerSecond,
    double TextureFramesPerSecond,
    double Nv12FramesPerSecond,
    double MfSampleFramesPerSecond,
    double H264InputFramesPerSecond,
    double H264OutputFramesPerSecond,
    double H264MegabitsPerSecond,
    long H264DroppedFrames,
    long TotalRawFrames,
    long TotalSelectedFrames,
    long TotalNv12Frames,
    long TotalMfSamples,
    long TotalH264InputFrames,
    long TotalH264OutputFrames,
    long TotalH264OutputBytes,
    long TotalH264DroppedFrames,
    TimeSpan RunningTime
);