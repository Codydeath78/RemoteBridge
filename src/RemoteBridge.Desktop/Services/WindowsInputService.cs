using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using RemoteBridge.Protocol;

namespace RemoteBridge.Desktop.Services;

public sealed class WindowsInputService
{
    private const uint InputMouse =
        0;

    private const uint MouseEventMove =
        0x0001;

    private const uint MouseEventAbsolute =
        0x8000;

    private const uint MouseEventLeftDown =
        0x0002;

    private const uint MouseEventLeftUp =
        0x0004;

    private const uint InputKeyboard =
        1;

    private const uint KeyEventExtendedKey =
        0x0001;

    private const uint KeyEventKeyUp =
        0x0002;
    
    private const uint MouseEventWheel =
        0x0800;


    public void KeyDown(
    RemoteKey key)
{
    if (!OperatingSystem.IsWindows())
    {
        throw new PlatformNotSupportedException(
            "Remote keyboard control is only supported on Windows."
        );
    }

    SendKeyboardWindows(
        key,
        isKeyUp: false
    );
}

public void MouseWheel(
    int wheelDelta)
{
    if (
        !OperatingSystem.IsWindows()
    )
    {
        throw new PlatformNotSupportedException(
            "Remote mouse control is only supported on Windows."
        );
    }

    if (wheelDelta == 0)
    {
        return;
    }

    SendMouseWheelWindows(
        wheelDelta
    );
}


public void KeyUp(
    RemoteKey key)
{
    if (!OperatingSystem.IsWindows())
    {
        throw new PlatformNotSupportedException(
            "Remote keyboard control is only supported on Windows."
        );
    }

    SendKeyboardWindows(
        key,
        isKeyUp: true
    );
}

    public void MoveMouseNormalized(
        double normalizedX,
        double normalizedY)
    {
        if (
            !OperatingSystem.IsWindows()
        )
        {
            throw new
                PlatformNotSupportedException(
                    "Remote mouse control is only supported on Windows."
                );
        }

        MoveMouseWindows(
            normalizedX,
            normalizedY
        );
    }

    public void LeftButtonDown()
{
    if (
        !OperatingSystem.IsWindows()
    )
    {
        throw new
            PlatformNotSupportedException(
                "Remote mouse control is only supported on Windows."
            );
    }

    SendMouseButtonWindows(
        MouseEventLeftDown
    );
}


public void LeftButtonUp()
{
    if (
        !OperatingSystem.IsWindows()
    )
    {
        throw new
            PlatformNotSupportedException(
                "Remote mouse control is only supported on Windows."
            );
    }

    SendMouseButtonWindows(
        MouseEventLeftUp
    );
}

[SupportedOSPlatform("windows")]
private static void
    SendMouseButtonWindows(
        uint mouseFlags)
{
    INPUT[] inputs =
    [
        new INPUT
        {
            type =
                InputMouse,

            union =
                new InputUnion
                {
                    mouse =
                        new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = 0,

                            dwFlags =
                                mouseFlags,

                            time = 0,

                            dwExtraInfo =
                                UIntPtr.Zero
                        }
                }
        }
    ];

    uint sent =
        SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<INPUT>()
        );

    if (sent != inputs.Length)
    {
        throw new
            InvalidOperationException(
                "Windows rejected the remote mouse button input."
            );
    }
}

    [SupportedOSPlatform("windows")]
