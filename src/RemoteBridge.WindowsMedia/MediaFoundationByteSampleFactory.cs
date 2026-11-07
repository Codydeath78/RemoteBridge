using System.Runtime.InteropServices;

using SharpGen.Runtime;

using Vortice.MediaFoundation;


namespace RemoteBridge.WindowsMedia;


internal static unsafe class
    MediaFoundationByteSampleFactory
{
    private const int
        MediaBufferLockVtableIndex =
            3;

    private const int
        MediaBufferUnlockVtableIndex =
            4;

    private const int
        MediaBufferSetCurrentLengthVtableIndex =
            6;

    private const int
        SampleAddBufferVtableIndex =
            42;


    [DllImport(
        "mfplat.dll",
        ExactSpelling = true)]
    private static extern int
        MFCreateMemoryBuffer(
            uint maxLength,
            out nint buffer
        );


    [DllImport(
        "mfplat.dll",
        ExactSpelling = true)]
    private static extern int
        MFCreateSample(
            out nint sample
        );


    public static IMFSample Create(
        byte[] bytes,
        long sampleTime100Ns,
        long sampleDuration100Ns)
    {
        ArgumentNullException.ThrowIfNull(
            bytes
        );


        if (bytes.Length == 0)
        {
            throw new ArgumentException(
                "The compressed media sample cannot be empty.",
                nameof(bytes)
            );
        }


        nint samplePointer =
            nint.Zero;

        nint bufferPointer =
            nint.Zero;


        IMFSample? sample =
            null;


        try
        {
            Result createSampleResult =
                MFCreateSample(
                    out samplePointer
                );


            createSampleResult
                .CheckError();


            if (
                samplePointer ==
                nint.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "MFCreateSample returned a null sample."
                    );
            }


            sample =
                new IMFSample(
                    samplePointer
                );


            //
            // The managed wrapper now owns the
            // native sample reference.
            //
            samplePointer =
                nint.Zero;


            Result createBufferResult =
                MFCreateMemoryBuffer(
                    checked(
                        (uint)bytes.Length
                    ),

                    out bufferPointer
                );


            createBufferResult
                .CheckError();


            if (
                bufferPointer ==
                nint.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "MFCreateMemoryBuffer returned a null buffer."
                    );
            }


            WriteBytes(
                bufferPointer,
                bytes
            );


            SetCurrentLength(
                bufferPointer,
                checked(
                    (uint)bytes.Length
                )
            );


            AddBuffer(
                sample.NativePointer,
                bufferPointer
            );


            //
            // IMFSample::AddBuffer AddRefs the
            // IMFMediaBuffer, so release our
            // original reference.
            //
            Marshal.Release(
                bufferPointer
            );


            bufferPointer =
                nint.Zero;


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


            IMFSample result =
                sample;


            sample =
                null;


            return result;
        }
        catch
        {
            sample?.Dispose();


            if (
                bufferPointer !=
                nint.Zero
            )
            {
                Marshal.Release(
                    bufferPointer
                );
            }


            if (
                samplePointer !=
                nint.Zero
            )
            {
                Marshal.Release(
                    samplePointer
                );
            }


            throw;
        }
    }


    private static void WriteBytes(
        nint bufferPointer,
        byte[] bytes)
    {
        nint* vtable =
            *(nint**)bufferPointer;


        var lockBuffer =
            (delegate* unmanaged[Stdcall]<
                nint,
                nint*,
                uint*,
                uint*,
                int>)
            vtable[
                MediaBufferLockVtableIndex
            ];


        var unlockBuffer =
            (delegate* unmanaged[Stdcall]<
                nint,
                int>)
            vtable[
                MediaBufferUnlockVtableIndex
            ];


        nint destination =
            nint.Zero;

        uint maxLength =
            0;

        uint currentLength =
            0;


        int lockHr =
            lockBuffer(
                bufferPointer,
                &destination,
                &maxLength,
                &currentLength
            );


        Result lockResult =
            lockHr;


        lockResult.CheckError();


        try
        {
            if (
                destination ==
                nint.Zero
            )
            {
                throw new
                    InvalidOperationException(
                        "IMFMediaBuffer::Lock returned a null data pointer."
                    );
            }


            if (
                bytes.Length >
                maxLength
            )
            {
                throw new
                    InvalidOperationException(
                        "The Media Foundation buffer is smaller than the H.264 access unit."
                    );
            }


            Marshal.Copy(
                bytes,
                0,
                destination,
                bytes.Length
            );
        }
        finally
        {
            int unlockHr =
                unlockBuffer(
                    bufferPointer
                );


            Result unlockResult =
                unlockHr;


            unlockResult
                .CheckError();
        }
    }


    private static void SetCurrentLength(
        nint bufferPointer,
        uint length)
    {
        nint* vtable =
            *(nint**)bufferPointer;


        var setCurrentLength =
            (delegate* unmanaged[Stdcall]<
                nint,
                uint,
                int>)
            vtable[
                MediaBufferSetCurrentLengthVtableIndex
            ];


        int hr =
            setCurrentLength(
                bufferPointer,
                length
            );


        Result result =
            hr;


        result.CheckError();
    }


    private static void AddBuffer(
        nint samplePointer,
        nint bufferPointer)
    {
        nint* vtable =
            *(nint**)samplePointer;


        var addBuffer =
            (delegate* unmanaged[Stdcall]<
                nint,
                nint,
                int>)
            vtable[
                SampleAddBufferVtableIndex
            ];


        int hr =
            addBuffer(
                samplePointer,
                bufferPointer
            );


        Result result =
            hr;


        result.CheckError();
    }
}