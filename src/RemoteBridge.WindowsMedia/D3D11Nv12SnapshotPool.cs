using Vortice.Direct3D11;
using Vortice.DXGI;


namespace RemoteBridge.WindowsMedia;


internal sealed class D3D11Nv12SnapshotPool :
    IDisposable
{
    private readonly Direct3D11DeviceBundle
        _deviceBundle;

    private readonly ID3D11Texture2D[]
        _textures;

    private readonly Queue<int>
        _freeSlots =
            new();

    private readonly bool[]
        _inUse;


    private bool
        _disposed;


    //
    // Preserve the old behavior for any existing
    // callers that do not provide dimensions.
    //
    public D3D11Nv12SnapshotPool(
        Direct3D11DeviceBundle deviceBundle,
        int capacity = 8)
        : this(
            deviceBundle,
            H264StreamSettings.Default.Width,
            H264StreamSettings.Default.Height,
            capacity
        )
    {
    }


    public D3D11Nv12SnapshotPool(
        Direct3D11DeviceBundle deviceBundle,
        uint width,
        uint height,
        int capacity = 8)
    {
        ArgumentNullException.ThrowIfNull(
            deviceBundle
        );


        if (
            width == 0 ||
            height == 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "NV12 snapshot dimensions must be greater than zero."
            );
        }


        //
        // NV12 dimensions should be even.
        //
        if (
            (width & 1) != 0 ||
            (height & 1) != 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "NV12 snapshot dimensions must be even."
            );
        }


        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity)
            );
        }


        _deviceBundle =
            deviceBundle;


        Width =
            width;

        Height =
            height;


        _textures =
            new ID3D11Texture2D[
                capacity
            ];


        _inUse =
            new bool[
                capacity
            ];


        Texture2DDescription description =
            new()
            {
                Width =
                    Width,

                Height =
                    Height,

                MipLevels =
                    1,

                ArraySize =
                    1,

                Format =
                    Format.NV12,

                SampleDescription =
                    new SampleDescription(
                        1,
                        0
                    ),

                Usage =
                    ResourceUsage.Default,

                BindFlags =
                    BindFlags.RenderTarget,

                CPUAccessFlags =
                    CpuAccessFlags.None,

                MiscFlags =
                    ResourceOptionFlags.None
            };


        try
        {
            for (
                int i = 0;
                i < capacity;
                i++
            )
            {
                _textures[i] =
                    _deviceBundle
                        .NativeDevice
                        .CreateTexture2D(
                            description
                        );


                _freeSlots.Enqueue(
                    i
                );
            }
        }
        catch
        {
            foreach (
                ID3D11Texture2D? texture
                in _textures
            )
            {
                texture?.Dispose();
            }


            throw;
        }
    }


    public uint Width
    {
        get;
    }


    public uint Height
    {
        get;
    }


    public int Capacity =>
        _textures.Length;


    public bool TryRentSnapshot(
        ID3D11Texture2D sourceTexture,
        out int slot,
        out ID3D11Texture2D texture)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        ArgumentNullException.ThrowIfNull(
            sourceTexture
        );


        Texture2DDescription
            sourceDescription =
                sourceTexture.Description;

        if (
            sourceDescription.Width !=
                Width ||
            sourceDescription.Height !=
                Height
        )
        {
            throw new InvalidOperationException(
                $"NV12 snapshot source is " +
                $"{sourceDescription.Width}x" +
                $"{sourceDescription.Height}, " +
                $"but this pool is configured for " +
                $"{Width}x{Height}."
            );
        }


        if (
            sourceDescription.Format !=
                Format.NV12
        )
        {
            throw new InvalidOperationException(
                $"Expected NV12 snapshot source, " +
                $"but received " +
                $"{sourceDescription.Format}."
            );
        }


        if (_freeSlots.Count == 0)
        {
            slot =
                -1;

            texture =
                null!;

            return false;
        }


        slot =
            _freeSlots.Dequeue();


        _inUse[slot] =
            true;


        texture =
            _textures[slot];


        _deviceBundle
            .NativeContext
            .CopyResource(
                texture,
                sourceTexture
            );

        _deviceBundle
            .NativeContext
            .Flush();


        return true;
    }


    public void Return(
        int slot)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this
        );


        if (
            slot < 0 ||
            slot >= _textures.Length
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot)
            );
        }


        if (!_inUse[slot])
        {
            throw new InvalidOperationException(
                $"NV12 snapshot slot {slot} " +
                $"is not currently in use."
            );
        }


        _inUse[slot] =
            false;


        _freeSlots.Enqueue(
            slot
        );
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed =
            true;


        foreach (
            ID3D11Texture2D texture
            in _textures
        )
        {
            texture.Dispose();
        }


        _freeSlots.Clear();


        GC.SuppressFinalize(
            this
        );
    }
}