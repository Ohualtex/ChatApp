using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class ImageMessageCard : Control
{
    private static readonly Font ClockFont = new("Segoe UI", 9);
    private readonly ChatAttachment attachment;
    private readonly byte[] bytes;
    private readonly Bitmap preview;
    private readonly Size sourceSize;
    private readonly string sender, time;
    private readonly DateTimeOffset? timestamp;
    private readonly ContextMenuStrip menu = new();
    private Rectangle imageBounds, clockBounds;
    private readonly InteractionFeedback interaction;
    internal ImageGalleryItem GalleryItem { get; }
    internal Func<IReadOnlyList<ImageGalleryItem>>? GalleryItems;

    private ImageMessageCard(ChatAttachment attachment, byte[] bytes, Bitmap preview, Size sourceSize,
        string sender, string time, DateTimeOffset? timestamp)
    {
        this.attachment = attachment;
        this.bytes = bytes;
        this.preview = preview;
        this.sourceSize = sourceSize;
        this.sender = sender;
        this.time = time;
        this.timestamp = timestamp;
        GalleryItem = new ImageGalleryItem(attachment, bytes, sender, timestamp, preview);
        interaction = new InteractionFeedback(this);
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        AccessibleName = $"Image from {sender}: {attachment.DisplayName}. Open image";
        menu.Items.Add("Save image as…", null, async (_, _) => await ImageViewer.SaveImageAsync(FindForm(), attachment, bytes));
        ContextMenuStrip = menu;
    }

    internal static ImageMessageCard? TryCreate(ChatAttachment attachment, byte[] bytes,
        string sender, string time, DateTimeOffset? timestamp)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var source = Image.FromStream(stream);
            if (source.Width > 4096 || source.Height > 4096) return null;
            double scale = Math.Min(1d, Math.Min(640d / source.Width, 640d / source.Height));
            var preview = new Bitmap(source, Math.Max(1, (int)Math.Round(source.Width * scale)),
                Math.Max(1, (int)Math.Round(source.Height * scale)));
            return new ImageMessageCard(attachment, bytes, preview, source.Size, sender, time, timestamp);
        }
        catch (Exception error) when (error is ArgumentException or OutOfMemoryException)
        {
            return null;
        }
    }

    internal void ResizeTo(int maximumWidth)
    {
        int Scale(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96d));
        maximumWidth = Math.Max(1, Math.Min(maximumWidth, Scale(320)));
        double scale = Math.Min(1d, Math.Min(maximumWidth / (double)sourceSize.Width, Scale(280) / (double)sourceSize.Height));
        int imageWidth = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
        int imageHeight = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
        Size clock = TextRenderer.MeasureText(time, ClockFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int inset = Scale(8);
        bool footer = time.Length > 0 && (imageWidth < clock.Width + inset * 2 || imageHeight < clock.Height + inset * 2);
        int width = Math.Min(maximumWidth, Math.Max(imageWidth, time.Length > 0 ? clock.Width + inset * 2 : 1));
        int height = imageHeight + (footer ? clock.Height + inset * 2 : 0);
        Size = new Size(width, height);
        imageBounds = new Rectangle((width - imageWidth) / 2, 0, imageWidth, imageHeight);
        clockBounds = new Rectangle(Math.Max(0, width - clock.Width - inset), height - clock.Height - inset,
            Math.Min(clock.Width, width), clock.Height);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Width < 1 || Height < 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int diameter = Math.Min((int)Math.Round(12 * DeviceDpi / 96d), Math.Min(Width, Height));
        using var clip = new GraphicsPath();
        clip.AddArc(0, 0, diameter, diameter, 180, 90);
        clip.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
        clip.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
        clip.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
        clip.CloseFigure();
        var state = e.Graphics.Save();
        e.Graphics.SetClip(clip);
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(preview, imageBounds);
        if (interaction.Hovered || interaction.Pressed)
        {
            using var overlay = new SolidBrush(interaction.Pressed ? Color.FromArgb(32, 0, 0, 0) : Color.FromArgb(14, 255, 255, 255));
            e.Graphics.FillRectangle(overlay, ClientRectangle);
        }
        if (time.Length > 0)
        {
            Rectangle shade = Rectangle.Inflate(clockBounds, 4, 2);
            using var background = new SolidBrush(Color.FromArgb(145, 0, 0, 0));
            e.Graphics.FillRectangle(background, shade);
            TextRenderer.DrawText(e.Graphics, time, ClockFont, clockBounds, Color.White,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.Right);
        }
        if (Focused && ShowFocusCues && Enabled)
        {
            using var outline = new Pen(ChatTheme.Accent, Math.Max(2, DeviceDpi / 48f));
            e.Graphics.DrawPath(outline, clip);
        }
        e.Graphics.Restore(state);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (Enabled && e.Button == MouseButtons.Left) OpenViewer();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Enabled && (e.KeyCode is Keys.Enter or Keys.Space))
        {
            e.Handled = e.SuppressKeyPress = true;
            OpenViewer();
        }
    }

    private void OpenViewer()
    {
        try
        {
            ImageGalleryItem[] items = GalleryItems?.Invoke().ToArray() ?? [GalleryItem];
            int index = Array.IndexOf(items, GalleryItem);
            if (index < 0) { items = [GalleryItem]; index = 0; }
            if (FindForm() is ChatForm owner) owner.OpenImageViewer(items, index, this);
        }
        catch (Exception error) when (error is ArgumentException or OutOfMemoryException or IOException)
        {
            MessageBox.Show(FindForm(), error.Message, "Could not display image", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { preview.Dispose(); menu.Dispose(); }
        base.Dispose(disposing);
    }
}
