using RemoteBridge.Desktop.Utilities;

namespace RemoteBridge.Tests;

public sealed class
    RemoteWheelMapperTests
{
    [Fact]
    public void OnePositiveNotchMapsTo120()
    {
        double result =
            RemoteWheelMapper
                .ToWindowsDelta(
                    1.0
                );

        Assert.Equal(
            120.0,
            result
        );
    }


    [Fact]
    public void OneNegativeNotchMapsToMinus120()
    {
        double result =
            RemoteWheelMapper
                .ToWindowsDelta(
                    -1.0
                );

        Assert.Equal(
            -120.0,
            result
        );
    }


    [Fact]
    public void FractionalTrackpadInputIsPreserved()
    {
        double result =
            RemoteWheelMapper
                .ToWindowsDelta(
                    0.25
                );

        Assert.Equal(
            30.0,
            result
        );
    }
}