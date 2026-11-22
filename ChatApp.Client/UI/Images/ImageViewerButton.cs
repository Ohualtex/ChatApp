using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal enum ImageViewerAction { Previous, Next, ZoomOut, ZoomIn, Fit, Save, Close }

internal sealed class ImageViewerButton(ImageViewerAction action) : InteractiveButton
{
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Parent?.BackColor ?? ChatTheme.Canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        float diameter = Math.Min(Width, Height) - 2 * scale;
        if (diameter <= 0) return;
        var circle = new RectangleF((Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
        bool arrow = action is ImageViewerAction.Previous or ImageViewerAction.Next;
        if (arrow || Hovered || Pressed || KeyboardFocused)
        {
            Color color = Enabled && Pressed ? Color.FromArgb(48, 51, 50)
                : KeyboardFocused ? ChatTheme.FocusSurface
                : Enabled && Hovered ? ChatTheme.Raised : Color.FromArgb(19, 20, 20);
            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, circle);
        }
        Color ink = Enabled ? ChatTheme.Ink : Color.FromArgb(79, 84, 81);
        using var pen = new Pen(ink, 1.8f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var state = g.Save();
        g.TranslateTransform(Width / 2f, Height / 2f + (Pressed ? scale : 0));
        g.ScaleTransform(scale, scale);
        pen.Width = 1.8f;
        switch (action)
        {
            case ImageViewerAction.Previous:
            case ImageViewerAction.Next:
                float direction = action == ImageViewerAction.Previous ? -1 : 1;
                g.DrawLines(pen, [new PointF(-2 * direction, -5), new PointF(3 * direction, 0), new PointF(-2 * direction, 5)]);
                break;
            case ImageViewerAction.ZoomOut:
            case ImageViewerAction.ZoomIn:
                g.DrawEllipse(pen, -8, -8, 12, 12);
                g.DrawLine(pen, 3, 3, 8, 8);
                g.DrawLine(pen, -5, -2, 1, -2);
                if (action == ImageViewerAction.ZoomIn) g.DrawLine(pen, -2, -5, -2, 1);
                break;
            case ImageViewerAction.Fit:
                g.DrawLines(pen, [new PointF(-3, -8), new PointF(-8, -8), new PointF(-8, -3)]);
                g.DrawLines(pen, [new PointF(3, -8), new PointF(8, -8), new PointF(8, -3)]);
                g.DrawLines(pen, [new PointF(-8, 3), new PointF(-8, 8), new PointF(-3, 8)]);
                g.DrawLines(pen, [new PointF(8, 3), new PointF(8, 8), new PointF(3, 8)]);
                break;
            case ImageViewerAction.Save:
                g.DrawLine(pen, 0, -8, 0, 3);
                g.DrawLines(pen, [new PointF(-4, -1), new PointF(0, 3), new PointF(4, -1)]);
                g.DrawLines(pen, [new PointF(-8, 4), new PointF(-8, 8), new PointF(8, 8), new PointF(8, 4)]);
                break;
            case ImageViewerAction.Close:
                g.DrawLine(pen, -6, -6, 6, 6);
                g.DrawLine(pen, 6, -6, -6, 6);
                break;
        }
        g.Restore(state);
    }
}
