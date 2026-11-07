using RemoteBridge.Protocol;


namespace RemoteBridge.Tests;


public sealed class H264FramePayloadTests
{
    [Fact]
    public void EncodeDecode_RoundTripsFrame()
    {
        byte[] h264 =
        [
            0x00,
            0x00,
            0x00,
            0x01,
            0x65,
            0x11,
            0x22,
            0x33
        ];


        H264FramePayload original =
            new(
                SequenceNumber:
                    1234,

                Timestamp100Ns:
                    9_876_543,

                Width:
                    1920,

                Height:
                    1080,

                IsKeyFrame:
                    true,

                H264Bytes:
                    h264
            );


        byte[] encoded =
            H264FramePayloadCodec
                .Encode(
                    original
                );


        H264FramePayload decoded =
            H264FramePayloadCodec
                .Decode(
                    encoded
                );


        Assert.Equal(
            original.SequenceNumber,
            decoded.SequenceNumber
        );

        Assert.Equal(
            original.Timestamp100Ns,
            decoded.Timestamp100Ns
        );

        Assert.Equal(
            original.Width,
            decoded.Width
        );

        Assert.Equal(
            original.Height,
            decoded.Height
        );

        Assert.True(
            decoded.IsKeyFrame
        );

        Assert.Equal(
            h264,
            decoded.H264Bytes
        );
    }


    [Fact]
    public void ContainsIdrFrame_DetectsIdr()
    {
        byte[] annexB =
        [
            0x00,
            0x00,
            0x00,
            0x01,

            0x09,
            0x10,

            0x00,
            0x00,
            0x00,
            0x01,

            0x65,
            0xAA,
            0xBB
        ];


        Assert.True(
            H264AnnexBInspector
                .ContainsIdrFrame(
                    annexB
                )
        );
    }


    [Fact]
    public void ContainsIdrFrame_ReturnsFalseForNonIdr()
    {
        byte[] annexB =
        [
            0x00,
            0x00,
            0x00,
            0x01,

            0x41,
            0xAA,
            0xBB
        ];


        Assert.False(
            H264AnnexBInspector
                .ContainsIdrFrame(
                    annexB
                )
        );
    }
}