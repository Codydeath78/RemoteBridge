using System.Diagnostics;

namespace RemoteBridge.WindowsMedia;

internal sealed class FrameRateGate
{
    private readonly long _intervalTicks;

    private long _nextFrameTimestamp;


    public FrameRateGate(
        int targetFramesPerSecond)
    {
        if (targetFramesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFramesPerSecond)
            );
        }

        _intervalTicks =
            Math.Max(
                1,
                Stopwatch.Frequency /
                targetFramesPerSecond
            );
    }


    public bool ShouldProcess(
        long timestamp)
    {
        if (_nextFrameTimestamp == 0)
        {
            _nextFrameTimestamp =
                timestamp +
                _intervalTicks;

            return true;
        }

        if (timestamp < _nextFrameTimestamp)
        {
            return false;
        }

        // Keep the schedule cumulative instead of doing
        // "timestamp + interval". This avoids accidentally
        // reducing a 144 Hz source to ~48 FPS, for example.
        do
        {
            _nextFrameTimestamp +=
                _intervalTicks;
        }
        while (
            _nextFrameTimestamp <= timestamp
        );

        return true;
    }


    public void Reset()
    {
        _nextFrameTimestamp =
            0;
    }
}