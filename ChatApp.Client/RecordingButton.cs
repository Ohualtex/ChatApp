namespace ChatApp.Client;

internal enum RecordingAction { Delete, Pause, Resume, Send, Microphone, Attach }

internal sealed class RecordingButton : Button
{
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
        float x = Width / 2f, y = Height / 2f;
        Color ink = !Enabled ? ChatTheme.Muted : Action is RecordingAction.Pause or RecordingAction.Resume
            ? Color.FromArgb(255, 153, 174) : ChatTheme.Ink;
        using var pen = new Pen(ink, 2) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        using var brush = new SolidBrush(ink);
        switch (Action)
        {
            case RecordingAction.Send:
                using (var green = new SolidBrush(Enabled ? ChatTheme.Accent : ChatTheme.Muted))
                    e.Graphics.FillEllipse(green, x - 19, y - 19, 38, 38);
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
                using (var clip = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    clip.AddLine(x + 6, y - 7, x + 6, y + 6);
                    clip.AddBezier(x + 6, y + 6, x + 6, y + 13, x - 6, y + 13, x - 6, y + 6);
                    clip.AddLine(x - 6, y + 6, x - 6, y - 6);
                    clip.AddBezier(x - 6, y - 6, x - 6, y - 13, x + 3, y - 13, x + 3, y - 6);
                    clip.AddLine(x + 3, y - 6, x + 3, y + 5);
                    clip.AddBezier(x + 3, y + 5, x + 3, y + 9, x - 3, y + 9, x - 3, y + 5);
                    clip.AddLine(x - 3, y + 5, x - 3, y - 5);
                    using var tilt = new System.Drawing.Drawing2D.Matrix();
                    tilt.RotateAt(35, new PointF(x, y));
                    clip.Transform(tilt);
                    e.Graphics.DrawPath(pen, clip);
                }
                break;
            case RecordingAction.Delete:
                e.Graphics.DrawLine(pen, x - 8, y - 7, x + 8, y - 7);
                e.Graphics.DrawRectangle(pen, x - 5, y - 6, 10, 14);
                e.Graphics.DrawLine(pen, x - 2, y - 10, x + 2, y - 10);
                e.Graphics.DrawLine(pen, x - 2, y - 3, x - 2, y + 4);
                e.Graphics.DrawLine(pen, x + 2, y - 3, x + 2, y + 4);
                break;
        }
    }
}
