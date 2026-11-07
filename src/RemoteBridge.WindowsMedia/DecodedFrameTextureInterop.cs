using System.Runtime.InteropServices;

using SharpGen.Runtime;

using Vortice.Direct3D11;
using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


internal static class DecodedFrameTextureInterop
{
    //
    // IMFSample inherits IMFAttributes.
    //
    // GetBufferByIndex is vtable slot 40.
    //
    private const int
        ImfSampleGetBufferByIndexSlot =
            40;


    //
    // IMFDXGIBuffer inherits directly from IUnknown.
    //
    // IUnknown:
    //   0 QueryInterface
    //   1 AddRef
    //   2 Release
    //
    // IMFDXGIBuffer:
    //   3 GetResource
    //   4 GetSubresourceIndex
    //
    private const int
        ImfDxgiBufferGetResourceSlot =
            3;

    private const int
        ImfDxgiBufferGetSubresourceIndexSlot =
            4;


    public static ID3D11Texture2D
        GetTexture2D(
            H264DecodedFrame frame,
            out uint subresourceIndex)
    {
        ArgumentNullException.ThrowIfNull(
            frame
        );


        IntPtr mediaBufferPointer =
            IntPtr.Zero;


        IMFMediaBuffer?
            mediaBuffer =
                null;


        IntPtr texturePointer =
            IntPtr.Zero;


        ID3D11Texture2D?
            texture =
                null;


        try
        {
            GetBufferByIndexDelegate
                getBufferByIndex =
                    GetComMethod<
                        GetBufferByIndexDelegate
                    >(
                        frame.Sample
                            .NativePointer,

                        ImfSampleGetBufferByIndexSlot
                    );


            int hr =
                getBufferByIndex(
                    frame.Sample
                        .NativePointer,

                    0,

                    out mediaBufferPointer
                );


            Marshal.ThrowExceptionForHR(
                hr
            );


            if (
                mediaBufferPointer ==
                IntPtr.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "The decoded Media Foundation sample did not contain a media buffer."
                    );
            }


            mediaBuffer =
                MarshallingHelpers
                    .FromPointer<
                        IMFMediaBuffer
                    >(
                        mediaBufferPointer
                    );


            if (mediaBuffer is null)
            {
                throw new
                    InvalidOperationException(
                        "The decoded Media Foundation buffer could not be wrapped."
                    );
            }


            //
            // Ownership of the COM reference is now held
            // by the Vortice wrapper.
            //
            mediaBufferPointer =
                IntPtr.Zero;


            using IMFDXGIBuffer
                dxgiBuffer =
                    mediaBuffer
                        .QueryInterface<
                            IMFDXGIBuffer
                        >();


            GetResourceDelegate
                getResource =
                    GetComMethod<
                        GetResourceDelegate
                    >(
                        dxgiBuffer
                            .NativePointer,

                        ImfDxgiBufferGetResourceSlot
                    );


            Guid textureInterfaceId =
                typeof(ID3D11Texture2D)
                    .GUID;


            hr =
                getResource(
                    dxgiBuffer
                        .NativePointer,

                    ref textureInterfaceId,

                    out texturePointer
                );


            Marshal.ThrowExceptionForHR(
                hr
            );


            if (
                texturePointer ==
                IntPtr.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "The decoded Media Foundation sample did not expose an ID3D11Texture2D."
                    );
            }


            texture =
                MarshallingHelpers
                    .FromPointer<
                        ID3D11Texture2D
                    >(
                        texturePointer
                    );


            if (texture is null)
            {
                throw new
                    InvalidOperationException(
                        "The decoded D3D11 texture could not be wrapped by Vortice."
                    );
            }


            //
            // The Vortice wrapper now owns this
            // COM reference.
            //
            texturePointer =
                IntPtr.Zero;


            GetSubresourceIndexDelegate
                getSubresourceIndex =
                    GetComMethod<
                        GetSubresourceIndexDelegate
                    >(
                        dxgiBuffer
                            .NativePointer,

                        ImfDxgiBufferGetSubresourceIndexSlot
                    );


            hr =
                getSubresourceIndex(
                    dxgiBuffer
                        .NativePointer,

                    out subresourceIndex
                );


            Marshal.ThrowExceptionForHR(
                hr
            );


            Texture2DDescription
                description =
                    texture.Description;


            if (
                description.Width !=
                    frame.TextureWidth ||
                description.Height !=
                    frame.TextureHeight
            )
            {
                throw new
                    InvalidOperationException(
                        "The decoded texture dimensions do not match the H264DecodedFrame metadata."
                    );
            }


            if (
                (int)description.Format !=
                frame.TextureFormat
            )
            {
                throw new
                    InvalidOperationException(
                        "The decoded texture format does not match the H264DecodedFrame metadata."
                    );
            }


            if (
                description.ArraySize !=
                frame.TextureArraySize
            )
            {
                throw new
                    InvalidOperationException(
                        "The decoded texture array size does not match the H264DecodedFrame metadata."
                    );
            }


            if (
                subresourceIndex !=
                frame.TextureSubresourceIndex
            )
            {
                throw new
                    InvalidOperationException(
                        "The decoded texture subresource index does not match the H264DecodedFrame metadata."
                    );
            }


            ID3D11Texture2D
                result =
                    texture;


            //
            // Transfer ownership to caller.
            //
            texture =
                null;


            return result;
        }
        finally
        {
            texture?.Dispose();

            mediaBuffer?.Dispose();


            //
            // These are only non-zero if wrapping
            // the returned COM pointer failed.
            //
            if (
                texturePointer !=
                IntPtr.Zero
            )
            {
                Marshal.Release(
                    texturePointer
                );
            }


            if (
                mediaBufferPointer !=
                IntPtr.Zero
            )
            {
                Marshal.Release(
                    mediaBufferPointer
                );
            }
        }
    }


    private static TDelegate
        GetComMethod<TDelegate>(
            IntPtr comObject,
            int vtableIndex)
        where TDelegate :
            Delegate
    {
        if (
            comObject ==
            IntPtr.Zero
        )
        {
            throw new
                InvalidOperationException(
                    "The COM object pointer is null."
                );
        }


        IntPtr vtable =
            Marshal.ReadIntPtr(
                comObject
            );


        if (
            vtable ==
            IntPtr.Zero
        )
        {
            throw new
                InvalidOperationException(
                    "The COM object vtable pointer is null."
                );
        }


        IntPtr methodPointer =
            Marshal.ReadIntPtr(
                vtable,
                checked(
                    vtableIndex *
                    IntPtr.Size
                )
            );


        if (
            methodPointer ==
            IntPtr.Zero
        )
        {
            throw new
                InvalidOperationException(
                    $"COM method slot {vtableIndex} is null."
                );
        }


        return
            Marshal
                .GetDelegateForFunctionPointer<
                    TDelegate
                >(
                    methodPointer
                );
    }


    [UnmanagedFunctionPointer(
        CallingConvention.Winapi
    )]
    private delegate int
        GetBufferByIndexDelegate(
            IntPtr @this,
            uint index,
            out IntPtr mediaBuffer
        );


    [UnmanagedFunctionPointer(
        CallingConvention.Winapi
    )]
    private delegate int
        GetResourceDelegate(
            IntPtr @this,
            ref Guid interfaceId,
            out IntPtr resource
        );


    [UnmanagedFunctionPointer(
        CallingConvention.Winapi
    )]
    private delegate int
        GetSubresourceIndexDelegate(
            IntPtr @this,
            out uint subresourceIndex
        );
}