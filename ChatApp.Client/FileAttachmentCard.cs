using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed class FileAttachmentCard : RoundedPanel
{
    private static readonly Font NameFont = new("Segoe UI", 10, FontStyle.Bold);
    private static readonly Font DetailFont = new("Segoe UI", 9);
    private static readonly Font ActionFont = new("Segoe UI", 10, FontStyle.Bold);
    private readonly ChatAttachment attachment;
    private readonly byte[] bytes;
    private readonly Label fileName = new() { AutoEllipsis = true, UseMnemonic = false, Font = NameFont };
    private readonly Label detail = new() { AutoEllipsis = true, UseMnemonic = false, Font = DetailFont };
    private readonly Label clock = new() { Font = DetailFont, TextAlign = ContentAlignment.MiddleRight };
    private readonly Button open = new() { Text = "Open", AccessibleName = "Open attachment" };
    private readonly Button save = new() { Text = "Save as…", AccessibleName = "Save attachment as" };
    private readonly FileTypeIcon icon;
    private readonly ToolTip tips = new();
    private readonly bool supportsOpen;
    private string? openPath;

    public FileAttachmentCard(ChatAttachment attachment, byte[] bytes, string time, bool outgoing)
    {
        this.attachment = attachment;
        this.bytes = bytes;
        string extension = Path.GetExtension(attachment.DisplayName).TrimStart('.').ToUpperInvariant();
        icon = new FileTypeIcon(extension);
        supportsOpen = CanOpen(attachment.DisplayName);
        BackColor = outgoing ? Color.FromArgb(17, 65, 51) : Color.FromArgb(37, 39, 39);
        fileName.Text = attachment.DisplayName;
        fileName.ForeColor = ChatTheme.Ink;
        detail.Text = (extension.Length == 0 ? "FILE" : extension) + " · " + FormatSize(bytes.Length);
        detail.ForeColor = clock.ForeColor = ChatTheme.Muted;
        clock.Text = time;
        AccessibleName = $"{attachment.DisplayName}, {detail.Text}";
        tips.SetToolTip(fileName, attachment.DisplayName);
        tips.SetToolTip(open, supportsOpen ? "Open with the default application" : "Save this file to use it outside ChatApp");
        foreach (Button button in new[] { open, save })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = outgoing ? ChatTheme.Sent : ChatTheme.Raised;
            button.BackColor = BackColor;
            button.ForeColor = ChatTheme.Accent;
            button.Font = ActionFont;
            button.UseVisualStyleBackColor = false;
        }
        open.Enabled = supportsOpen;
        open.Click += async (_, _) => await OpenAsync();
        save.Click += async (_, _) => await SaveAsync();
        Controls.AddRange([icon, fileName, detail, clock, open, save]);
    }

    internal static bool CanOpen(string name) => Path.GetExtension(name).ToLowerInvariant() is
        ".txt" or ".pdf" or ".docx" or ".xlsx" or ".pptx" or ".csv" or ".rtf"
        or ".odt" or ".ods" or ".odp" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp"
        or ".mp4" or ".mov" or ".webm" or ".wav" or ".mp3" or ".flac" or ".ogg"
        or ".zip" or ".7z" or ".rar";

    private static string FormatSize(int length) => length < 1024 ? $"{length} B"
        : length < 1024 * 1024 ? $"{length / 1024d:0.#} KB" : $"{length / (1024d * 1024):0.#} MB";

    internal void ResizeTo(int width)
    {
        int Scale(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96d));
        Size = new Size(Math.Max(1, width), Scale(128));
        int inset = Scale(12), left = Scale(60), actionTop = Scale(88);
        icon.Bounds = new Rectangle(inset, Scale(18), Scale(32), Scale(40));
        fileName.Bounds = new Rectangle(left, Scale(14), Math.Max(1, Width - left - inset), Scale(26));
        detail.Bounds = new Rectangle(left, Scale(40), Math.Max(1, Width - left - inset), Scale(23));
        clock.Bounds = new Rectangle(inset, Scale(64), Math.Max(1, Width - inset * 2), Scale(20));
        int middle = Width / 2;
        open.Bounds = new Rectangle(0, actionTop, Math.Max(1, middle), Height - actionTop);
        save.Bounds = new Rectangle(middle + 1, actionTop, Math.Max(1, Width - middle - 1), Height - actionTop);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        int top = open.Top;
        using var line = new Pen(Parent?.BackColor ?? ChatTheme.Canvas);
        e.Graphics.DrawLine(line, 0, top, Width, top);
    }

    private async Task SaveAsync()
    {
        using var dialog = new SaveFileDialog
        {
            FileName = attachment.DisplayName, Filter = "All files (*.*)|*.*", Title = "Save attachment"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        SetBusy(true);
        try { await File.WriteAllBytesAsync(dialog.FileName, bytes); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { ShowError(error, "Could not save attachment"); }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private async Task OpenAsync()
    {
        if (!supportsOpen) return;
        SetBusy(true);
        try
        {
            if (openPath is null || !File.Exists(openPath))
            {
                string directory = Path.Combine(Path.GetTempPath(), "ChatApp", "attachments", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string name = attachment.DisplayName;
                if (string.IsNullOrWhiteSpace(name)) name = "attachment";
                string path = Path.Combine(directory, name);
                await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    await file.WriteAsync(bytes);
                openPath = path;
            }
            using Process? process = Process.Start(new ProcessStartInfo(openPath) { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        { ShowError(error, "Could not open attachment"); }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        open.Enabled = !busy && supportsOpen;
        save.Enabled = !busy;
    }

    private void ShowError(Exception error, string title)
    {
        if (!IsDisposed) MessageBox.Show(FindForm(), error.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }

    private sealed class FileTypeIcon(string extension) : Control
    {
        private static readonly Font TypeFont = new("Segoe UI", 7, FontStyle.Bold);

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = extension switch
            {
                "PDF" => Color.FromArgb(219, 76, 79),
                "DOCX" => Color.FromArgb(58, 126, 211),
                "XLSX" or "CSV" => Color.FromArgb(50, 157, 104),
                "PPTX" => Color.FromArgb(218, 123, 68),
                _ => Color.FromArgb(108, 125, 132)
            };
            float fold = Width * .3f;
            using var paper = new SolidBrush(color);
            PointF[] outline = [new(1, 1), new(Width - fold, 1), new(Width - 1, fold), new(Width - 1, Height - 1), new(1, Height - 1)];
            e.Graphics.FillPolygon(paper, outline);
            using var corner = new SolidBrush(Color.FromArgb(110, Color.White));
            e.Graphics.FillPolygon(corner, [new PointF(Width - fold, 1), new(Width - fold, fold), new(Width - 1, fold)]);
            string text = extension.Length is > 0 and <= 4 ? extension : "FILE";
            TextRenderer.DrawText(e.Graphics, text, TypeFont, new Rectangle(0, Height / 3, Width, Height * 2 / 3), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
