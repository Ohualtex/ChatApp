namespace ChatApp.Client;

internal sealed class ImageViewer : Form
{
    private Bitmap image;
    private readonly ImageGalleryItem[] items;
    private int currentIndex;
    private ImageGalleryItem CurrentItem => items[currentIndex];
    private readonly Label caption = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly InteractiveButton previous = new() { Text = "\u2039", AccessibleName = "Previous image", Size = new Size(36, 44), Anchor = AnchorStyles.None };
    private readonly InteractiveButton next = new() { Text = "\u203a", AccessibleName = "Next image", Size = new Size(36, 44), Anchor = AnchorStyles.None };
    private readonly ZoomImageCanvas picture;
    private readonly InteractiveButton zoomOut = new() { Text = "\u2212", AccessibleName = "Zoom out", Size = new Size(36, 36) };
    private readonly InteractiveButton zoomIn = new() { Text = "+", AccessibleName = "Zoom in", Size = new Size(36, 36) };
    private readonly InteractiveButton fit = new() { AccessibleName = "Fit image to window", Size = new Size(64, 36) };
    private readonly ToolTip tips = new();

    public ImageViewer(ChatAttachment attachment, byte[] bytes, string sender, DateTimeOffset? timestamp)
        : this([new ImageGalleryItem(attachment, bytes, sender, timestamp)], 0) { }

    public ImageViewer(IReadOnlyList<ImageGalleryItem> gallery, int index)
    {
        if (gallery.Count == 0) throw new ArgumentException("The image gallery is empty.", nameof(gallery));
        if (index < 0 || index >= gallery.Count) throw new ArgumentOutOfRangeException(nameof(index));
        items = gallery.ToArray();
        currentIndex = index;
        image = Decode(CurrentItem);
        picture = new ZoomImageCanvas(image) { Dock = DockStyle.Fill };
        BackColor = ChatTheme.Canvas;
        ForeColor = ChatTheme.Ink;
        Font = new Font("Segoe UI", 10);
        MinimumSize = new Size(640, 440);
        ClientSize = new Size(1000, 720);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 72, ColumnCount = 6, RowCount = 1,
            Padding = new Padding(20, 10, 12, 10), BackColor = ChatTheme.Canvas
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int column = 0; column < 5; column++)
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var save = new InteractiveButton { Text = "Save as\u2026", AutoSize = true, AccessibleName = "Save image as" };
        var close = new InteractiveButton { Text = "\u00d7", Size = new Size(36, 36), AccessibleName = "Close image", DialogResult = DialogResult.Cancel };
        foreach (Button button in new[] { zoomOut, zoomIn, fit, save, close })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ChatTheme.Raised;
            button.BackColor = ChatTheme.Canvas;
            button.ForeColor = ChatTheme.Ink;
            button.Anchor = AnchorStyles.None;
            button.Padding = new Padding(12, 6, 12, 6);
        }
        save.Click += async (_, _) =>
        {
            save.Enabled = false;
            try
            {
                ImageGalleryItem item = CurrentItem;
                await SaveImageAsync(this, item.Attachment, item.Bytes);
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
        CancelButton = close;
        header.Controls.Add(caption, 0, 0);
        header.Controls.Add(zoomOut, 1, 0);
        header.Controls.Add(zoomIn, 2, 0);
        header.Controls.Add(fit, 3, 0);
        header.Controls.Add(save, 4, 0);
        header.Controls.Add(close, 5, 0);
        var canvas = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 20), ColumnCount = 3, RowCount = 1
        };
        int sideWidth = items.Length > 1 ? 48 : 0;
        canvas.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, sideWidth));
        canvas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        canvas.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, sideWidth));
        previous.Visible = next.Visible = items.Length > 1;
        canvas.Controls.Add(previous, 0, 0);
        canvas.Controls.Add(picture, 1, 0);
        canvas.Controls.Add(next, 2, 0);
        Controls.Add(canvas);
        Controls.Add(header);
        Shown += (_, _) => ChatTheme.DarkTitle(this);
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
        Text = picture.AccessibleName = item.Attachment.DisplayName;
        string date = item.Timestamp?.ToLocalTime().ToString("dd MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? "";
        caption.Text = item.Sender + "\n" + date + (items.Length > 1 ? $"  \u00b7  {currentIndex + 1} / {items.Length}" : "");
        previous.Enabled = currentIndex > 0;
        next.Enabled = currentIndex < items.Length - 1;
    }

    private void Navigate(int direction)
    {
        int target = currentIndex + direction;
        if (target < 0 || target >= items.Length) return;
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
        fit.Text = $"{picture.ZoomPercent:0}%";
        zoomOut.Enabled = picture.ZoomFactor > 1.00001;
        zoomIn.Enabled = picture.ZoomFactor < 7.99999;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
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
        return base.ProcessCmdKey(ref msg, keyData);
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
