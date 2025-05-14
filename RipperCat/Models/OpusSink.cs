using Concentus;
using Concentus.Oggfile;

namespace RipperCat.Models;

internal sealed class OpusSink(IOpusEncoder enc, System.IO.Stream fs) : IAudioSink
{
    private readonly OpusOggWriteStream _ogg = new(enc, fs, null, 48_000, 5, false);

    public void Write(short[] pcm) => _ogg.WriteSamples(pcm, 0, pcm.Length);
    public void Dispose()          { _ogg.Finish(); /* flush */ }
}