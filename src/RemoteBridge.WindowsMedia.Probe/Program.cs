using RemoteBridge.WindowsMedia;

Console.WriteLine(
    "RemoteBridge WGC Capture Probe"
);

Console.WriteLine(
    "=============================="
);

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine(
        "This probe only runs on Windows."
    );

    return;
}

using var capture =
    new WindowsGraphicsCaptureService();


long h264EventFrames =
    0;

long h264EventBytes =
    0;

H264EncodedAccessUnit?
    firstH264AccessUnit =
        null;

object decoderProbeFramesLock =
        new();


List<H264EncodedAccessUnit>
    decoderProbeFrames =
        new();


capture.H264AccessUnitReady +=
    accessUnit =>
    {
        Interlocked.Increment(
            ref h264EventFrames
        );


        Interlocked.Add(
            ref h264EventBytes,
            accessUnit.Bytes.Length
        );


        Interlocked.CompareExchange(
            ref firstH264AccessUnit,
            accessUnit,
            null
        );


        lock (decoderProbeFramesLock)
        {
            if (
                decoderProbeFrames.Count <
                16 //was 16 originally, but we need more frames to probe the decoder output.
            )
            {
                decoderProbeFrames.Add(
                    accessUnit
                );
            }
        }
    };


capture.StatusChanged +=
    message =>
    {
        Console.WriteLine(
            $"[WGC] {message}"
        );
    };




Console.WriteLine();
Console.WriteLine(
    "Media Foundation Hardware Encoder Probe"
);

Console.WriteLine(
    "======================================="
);

try
{
    HardwareVideoEncoderProbeResult
        encoderProbe =
            HardwareVideoEncoderProbe
                .Run();

    Console.WriteLine(
        $"D3D11 feature level: " +
        $"{encoderProbe.D3D11FeatureLevel}"
    );

    Console.WriteLine(
        $"DXGI device manager: " +
        $"{(
            encoderProbe
                .DxgiDeviceManagerReady
                ? "READY"
                : "FAILED"
        )}"
    );

    Console.WriteLine(
        $"Hardware video encoders found: " +
        $"{encoderProbe.EncoderNames.Count}"
    );

    if (
        encoderProbe
            .EncoderNames
            .Count == 0
    )
    {
        Console.WriteLine(
            "WARNING: Windows reported no hardware video encoder MFTs."
        );
    }
    else
    {
        foreach (
            string encoderName
            in encoderProbe
                .EncoderNames
        )
        {
            Console.WriteLine(
                $"  - {encoderName}"
            );
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine(
        "Hardware encoder probe FAILED:"
    );

    Console.WriteLine(
        ex
    );

    Environment.ExitCode =
        1;

    return;
}

Console.WriteLine();


Console.WriteLine();
Console.WriteLine(
    "H.264 Decoder Negotiation Probe"
);

Console.WriteLine(
    "==============================="
);


try
{
    H264DecoderNegotiationProbeResult
        decoderResult =
            H264DecoderNegotiationProbe
                .Run();


    Console.WriteLine(
        $"Decoder: " +
        $"{decoderResult.DecoderName ?? "(none)"}"
    );


    Console.WriteLine(
        $"Activated: " +
        $"{decoderResult.Activated}"
    );


    Console.WriteLine(
        $"Async transform: " +
        $"{decoderResult.AsyncTransform}"
    );


    Console.WriteLine(
        $"D3D11 aware: " +
        $"{decoderResult.D3D11Aware}"
    );


    Console.WriteLine(
        $"DXGI manager attached: " +
        $"{decoderResult.D3DManagerAttached}"
    );


    Console.WriteLine(
        $"H.264 input accepted: " +
        $"{decoderResult.H264InputAccepted}"
    );


    Console.WriteLine(
        $"NV12 output accepted: " +
        $"{decoderResult.Nv12OutputAccepted}"
    );


    if (
        decoderResult.Error is not null
    )
    {
        Console.WriteLine(
            $"Error: " +
            $"{decoderResult.Error}"
        );
    }


    if (
        !decoderResult.Activated ||
        !decoderResult.D3D11Aware ||
        !decoderResult.D3DManagerAttached ||
        !decoderResult.H264InputAccepted ||
        !decoderResult.Nv12OutputAccepted
    )
    {
        Environment.ExitCode =
            1;
    }
}
catch (Exception ex)
{
    Console.WriteLine(
        "H.264 decoder probe FAILED:"
    );

    Console.WriteLine(
        ex
    );


    Environment.ExitCode =
        1;
}


Console.WriteLine();

Console.WriteLine(
    "H.264 1080p60 Media Type Negotiation Probe"
);

Console.WriteLine(
    "========================================="
);


try
{
    H264NegotiationProbeResult
        negotiation =
            H264MediaTypeNegotiationProbe
                .Run();


    if (
        negotiation
            .Attempts
            .Count == 0
    )
    {
        Console.WriteLine(
            "No H.264 hardware encoder candidates were found."
        );

        Environment.ExitCode =
            1;

        return;
    }


    foreach (
        H264NegotiationAttempt attempt
        in negotiation.Attempts
    )
    {
        Console.WriteLine(
            $"Encoder: {attempt.EncoderName}"
        );


        Console.WriteLine(
            $"  H.264 output type: " +
            $"{(
                attempt.OutputTypeAccepted
                    ? "ACCEPTED"
                    : "REJECTED"
            )}"
        );


        Console.WriteLine(
            $"  NV12 input type:  " +
            $"{(
                attempt.InputTypeAccepted
                    ? "ACCEPTED"
                    : "REJECTED"
            )}"
        );


        if (
            !string.IsNullOrWhiteSpace(
                attempt.Error
            )
        )
        {
            Console.WriteLine(
                $"  Error: {attempt.Error}"
            );
        }
    }


    Console.WriteLine();


    if (
        negotiation
            .SelectedEncoderName
        is null
    )
    {
        Console.WriteLine(
            "No hardware encoder accepted the RemoteBridge 1080p60 configuration."
        );

        Environment.ExitCode =
            1;

        return;
    }


    Console.WriteLine(
        $"Selected negotiated encoder: " +
        $"{negotiation.SelectedEncoderName}"
    );
}
catch (Exception ex)
{
    Console.WriteLine(
        "H.264 media type negotiation FAILED:"
    );

    Console.WriteLine(
        ex
    );

    Environment.ExitCode =
        1;

    return;
}


