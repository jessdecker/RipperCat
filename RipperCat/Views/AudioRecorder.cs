using System;
using System.Collections.Generic;
using System.Threading;
using PortAudioSharp;

public sealed class AudioRecorder
{
    private const int SampleRate = 44100;
    private const int Channels   = 2;
    private const PaSampleFormat Format = PaSampleFormat.paInt16;

    public record AudioDeviceInfo(int Index, string Name)
    {
        public override string ToString() => $"{Index}: {Name}";
    }

    public IEnumerable<AudioDeviceInfo> ListInputDevices()
    {
        PortAudio.Pa_Initialize();
        var count = PortAudio.Pa_GetDeviceCount();
        var list = new List<AudioDeviceInfo>();
        for (int i = 0; i < count; i++)
        {
            var info = PortAudio.Pa_GetDeviceInfo(i);
            if (info.maxInputChannels > 0)
                list.Add(new AudioDeviceInfo(i, Marshal.PtrToStringAnsi(info.name)!));
        }
        return list;
    }

    public void CaptureToFile(AudioDeviceInfo device, string path, CancellationToken token)
    {
        PortAudio.Pa_Initialize();

        using var writer = new WavFileWriter(path, Channels, SampleRate);

        unsafe
        {
            var parameters = new PaStreamParameters
            {
                device                    = device.Index,
                channelCount              = Channels,
                sampleFormat              = Format,
                suggestedLatency          = PortAudio.Pa_GetDeviceInfo(device.Index).defaultLowInputLatency,
                hostApiSpecificStreamInfo = IntPtr.Zero
            };

            PaStream* stream;
            PortAudio.Pa_OpenStream(&stream, &parameters, null, SampleRate, 1024, PaStreamFlags.paClipOff, null, IntPtr.Zero).Check();
            PortAudio.Pa_StartStream(stream).Check();

            var buffer = new short[1024 * Channels];
            fixed (short* ptr = buffer)
            {
                while (!token.IsCancellationRequested)
                {
                    PortAudio.Pa_ReadStream(stream, ptr, (uint)buffer.Length / (uint)Channels).Check();
                    writer.WriteSamples(buffer, 0, buffer.Length);
                }
            }

            PortAudio.Pa_StopStream(stream);
            PortAudio.Pa_CloseStream(stream);
        }
    }
}