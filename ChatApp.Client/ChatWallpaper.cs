using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ChatApp.Client;

internal static class ChatWallpaper
{
    private static readonly Bitmap Wallpaper = CreateWallpaper();

    public static void Paint(Graphics graphics, Rectangle bounds, int verticalOffset = 0)
    {
        using var background = new SolidBrush(ChatTheme.Canvas);
        graphics.FillRectangle(background, bounds);
        graphics.DrawImageUnscaled(Wallpaper, 0, -verticalOffset);
    }

    private static Bitmap CreateWallpaper()
    {
        using var stream = typeof(ChatWallpaper).Assembly.GetManifestResourceStream("ChatApp.Client.Assets.chat-wallpaper.png")
            ?? throw new InvalidOperationException("The chat wallpaper resource is missing.");
        using var source = new Bitmap(stream);
        // Fit the largest display once at startup, never the current window or viewport.
        int width = Math.Max(1920, Screen.AllScreens.Max(screen => screen.Bounds.Width));
        int height = Math.Max(1080, Screen.AllScreens.Max(screen => screen.Bounds.Height));
        float scale = Math.Max(width / (float)source.Width, height / (float)source.Height);
        var wallpaper = new Bitmap((int)Math.Ceiling(source.Width * scale),
            (int)Math.Ceiling(source.Height * scale), PixelFormat.Format32bppPArgb);
        Color background = ChatTheme.Canvas;
        Color line = Color.FromArgb(32, 35, 33);
        using var colors = new ImageAttributes();
        colors.SetColorMatrix(new ColorMatrix(new[]
        {
            new[] { (background.R - line.R) / 255f, 0, 0, 0, 0 },
            new[] { 0, (background.G - line.G) / 255f, 0, 0, 0 },
            new[] { 0, 0, (background.B - line.B) / 255f, 0, 0 },
            new[] { 0f, 0, 0, 1, 0 },
            new[] { line.R / 255f, line.G / 255f, line.B / 255f, 0, 1 }
        }));
        // Mirror only the edge samples for the resize filter; never tile the finished image.
        colors.SetWrapMode(WrapMode.TileFlipXY);
        using var graphics = Graphics.FromImage(wallpaper);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(Point.Empty, wallpaper.Size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, colors);
        return wallpaper;
    }
}

internal sealed class WallpaperPanel : Panel
{
    private readonly Func<int> verticalOffset;

    public WallpaperPanel(Func<int> verticalOffset)
    {
        this.verticalOffset = verticalOffset;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
        => ChatWallpaper.Paint(e.Graphics, ClientRectangle, verticalOffset());
}