Console.WriteLine();



Console.WriteLine();

Console.WriteLine(
    "D3D11 BGRA -> NV12 GPU Conversion Probe"
);

Console.WriteLine(
    "======================================="
);


try
{
    D3D11Nv12CapabilityProbeResult
        conversionProbe =
            D3D11Nv12CapabilityProbe
                .Run();


    Console.WriteLine(
        $"D3D11 feature level: " +
        $"{conversionProbe.FeatureLevel}"
    );


    Console.WriteLine(
        $"D3D11 video device: " +
        $"{(
            conversionProbe
                .VideoDeviceAvailable
                ? "READY"
                : "UNAVAILABLE"
        )}"
    );


    Console.WriteLine(
        $"BGRA video-processor input: " +
        $"{(
            conversionProbe
                .BgraInputSupported
                ? "SUPPORTED"
                : "NOT SUPPORTED"
        )}"
    );


    Console.WriteLine(
        $"NV12 video-processor output: " +
        $"{(
            conversionProbe
                .Nv12OutputSupported
                ? "SUPPORTED"
                : "NOT SUPPORTED"
        )}"
    );


    Console.WriteLine(
        $"GPU BGRA -> NV12 path: " +
        $"{(
            conversionProbe
                .ConversionPathSupported
                ? "READY"
                : "NOT AVAILABLE"
        )}"
    );


    if (
        !conversionProbe
            .ConversionPathSupported
    )
    {
        Environment.ExitCode =
            1;

        return;
    }
}
catch (Exception ex)
{
    Console.WriteLine(
        "D3D11 conversion capability probe FAILED:"
    );

    Console.WriteLine(
        ex
    );

    Environment.ExitCode =
        1;

    return;
}


Console.WriteLine();

Console.WriteLine();

Console.WriteLine(
    "D3D11 BGRA/NV12 Resource Creation Probe"
);

Console.WriteLine(
    "======================================="
);


try
{
    D3D11Nv12ResourceProbeResult result =
        D3D11Nv12ResourceProbe.Run();


    Console.WriteLine(
        $"Video device: " +
        $"{(
            result.VideoDeviceReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"Video context: " +
        $"{(
            result.VideoContextReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"Video processor: " +
        $"{(
            result.ProcessorReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"BGRA texture: " +
        $"{(
            result.BgraTextureReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"NV12 texture: " +
        $"{(
            result.Nv12TextureReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"BGRA input view: " +
        $"{(
            result.InputViewReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"NV12 output view: " +
        $"{(
            result.OutputViewReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"Conversion resources: " +
        $"{result.Width}x{result.Height}"
    );
}
catch (Exception ex)
{
    Console.WriteLine(
        "D3D11 NV12 resource probe FAILED:"
    );

    Console.WriteLine(
        ex
    );

    Environment.ExitCode =
        1;

    return;
}


