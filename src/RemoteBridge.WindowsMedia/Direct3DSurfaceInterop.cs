using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

using Windows.Graphics.DirectX.Direct3D11;

using Vortice.Direct3D11;

using WinRT;

namespace RemoteBridge.WindowsMedia;

internal static partial class Direct3DSurfaceInterop
{
    private static readonly Guid
        ID3D11Texture2DGuid =
            new(
                "6F15AAF2-D208-4E89-9AB4-489535D34F9C"
            );


    public static nint
        GetTexture2DPointer(
            IDirect3DSurface surface)
    {
        IDirect3DDxgiInterfaceAccess
            access =
                surface
                    .As<
                        IDirect3DDxgiInterfaceAccess
                    >();

        Guid iid =
            ID3D11Texture2DGuid;

        int hr =
            access
                .GetInterface(
                    in iid,
                    out nint texturePointer
                );

        Marshal.ThrowExceptionForHR(
            hr
        );

        if (
            texturePointer ==
            nint.Zero
        )
        {
            throw new InvalidOperationException(
                "IDirect3DDxgiInterfaceAccess returned a null ID3D11Texture2D."
            );
        }

        return texturePointer;
    }

    public static ID3D11Texture2D GetTexture2D(
    IDirect3DSurface surface)
{
    nint texturePointer =
        GetTexture2DPointer(
            surface
        );

    try
    {
        //
        // GetInterface() returned an AddRef'd
        // ID3D11Texture2D pointer.
        //
        // The Vortice wrapper now owns that
        // reference and Dispose() will release it.
        //
        return new ID3D11Texture2D(
            texturePointer
        );
    }
    catch
    {
        Marshal.Release(
            texturePointer
        );

        throw;
    }
}


    [GeneratedComInterface]
    [Guid(
        "A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"
    )]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown
    )]
    internal partial interface
        IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig]
        int GetInterface(
            in Guid iid,
            out nint result);
    }
}