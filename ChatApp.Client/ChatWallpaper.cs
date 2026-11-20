using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ChatApp.Client;

internal static class ChatWallpaper
{
    private static readonly Bitmap Tile = CreateTile();

    public static void Paint(Graphics graphics, Rectangle bounds)
    {
        using var brush = new TextureBrush(Tile, WrapMode.Tile);
        graphics.FillRectangle(brush, bounds);
    }

    private static Bitmap CreateTile()
    {
        using var stream = typeof(ChatWallpaper).Assembly.GetManifestResourceStream("ChatApp.Client.Assets.chat-wallpaper.png")
            ?? throw new InvalidOperationException("The chat wallpaper resource is missing.");
        using var source = new Bitmap(stream);
        // Prepare one fixed-size tile; window dimensions never enter this calculation.
        var tile = new Bitmap(source.Width / 2, source.Height / 2, PixelFormat.Format32bppPArgb);
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
        colors.SetWrapMode(WrapMode.TileFlipXY);
        using var graphics = Graphics.FromImage(tile);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(Point.Empty, tile.Size), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, colors);
        return tile;
    }
}
