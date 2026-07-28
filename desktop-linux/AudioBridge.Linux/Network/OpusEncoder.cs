using Concentus;
using Concentus.Enums;
using Concentus.Structs;
using System;

namespace AudioBridge.Desktop.Network;

public sealed class OpusEncoderWrapper : IDisposable
{
    private static readonly int[] ValidFrameSamples = [120, 240, 480, 960, 1920, 2880];

    private readonly Concentus.Structs.OpusEncoder _encoder;
    private int _frameSamples;
    private int _maxOutputBytes;

    public int SampleRate { get; }
    public int Channels { get; }
    public int Bitrate { get; private set; }
    public int FrameSizeMs { get; private set; }
    public int FrameSamples => _frameSamples;
    public int MaxOutputBytes => _maxOutputBytes;

    private static int SnapToValidFrameSamples(int sampleRate, int frameSizeMs)
    {
        int requested = sampleRate * frameSizeMs / 1000;
        int best = ValidFrameSamples[0];
        int minDiff = Math.Abs(requested - best);
        for (int i = 1; i < ValidFrameSamples.Length; i++)
        {
            int diff = Math.Abs(requested - ValidFrameSamples[i]);
            if (diff < minDiff)
            {
                minDiff = diff;
                best = ValidFrameSamples[i];
            }
        }
        if (best != requested)
            Console.WriteLine($"[OpusEncoder] WARNING: frame {frameSizeMs}ms ({requested} samples) non valido per Opus, usato {best} samples ({best * 1000 / sampleRate}ms)");
        return best;
    }

    public OpusEncoderWrapper(int sampleRate, int channels, int bitrate, int frameSizeMs)
    {
        SampleRate = sampleRate;
        Channels = channels;
        Bitrate = bitrate;
        FrameSizeMs = frameSizeMs;

        _encoder = (Concentus.Structs.OpusEncoder)OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_AUDIO);
        _encoder.Bitrate = bitrate;
        _encoder.Complexity = 5;

        _frameSamples = SnapToValidFrameSamples(sampleRate, frameSizeMs);
        _maxOutputBytes = _frameSamples * channels * 2;
    }

    public int Encode(short[] pcm, int offset, int frameSize, byte[] output, int outputOffset, int maxBytes)
    {
        var pcmSpan = new ReadOnlySpan<short>(pcm, offset, frameSize * Channels);
        var outSpan = new Span<byte>(output, outputOffset, maxBytes);
        return _encoder.Encode(pcmSpan, frameSize, outSpan, maxBytes);
    }

    public void SetBitrate(int bitrate)
    {
        Bitrate = bitrate;
        _encoder.Bitrate = bitrate;
    }

    public void SetFrameSizeMs(int frameSizeMs)
    {
        FrameSizeMs = frameSizeMs;
        _frameSamples = SnapToValidFrameSamples(SampleRate, frameSizeMs);
        _maxOutputBytes = _frameSamples * Channels * 2;
        Reset();
    }

    public void Reset()
    {
        _encoder.ResetState();
    }

    public void Dispose()
    {
        _encoder.Dispose();
    }
}
