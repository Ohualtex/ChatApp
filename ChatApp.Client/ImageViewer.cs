namespace ChatApp.Client;

internal sealed class ImageViewer : UserControl
{
    private Bitmap image;
    private readonly ImageGalleryItem[] items;
    private int currentIndex;
    private ImageGalleryItem CurrentItem => items[currentIndex];
    private readonly Label caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ImageViewerButton previous = new(ImageViewerAction.Previous) { AccessibleName = "Previous image" };
    private readonly ImageViewerButton next = new(ImageViewerAction.Next) { AccessibleName = "Next image" };
    private readonly ZoomImageCanvas picture;
    private readonly ImageViewerButton zoomOut = new(ImageViewerAction.ZoomOut) { AccessibleName = "Zoom out" };
    private readonly ImageViewerButton zoomIn = new(ImageViewerAction.ZoomIn) { AccessibleName = "Zoom in" };
    private readonly ImageViewerButton fit = new(ImageViewerAction.Fit) { AccessibleName = "Fit image to window" };
    private readonly ImageViewerButton save = new(ImageViewerAction.Save) { AccessibleName = "Save image as" };
    private readonly ImageViewerButton close = new(ImageViewerAction.Close) { AccessibleName = "Close image" };
    private readonly ConversationAvatar avatar = new() { BackColor = ChatTheme.Canvas };
    private readonly Label detail = new() { AutoEllipsis = true, ForeColor = ChatTheme.Muted };
    private readonly ImageThumbnailStrip thumbnails;
    internal event EventHandler? CloseRequested;
    private readonly ToolTip tips = new();

    public ImageViewer(IReadOnlyList<ImageGalleryItem> gallery, int index)
    {
        if (gallery.Count == 0) throw new ArgumentException("The image gallery is empty.", nameof(gallery));
        if (index < 0 || index >= gallery.Count) throw new ArgumentOutOfRangeException(nameof(index));
        items = gallery.ToArray();
        currentIndex = index;
        image = Decode(CurrentItem);
        picture = new ZoomImageCanvas(image);
        BackColor = ChatTheme.Canvas;
        ForeColor = ChatTheme.Ink;
        Font = new Font("Segoe UI", 10);
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        AccessibleName = "Image viewer";
        caption.Dock = DockStyle.None;
        caption.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        thumbnails = new ImageThumbnailStrip(items);
        thumbnails.SelectionRequested += ShowImage;
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        save.Click += async (_, _) =>
        {
            save.Enabled = false;
            try
            {
                ImageGalleryItem item = CurrentItem;
                await SaveImageAsync(FindForm(), item.Attachment, item.Bytes);
            }
            finally { if (!IsDisposed) save.Enabled = true; }
        };
        previous.Click += (_, _) => Navigate(-1);
        next.Click += (_, _) => Navigate(1);
        tips.SetToolTip(previous, "Previous image (Left at fit, Page Up)");
        tips.SetToolTip(next, "Next image (Right at fit, Page Down)");
        zoomOut.Click += (_, _) => picture.ZoomBy(1 / 1.25);
        zoomIn.Click += (_, _) => picture.ZoomBy(1.25);
        fit.Click += (_, _) => picture.FitImage();
        tips.SetToolTip(zoomOut, "Zoom out (−)");
        tips.SetToolTip(zoomIn, "Zoom in (+)");
        tips.SetToolTip(fit, "Fit image to window (Home or Ctrl+0)");
        picture.GalleryNavigationRequested += Navigate;
        picture.ZoomChanged += (_, _) => UpdateZoomControls();
        Controls.AddRange([picture, previous, next, avatar, caption, detail, zoomOut, zoomIn, fit, save, close, thumbnails]);
        previous.Visible = next.Visible = items.Length > 1;
        tips.SetToolTip(save, "Save image as");
        tips.SetToolTip(close, "Close (Esc)");
        LayoutViewer();
        UpdateCurrentImage();
        UpdateZoomControls();
    }

