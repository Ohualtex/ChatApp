using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class AttachmentDropOverlay : Control
{
    private static readonly Font HeadingFont = new("Segoe UI", 20);
    private static readonly Font HintFont = new("Segoe UI", 10);

    public AttachmentDropOverlay()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Visible = false;
        TabStop = false;
        Text = "Drop files here";
        AccessibleName = Text;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(ChatTheme.Canvas);
        if (Width < 8 || Height < 8) return;
        int inset = Math.Max(4, Math.Min((int)Math.Round(24 * DeviceDpi / 96d), Math.Min(Width, Height) / 8));
        var bounds = new Rectangle(inset, inset, Width - inset * 2, Height - inset * 2);
        int diameter = Math.Max(1, Math.Min(48, Math.Min(bounds.Width, bounds.Height)));
        using var shape = new GraphicsPath();
        shape.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        shape.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        shape.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        shape.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        shape.CloseFigure();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(Color.FromArgb(17, 53, 42));
        using var outline = new Pen(ChatTheme.Accent, 2) { DashStyle = DashStyle.Dash };
        e.Graphics.FillPath(fill, shape);
        e.Graphics.DrawPath(outline, shape);
        var heading = new Rectangle(bounds.Left + 12, bounds.Top + bounds.Height / 2 - 30, Math.Max(1, bounds.Width - 24), 52);
        TextRenderer.DrawText(e.Graphics, Text, HeadingFont, heading, ChatTheme.Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        var hint = new Rectangle(heading.Left, heading.Bottom, heading.Width, 30);
        TextRenderer.DrawText(e.Graphics, "Review before sending", HintFont, hint, ChatTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
