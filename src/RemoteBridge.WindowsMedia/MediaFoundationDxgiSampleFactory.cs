using System.Runtime.InteropServices;

using SharpGen.Runtime;

using Vortice.Direct3D11;
using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


internal static class MediaFoundationDxgiSampleFactory
{
    private static readonly Guid
        ID3D11Texture2DGuid =
            new(
                "6F15AAF2-D208-4E89-9AB4-489535D34F9C"
            );


    public static IMFSample CreateNv12Sample(
        ID3D11Texture2D texture,
        long sampleTime100Ns,
        long sampleDuration100Ns)
    {
        ArgumentNullException.ThrowIfNull(
            texture
        );


        nint bufferPointer =
            nint.Zero;

        nint samplePointer =
            nint.Zero;

        IMFMediaBuffer? mediaBuffer =
            null;

        IMFSample? sample =
            null;


        try
        {
            Guid textureIid =
                ID3D11Texture2DGuid;


            Result bufferResult =
                MFCreateDXGISurfaceBufferNative(
                    ref textureIid,
                    texture.NativePointer,
                    0,
                    false,
                    out bufferPointer
                );


            bufferResult.CheckError();


            if (bufferPointer == nint.Zero)
            {
                throw new InvalidOperationException(
                    "MFCreateDXGISurfaceBuffer returned a null buffer."
                );
            }


            mediaBuffer =
                MarshallingHelpers
                    .FromPointer<
                        IMFMediaBuffer
                    >(
                        bufferPointer
                    );


            if (mediaBuffer is null)
            {
                throw new InvalidOperationException(
                    "Could not wrap the Media Foundation DXGI buffer."
                );
            }


            //
            // Ownership has moved into the
            // SharpGen/Vortice wrapper.
            //
            bufferPointer =
                nint.Zero;


            Result sampleResult =
                MFCreateSampleNative(
                    out samplePointer
                );


            sampleResult.CheckError();


            if (samplePointer == nint.Zero)
            {
                throw new InvalidOperationException(
                    "MFCreateSample returned a null sample."
                );
            }


            sample =
                MarshallingHelpers
                    .FromPointer<
                        IMFSample
                    >(
                        samplePointer
                    );


            if (sample is null)
            {
                throw new InvalidOperationException(
                    "Could not wrap the Media Foundation sample."
                );
            }


            samplePointer =
                nint.Zero;


            //
            // AddBuffer retains the media buffer.
            //
            sample.AddBuffer(
                mediaBuffer
            );


            MediaFoundationSampleInterop
                .SetSampleTime(
                    sample,
                    sampleTime100Ns
            );


            MediaFoundationSampleInterop
                .SetSampleDuration(
                    sample,
                    sampleDuration100Ns
            );


            //
            // The sample now retains the buffer.
            // Release our temporary wrapper reference.
            //
            mediaBuffer.Dispose();

            mediaBuffer =
                null;


            IMFSample result =
                sample;

            sample =
                null;

            return result;
        }
        catch
        {
            sample?.Dispose();
            mediaBuffer?.Dispose();


            if (
                samplePointer !=
                nint.Zero
            )
            {
                Marshal.Release(
                    samplePointer
                );
            }


            if (
                bufferPointer !=
                nint.Zero
            )
            {
                Marshal.Release(
                    bufferPointer
                );
            }


            throw;
        }
    }

    [DllImport(
    "mfplat.dll",
    EntryPoint = "MFCreateDXGISurfaceBuffer",
    ExactSpelling = true
)]
private static extern Result
    MFCreateDXGISurfaceBufferNative(
        ref Guid riid,
        nint surface,
        uint subresourceIndex,
        [MarshalAs(UnmanagedType.Bool)]
        bool bottomUpWhenLinear,
        out nint mediaBuffer
    );


[DllImport(
    "mfplat.dll",
    EntryPoint = "MFCreateSample",
    ExactSpelling = true
)]
private static extern Result
    MFCreateSampleNative(
        out nint sample
    );
}