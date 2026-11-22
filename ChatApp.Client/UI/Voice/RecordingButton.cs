namespace ChatApp.Client;

internal enum RecordingAction { Delete, Pause, Resume, Send, Microphone, Attach }

internal sealed class RecordingButton : InteractiveButton
{
    private static readonly Font AttachFont = new("Segoe MDL2 Assets", 22, FontStyle.Regular, GraphicsUnit.Pixel);
    private RecordingAction action;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal RecordingAction Action { get => action; set { action = value; Invalidate(); } }
    public RecordingButton(RecordingAction action)
    {
        this.action = action;
        Dock = DockStyle.Fill;
        Margin = new Padding(2);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = ChatTheme.Raised;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        float x = Width / scale / 2f, y = Height / scale / 2f;
        bool active = Hovered || Pressed || KeyboardFocused;
        bool greenSurface = Action == RecordingAction.Send || (Action == RecordingAction.Microphone && active && Enabled);
        var state = e.Graphics.Save();
        e.Graphics.ScaleTransform(scale, scale);
        float diameter = Math.Max(2, Math.Min(40, Math.Min(Width / scale, Height / scale) - 4));
        var circle = new RectangleF(x - diameter / 2, y - diameter / 2, diameter, diameter);
        if (greenSurface || (active && Enabled))
        {
            Color surface = greenSurface
                ? !Enabled ? ChatTheme.Muted : Pressed ? Color.FromArgb(28, 171, 82)
                    : KeyboardFocused ? ChatTheme.FocusInk : Hovered ? Color.FromArgb(52, 225, 119) : ChatTheme.Accent
                : Pressed ? Color.FromArgb(32, 35, 34)
                    : KeyboardFocused ? ChatTheme.FocusSurface : Color.FromArgb(58, 61, 60);
            using var fill = new SolidBrush(surface);
            e.Graphics.FillEllipse(fill, circle);
        }
        if (Pressed) y += 1;
        Color ink = !Enabled ? ChatTheme.Muted : greenSurface ? ChatTheme.Canvas
            : Action is RecordingAction.Pause or RecordingAction.Resume ? Color.FromArgb(255, 153, 174) : ChatTheme.Ink;
        using var pen = new Pen(ink, 2) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        using var brush = new SolidBrush(ink);
        switch (Action)
        {
            case RecordingAction.Send:
                using (var dark = new SolidBrush(ChatTheme.Canvas))
                    e.Graphics.FillPolygon(dark, [new PointF(x - 6, y - 8), new PointF(x + 10, y), new PointF(x - 6, y + 8), new PointF(x - 3, y)]);
                break;
            case RecordingAction.Pause:
                e.Graphics.FillRectangle(brush, x - 6, y - 7, 4, 14);
                e.Graphics.FillRectangle(brush, x + 2, y - 7, 4, 14);
                break;
            case RecordingAction.Resume:
                e.Graphics.FillPolygon(brush, [new PointF(x - 5, y - 8), new PointF(x + 7, y), new PointF(x - 5, y + 8)]);
                break;
            case RecordingAction.Microphone:
                using (var capsule = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    capsule.AddArc(x - 4, y - 11, 8, 8, 180, 180);
                    capsule.AddLine(x + 4, y - 7, x + 4, y - 1);
                    capsule.AddArc(x - 4, y - 5, 8, 8, 0, 180);
                    capsule.CloseFigure();
                    e.Graphics.DrawPath(pen, capsule);
                }
                e.Graphics.DrawLine(pen, x - 8, y - 2, x - 8, y);
                e.Graphics.DrawArc(pen, x - 8, y - 8, 16, 16, 0, 180);
                e.Graphics.DrawLine(pen, x + 8, y, x + 8, y - 2);
                e.Graphics.DrawLine(pen, x, y + 8, x, y + 12);
                e.Graphics.DrawLine(pen, x - 4, y + 12, x + 4, y + 12);
                break;
            case RecordingAction.Attach:
                break;
            case RecordingAction.Delete:
                e.Graphics.DrawLine(pen, x - 8, y - 7, x + 8, y - 7);
                e.Graphics.DrawRectangle(pen, x - 5, y - 6, 10, 14);
                e.Graphics.DrawLine(pen, x - 2, y - 10, x + 2, y - 10);
                e.Graphics.DrawLine(pen, x - 2, y - 3, x - 2, y + 4);
                e.Graphics.DrawLine(pen, x + 2, y - 3, x + 2, y + 4);
                break;
        }
        e.Graphics.Restore(state);
        if (Action == RecordingAction.Attach)
        {
            Rectangle bounds = ClientRectangle;
            if (Pressed) bounds.Offset(0, Math.Max(1, (int)Math.Round(scale)));
            using var font = new Font(AttachFont.FontFamily, AttachFont.Size * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(e.Graphics, "\uE723", font, bounds, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
