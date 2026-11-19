using System.Runtime.InteropServices;
using System.Text;

namespace ChatApp.Client;

internal sealed class VoiceRecorder : IDisposable
{
    private readonly string alias = "voice" + Guid.NewGuid().ToString("N");
    private readonly string path = Path.Combine(Path.GetTempPath(), "chatapp-voice-" + Guid.NewGuid().ToString("N") + ".wav");
    private bool opened;

    public void Start()
    {
        try
        {
            Command($"open new type waveaudio alias {alias}");
            opened = true;
            Command($"set {alias} time format ms format tag pcm channels 1 samplespersec 22050 bitspersample 16 alignment 2 bytespersec 44100");
            Command($"record {alias} to 60000");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public byte[] Finish()
    {
        if (!opened) throw new IOException("Recording has not started.");
        Command($"stop {alias}");
        Command($"save {alias} \"{path}\" wait");
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length > ChatAttachment.MaximumBytes) throw new IOException("The recording exceeds 10 MB.");
        WaveAudio.Duration(bytes);
        return bytes;
    }

    private static void Command(string command)
    {
        uint result = MciSendString(command, null, 0, IntPtr.Zero);
        if (result == 0) return;
        var description = new StringBuilder(256);
        MciGetErrorString(result, description, description.Capacity);
        throw new IOException("Audio recording failed. Check your microphone and Windows microphone permissions. " + description);
    }

    public void Dispose()
    {
        if (opened)
        {
            MciSendString($"close {alias}", null, 0, IntPtr.Zero);
            opened = false;
        }
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A locked temporary recording can be reclaimed by Windows temporary-file cleanup.
        }
    }

    [DllImport("winmm.dll", EntryPoint = "mciSendStringW", CharSet = CharSet.Unicode)]
    private static extern uint MciSendString(string command, StringBuilder? result, int length, IntPtr callback);

    [DllImport("winmm.dll", EntryPoint = "mciGetErrorStringW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MciGetErrorString(uint error, StringBuilder text, int length);
}
