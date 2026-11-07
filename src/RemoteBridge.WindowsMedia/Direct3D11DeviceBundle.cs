using System.Runtime.InteropServices;

using SharpGen.Runtime;

using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

using Windows.Graphics.DirectX.Direct3D11;

using WinRT;

using static Vortice.Direct3D11.D3D11;

namespace RemoteBridge.WindowsMedia;

public sealed class Direct3D11DeviceBundle :
    IDisposable
{
    private bool _disposed;

    public ID3D11Device NativeDevice
    {
        get;
    }

    public ID3D11DeviceContext NativeContext
    {
        get;
    }

    public IDirect3DDevice WinRtDevice
    {
        get;
    }

    public FeatureLevel FeatureLevel
    {
        get;
    }


    private Direct3D11DeviceBundle(
    ID3D11Device nativeDevice,
    ID3D11DeviceContext nativeContext,
    IDirect3DDevice winRtDevice,
    FeatureLevel featureLevel)
{
    NativeDevice =
        nativeDevice;

    NativeContext =
        nativeContext;

    WinRtDevice =
        winRtDevice;

    FeatureLevel =
        featureLevel;
}


    public static Direct3D11DeviceBundle Create()
    {
        FeatureLevel[] featureLevels =
        [
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        ];

        Result result =
            D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport |
                DeviceCreationFlags.VideoSupport,
                featureLevels,
                out ID3D11Device nativeDevice,
                out FeatureLevel featureLevel,
                out ID3D11DeviceContext nativeContext
            );

        result.CheckError();

        if (nativeDevice is null)
        {
            throw new InvalidOperationException(
                "D3D11CreateDevice succeeded but returned no device."
            );
        }

        try
        {
            // WGC and Media Foundation will eventually
            // access this device from different threads.
            using (
                ID3D11Multithread multithread =
                    nativeDevice
                        .QueryInterface<
                            ID3D11Multithread
                        >()
            )
            {
                multithread
                    .SetMultithreadProtected(
                        true
                    );
            }

            using IDXGIDevice dxgiDevice =
                nativeDevice
                    .QueryInterface<
                        IDXGIDevice
                    >();

            int hr =
                CreateDirect3D11DeviceFromDXGIDevice(
                    dxgiDevice.NativePointer,
                    out nint graphicsDevicePointer
                );

            Marshal.ThrowExceptionForHR(
                hr
            );

            if (
                graphicsDevicePointer ==
                nint.Zero
            )
            {
                throw new InvalidOperationException(
                    "CreateDirect3D11DeviceFromDXGIDevice returned a null pointer."
                );
            }

            IDirect3DDevice winRtDevice;

            try
            {
                winRtDevice =
                    MarshalInterface<
                        IDirect3DDevice
                    >
                    .FromAbi(
                        graphicsDevicePointer
                    );
            }
            finally
            {
                Marshal.Release(
                    graphicsDevicePointer
                );
            }

            return new Direct3D11DeviceBundle(
                nativeDevice,
                nativeContext,
                winRtDevice,
                featureLevel
            );
        }
        catch
        {
            nativeContext.Dispose();
            nativeDevice.Dispose();
            throw;
        }
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        WinRtDevice.Dispose();
        NativeContext.Dispose();
        NativeDevice.Dispose();

        GC.SuppressFinalize(
            this
        );
    }


    [DllImport(
        "d3d11.dll",
        ExactSpelling = true
    )]
    private static extern int
        CreateDirect3D11DeviceFromDXGIDevice(
            nint dxgiDevice,
            out nint graphicsDevice);
}