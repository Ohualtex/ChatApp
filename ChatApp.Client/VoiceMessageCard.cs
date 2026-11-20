using System.Drawing.Drawing2D;

namespace ChatApp.Client;

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
    private Rectangle PlayArea => new(46, 0, 40, 46);

    public VoiceMessageCard(VoiceClip clip, string sender, string time)
    {
        this.clip = clip; this.sender = sender; this.time = time;
        peaks = ReadPeaks(clip);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = $"Voice message from {sender}, {WaveAudio.FormatTime(clip.Duration)}. Play";
        timer.Tick += (_, _) => Invalidate();
        playback.Changed += () =>
        {
            if (playback.Playing) timer.Start(); else timer.Stop();
            AccessibleName = $"Voice message from {sender}. {(playback.Playing ? "Pause" : "Play")}";
            Invalidate();
        };
        menu.Items.Add("Save audio...", null, async (_, _) => await SaveAsync());
        ContextMenuStrip = menu;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && PlayArea.Contains(e.Location)) TogglePlayback();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    { base.OnMouseMove(e); Cursor = PlayArea.Contains(e.Location) ? Cursors.Hand : Cursors.Default; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) { e.SuppressKeyPress = true; TogglePlayback(); }
        base.OnKeyDown(e);
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
        using var ink = new SolidBrush(ChatTheme.Ink);
        if (playback.Playing)
        {
            g.FillRectangle(ink, 59, 16, 4, 16); g.FillRectangle(ink, 67, 16, 4, 16);
        }
        else g.FillPolygon(ink, [new PointF(59, 15), new PointF(72, 24), new PointF(59, 33)]);
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
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, PlayArea, ChatTheme.Muted, BackColor);
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
