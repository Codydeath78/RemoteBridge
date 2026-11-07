using System.Runtime.InteropServices;

using SharpGen.Runtime;

using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


public sealed record
    MediaFoundationDecoderOutputStreamInfo(
        uint Flags,
        uint BufferSize,
        uint Alignment,
        bool ProvidesSamples,
        bool CanProvideSamples
    );


public sealed record
    MediaFoundationDecodedTextureInfo(
        uint Width,
        uint Height,
        int Format,
        uint ArraySize,
        uint SubresourceIndex
    );


internal static unsafe class
    MediaFoundationDecoderOutputInterop
{
    private const int
        GetOutputStreamInfoVtableIndex =
            7;

    private const int
        ProcessOutputVtableIndex =
            25;

    private const int
        SampleGetBufferByIndexVtableIndex =
            40;


    private const uint
        MftOutputStreamProvidesSamples =
            0x00000100;

    private const uint
        MftOutputStreamCanProvideSamples =
            0x00000200;


    private const int
        MfETransformNeedMoreInput =
            unchecked(
                (int)0xC00D6D72
            );


    private static readonly Guid
        ImfDxgiBufferGuid =
            new(
                "E7174CFA-1C9E-48B1-8866-626226BFC258"
            );


    private static readonly Guid
        Id3D11Texture2DGuid =
            new(
                "6F15AAF2-D208-4E89-9AB4-489535D34F9C"
            );


    [StructLayout(
        LayoutKind.Sequential)]
    private struct
        OutputStreamInfoNative
    {
        public uint Flags;

        public uint BufferSize;

        public uint Alignment;
    }


    [StructLayout(
        LayoutKind.Sequential)]
    private struct
        OutputDataBufferNative
    {
        public uint StreamId;

        public nint Sample;

        public uint Status;

        public nint Events;
    }


    [StructLayout(
        LayoutKind.Sequential)]
    private struct
        SampleDescriptionNative
    {
        public uint Count;

        public uint Quality;
    }


    [StructLayout(
        LayoutKind.Sequential)]
    private struct
        Texture2DDescriptionNative
    {
        public uint Width;

        public uint Height;

        public uint MipLevels;

        public uint ArraySize;

        public int Format;

        public SampleDescriptionNative
            SampleDescription;

        public int Usage;

        public uint BindFlags;

        public uint CpuAccessFlags;

        public uint MiscFlags;
    }


    public static
        MediaFoundationDecoderOutputStreamInfo
        GetOutputStreamInfo(
            IMFTransform transform)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );


        nint transformPointer =
            transform.NativePointer;


        if (
            transformPointer ==
            nint.Zero
        )
        {
            throw new
                InvalidOperationException(
                    "The decoder IMFTransform pointer is null."
                );
        }


        nint* vtable =
            *(nint**)transformPointer;


        var getOutputStreamInfo =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                OutputStreamInfoNative*,
                int>)
            vtable[
                GetOutputStreamInfoVtableIndex
            ];


        OutputStreamInfoNative info =
            default;


        int hr =
            getOutputStreamInfo(
                transformPointer,
                0,
                &info
            );


        Result result =
            hr;


        result.CheckError();


        return new
            MediaFoundationDecoderOutputStreamInfo(
                Flags:
                    info.Flags,

                BufferSize:
                    info.BufferSize,

                Alignment:
                    info.Alignment,

                ProvidesSamples:
                    (
                        info.Flags &
                        MftOutputStreamProvidesSamples
                    ) != 0,

                CanProvideSamples:
                    (
                        info.Flags &
                        MftOutputStreamCanProvideSamples
                    ) != 0
            );
    }


    public static bool
        TryProcessProvidedOutput(
            IMFTransform transform,
            out IMFSample? sample,
            out uint processStatus,
            out uint bufferStatus)
    {
        ArgumentNullException.ThrowIfNull(
            transform
        );


        sample =
            null;

        processStatus =
            0;

        bufferStatus =
            0;


        nint transformPointer =
            transform.NativePointer;


        if (
            transformPointer ==
            nint.Zero
        )
        {
            throw new
                InvalidOperationException(
                    "The decoder IMFTransform pointer is null."
                );
        }


        nint* vtable =
            *(nint**)transformPointer;


        var processOutput =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                uint,
                OutputDataBufferNative*,
                uint*,
                int>)
            vtable[
                ProcessOutputVtableIndex
            ];


        OutputDataBufferNative output =
            new()
            {
                StreamId =
                    0,

                Sample =
                    nint.Zero,

                Status =
                    0,

                Events =
                    nint.Zero
            };


        uint nativeProcessStatus =
            0;


        int hr =
            processOutput(
                transformPointer,
                0,
                1,
                &output,
                &nativeProcessStatus
            );


        processStatus =
            nativeProcessStatus;

        bufferStatus =
            output.Status;


        if (
            output.Events !=
            nint.Zero
        )
        {
            Marshal.Release(
                output.Events
            );

            output.Events =
                nint.Zero;
        }


        if (
            hr ==
            MfETransformNeedMoreInput
        )
        {
            if (
                output.Sample !=
                nint.Zero
            )
            {
                Marshal.Release(
                    output.Sample
                );
            }

            return false;
        }


        if (hr < 0)
        {
            if (
                output.Sample !=
                nint.Zero
            )
            {
                Marshal.Release(
                    output.Sample
                );
            }


            Result result =
                hr;

            result.CheckError();
        }


        if (
            output.Sample ==
            nint.Zero
        )
        {
            throw new
                InvalidOperationException(
                    "Decoder ProcessOutput succeeded but returned no IMFSample."
                );
        }


        sample =
            new IMFSample(
                output.Sample
            );


        return true;
    }


    public static
        MediaFoundationDecodedTextureInfo
        GetTextureInfo(
            IMFSample sample)
    {
        ArgumentNullException.ThrowIfNull(
            sample
        );


        nint samplePointer =
            sample.NativePointer;


        if (
            samplePointer ==
            nint.Zero
        )
        {
            throw new
                InvalidOperationException(
                    "The decoded IMFSample pointer is null."
                );
        }


        nint mediaBufferPointer =
            nint.Zero;

        nint dxgiBufferPointer =
            nint.Zero;

        nint texturePointer =
            nint.Zero;


        try
        {
            nint* sampleVtable =
                *(nint**)samplePointer;


            var getBufferByIndex =
                (delegate* unmanaged[Stdcall]<
                    nint,
                    uint,
                    nint*,
                    int>)
                sampleVtable[
                    SampleGetBufferByIndexVtableIndex
                ];


            int bufferHr =
                getBufferByIndex(
                    samplePointer,
                    0,
                    &mediaBufferPointer
                );


            Result bufferResult =
                bufferHr;

            bufferResult.CheckError();


            if (
                mediaBufferPointer ==
                nint.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "Decoded sample returned a null media buffer."
                    );
            }


            Guid dxgiBufferIid =
                ImfDxgiBufferGuid;


            int queryHr =
                Marshal.QueryInterface(
                    mediaBufferPointer,
                    in dxgiBufferIid,
                    out dxgiBufferPointer
                );


            Result queryResult =
                queryHr;

            queryResult.CheckError();


            if (
                dxgiBufferPointer ==
                nint.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "Decoded output buffer is not an IMFDXGIBuffer."
                    );
            }


            nint* dxgiVtable =
                *(nint**)dxgiBufferPointer;


            //
            // IMFDXGIBuffer:
            //
            // 0 QueryInterface
            // 1 AddRef
            // 2 Release
            // 3 GetResource
            // 4 GetSubresourceIndex
            //
            var getResource =
                (delegate* unmanaged[Stdcall]<
                    nint,
                    Guid*,
                    nint*,
                    int>)
                dxgiVtable[3];


            var getSubresourceIndex =
                (delegate* unmanaged[Stdcall]<
                    nint,
                    uint*,
                    int>)
                dxgiVtable[4];


            Guid textureIid =
                Id3D11Texture2DGuid;


            int resourceHr =
                getResource(
                    dxgiBufferPointer,
                    &textureIid,
                    &texturePointer
                );


            Result resourceResult =
                resourceHr;

            resourceResult.CheckError();


            if (
                texturePointer ==
                nint.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "IMFDXGIBuffer returned a null ID3D11Texture2D."
                    );
            }


            uint subresourceIndex =
                0;


            int subresourceHr =
                getSubresourceIndex(
                    dxgiBufferPointer,
                    &subresourceIndex
                );


            Result subresourceResult =
                subresourceHr;

            subresourceResult.CheckError();


            //
            // ID3D11Texture2D::GetDesc
            // is vtable index 10.
            //
            nint* textureVtable =
                *(nint**)texturePointer;


            var getDescription =
                (delegate* unmanaged[Stdcall]<
                    nint,
                    Texture2DDescriptionNative*,
                    void>)
                textureVtable[10];


            Texture2DDescriptionNative
                description =
                    default;


            getDescription(
                texturePointer,
                &description
            );


            return new
                MediaFoundationDecodedTextureInfo(
                    Width:
                        description.Width,

                    Height:
                        description.Height,

                    Format:
                        description.Format,

                    ArraySize:
                        description.ArraySize,

                    SubresourceIndex:
                        subresourceIndex
                );
        }
        finally
        {
            if (
                texturePointer !=
                nint.Zero
            )
            {
                Marshal.Release(
                    texturePointer
                );
            }


            if (
                dxgiBufferPointer !=
                nint.Zero
            )
            {
                Marshal.Release(
                    dxgiBufferPointer
                );
            }


            if (
                mediaBufferPointer !=
                nint.Zero
            )
            {
                Marshal.Release(
                    mediaBufferPointer
                );
            }
        }
    }
}