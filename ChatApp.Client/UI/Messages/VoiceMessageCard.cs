using System.Drawing.Drawing2D;

namespace ChatApp.Client;

// Shows the voice waveform and playback progress for a clip owned by conversation history.
internal sealed class VoiceMessageCard : Control
{
    private static readonly Color Progress = Color.FromArgb(83, 191, 229);
    private static readonly Font MetaFont = new("Segoe UI", 9);
    private static readonly Font AvatarFont = new("Segoe UI", 12, FontStyle.Bold);
    private readonly VoiceClip clip;
    private readonly string sender, time;
    private readonly float[] peaks;
    private readonly VoicePlayback playback = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 80 };
    private readonly ContextMenuStrip menu = new();
    private readonly PlaybackButton playButton;

    public VoiceMessageCard(VoiceClip clip, string sender, string time)
    {
        this.clip = clip; this.sender = sender; this.time = time;
        peaks = ReadPeaks(clip);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        AccessibleRole = AccessibleRole.Grouping;
        playButton = new PlaybackButton(() => playback.Playing)
        {
            Bounds = new Rectangle(46, 0, 40, 46), AccessibleName = $"Play voice message from {sender}"
        };
        playButton.Click += (_, _) => TogglePlayback();
        Controls.Add(playButton);
        AccessibleName = $"Voice message from {sender}, {WaveAudio.FormatTime(clip.Duration)}. Play";
        timer.Tick += (_, _) => Invalidate();
        playback.Changed += () =>
        {
            if (playback.Playing) timer.Start(); else timer.Stop();
            playButton.AccessibleName = $"{(playback.Playing ? "Pause" : "Play")} voice message from {sender}";
            playButton.Invalidate();
            Invalidate();
        };
        menu.Items.Add("Save audio...", null, async (_, _) => await SaveAsync());
        ContextMenuStrip = menu;
        playButton.ContextMenuStrip = menu;
    }

    private void TogglePlayback()
    {
        try
        {
            if (playback.Playing || playback.Paused) playback.TogglePause();
            else playback.Play(clip);
        }
        catch (IOException error)
        { MessageBox.Show(FindForm(), error.Message, "Could not play recording", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var avatar = new SolidBrush(ChatTheme.Surface);
        g.FillEllipse(avatar, 0, 5, 40, 40);
        string initial = string.IsNullOrEmpty(sender) ? "?" : System.Globalization.StringInfo.GetNextTextElement(sender).ToUpperInvariant();
        TextRenderer.DrawText(g, initial, AvatarFont, new Rectangle(0, 5, 40, 40), ChatTheme.Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        var wave = new Rectangle(94, 9, Math.Max(1, Width - 104), 30);
        double fraction = clip.Duration.TotalSeconds > 0 ? playback.Position.TotalSeconds / clip.Duration.TotalSeconds : 0;
        using var future = new Pen(Color.FromArgb(103, 154, 143), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var past = new Pen(Progress, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        for (int i = 0; i < peaks.Length; i++)
        {
            float x = wave.X + (i + 0.5f) * wave.Width / peaks.Length;
            float height = 3 + peaks[i] * (wave.Height - 5);
            g.DrawLine(i / (double)peaks.Length < fraction ? past : future, x, wave.Y + (wave.Height - height) / 2,
                x, wave.Y + (wave.Height + height) / 2);
        }
        using var knob = new SolidBrush(Progress);
        float progressX = wave.Left + (float)Math.Clamp(fraction, 0, 1) * wave.Width;
        g.FillEllipse(knob, progressX - 5, wave.Top + wave.Height / 2 - 5, 10, 10);
        TimeSpan shown = playback.Playing || playback.Paused ? playback.Position : clip.Duration;
        TextRenderer.DrawText(g, WaveAudio.FormatTime(shown), MetaFont, new Rectangle(94, 45, Math.Max(1, Width - 150), 20), Progress, TextFormatFlags.Left);
        TextRenderer.DrawText(g, time, MetaFont, new Rectangle(Math.Max(0, Width - 54), 45, 54, 20), ChatTheme.Muted, TextFormatFlags.Right);
    }

    private sealed class PlaybackButton(Func<bool> playing) : InteractiveButton
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? ChatTheme.Raised);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float x = Width / 2f, y = Height / 2f;
            var circle = new RectangleF(x - 17, y - 17, 34, 34);
            if (Hovered || Pressed || KeyboardFocused)
            {
                using var fill = new SolidBrush(Pressed ? Color.FromArgb(24, 42, 35)
                    : KeyboardFocused ? ChatTheme.FocusSurface : Color.FromArgb(65, 89, 80));
                e.Graphics.FillEllipse(fill, circle);
            }
            if (Pressed) y += 1;
            using var ink = new SolidBrush(Enabled ? ChatTheme.Ink : ChatTheme.Muted);
            if (playing())
            {
                e.Graphics.FillRectangle(ink, x - 6, y - 8, 4, 16);
                e.Graphics.FillRectangle(ink, x + 2, y - 8, 4, 16);
            }
            else e.Graphics.FillPolygon(ink, [new PointF(x - 6, y - 9), new PointF(x + 7, y), new PointF(x - 6, y + 9)]);
        }
    }

    private static float[] ReadPeaks(VoiceClip clip)
    {
        var levels = new float[40];
        using var stream = File.OpenRead(clip.Path);
        using var reader = new BinaryReader(stream);
        stream.Position = 12;
        ushort bits = 16, alignment = 2;
        while (stream.Position + 8 <= stream.Length)
        {
            uint id = reader.ReadUInt32(), size = reader.ReadUInt32();
            long next = stream.Position + size + (size & 1);
            if (id == 0x20746D66)
            {
                stream.Position += 12; alignment = reader.ReadUInt16(); bits = reader.ReadUInt16();
            }
            else if (id == 0x61746164)
            {
                long start = stream.Position;
                byte[] samples = new byte[2048];
                for (int i = 0; i < levels.Length; i++)
                {
                    long offset = (long)size * i / levels.Length / alignment * alignment;
                    stream.Position = start + offset;
                    int count = stream.Read(samples, 0, (int)Math.Min(samples.Length, size - offset));
                    int peak = 0;
                    for (int j = 0; j + bits / 8 <= count; j += bits / 8)
                        peak = Math.Max(peak, bits == 16 ? Math.Abs((int)BitConverter.ToInt16(samples, j)) : Math.Abs(samples[j] - 128) * 256);
                    levels[i] = MathF.Sqrt(peak / 32768f);
                }
                break;
            }
            stream.Position = next;
        }
        return levels;
    }

    private async Task SaveAsync()
    {
        using var dialog = new SaveFileDialog { FileName = clip.FileName, Filter = "WAV audio (*.wav)|*.wav", Title = "Save voice message" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            await using var source = File.OpenRead(clip.Path);
            await using var target = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
            await source.CopyToAsync(target);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { MessageBox.Show(FindForm(), error.Message, "Could not save recording", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { playback.Dispose(); timer.Dispose(); menu.Dispose(); }
        base.Dispose(disposing);
    }
}
