using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class JumpToLatestButton : InteractiveButton
{
    public JumpToLatestButton()
    {
        AccessibleName = "Jump to latest message";
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;
        // Clip the window as well as the drawing so the corners do not intercept clicks.
        using var circle = new GraphicsPath();
        circle.AddEllipse(ClientRectangle);
        Region = new Region(circle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Pressed ? ChatTheme.Surface
            : KeyboardFocused ? ChatTheme.FocusSurface
            : Hovered ? Color.FromArgb(58, 61, 60) : ChatTheme.Raised);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        float x = Width / 2f, y = Height / 2f + (Pressed ? scale : 0);
        using var arrow = new Pen(ChatTheme.Ink, 2 * scale)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round
        };
        g.DrawLines(arrow, new PointF[]
        {
            new(x - 6 * scale, y - 3 * scale),
            new(x, y + 3 * scale),
            new(x + 6 * scale, y - 3 * scale)
        });

    }
}