private static void SendMouseWheelWindows(
    int wheelDelta)
{
    wheelDelta =
        Math.Clamp(
            wheelDelta,
            -1200,
            1200
        );

    INPUT[] inputs =
    [
        new INPUT
        {
            type =
                InputMouse,

            union =
                new InputUnion
                {
                    mouse =
                        new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,

                            mouseData =
                                unchecked(
                                    (uint)wheelDelta
                                ),

                            dwFlags =
                                MouseEventWheel,

                            time = 0,

                            dwExtraInfo =
                                UIntPtr.Zero
                        }
                }
        }
    ];

    uint sent =
        SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<INPUT>()
        );

    if (sent != inputs.Length)
    {
        throw new InvalidOperationException(
            "Windows rejected the remote mouse wheel input."
        );
    }
}


    [SupportedOSPlatform("windows")]
    private static void
        MoveMouseWindows(
            double normalizedX,
            double normalizedY)
    {
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

        int absoluteX =
            (int)Math.Round(
                normalizedX *
                65535.0
            );

        int absoluteY =
            (int)Math.Round(
                normalizedY *
                65535.0
            );

        INPUT[] inputs =
        [
            new INPUT
            {
                type =
                    InputMouse,

                union =
                    new InputUnion
                    {
                        mouse =
                            new MOUSEINPUT
                            {
                                dx =
                                    absoluteX,

                                dy =
                                    absoluteY,

                                mouseData =
                                    0,

                                dwFlags =
                                    MouseEventMove |
                                    MouseEventAbsolute,

                                time =
                                    0,

                                dwExtraInfo =
                                    UIntPtr.Zero
                            }
                    }
            }
        ];

        uint sent =
            SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf<INPUT>()
            );

        if (sent != inputs.Length)
        {
            throw new
                InvalidOperationException(
                    "Windows rejected the remote mouse input."
                );
        }
    }

[SupportedOSPlatform("windows")]
private static void SendKeyboardWindows(
    RemoteKey key,
    bool isKeyUp)
{
    if (
        key == RemoteKey.None ||
        !Enum.IsDefined(
            typeof(RemoteKey),
            key
        )
    )
    {
        throw new ArgumentOutOfRangeException(
            nameof(key)
        );
    }

    uint flags =
        isKeyUp
            ? KeyEventKeyUp
            : 0;

    if (IsExtendedKey(key))
    {
        flags |=
            KeyEventExtendedKey;
    }

    INPUT[] inputs =
    [
        new INPUT
        {
            type =
                InputKeyboard,

            union =
                new InputUnion
                {
                    keyboard =
                        new KEYBDINPUT
                        {
                            wVk =
                                (ushort)key,

                            wScan =
                                0,

                            dwFlags =
                                flags,

                            time =
                                0,

                            dwExtraInfo =
                                UIntPtr.Zero
                        }
                }
        }
    ];

    uint sent =
        SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<INPUT>()
        );

    if (sent != inputs.Length)
    {
        throw new InvalidOperationException(
            "Windows rejected the remote keyboard input."
        );
    }
}


private static bool IsExtendedKey(
    RemoteKey key)
{
    return key is
        RemoteKey.RightAlt or
        RemoteKey.RightCtrl or

        RemoteKey.Insert or
        RemoteKey.Delete or

        RemoteKey.Home or
        RemoteKey.End or

        RemoteKey.PageUp or
        RemoteKey.PageDown or

        RemoteKey.Left or
        RemoteKey.Right or
        RemoteKey.Up or
        RemoteKey.Down or

        RemoteKey.Divide;
}


    [DllImport(
        "user32.dll",
        SetLastError = true
    )]
    private static extern uint
        SendInput(
            uint cInputs,
            INPUT[] pInputs,
            int cbSize
        );


    [StructLayout(
        LayoutKind.Sequential
    )]
    private struct INPUT
    {
        public uint type;

        public InputUnion union;
    }


    [StructLayout(
        LayoutKind.Explicit
    )]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mouse;

        [FieldOffset(0)]
        public KEYBDINPUT keyboard;
    }


    [StructLayout(
        LayoutKind.Sequential
    )]
    private struct MOUSEINPUT
    {
        public int dx;

        public int dy;

        public uint mouseData;

        public uint dwFlags;

        public uint time;

        public UIntPtr dwExtraInfo;
    }

    [StructLayout(
    LayoutKind.Sequential
)]
private struct KEYBDINPUT
{
    public ushort wVk;

    public ushort wScan;

    public uint dwFlags;

    public uint time;

    public UIntPtr dwExtraInfo;
}
}