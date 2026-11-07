using Avalonia.Input;
using RemoteBridge.Protocol;

namespace RemoteBridge.Desktop.Utilities;

public static class RemoteKeyMapper
{
    public static bool TryMap(
        Key key,
        out RemoteKey remoteKey)
    {
        remoteKey =
            key switch
            {
                Key.A => RemoteKey.A,
                Key.B => RemoteKey.B,
                Key.C => RemoteKey.C,
                Key.D => RemoteKey.D,
                Key.E => RemoteKey.E,
                Key.F => RemoteKey.F,
                Key.G => RemoteKey.G,
                Key.H => RemoteKey.H,
                Key.I => RemoteKey.I,
                Key.J => RemoteKey.J,
                Key.K => RemoteKey.K,
                Key.L => RemoteKey.L,
                Key.M => RemoteKey.M,
                Key.N => RemoteKey.N,
                Key.O => RemoteKey.O,
                Key.P => RemoteKey.P,
                Key.Q => RemoteKey.Q,
                Key.R => RemoteKey.R,
                Key.S => RemoteKey.S,
                Key.T => RemoteKey.T,
                Key.U => RemoteKey.U,
                Key.V => RemoteKey.V,
                Key.W => RemoteKey.W,
                Key.X => RemoteKey.X,
                Key.Y => RemoteKey.Y,
                Key.Z => RemoteKey.Z,

                Key.D0 => RemoteKey.D0,
                Key.D1 => RemoteKey.D1,
                Key.D2 => RemoteKey.D2,
                Key.D3 => RemoteKey.D3,
                Key.D4 => RemoteKey.D4,
                Key.D5 => RemoteKey.D5,
                Key.D6 => RemoteKey.D6,
                Key.D7 => RemoteKey.D7,
                Key.D8 => RemoteKey.D8,
                Key.D9 => RemoteKey.D9,

                Key.NumPad0 => RemoteKey.NumPad0,
                Key.NumPad1 => RemoteKey.NumPad1,
                Key.NumPad2 => RemoteKey.NumPad2,
                Key.NumPad3 => RemoteKey.NumPad3,
                Key.NumPad4 => RemoteKey.NumPad4,
                Key.NumPad5 => RemoteKey.NumPad5,
                Key.NumPad6 => RemoteKey.NumPad6,
                Key.NumPad7 => RemoteKey.NumPad7,
                Key.NumPad8 => RemoteKey.NumPad8,
                Key.NumPad9 => RemoteKey.NumPad9,

                Key.Space => RemoteKey.Space,
                Key.Return => RemoteKey.Return,
                Key.Back => RemoteKey.Backspace,
                Key.Tab => RemoteKey.Tab,
                Key.Escape => RemoteKey.Escape,

                Key.Delete => RemoteKey.Delete,
                Key.Insert => RemoteKey.Insert,
                Key.Home => RemoteKey.Home,
                Key.End => RemoteKey.End,
                Key.PageUp => RemoteKey.PageUp,
                Key.PageDown => RemoteKey.PageDown,

                Key.Left => RemoteKey.Left,
                Key.Right => RemoteKey.Right,
                Key.Up => RemoteKey.Up,
                Key.Down => RemoteKey.Down,

                Key.LeftShift =>
                    RemoteKey.LeftShift,

                Key.RightShift =>
                    RemoteKey.RightShift,

                Key.LeftCtrl =>
                    RemoteKey.LeftCtrl,

                Key.RightCtrl =>
                    RemoteKey.RightCtrl,

                Key.LeftAlt =>
                    RemoteKey.LeftAlt,

                Key.RightAlt =>
                    RemoteKey.RightAlt,

                Key.LWin => RemoteKey.LWin,
                Key.RWin => RemoteKey.RWin,

                Key.F1 => RemoteKey.F1,
                Key.F2 => RemoteKey.F2,
                Key.F3 => RemoteKey.F3,
                Key.F4 => RemoteKey.F4,
                Key.F5 => RemoteKey.F5,
                Key.F6 => RemoteKey.F6,
                Key.F7 => RemoteKey.F7,
                Key.F8 => RemoteKey.F8,
                Key.F9 => RemoteKey.F9,
                Key.F10 => RemoteKey.F10,
                Key.F11 => RemoteKey.F11,
                Key.F12 => RemoteKey.F12,

                Key.CapsLock =>
                    RemoteKey.CapsLock,

                Key.Add =>
                    RemoteKey.Add,

                Key.Subtract =>
                    RemoteKey.Subtract,

                Key.Multiply =>
                    RemoteKey.Multiply,

                Key.Divide =>
                    RemoteKey.Divide,

                Key.Decimal =>
                    RemoteKey.Decimal,

                Key.Separator =>
                    RemoteKey.Separator,

                Key.OemSemicolon =>
                    RemoteKey.OemSemicolon,

                Key.OemPlus =>
                    RemoteKey.OemPlus,

                Key.OemComma =>
                    RemoteKey.OemComma,

                Key.OemMinus =>
                    RemoteKey.OemMinus,

                Key.OemPeriod =>
                    RemoteKey.OemPeriod,

                Key.OemQuestion =>
                    RemoteKey.OemQuestion,

                Key.OemTilde =>
                    RemoteKey.OemTilde,

                Key.OemOpenBrackets =>
                    RemoteKey.OemOpenBrackets,

                Key.OemPipe =>
                    RemoteKey.OemPipe,

                Key.OemCloseBrackets =>
                    RemoteKey.OemCloseBrackets,

                Key.OemQuotes =>
                    RemoteKey.OemQuotes,

                _ =>
                    RemoteKey.None
            };

        return remoteKey !=
            RemoteKey.None;
    }
}