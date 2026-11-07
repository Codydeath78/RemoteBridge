using Avalonia.Input;
using RemoteBridge.Desktop.Utilities;
using RemoteBridge.Protocol;

namespace RemoteBridge.Tests;

public sealed class RemoteKeyMapperTests
{
    [Fact]
    public void LetterMapsCorrectly()
    {
        bool success =
            RemoteKeyMapper.TryMap(
                Key.A,
                out RemoteKey key
            );

        Assert.True(success);

        Assert.Equal(
            RemoteKey.A,
            key
        );
    }


    [Fact]
    public void NumberMapsCorrectly()
    {
        bool success =
            RemoteKeyMapper.TryMap(
                Key.D5,
                out RemoteKey key
            );

        Assert.True(success);

        Assert.Equal(
            RemoteKey.D5,
            key
        );
    }


    [Fact]
    public void ModifierMapsCorrectly()
    {
        bool success =
            RemoteKeyMapper.TryMap(
                Key.LeftCtrl,
                out RemoteKey key
            );

        Assert.True(success);

        Assert.Equal(
            RemoteKey.LeftCtrl,
            key
        );
    }


    [Fact]
    public void ReturnMapsCorrectly()
    {
        bool success =
            RemoteKeyMapper.TryMap(
                Key.Return,
                out RemoteKey key
            );

        Assert.True(success);

        Assert.Equal(
            RemoteKey.Return,
            key
        );
    }


    [Fact]
    public void WindowsVirtualKeyValueIsPreserved()
    {
        Assert.Equal(
            0x41,
            (int)RemoteKey.A
        );

        Assert.Equal(
            0xA2,
            (int)RemoteKey.LeftCtrl
        );
    }
}