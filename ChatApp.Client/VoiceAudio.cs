using System.Buffers.Binary;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;

namespace ChatApp.Client;

internal static class WaveAudio
{
    public static TimeSpan Duration(byte[] bytes)
    {
        ReadOnlySpan<byte> wave = bytes;
        if (wave.Length < 12 || !wave[..4].SequenceEqual("RIFF"u8) || !wave.Slice(8, 4).SequenceEqual("WAVE"u8))
            throw new IOException("This file is not a supported PCM WAV recording.");
        uint riffSize = BinaryPrimitives.ReadUInt32LittleEndian(wave.Slice(4, 4));
        if (riffSize != wave.Length - 8) throw new IOException("The recording is incomplete.");
        uint rate = 0, dataSize = 0;
        bool formatFound = false, dataFound = false;
        ushort blockAlignment = 0;
        for (int offset = 12; offset + 8 <= wave.Length;)
        {
            ReadOnlySpan<byte> id = wave.Slice(offset, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(wave.Slice(offset + 4, 4));
            offset += 8;
            if (size > wave.Length - offset) throw new IOException("The recording is incomplete.");
            if (id.SequenceEqual("fmt "u8))
            {
                if (size < 16) throw new IOException("The recording has an invalid audio format.");
                ReadOnlySpan<byte> format = wave.Slice(offset, 16);
                ushort encoding = BinaryPrimitives.ReadUInt16LittleEndian(format);
                ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..]);
                uint samples = BinaryPrimitives.ReadUInt32LittleEndian(format[4..]);
                rate = BinaryPrimitives.ReadUInt32LittleEndian(format[8..]);
                ushort alignment = BinaryPrimitives.ReadUInt16LittleEndian(format[12..]);
                ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(format[14..]);
                if (encoding != 1 || channels is < 1 or > 2 || samples is < 8000 or > 96000
                    || bits is not (8 or 16) || alignment != channels * bits / 8 || rate != samples * alignment)
                    throw new IOException("Only mono or stereo PCM WAV audio is supported.");
                blockAlignment = alignment;
                formatFound = true;
            }
            else if (id.SequenceEqual("data"u8))
            {
                dataSize = size;
                dataFound = true;
            }
            offset += checked((int)size + ((int)size & 1));
        }
        if (!formatFound || !dataFound || dataSize == 0 || rate == 0 || dataSize % blockAlignment != 0)
            throw new IOException("The recording contains no playable audio.");
        return TimeSpan.FromSeconds((double)dataSize / rate);
    }
}

internal sealed class VoicePlayback : IDisposable
{
    private static VoicePlayback? active;
    private readonly System.Windows.Forms.Timer timer = new();
    private SoundPlayer? player;
    private MemoryStream? stream;
    public bool Playing => player is not null;
    public event Action? Changed;

    public VoicePlayback() => timer.Tick += (_, _) => Stop();

    public void Play(byte[] bytes)
    {
        TimeSpan duration = WaveAudio.Duration(bytes);
        active?.Stop();
        try
        {
            stream = new MemoryStream(bytes, writable: false);
            player = new SoundPlayer(stream);
            player.Load();
            player.Play();
            active = this;
            timer.Interval = (int)Math.Clamp(duration.TotalMilliseconds + 100, 100, int.MaxValue);
            timer.Start();
            Changed?.Invoke();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        timer.Stop();
        player?.Stop();
        player?.Dispose();
        player = null;
        stream?.Dispose();
        stream = null;
        if (ReferenceEquals(active, this)) active = null;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        Stop();
        timer.Dispose();
    }
}
