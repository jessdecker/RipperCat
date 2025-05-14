using System;
using System.IO;

public sealed class WavFileWriter : IDisposable
{
    private readonly FileStream _fs;
    private readonly int _channels;
    private readonly int _sampleRate;
    private long _bytesWritten;

    public WavFileWriter(string path, int channels, int sampleRate)
    {
        _fs = File.Create(path);
        _channels = channels;
        _sampleRate = sampleRate;
        WriteHeaderPlaceHolder();
    }

    private void WriteHeaderPlaceHolder()
    {
        // 44‑byte WAV header placeholder
        Span<byte> header = stackalloc byte[44];
        _fs.Write(header);
    }

    public void WriteSamples(short[] buffer, int offset, int count)
    {
        var span = new Span<byte>(new byte[count * 2]);
        Buffer.BlockCopy(buffer, offset * 2, span.ToArray(), 0, count * 2);
        _fs.Write(span);
        _bytesWritten += count * 2;
    }

    public void Dispose()
    {
        // patch WAV header
        _fs.Seek(0, SeekOrigin.Begin);
        var header = BuildHeader(_bytesWritten, _channels, _sampleRate);
        _fs.Write(header);
        _fs.Dispose();
    }

    private static byte[] BuildHeader(long dataLength, int channels, int sampleRate)
    {
        var blockAlign = (short)(channels * 2);
        var byteRate   = sampleRate * blockAlign;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.ASCII);

        bw.Write("RIFF"u8.ToArray());
        bw.Write((int)(36 + dataLength));
        bw.Write("WAVE"u8.ToArray());
        bw.Write("fmt "u8.ToArray());
        bw.Write(16); // PCM chunk size
        bw.Write((short)1); // PCM format
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write((short)16); // bits per sample
        bw.Write("data"u8.ToArray());
        bw.Write((int)dataLength);

        return ms.ToArray();
    }
}