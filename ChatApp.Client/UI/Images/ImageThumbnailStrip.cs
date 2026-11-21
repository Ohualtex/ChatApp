using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class ImageThumbnailStrip : Control
{
    private readonly List<Bitmap> previews = [];
    private readonly InteractionFeedback interaction;
    private int selected, hovered = -1, offset;
    internal event Action<int>? SelectionRequested;
    private int Cell => Math.Max(1, (int)Math.Round(84 * DeviceDpi / 96d));
    private int Tile => Math.Max(1, (int)Math.Round(68 * DeviceDpi / 96d));

    public ImageThumbnailStrip(IReadOnlyList<ImageGalleryItem> items)
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        TabStop = true;
        AccessibleName = "Conversation images";
        AccessibleRole = AccessibleRole.List;
        interaction = new InteractionFeedback(this);
        foreach (ImageGalleryItem item in items)
        {
            Image source = item.Preview;
            double scale = Math.Min(1, 96d / Math.Max(source.Width, source.Height));
            previews.Add(new Bitmap(source, Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale))));
        }
    }

    internal void SelectImage(int index)
    {
        selected = index;
        EnsureSelectedVisible();
        AccessibleDescription = $"Image {index + 1} of {previews.Count}";
        Invalidate();
    }

    private int Start => Math.Max(0, (ClientSize.Width - previews.Count * Cell) / 2);
    private void EnsureSelectedVisible()
    {
        if (selected * Cell < offset) offset = selected * Cell;
        if ((selected + 1) * Cell > offset + ClientSize.Width)
            offset = (selected + 1) * Cell - ClientSize.Width;
        offset = Math.Clamp(offset, 0, Math.Max(0, previews.Count * Cell - ClientSize.Width));
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (previews is null) return;
        EnsureSelectedVisible();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(ChatTheme.Canvas);
        using var border = new Pen(ChatTheme.Raised);
        g.DrawLine(border, 0, 0, Width, 0);
        for (int i = 0; i < previews.Count; i++)
        {
            var tile = new Rectangle(Start + i * Cell - offset + (Cell - Tile) / 2, (Height - Tile) / 2, Tile, Tile);
            if (!tile.IntersectsWith(ClientRectangle)) continue;
            Color color = i == selected ? ChatTheme.Accent : i == hovered && interaction.Hovered ? ChatTheme.Muted : ChatTheme.Raised;
            using var background = new SolidBrush(i == hovered && interaction.Pressed ? ChatTheme.Surface : ChatTheme.Raised);
            g.FillRectangle(background, tile);
            Bitmap preview = previews[i];
            float scale = Math.Min((Tile - 8f) / preview.Width, (Tile - 8f) / preview.Height);
            var bounds = new RectangleF(tile.X + (Tile - preview.Width * scale) / 2, tile.Y + (Tile - preview.Height * scale) / 2,
                preview.Width * scale, preview.Height * scale);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(preview, bounds);
            using var outline = new Pen(color, i == selected ? 3 * DeviceDpi / 96f : 1);
            g.DrawRectangle(outline, tile);
        }
        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(ChatTheme.Muted);
            g.DrawRectangle(focus, 1, 2, Math.Max(0, Width - 3), Math.Max(0, Height - 4));
        }
    }

    private int HitTest(Point point)
    {
        int x = point.X - Start + offset;
        int index = x < 0 ? -1 : x / Cell;
        return index >= 0 && index < previews.Count ? index : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int next = HitTest(e.Location);
        if (next != hovered) { hovered = next; Invalidate(); }
        Cursor = next < 0 ? Cursors.Default : Cursors.Hand;
    }

    protected override void OnMouseLeave(EventArgs e) { hovered = -1; base.OnMouseLeave(e); Invalidate(); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && HitTest(e.Location) is int index && index >= 0)
        { Focus(); SelectionRequested?.Invoke(index); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        offset = Math.Clamp(offset - Math.Sign(e.Delta) * Cell, 0, Math.Max(0, previews.Count * Cell - ClientSize.Width));
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyData is Keys.Left or Keys.Right)
        {
            SelectionRequested?.Invoke(Math.Clamp(selected + (e.KeyCode == Keys.Left ? -1 : 1), 0, previews.Count - 1));
            e.Handled = e.SuppressKeyPress = true;
        }
        else base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) foreach (Bitmap preview in previews) preview.Dispose();
        base.Dispose(disposing);
    }
}
