namespace RemoteBridge.WindowsMedia;


public interface IH264DecoderService :
    IAsyncDisposable
{
    event Action<DecodedVideoFrameInfo>?
        FrameDecoded;


    event Action<string>?
        StatusChanged;


    event Action<Exception>?
        DecoderFailed;


    bool IsRunning
    {
        get;
    }


    long QueuedFrames
    {
        get;
    }


    long DecodedFrames
    {
        get;
    }


    long DroppedUntilKeyFrame
    {
        get;
    }


    void Start();


    bool TryQueue(
        H264DecoderInputFrame frame
    );


    Task StopAsync();
}