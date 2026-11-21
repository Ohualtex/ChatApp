namespace ChatApp.Client;

internal sealed class ImageViewer : Form
{
    private readonly Bitmap image;
    private readonly PictureBox picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };

    public ImageViewer(ChatAttachment attachment, byte[] bytes, string sender, DateTimeOffset? timestamp)
    {
        using var stream = new MemoryStream(bytes);
        using var source = Image.FromStream(stream);
        image = new Bitmap(source);
        Text = attachment.DisplayName;
        BackColor = ChatTheme.Canvas;
        ForeColor = ChatTheme.Ink;
        Font = new Font("Segoe UI", 10);
        MinimumSize = new Size(640, 440);
        ClientSize = new Size(1000, 720);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = 72, ColumnCount = 3, RowCount = 1,
            Padding = new Padding(20, 10, 12, 10), BackColor = ChatTheme.Canvas
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var caption = new Label
        {
            Text = sender + "\n" + (timestamp?.ToLocalTime().ToString("dd MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? ""),
            Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
        };
        var save = new InteractiveButton { Text = "Save as…", AutoSize = true, AccessibleName = "Save image as" };
        var close = new InteractiveButton { Text = "×", AutoSize = true, AccessibleName = "Close image", DialogResult = DialogResult.Cancel };
        foreach (Button button in new[] { save, close })
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
            try { await SaveImageAsync(this, attachment, bytes); }
            finally { if (!IsDisposed) save.Enabled = true; }
        };
        CancelButton = close;
        header.Controls.Add(caption, 0, 0);
        header.Controls.Add(save, 1, 0);
        header.Controls.Add(close, 2, 0);
        var canvas = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 8, 20, 20) };
        picture.Image = image;
        picture.AccessibleName = attachment.DisplayName;
        canvas.Controls.Add(picture);
        Controls.Add(canvas);
        Controls.Add(header);
        Shown += (_, _) => ChatTheme.DarkTitle(this);
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
        if (disposing) { picture.Image = null; image.Dispose(); }
        base.Dispose(disposing);
    }
}
