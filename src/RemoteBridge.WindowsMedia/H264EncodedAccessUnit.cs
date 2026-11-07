namespace RemoteBridge.WindowsMedia;


public sealed record H264EncodedAccessUnit(
    byte[] Bytes,
    long Timestamp100Ns,
    ushort Width,
    ushort Height
);