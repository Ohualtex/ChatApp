namespace ChatApp.Client;

internal sealed class VoiceLevel : Control
{
    private readonly Queue<float> levels = new(18);
    public VoiceLevel() { Dock = DockStyle.Fill; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
    public void Push(float level) { if (levels.Count == 18) levels.Dequeue(); levels.Enqueue(level); Invalidate(); }
    public void Clear() { levels.Clear(); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(ChatTheme.Muted, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var values = levels.ToArray();
        for (int i = 0; i < 18; i++)
        {
            float value = i < 18 - values.Length ? 0 : values[i - (18 - values.Length)];
            float height = 2 + Math.Clamp(value * 4, 0, 1) * Math.Max(0, Height - 16);
            float x = (i + 0.5f) * Width / 18;
            e.Graphics.DrawLine(pen, x, (Height - height) / 2, x, (Height + height) / 2);
        }
    }
}
