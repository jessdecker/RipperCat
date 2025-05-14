using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using PortAudioSharp;
using Stream = PortAudioSharp.Stream;

namespace RipperCat.Views;

public sealed class AudioRecorder
{
    private const int SampleRate = 48_000;
    private const int Channels = 2;

    // ---------- device helper ------------------------------------
    public record AudioDeviceInfo(int Index, string Name)
    {
        public override string ToString() => Name;
    }

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
    //  1.  Single-file capture (unchanged)
    // =============================================================
    public void CaptureToOpus(AudioDeviceInfo device, string path, CancellationToken token)
    {
        PortAudio.Initialize();

        var encoder = OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_AUDIO);
        encoder.Bitrate = 192_000;

        using var fs = File.Create(path);
        var ogg = new OpusOggWriteStream(encoder, fs, null, SampleRate, 5, leaveOpen: false);

        try
        {
            var pa = new StreamParameters
            {
                device = device.Index,
                channelCount = Channels,
                sampleFormat = SampleFormat.Int16,
                suggestedLatency = PortAudio.GetDeviceInfo(device.Index).defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            unsafe
            {
                Stream.Callback cb = (nint input, nint output, uint frames,
                    ref StreamCallbackTimeInfo t, StreamCallbackFlags f, IntPtr u) =>
                {
                    if (input == IntPtr.Zero) return StreamCallbackResult.Continue;

                    int samples = (int)frames * Channels;
                    var pcm = new short[samples];
                    Marshal.Copy(input, pcm, 0, samples);
                    ogg.WriteSamples(pcm, 0, samples);

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
        }
        finally
        {
            ogg.Finish(); // flush pages & close file
        }
    }

    // =============================================================
    //  2.  Gap-free multi-file capture with Song Break
    // =============================================================
    private volatile bool _breakRequested;

    /// Call from UI when the “Song Break” button is pressed.
    public void RequestSongBreak() => _breakRequested = true;

    /// <param name="nextPath">Factory that returns the *next* filename each time it’s called.</param>
    public void RecordContinuously(AudioDeviceInfo device,
        Func<string> nextPath,
        CancellationToken token)
    {
        PortAudio.Initialize();

        var pa = new StreamParameters
        {
            device = device.Index,
            channelCount = Channels,
            sampleFormat = SampleFormat.Int16,
            suggestedLatency = PortAudio.GetDeviceInfo(device.Index).defaultLowInputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero
        };

        // current file state (mutable inside callback)
        var fs = File.Create(nextPath());
        var encoder = NewEncoder();
        var ogg = NewWriter(encoder, fs);

        unsafe
        {
            Stream.Callback cb = (nint input, nint output, uint frames,
                ref StreamCallbackTimeInfo _, StreamCallbackFlags __, IntPtr ___) =>
            {
                if (input == IntPtr.Zero) return StreamCallbackResult.Continue;

                // on song break: swap writers without stopping stream
                if (_breakRequested)
                {
                    _breakRequested = false;

                    ogg.Finish();
                    fs.Dispose();
                    encoder.Dispose();

                    fs = File.Create(nextPath());
                    encoder = NewEncoder();
                    ogg = NewWriter(encoder, fs);
                }

                int samples = (int)frames * Channels;
                var pcm = new short[samples];
                Marshal.Copy(input, pcm, 0, samples);
                ogg.WriteSamples(pcm, 0, samples);

                return token.IsCancellationRequested
                    ? StreamCallbackResult.Complete
                    : StreamCallbackResult.Continue;
            };

            using var stream = new Stream(pa, null, SampleRate, 1024,
                StreamFlags.ClipOff, cb, IntPtr.Zero);

            stream.Start();
            token.WaitHandle.WaitOne(); // block until UI calls Cancel
            stream.Stop();
        }

        // final flush + dispose
        ogg.Finish();
        fs.Dispose();
        encoder.Dispose();
    }

    // ---------- helpers ------------------------------------------
// ---------- helpers ------------------------------------------
    private static IOpusEncoder NewEncoder()
    {
        var enc = OpusCodecFactory.CreateEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_AUDIO);
        enc.Bitrate = 192_000;
        return enc;
    }

    private static OpusOggWriteStream NewWriter(IOpusEncoder enc, System.IO.Stream fs) =>
        new(enc, fs, null, SampleRate, 5, leaveOpen: false);
}
