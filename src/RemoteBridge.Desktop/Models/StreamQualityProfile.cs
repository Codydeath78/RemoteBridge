using System.Collections.Generic;


namespace RemoteBridge.Desktop.Models;


public sealed record StreamQualityProfile(
    string Name,
    int MaxWidth,
    int MaxHeight,
    int TargetFps,
    uint H264Bitrate)
{
    public override string ToString()
    {
        return Name;
    }
}


public static class StreamQualityProfiles
{
    public static readonly
        StreamQualityProfile Balanced =
            new(
                "Balanced — 1280×720 @ 30 FPS",
                1280,
                720,
                30,
                4_000_000
            );


    public static readonly
        StreamQualityProfile HighQuality =
            new(
                "High Quality — 1920×1080 @ 30 FPS",
                1920,
                1080,
                30,
                8_000_000
            );


    public static readonly
        StreamQualityProfile Smooth60 =
            new(
                "Smooth — 1280×720 @ 60 FPS",
                1280,
                720,
                60,
                6_000_000
            );


    public static readonly
        StreamQualityProfile HighQuality60 =
            new(
                "High Quality 60 — 1920×1080 @ 60 FPS",
                1920,
                1080,
                60,
                12_000_000
            );


    public static IReadOnlyList<
        StreamQualityProfile
    > All { get; } =
    [
        Balanced,
        HighQuality,
        Smooth60,
        HighQuality60
    ];
}