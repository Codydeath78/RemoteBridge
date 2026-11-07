using RemoteBridge.Desktop.Utilities;

namespace RemoteBridge.Tests;

public sealed class
    RemotePointerMapperTests
{
    [Fact]
    public void CenterMapsToCenter()
    {
        bool success =
            RemotePointerMapper
                .TryNormalizeUniform(
                    controlWidth: 400,
                    controlHeight: 225,
                    imageWidth: 1920,
                    imageHeight: 1080,
                    pointerX: 200,
                    pointerY: 112.5,
                    out double x,
                    out double y
                );

        Assert.True(
            success
        );

        Assert.Equal(
            0.5,
            x,
            precision: 5
        );

        Assert.Equal(
            0.5,
            y,
            precision: 5
        );
    }


    [Fact]
    public void LetterboxCenterStillMapsToCenter()
    {
        bool success =
            RemotePointerMapper
                .TryNormalizeUniform(
                    controlWidth: 400,
                    controlHeight: 400,
                    imageWidth: 1920,
                    imageHeight: 1080,
                    pointerX: 200,
                    pointerY: 200,
                    out double x,
                    out double y
                );

        Assert.True(
            success
        );

        Assert.Equal(
            0.5,
            x,
            precision: 5
        );

        Assert.Equal(
            0.5,
            y,
            precision: 5
        );
    }


    [Fact]
    public void PointerInsideLetterboxIsRejected()
    {
        bool success =
            RemotePointerMapper
                .TryNormalizeUniform(
                    controlWidth: 400,
                    controlHeight: 400,
                    imageWidth: 1920,
                    imageHeight: 1080,
                    pointerX: 200,
                    pointerY: 20,
                    out _,
                    out _
                );

        Assert.False(
            success
        );
    }


    [Fact]
    public void TopLeftImageCornerMapsToZero()
    {
        // 16:9 image inside a 400×400
        // control renders 400×225.
        // Vertical offset = 87.5.
        bool success =
            RemotePointerMapper
                .TryNormalizeUniform(
                    controlWidth: 400,
                    controlHeight: 400,
                    imageWidth: 1920,
                    imageHeight: 1080,
                    pointerX: 0,
                    pointerY: 87.5,
                    out double x,
                    out double y
                );

        Assert.True(
            success
        );

        Assert.Equal(
            0.0,
            x,
            precision: 5
        );

        Assert.Equal(
            0.0,
            y,
            precision: 5
        );
    }
}