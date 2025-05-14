using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Concentus;
using Concentus.Enums;
using PortAudioSharp;
using RipperCat.Views;
using Stream = PortAudioSharp.Stream;

namespace RipperCat.Models;

public sealed class AudioRecorder
{
    private const int SampleRate = 48_000;
    private const int Channels   = 2;

    // ---------- device helper ------------------------------------
    public record AudioDeviceInfo(int Index, string Name)
    { public override string ToString() => Name; }

    public IEnumerable<AudioDeviceInfo> ListInputDevices()
    {
        PortAudio.Initialize();
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (info.maxInputChannels > 0)
                yield return new AudioDeviceInfo(i, info.name);
        }
    }

    // =============================================================
    //  Single-file Opus (unchanged)
    // =============================================================
    public void CaptureToOpus(AudioDeviceInfo dev, string path, CancellationToken token)
    {
        PortAudio.Initialize();

        using var fs  = File.Create(path);
        var enc = OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_AUDIO);
        enc.Bitrate = 192_000;

        using var sink = new OpusSink(enc, fs);

        RunPortAudio(dev, sink, token);
    }

    // =============================================================
    //  Continuous recording with Song Break
    // =============================================================
    private volatile bool _break;
    public void RequestSongBreak() => _break = true;

    public void RecordContinuously(AudioDeviceInfo dev,
                                   Func<string> nextPath,
                                   AudioFormat fmt,
                                   CancellationToken token)
    {
        PortAudio.Initialize();

        var sink = CreateSink(nextPath(), fmt);

        unsafe
        {
            var pa = new StreamParameters
            {
                device       = dev.Index,
                channelCount = Channels,
                sampleFormat = SampleFormat.Int16,
                suggestedLatency = PortAudio.GetDeviceInfo(dev.Index).defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            Stream.Callback cb = (IntPtr input, IntPtr _, uint frames,
                                  ref StreamCallbackTimeInfo __, StreamCallbackFlags ___, IntPtr ____) =>
            {
                if (input == IntPtr.Zero) return StreamCallbackResult.Continue;

                if (_break)
                {
                    _break = false;
                    sink.Dispose();
                    sink = CreateSink(nextPath(), fmt);
                }

                int samples = (int)frames * Channels;
                var pcm = new short[samples];
                Marshal.Copy(input, pcm, 0, samples);
                sink.Write(pcm);

                return token.IsCancellationRequested
                         ? StreamCallbackResult.Complete
                         : StreamCallbackResult.Continue;
            };

            using var stream = new Stream(pa, null, SampleRate, 1024,
                                          StreamFlags.ClipOff, cb, IntPtr.Zero);

            stream.Start();
            token.WaitHandle.WaitOne();
            stream.Stop();
        }

        sink.Dispose();
    }

    // =============================================================
    //  Helpers
    // =============================================================
    private static void RunPortAudio(AudioDeviceInfo dev, IAudioSink sink, CancellationToken token)
    {
        var pa = new StreamParameters
        {
            device       = dev.Index,
            channelCount = Channels,
            sampleFormat = SampleFormat.Int16,
            suggestedLatency = PortAudio.GetDeviceInfo(dev.Index).defaultLowInputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero
        };

        unsafe
        {
            Stream.Callback cb = (IntPtr input, IntPtr _, uint frames,
                                  ref StreamCallbackTimeInfo __, StreamCallbackFlags ___, IntPtr ____) =>
            {
                if (input == IntPtr.Zero) return StreamCallbackResult.Continue;

                int samples = (int)frames * Channels;
                var pcm = new short[samples];
                Marshal.Copy(input, pcm, 0, samples);
                sink.Write(pcm);

                return token.IsCancellationRequested
                         ? StreamCallbackResult.Complete
                         : StreamCallbackResult.Continue;
            };

            using var stream = new Stream(pa, null, SampleRate, 1024,
                                          StreamFlags.ClipOff, cb, IntPtr.Zero);

            stream.Start();
            token.WaitHandle.WaitOne();
            stream.Stop();
        }

        sink.Dispose();
    }

    private static IAudioSink CreateSink(string path, AudioFormat fmt) =>
        fmt == AudioFormat.Mp3
          ? new Mp3Sink(File.Create(path))
          : new OpusSink(OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_AUDIO)
                        , File.Create(path));
}
