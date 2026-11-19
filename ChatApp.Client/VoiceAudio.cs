using System.Buffers.Binary;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;

namespace ChatApp.Client;

internal static class WaveAudio
{
    public static TimeSpan Duration(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return Duration(stream);
    }

    public static TimeSpan Duration(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        stream.Position = 0;
        if (stream.Length < 12 || reader.ReadUInt32() != 0x46464952)
            throw new IOException("This file is not a PCM WAV recording.");
        uint riffSize = reader.ReadUInt32();
        if (riffSize != stream.Length - 8 || reader.ReadUInt32() != 0x45564157)
            throw new IOException("The recording is incomplete.");
        uint rate = 0, dataSize = 0;
        ushort alignment = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            uint id = reader.ReadUInt32(), size = reader.ReadUInt32();
            long next = stream.Position + size + (size & 1);
            if (next > stream.Length) throw new IOException("The recording is incomplete.");
            if (id == 0x20746D66)
            {
                if (size < 16) throw new IOException("The audio format is invalid.");
                ushort encoding = reader.ReadUInt16(), channels = reader.ReadUInt16();
                uint samples = reader.ReadUInt32();
                rate = reader.ReadUInt32(); alignment = reader.ReadUInt16();
                ushort bits = reader.ReadUInt16();
                if (encoding != 1 || channels is < 1 or > 2 || samples is < 8000 or > 96000
                    || bits is not (8 or 16) || alignment != channels * bits / 8 || rate != samples * alignment)
                    throw new IOException("Only mono or stereo PCM WAV audio is supported.");
            }
            else if (id == 0x61746164) dataSize = size;
            stream.Position = next;
        }
        if (rate == 0 || alignment == 0 || dataSize == 0 || dataSize % alignment != 0)
            throw new IOException("The recording contains no playable audio.");
        return TimeSpan.FromSeconds((double)dataSize / rate);
    }

    public static string FormatTime(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
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