Console.WriteLine();

Console.WriteLine();

Console.WriteLine(
    "D3D11 Real BGRA -> NV12 Blit Probe"
);

Console.WriteLine(
    "=================================="
);


try
{
    D3D11Nv12BltProbeResult result =
        D3D11Nv12BltProbe.Run();


    Console.WriteLine(
        $"Processor: " +
        $"{(
            result.ProcessorReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"BGRA texture: " +
        $"{(
            result.InputTextureReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"NV12 texture: " +
        $"{(
            result.OutputTextureReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"BGRA input view: " +
        $"{(
            result.InputViewReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"NV12 output view: " +
        $"{(
            result.OutputViewReady
                ? "READY"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"VideoProcessorBlt: " +
        $"{(
            result.BlitSucceeded
                ? "SUCCESS"
                : "FAILED"
        )}"
    );


    Console.WriteLine(
        $"GPU conversion size: " +
        $"{result.Width}x{result.Height}"
    );
}
catch (Exception ex)
{
    Console.WriteLine(
        "D3D11 BGRA -> NV12 blit probe FAILED:"
    );

    Console.WriteLine(
        ex
    );

    Environment.ExitCode =
        1;

    return;
}


Console.WriteLine();


capture.StatisticsUpdated +=
    statistics =>
    {
        Console.WriteLine(
            $"[WGC] " +
            $"{statistics.Width}×" +
            $"{statistics.Height}" +
            $" | raw " +
            $"{statistics.RawFramesPerSecond:F1} FPS" +
            $" | gated " +
            $"{statistics.SelectedFramesPerSecond:F1} FPS" +
            $" | textures " +
            $"{statistics.TextureFramesPerSecond:F1} FPS" +
            $" | NV12 " +
            $"{statistics.Nv12FramesPerSecond:F1} FPS" +
            $" | MF samples " +
            $"{statistics.MfSampleFramesPerSecond:F1} FPS" +
            $" | H264 in " +
            $"{statistics.H264InputFramesPerSecond:F1}" +
            $" | out " +
            $"{statistics.H264OutputFramesPerSecond:F1}" +
            $" | " +
            $"{statistics.H264MegabitsPerSecond:F1} Mbps" +
            $" | dropped " +
            $"{statistics.H264DroppedFrames}"
        );
    };


