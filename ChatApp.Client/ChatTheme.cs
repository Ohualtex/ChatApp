using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ChatApp.Client;

internal static class ChatTheme
{
    public static readonly Color Canvas = Color.FromArgb(22, 23, 23);
    public static readonly Color Surface = Color.FromArgb(32, 34, 34);
    public static readonly Color Raised = Color.FromArgb(43, 45, 45);
    public static readonly Color Accent = Color.FromArgb(37, 211, 102);
    public static readonly Color Ink = Color.FromArgb(242, 244, 243);
    public static readonly Color Muted = Color.FromArgb(166, 174, 170);
    public static readonly Color Sent = Color.FromArgb(21, 76, 60);

    public static void DarkTitle(Form form)
    {
        int enabled = 1;
        DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint handle, int attribute, ref int value, int size);
}

internal sealed class RoundedPanel : Panel
{
    public RoundedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? ChatTheme.Canvas);
        if (Width < 1 || Height < 1) return;
        float diameter = Math.Min(20, Math.Min(Width, Height));
        using var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter - 1, 0, diameter, diameter, 270, 90);
        path.AddArc(Width - diameter - 1, Height - diameter - 1, diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter - 1, diameter, diameter, 90, 90);
        path.CloseFigure();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
    }
}
