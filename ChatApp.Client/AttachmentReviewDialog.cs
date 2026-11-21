namespace ChatApp.Client;

internal sealed class AttachmentReviewDialog : Form
{
    private static readonly Font NameFont = new("Segoe UI", 10, FontStyle.Bold);
    private static readonly Font DetailFont = new("Segoe UI", 9);
    private readonly ListBox files = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
        IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 64, BackColor = ChatTheme.Canvas };
    private readonly PictureBox preview = new() { Dock = DockStyle.Fill };
    private readonly Label previewHint = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = ChatTheme.Muted };
    private readonly Label summary = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    private readonly InteractiveButton send = new() { Text = "Send", AutoSize = true, DialogResult = DialogResult.OK };
    internal IReadOnlyList<AttachmentCandidate> SelectedFiles => files.Items.Cast<AttachmentCandidate>().ToArray();

    public AttachmentReviewDialog(IEnumerable<AttachmentCandidate> candidates, string destination)
    {
        Text = "Send attachments";
        ClientSize = new Size(760, 500);
        MinimumSize = new Size(640, 400);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 10);
        BackColor = ChatTheme.Canvas;
        ForeColor = ChatTheme.Ink;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.Controls.Add(new Label { Text = $"Files for {destination}", Dock = DockStyle.Fill,
            AutoEllipsis = true, Font = NameFont, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.Controls.Add(files, 0, 0);
        var imageHost = new Panel { Dock = DockStyle.Fill, BackColor = ChatTheme.Surface, Margin = new Padding(12, 0, 0, 0) };
        imageHost.Controls.Add(preview);
        imageHost.Controls.Add(previewHint);
        body.Controls.Add(imageHost, 1, 0);
        layout.Controls.Add(body, 0, 1);
        var footer = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        var remove = new InteractiveButton { Text = "Remove", AutoSize = true };
        var cancel = new InteractiveButton { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        foreach (Button button in new[] { remove, send, cancel })
        {
            button.BackColor = ReferenceEquals(button, send) ? ChatTheme.Accent : ChatTheme.Surface;
            button.ForeColor = ReferenceEquals(button, send) ? ChatTheme.Canvas : ChatTheme.Ink;
            button.Padding = new Padding(12, 8, 12, 8);
            button.Margin = new Padding(6, 14, 0, 0);
            actions.Controls.Add(button);
        }
        footer.Controls.Add(summary);
        footer.Controls.Add(actions);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        AcceptButton = send;
        CancelButton = cancel;
        files.AccessibleName = "Attachments to send";
        files.DrawItem += DrawFile;
        files.SelectedIndexChanged += (_, _) => UpdatePreview();
        files.DpiChangedAfterParent += (_, _) => files.ItemHeight = (int)Math.Round(64 * DeviceDpi / 96d);
        preview.SizeChanged += (_, _) => SizePreview();
        remove.Click += (_, _) =>
        {
            int index = files.SelectedIndex;
            if (index < 0) return;
            files.Items.RemoveAt(index);
            if (files.Items.Count > 0) files.SelectedIndex = Math.Min(index, files.Items.Count - 1);
            else UpdatePreview();
            UpdateSummary();
        };
        foreach (AttachmentCandidate candidate in candidates) files.Items.Add(candidate);
        if (files.Items.Count > 0) files.SelectedIndex = 0;
        UpdateSummary();
        Shown += (_, _) => ChatTheme.DarkTitle(this);
    }

    private void UpdateSummary()
    {
        var candidates = SelectedFiles;
        int errors = candidates.Count(file => file.Error is not null);
        summary.Text = $"{candidates.Count} file(s)" + (errors > 0 ? $" · {errors} invalid" : "");
        send.Enabled = candidates.Count > 0 && errors == 0;
    }

    private void DrawFile(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var file = (AttachmentCandidate)files.Items[e.Index];
        using var background = new SolidBrush(e.Index == files.SelectedIndex ? ChatTheme.Raised : ChatTheme.Canvas);
        e.Graphics.FillRectangle(background, e.Bounds);
        int inset = (int)Math.Round(10 * DeviceDpi / 96d);
        var name = new Rectangle(e.Bounds.Left + inset, e.Bounds.Top + inset, Math.Max(1, e.Bounds.Width - inset * 2), e.Bounds.Height / 2 - inset);
        var description = new Rectangle(name.Left, e.Bounds.Top + e.Bounds.Height / 2, name.Width, e.Bounds.Height / 2 - inset);
        const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, file.Name, NameFont, name, ChatTheme.Ink, flags);
        TextRenderer.DrawText(e.Graphics, file.Description, DetailFont, description,
            file.Error is null ? ChatTheme.Muted : Color.FromArgb(244, 144, 144), flags);
    }

    private void UpdatePreview()
    {
        Image? previous = preview.Image;
        preview.Image = null;
        previous?.Dispose();
        preview.Visible = false;
        previewHint.Visible = true;
        if (files.SelectedItem is not AttachmentCandidate file) { previewHint.Text = "No files selected"; return; }
        previewHint.Text = file.Error ?? file.Description;
        if (file.Error is not null || Path.GetExtension(file.Name).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp")) return;
        try
        {
            using var stream = File.OpenRead(file.Path);
            using var source = Image.FromStream(stream);
            if (source.Width > 4096 || source.Height > 4096) return;
            double scale = Math.Min(1, Math.Min(480d / source.Width, 480d / source.Height));
            preview.Image = new Bitmap(source, Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
            SizePreview();
            preview.Visible = true;
            previewHint.Visible = false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or OutOfMemoryException)
        { previewHint.Text = "Image preview unavailable"; }
    }

    private void SizePreview()
    {
        if (preview.Image is not { } image) return;
        preview.SizeMode = image.Width <= preview.Width && image.Height <= preview.Height ? PictureBoxSizeMode.CenterImage : PictureBoxSizeMode.Zoom;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { preview.Image?.Dispose(); preview.Image = null; }
        base.Dispose(disposing);
    }
}
