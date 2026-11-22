using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static ChatApp.Client.WaveInput;

namespace ChatApp.Client;

// Captures PCM audio through WinMM and writes it directly to a temporary WAV file.
internal sealed class MicrophoneRecorder : IDisposable
{
    private const int SampleRate = 22050, BufferSize = 8192;
    private readonly string path = VoiceClip.NewPath();
    private readonly List<(IntPtr Header, IntPtr Data)> buffers = [];
    private readonly Queue<(IntPtr Header, IntPtr Data)> pending = new();
    private readonly byte[] scratch = new byte[BufferSize];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 40 };
    private readonly Stopwatch elapsed = new();
    private FileStream? file;
    private IntPtr device;
    private bool retained, disposed;
    public bool Paused { get; private set; }
    public float Level { get; private set; }
    public TimeSpan Elapsed => elapsed.Elapsed;
    public event Action<Exception>? Failed;

    public MicrophoneRecorder()
    {
        timer.Tick += (_, _) =>
        {
            try { Drain(requeue: true); }
            catch (Exception error) when (error is IOException or InvalidOperationException)
            {
                timer.Stop();
                Failed?.Invoke(error);
            }
        };
    }

    public void Start()
    {
        if (device != IntPtr.Zero || disposed) throw new InvalidOperationException("The recorder is already in use.");
        try
        {
            file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            // Reserve the PCM header; its final data length is known only when recording stops.
            file.Write(new byte[44]);
            var format = new WaveFormat { FormatTag = 1, Channels = 1, SamplesPerSecond = SampleRate,
                AverageBytesPerSecond = SampleRate * 2, BlockAlignment = 2, BitsPerSample = 16 };
            Check(WaveInOpen(out device, uint.MaxValue, ref format, IntPtr.Zero, IntPtr.Zero, 0));
            for (int i = 0; i < 4; i++)
            {
                IntPtr data = Marshal.AllocHGlobal(BufferSize);
                IntPtr header = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
                buffers.Add((header, data));
                Marshal.StructureToPtr(new WaveHeader { Data = data, BufferLength = BufferSize }, header, false);
                Check(WaveInPrepareHeader(device, header, (uint)Marshal.SizeOf<WaveHeader>()));
                Check(WaveInAddBuffer(device, header, (uint)Marshal.SizeOf<WaveHeader>()));
                pending.Enqueue((header, data));
            }
            Check(WaveInStart(device));
            elapsed.Restart();
            timer.Start();
        }
        catch { Dispose(); throw; }
    }

    public void TogglePause()
    {
        if (device == IntPtr.Zero) return;
        if (Paused)
        {
            Check(WaveInStart(device));
            elapsed.Start();
        }
        else
        {
            Check(WaveInStop(device));
            elapsed.Stop();
            Drain(requeue: true);
        }
        Paused = !Paused;
        Level = 0;
    }

    private void Drain(bool requeue)
    {
        if (file is null) return;
        int available = pending.Count;
        while (available-- > 0 && pending.TryPeek(out var buffer))
        {
            WaveHeader header = Marshal.PtrToStructure<WaveHeader>(buffer.Header);
            if ((header.Flags & 1) == 0) break;
            pending.Dequeue();
            int count = checked((int)header.BytesRecorded);
            if (count > BufferSize) throw new IOException("The microphone returned an invalid audio buffer.");
            if (count > 0)
            {
                Marshal.Copy(buffer.Data, scratch, 0, count);
                if (file.Length + count > uint.MaxValue + 8L)
                    throw new IOException("The recording has reached the WAV format capacity.");
                file.Write(scratch, 0, count);
                int peak = 0;
                for (int i = 0; i + 1 < count; i += 2)
                    peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(scratch, i)));
                Level = peak / 32768f;
            }
            if (requeue)
            {
                header.BytesRecorded = 0;
                Marshal.StructureToPtr(header, buffer.Header, false);
                Check(WaveInAddBuffer(device, buffer.Header, (uint)Marshal.SizeOf<WaveHeader>()));
                pending.Enqueue(buffer);
            }
        }
    }

    // Finalize the WAV header and hand ownership of the temporary file to the returned clip.
    public VoiceClip Finish()
    {
        if (device == IntPtr.Zero || file is null) throw new IOException("Recording has not started.");
        timer.Stop(); elapsed.Stop();
        Check(WaveInReset(device));
        Drain(requeue: false);
        ReleaseDevice();
        long dataLength = file.Length - 44;
        if (dataLength <= 0 || dataLength > uint.MaxValue - 36)
            throw new IOException("This recording cannot be represented as a WAV file.");
        file.Position = 0;
        using (var writer = new BinaryWriter(file, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8); writer.Write((uint)(dataLength + 36)); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(SampleRate);
            writer.Write(SampleRate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write((uint)dataLength);
        }
        file.Dispose(); file = null;
        var clip = new VoiceClip(path);
        retained = true;
        return clip;
    }

    private void ReleaseDevice()
    {
        if (device != IntPtr.Zero)
        {
            // Return queued native buffers before unpreparing headers and freeing their memory.
            WaveInReset(device);
            foreach (var buffer in buffers)
                WaveInUnprepareHeader(device, buffer.Header, (uint)Marshal.SizeOf<WaveHeader>());
            WaveInClose(device);
            device = IntPtr.Zero;
        }
        foreach (var buffer in buffers)
        {
            Marshal.FreeHGlobal(buffer.Header);
            Marshal.FreeHGlobal(buffer.Data);
        }
        buffers.Clear();
        pending.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Dispose(); elapsed.Stop();
        ReleaseDevice();
        file?.Dispose(); file = null;
        if (!retained) VoiceClip.DeleteTemporary(path);
    }

}
