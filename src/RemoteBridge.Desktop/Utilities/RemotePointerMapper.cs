using System;

namespace RemoteBridge.Desktop.Utilities;

public static class RemotePointerMapper
{
    public static bool
        TryNormalizeUniform(
            double controlWidth,
            double controlHeight,
            double imageWidth,
            double imageHeight,
            double pointerX,
            double pointerY,
            out double normalizedX,
            out double normalizedY)
    {
        normalizedX = 0;
        normalizedY = 0;

        if (
            controlWidth <= 0 ||
            controlHeight <= 0 ||
            imageWidth <= 0 ||
            imageHeight <= 0
        )
        {
            return false;
        }

        double scale =
            Math.Min(
                controlWidth /
                imageWidth,

                controlHeight /
                imageHeight
            );

        double renderedWidth =
            imageWidth *
            scale;

        double renderedHeight =
            imageHeight *
            scale;

        double offsetX =
            (
                controlWidth -
                renderedWidth
            ) /
            2.0;

        double offsetY =
            (
                controlHeight -
                renderedHeight
            ) /
            2.0;

        if (
            pointerX < offsetX ||
            pointerX >
                offsetX +
                renderedWidth ||
            pointerY < offsetY ||
            pointerY >
                offsetY +
                renderedHeight
        )
        {
            return false;
        }

        normalizedX =
            (
                pointerX -
                offsetX
            ) /
            renderedWidth;

        normalizedY =
            (
                pointerY -
                offsetY
            ) /
            renderedHeight;

        normalizedX =
            Math.Clamp(
                normalizedX,
                0.0,
                1.0
            );

        normalizedY =
            Math.Clamp(
                normalizedY,
                0.0,
                1.0
            );

        return true;
    }
}