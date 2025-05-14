using System;

namespace RipperCat.Models;

internal sealed class Mp3Sink(System.IO.Stream fs) : IAudioSink
{
    private readonly NAudio.Lame.LameMP3FileWriter _mp3 = new(
        fs, new NAudio.Wave.WaveFormat(48_000, 16, 2), 192_000);

    public void Write(short[] pcm)
    {
        var bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        _mp3.Write(bytes, 0, bytes.Length);
    }
    public void Dispose() => _mp3.Dispose();
}