try
{
    capture.Start();

    Console.WriteLine();
    Console.WriteLine(
        "Capture will run for 15 seconds."
    );

    Console.WriteLine(
        "Move windows, scroll a page, or play motion on screen."
    );

    Console.WriteLine();

    await Task.Delay(
        TimeSpan.FromSeconds(
            15
        )
    );

    capture.Stop();


    Console.WriteLine();
    Console.WriteLine(
        "Real H.264 Decoder Input Probe"
    );

    Console.WriteLine(
        "============================"
    );


    H264EncodedAccessUnit?
        decoderAccessUnit =
            firstH264AccessUnit;


    if (decoderAccessUnit is null)
    {
        Console.WriteLine(
            "FAILED: No H.264 access unit was produced."
        );

        Environment.ExitCode =
            1;
    }
    else
    {
        Console.WriteLine(
            $"Captured H.264 access unit: " +
            $"{decoderAccessUnit.Bytes.Length:N0} bytes"
        );


        int previewLength =
            Math.Min(
                16,
                decoderAccessUnit
                    .Bytes
                    .Length
            );


        Console.WriteLine(
            $"First bytes: " +
            $"{Convert.ToHexString(
                decoderAccessUnit
                    .Bytes
                    .AsSpan(
                        0,
                        previewLength
                    )
            )}"
        );


        try
        {
            H264DecoderInputProbeResult
                inputResult =
                    H264DecoderInputProbe
                        .Run(
                            decoderAccessUnit.Bytes,
                            decoderAccessUnit
                                .Timestamp100Ns
                        );


            Console.WriteLine(
                $"Decoder: " +
                $"{inputResult.DecoderName ?? "(none)"}"
            );


            Console.WriteLine(
                $"Input bytes: " +
                $"{inputResult.InputBytes:N0}"
            );


            Console.WriteLine(
                $"Async transform: " +
                $"{inputResult.AsyncTransform}"
            );


            Console.WriteLine(
                $"D3D11 aware: " +
                $"{inputResult.D3D11Aware}"
            );


            Console.WriteLine(
                $"DXGI manager attached: " +
                $"{inputResult.D3DManagerAttached}"
            );


            Console.WriteLine(
                $"ProcessInput accepted: " +
                $"{inputResult.ProcessInputAccepted}"
            );


            if (
                inputResult.Error is not null
            )
            {
                Console.WriteLine(
                    $"Error: " +
                    $"{inputResult.Error}"
                );
            }


            if (
                !inputResult.D3D11Aware ||
                !inputResult.D3DManagerAttached ||
                !inputResult.ProcessInputAccepted
            )
            {
                Environment.ExitCode =
                    1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Real H.264 decoder input probe FAILED:"
            );

            Console.WriteLine(
                ex
            );

            Environment.ExitCode =
                1;
        }
    }

    Console.WriteLine();
    Console.WriteLine(
        "Real H.264 Decoder Output Probe"
    );

    Console.WriteLine(
        "============================="
    );


    try
    {
        H264EncodedAccessUnit[]
            outputProbeFrames;


        lock (decoderProbeFramesLock)
        {
            outputProbeFrames =
                decoderProbeFrames
                    .ToArray();
        }


        Console.WriteLine(
            $"Decoder probe input frames: " +
            $"{outputProbeFrames.Length}"
        );


        H264DecoderOutputProbeResult
            outputResult =
                H264DecoderOutputProbe
                    .Run(
                        outputProbeFrames
                    );


        Console.WriteLine(
            $"Decoder: " +
            $"{outputResult.DecoderName ?? "(none)"}"
        );


        Console.WriteLine(
            $"Inputs submitted before output: " +
            $"{outputResult.InputSamplesSubmitted}"
        );


        Console.WriteLine(
            $"Output stream flags: " +
            $"0x{outputResult.OutputStreamFlags:X8}"
        );


        Console.WriteLine(
            $"Decoder provides samples: " +
            $"{outputResult.ProvidesSamples}"
        );


        Console.WriteLine(
            $"Decoder can provide samples: " +
            $"{outputResult.CanProvideSamples}"
        );


        Console.WriteLine(
            $"ProcessOutput produced frame: " +
            $"{outputResult.OutputProduced}"
        );


        Console.WriteLine(
            $"DXGI-backed output: " +
            $"{outputResult.DxgiBacked}"
        );


        string formatName =
            outputResult.TextureFormat ==
                103
                ? "NV12"
                : $"DXGI_FORMAT({outputResult.TextureFormat})";


        Console.WriteLine(
            $"Decoded texture: " +
            $"{outputResult.TextureWidth}x" +
            $"{outputResult.TextureHeight} " +
            $"| {formatName}"
        );


        Console.WriteLine(
            $"Texture array size: " +
            $"{outputResult.TextureArraySize}"
        );


        Console.WriteLine(
            $"Texture subresource: " +
            $"{outputResult.TextureSubresourceIndex}"
        );


        Console.WriteLine(
            $"Process status: " +
            $"0x{outputResult.ProcessStatus:X8}"
        );


        Console.WriteLine(
            $"Buffer status: " +
            $"0x{outputResult.BufferStatus:X8}"
        );


        if (
            outputResult.Error is not null
        )
        {
            Console.WriteLine(
                $"Error: " +
                $"{outputResult.Error}"
            );
        }


        if (
            !outputResult.OutputProduced ||
            !outputResult.DxgiBacked ||
            outputResult.TextureFormat !=
                103
        )
        {
            Environment.ExitCode =
                1;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "Real H.264 decoder output probe FAILED:"
        );


        Console.WriteLine(
            ex
        );


        Environment.ExitCode =
            1;
    }

    Console.WriteLine();
    Console.WriteLine(
        $"H.264 access-unit events: " +
        $"{h264EventFrames:N0}"
    );

    Console.WriteLine(
        $"H.264 event bytes: " +
        $"{h264EventBytes:N0}"
    );

    Console.WriteLine();
    Console.WriteLine(
        "WGC benchmark completed."
    );
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine(
        "WGC benchmark FAILED:"
    );

    Console.WriteLine(
        ex
    );

    Environment.ExitCode =
        1;
}

Console.WriteLine();
Console.WriteLine(
    "Press Enter to exit."
);

Console.ReadLine();