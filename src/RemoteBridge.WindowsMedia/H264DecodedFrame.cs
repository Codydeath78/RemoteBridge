using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


public sealed class H264DecodedFrame :
    IDisposable
{
    private IMFSample?
        _sample;


    internal H264DecodedFrame(
        IMFSample sample,
        uint textureWidth,
        uint textureHeight,
        int textureFormat,
        uint textureArraySize,
        uint textureSubresourceIndex,
        uint processStatus,
        uint bufferStatus)
    {
        _sample =
            sample;

        TextureWidth =
            textureWidth;

        TextureHeight =
            textureHeight;

        TextureFormat =
            textureFormat;

        TextureArraySize =
            textureArraySize;

        TextureSubresourceIndex =
            textureSubresourceIndex;

        ProcessStatus =
            processStatus;

        BufferStatus =
            bufferStatus;
    }


    internal IMFSample Sample =>
        _sample ??
        throw new ObjectDisposedException(
            nameof(H264DecodedFrame)
        );


    public uint TextureWidth
    {
        get;
    }


    public uint TextureHeight
    {
        get;
    }


    public int TextureFormat
    {
        get;
    }


    public uint TextureArraySize
    {
        get;
    }


    public uint TextureSubresourceIndex
    {
        get;
    }


    public uint ProcessStatus
    {
        get;
    }


    public uint BufferStatus
    {
        get;
    }


    public void Dispose()
    {
        IMFSample?
            sample =
                Interlocked.Exchange(
                    ref _sample,
                    null
                );


        sample?.Dispose();
    }
}