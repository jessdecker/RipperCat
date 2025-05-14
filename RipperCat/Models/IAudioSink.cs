using System;

namespace RipperCat.Models;

public interface IAudioSink : IDisposable
{
    void Write(short[] pcm);
}