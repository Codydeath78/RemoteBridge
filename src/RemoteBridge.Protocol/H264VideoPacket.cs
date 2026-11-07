using System;
using System.Buffers.Binary;


namespace RemoteBridge.Protocol;


public sealed record H264VideoPacketData(
    byte[] Bytes,
    long Timestamp100Ns,
    ushort Width,
    ushort Height
);


public static class H264VideoPacket
{
    //
    // Header layout:
    //
    //  0..3   Magic: "RBH1"
    //  4      Version
    //  5      Packet type
    //  6..7   Reserved
    //  8..15  Timestamp100Ns
    // 16..17  Width
    // 18..19  Height
    // 20..23  H.264 payload length
    // 24..    H.264 bytes
    //
    private const int HeaderSize =
        24;


    private const byte Version =
        1;


    private const byte VideoPacketType =
        1;


    private static ReadOnlySpan<byte> Magic =>
        "RBH1"u8;


    public static byte[] Create(
        byte[] h264Bytes,
        long timestamp100Ns,
        ushort width,
        ushort height)
    {
        ArgumentNullException.ThrowIfNull(
            h264Bytes
        );


        if (h264Bytes.Length == 0)
        {
            throw new ArgumentException(
                "H.264 payload cannot be empty.",
                nameof(h264Bytes)
            );
        }


        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width)
            );
        }


        if (height == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height)
            );
        }


        int packetLength =
            checked(
                HeaderSize +
                h264Bytes.Length
            );


        byte[] packet =
            new byte[
                packetLength
            ];


        Magic.CopyTo(
            packet.AsSpan(
                0,
                4
            )
        );


        packet[4] =
            Version;

        packet[5] =
            VideoPacketType;

        packet[6] =
            0;

        packet[7] =
            0;


        BinaryPrimitives
            .WriteInt64LittleEndian(
                packet.AsSpan(
                    8,
                    8
                ),
                timestamp100Ns
            );


        BinaryPrimitives
            .WriteUInt16LittleEndian(
                packet.AsSpan(
                    16,
                    2
                ),
                width
            );


        BinaryPrimitives
            .WriteUInt16LittleEndian(
                packet.AsSpan(
                    18,
                    2
                ),
                height
            );


        BinaryPrimitives
            .WriteInt32LittleEndian(
                packet.AsSpan(
                    20,
                    4
                ),
                h264Bytes.Length
            );


        h264Bytes
            .AsSpan()
            .CopyTo(
                packet.AsSpan(
                    HeaderSize
                )
            );


        return packet;
    }


    public static bool TryParse(
        ReadOnlySpan<byte> packet,
        out H264VideoPacketData? video)
    {
        video =
            null;


        if (
            packet.Length <
            HeaderSize
        )
        {
            return false;
        }


        if (
            !packet
                .Slice(
                    0,
                    4
                )
                .SequenceEqual(
                    Magic
                )
        )
        {
            return false;
        }


        if (
            packet[4] !=
                Version ||
            packet[5] !=
                VideoPacketType
        )
        {
            return false;
        }


        long timestamp100Ns =
            BinaryPrimitives
                .ReadInt64LittleEndian(
                    packet.Slice(
                        8,
                        8
                    )
                );


        ushort width =
            BinaryPrimitives
                .ReadUInt16LittleEndian(
                    packet.Slice(
                        16,
                        2
                    )
                );


        ushort height =
            BinaryPrimitives
                .ReadUInt16LittleEndian(
                    packet.Slice(
                        18,
                        2
                    )
                );


        int payloadLength =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    packet.Slice(
                        20,
                        4
                    )
                );


        if (
            width == 0 ||
            height == 0 ||
            payloadLength <= 0
        )
        {
            return false;
        }


        if (
            payloadLength !=
            packet.Length -
            HeaderSize
        )
        {
            return false;
        }


        byte[] h264Bytes =
            packet
                .Slice(
                    HeaderSize,
                    payloadLength
                )
                .ToArray();


        video =
            new H264VideoPacketData(
                h264Bytes,
                timestamp100Ns,
                width,
                height
            );


        return true;
    }
}