using System.Media;
using System.Diagnostics;
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
            // RIFF chunks are word-aligned; odd-sized chunks include one padding byte.
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

internal sealed class VoiceClip : IDisposable
{
    public string Path { get; }
    public TimeSpan Duration { get; }
    public string FileName => "voice-message.wav";
    public static string NewPath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chatapp-voice-" + Guid.NewGuid().ToString("N") + ".wav");

    public VoiceClip(string path)
    {
        using var stream = File.OpenRead(path);
        Duration = WaveAudio.Duration(stream);
        Path = path;
    }

    // VoiceClip owns this temporary file; callers must not pass an original user file.
    public void Dispose() => DeleteTemporary(Path);

    public static void DeleteTemporary(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}

internal sealed class VoicePlayback : IDisposable
{
    // All cards share one active player so starting another message stops the previous one.
    private static VoicePlayback? active;
    private readonly System.Windows.Forms.Timer timer = new();
    private SoundPlayer? player;
    private MemoryStream? stream;
    private string? alias;
    private readonly Stopwatch elapsed = new();
    private TimeSpan duration;
    public bool Paused { get; private set; }
    public bool Playing => (player is not null || alias is not null) && !Paused;
    public TimeSpan Position => elapsed.Elapsed < duration ? elapsed.Elapsed : duration;
    public event Action? Changed;

    public VoicePlayback() => timer.Tick += (_, _) => Stop();

    public void Play(byte[] bytes)
    {
        duration = WaveAudio.Duration(bytes);
        active?.Stop();
        try
        {
            stream = new MemoryStream(bytes, writable: false);
            player = new SoundPlayer(stream);
            player.Load();
            player.Play();
            elapsed.Restart();
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

    // File-backed playback uses an MCI alias so the recording can be paused and resumed.
    public void Play(VoiceClip clip)
    {
        active?.Stop();
        try
        {
            alias = "play" + Guid.NewGuid().ToString("N");
            NativeAudio.Command($"open \"{clip.Path}\" type waveaudio alias {alias}");
            NativeAudio.Command($"play {alias}");
            duration = clip.Duration;
            elapsed.Restart();
            active = this;
            timer.Interval = (int)Math.Clamp(clip.Duration.TotalMilliseconds + 100, 100, int.MaxValue);
            timer.Start();
            Changed?.Invoke();
        }
        catch { Stop(); throw; }
    }

    public void TogglePause()
    {
        if (alias is null) return;
        NativeAudio.Command($"{(Paused ? "resume" : "pause")} {alias}");
        Paused = !Paused;
        if (Paused) { elapsed.Stop(); timer.Stop(); }
        else
        {
            elapsed.Start();
            timer.Interval = (int)Math.Clamp((duration - Position).TotalMilliseconds + 100, 100, int.MaxValue);
            timer.Start();
        }
        Changed?.Invoke();
    }

    public void Stop()
    {
        timer.Stop();
        elapsed.Reset();
        Paused = false;
        if (alias is not null)
        {
            NativeAudio.Close(alias);
            alias = null;
        }
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

internal static class NativeAudio
{
    public static void Command(string command)
    {
        uint result = MciSendString(command, null, 0, IntPtr.Zero);
        if (result == 0) return;
        var text = new StringBuilder(256);
        MciGetErrorString(result, text, text.Capacity);
        throw new IOException("Audio operation failed. " + text);
    }

    public static void Close(string alias) => MciSendString($"close {alias}", null, 0, IntPtr.Zero);

    [DllImport("winmm.dll", EntryPoint = "mciSendStringW", CharSet = CharSet.Unicode)]
    private static extern uint MciSendString(string command, StringBuilder? result, int length, IntPtr callback);
    [DllImport("winmm.dll", EntryPoint = "mciGetErrorStringW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MciGetErrorString(uint error, StringBuilder text, int length);
}
