using RemoteBridge.Protocol;

namespace RemoteBridge.Tests;

public sealed class
    SessionCodeUtilityTests
{
    [Fact]
    public void Normalize_RemovesSpaces()
    {
        string result =
            SessionCodeUtility
                .Normalize(
                    "123 456 789"
                );

        Assert.Equal(
            "123456789",
            result
        );
    }


    [Fact]
    public void Normalize_RemovesNonDigits()
    {
        string result =
            SessionCodeUtility
                .Normalize(
                    "123-456-789"
                );

        Assert.Equal(
            "123456789",
            result
        );
    }


    [Fact]
    public void ValidCode_HasNineDigits()
    {
        Assert.True(
            SessionCodeUtility
                .IsValid(
                    "123 456 789"
                )
        );
    }


    [Fact]
    public void ShortCode_IsInvalid()
    {
        Assert.False(
            SessionCodeUtility
                .IsValid(
                    "123"
                )
        );
    }


    [Fact]
    public void Format_AddsGroups()
    {
        string result =
            SessionCodeUtility
                .Format(
                    "123456789"
                );

        Assert.Equal(
            "123 456 789",
            result
        );
    }
}