    private static Bitmap Decode(ImageGalleryItem item)
    {
        using var stream = new MemoryStream(item.Bytes);
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    private void UpdateCurrentImage()
    {
        ImageGalleryItem item = CurrentItem;
        picture.AccessibleName = item.Attachment.DisplayName;
        avatar.ShowConversation(item.Sender.Length > 0 ? item.Sender : "Unknown", false);
        string date = item.Timestamp?.ToLocalTime().ToString("dd MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "";
        caption.Text = item.Sender;
        detail.Text = date + (items.Length > 1 ? $"  /  {currentIndex + 1} of {items.Length}" : "");
        thumbnails.SelectImage(currentIndex);
        previous.Enabled = currentIndex > 0;
        next.Enabled = currentIndex < items.Length - 1;
    }

    private void Navigate(int direction) => ShowImage(currentIndex + direction);

    private void ShowImage(int target)
    {
        if (target < 0 || target >= items.Length || target == currentIndex) return;
        try
        {
            Bitmap replacement = Decode(items[target]);
            Bitmap old = image;
            image = replacement;
            currentIndex = target;
            picture.SetImage(image);
            old.Dispose();
            UpdateCurrentImage();
        }
        catch (Exception error) when (error is ArgumentException or OutOfMemoryException or IOException)
        {
            MessageBox.Show(this, error.Message, "Could not display image", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateZoomControls()
    {
        tips.SetToolTip(fit, $"Fit image to window (Home or Ctrl+0) - {picture.ZoomPercent:0}%");
        zoomOut.Enabled = picture.ZoomFactor > 1.00001;
        zoomIn.Enabled = picture.ZoomFactor < 7.99999;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        => HandleViewerKey(keyData) || base.ProcessCmdKey(ref msg, keyData);

    internal bool HandleViewerKey(Keys keyData)
    {
        if (keyData == Keys.Escape) { CloseRequested?.Invoke(this, EventArgs.Empty); return true; }
        Keys key = keyData & Keys.KeyCode;
        if (keyData == Keys.PageUp || (keyData == Keys.Left && picture.ZoomFactor <= 1.00001))
        { Navigate(-1); return true; }
        if (keyData == Keys.PageDown || (keyData == Keys.Right && picture.ZoomFactor <= 1.00001))
        { Navigate(1); return true; }
        if ((keyData & Keys.Alt) == 0)
        {
            if (key is Keys.Add or Keys.Oemplus) { picture.ZoomBy(1.25); return true; }
            if (key is Keys.Subtract or Keys.OemMinus) { picture.ZoomBy(1 / 1.25); return true; }
            if (key == Keys.Home || keyData == (Keys.Control | Keys.D0)) { picture.FitImage(); return true; }
        }
        return false;
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & ~Keys.Shift) == Keys.Tab)
        {
            SelectNextControl(ActiveControl, (keyData & Keys.Shift) == 0, true, true, true);
            return true;
        }
        return base.ProcessDialogKey(keyData);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (thumbnails is not null) LayoutViewer();
    }

    private void LayoutViewer()
    {
        int S(int value) => (int)Math.Round(value * DeviceDpi / 96d);
        int top = S(78), bottom = S(104), size = S(40), gap = S(8), margin = S(20);
        int x = ClientSize.Width - margin;
        foreach (Control control in new Control[] { close, save, fit, zoomIn, zoomOut })
        {
            x -= size;
            control.Bounds = new Rectangle(x, (top - size) / 2, size, size);
            x -= gap;
        }
        avatar.Bounds = new Rectangle(margin, S(17), S(44), S(44));
        int textLeft = avatar.Right + S(14);
        int textWidth = Math.Max(1, x - textLeft - gap);
        caption.Bounds = new Rectangle(textLeft, S(18), textWidth, S(23));
        detail.Bounds = new Rectangle(textLeft, S(43), textWidth, S(22));
        int side = S(70), arrow = S(48);
        int bodyHeight = Math.Max(1, Height - top - bottom);
        picture.Bounds = new Rectangle(side, top + S(6), Math.Max(1, Width - side * 2), Math.Max(1, bodyHeight - S(20)));
        previous.Bounds = new Rectangle(S(12), top + (bodyHeight - arrow) / 2, arrow, arrow);
        next.Bounds = new Rectangle(Width - S(12) - arrow, previous.Top, arrow, arrow);
        thumbnails.Bounds = new Rectangle(0, Math.Max(top, Height - bottom), Width, bottom);
    }

    internal static async Task SaveImageAsync(IWin32Window? owner, ChatAttachment attachment, byte[] bytes)
    {
        using var dialog = new SaveFileDialog
        {
            FileName = attachment.DisplayName, Filter = "All files (*.*)|*.*", Title = "Save image"
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        try { await File.WriteAllBytesAsync(dialog.FileName, bytes); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            if (owner is not Control control || !control.IsDisposed)
                MessageBox.Show(owner, error.Message, "Could not save image", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { tips.Dispose(); picture.Dispose(); image.Dispose(); }
        base.Dispose(disposing);
    }
}
