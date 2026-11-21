using System.Runtime.InteropServices;

namespace ChatApp.Client;

internal sealed partial class ChatForm
{
    private readonly AttachmentDropOverlay attachmentDrop = new();
    private readonly HashSet<Control> attachmentDropTargets = [];
    private Panel? attachmentDropHost;
    private Control? attachmentHeader;
    private int attachmentDragGeneration;
    private bool preparingAttachments;
    private string? attachmentStatus;

    private bool CanAcceptFileDrop() => imageViewer is null && Enabled && connection is not null && !connecting && !sending && !preparingAttachments
        && !recordingBar.Active && selected.Online && chatContent is { Visible: true } && !closing.IsCancellationRequested;

    private void InitializeAttachmentDrop()
    {
        if (attachmentDropHost is null) return;
        RegisterAttachmentDropTarget(attachmentDropHost);
        attachmentDropHost.SizeChanged += (_, _) => UpdateAttachmentDropBounds();
        attachmentDropHost.VisibleChanged += (_, _) => { if (!attachmentDropHost.Visible) HideAttachmentDrop(); };
        Deactivate += (_, _) => HideAttachmentDrop();
    }

    private void RegisterAttachmentDropTarget(Control control)
    {
        if (!attachmentDropTargets.Add(control)) return;
        control.AllowDrop = true;
        control.DragEnter += AttachmentDragOver;
        control.DragOver += AttachmentDragOver;
        control.DragLeave += (_, _) =>
        {
            int generation = attachmentDragGeneration;
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() =>
            {
                if (!IsDisposed && generation == attachmentDragGeneration) HideAttachmentDrop();
            });
        };
        control.DragDrop += AttachmentDropped;
        control.ControlAdded += (_, e) => { if (e.Control is not null) RegisterAttachmentDropTarget(e.Control); };
        control.Disposed += (_, _) => attachmentDropTargets.Remove(control);
        foreach (Control child in control.Controls) RegisterAttachmentDropTarget(child);
    }

    private static string[] DropPaths(IDataObject? data)
    {
        try
        {
            return data?.GetData(DataFormats.FileDrop) is string[] paths
                ? paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() : [];
        }
        catch (ExternalException) { return []; }
    }

    private void AttachmentDragOver(object? sender, DragEventArgs e)
    {
        attachmentDragGeneration++;
        e.Effect = DragDropEffects.None;
        string[] paths = DropPaths(e.Data);
        if (!CanAcceptFileDrop() || paths.Length == 0 || paths.Any(Directory.Exists)
            || (e.AllowedEffect & DragDropEffects.Copy) == 0 || attachmentDropHost is null)
        { HideAttachmentDrop(); return; }
        UpdateAttachmentDropBounds();
        Point point = attachmentDropHost.PointToClient(new Point(e.X, e.Y));
        if (!attachmentDrop.Bounds.Contains(point)) { HideAttachmentDrop(); return; }
        e.Effect = DragDropEffects.Copy;
        attachmentDrop.Text = paths.Length == 1 ? "Drop file here" : "Drop files here";
        attachmentDrop.Visible = true;
        attachmentDrop.BringToFront();
    }

    private void UpdateAttachmentDropBounds()
    {
        if (attachmentDropHost is null || attachmentHeader is null) return;
        int top = attachmentHeader.Bottom;
        attachmentDrop.Bounds = new Rectangle(0, top, attachmentDropHost.ClientSize.Width,
            Math.Max(0, attachmentDropHost.ClientSize.Height - top));
    }

    private void HideAttachmentDrop()
    {
        attachmentDragGeneration++;
        attachmentDrop.Visible = false;
    }

    private async void AttachmentDropped(object? sender, DragEventArgs e)
    {
        string[] paths = DropPaths(e.Data);
        bool accepted = paths.Length > 0 && !paths.Any(Directory.Exists) && CanAcceptFileDrop() && attachmentDropHost is not null
            && attachmentDrop.Bounds.Contains(attachmentDropHost.PointToClient(new Point(e.X, e.Y)))
            && (e.AllowedEffect & DragDropEffects.Copy) != 0;
        HideAttachmentDrop();
        e.Effect = accepted && paths.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        if (e.Effect == DragDropEffects.Copy) await ReviewDroppedAttachmentsAsync(paths);
    }

    private async Task ReviewDroppedAttachmentsAsync(string[] paths)
    {
        if (!CanAcceptFileDrop() || connection is not { } current) return;
        Conversation destination = selected;
        CancellationToken token = closing.Token;
        preparingAttachments = true;
        attachmentStatus = "Preparing attachments…";
        RefreshControls();
        try
        {
            AttachmentCandidate[] candidates = await Task.Run(() => paths.Select(path =>
            {
                token.ThrowIfCancellationRequested();
                return AttachmentCandidate.Inspect(path);
            }).ToArray(), token);
            if (token.IsCancellationRequested || IsDisposed) return;
            if (!ReferenceEquals(connection, current) || !destination.Online)
            {
                MessageBox.Show(this, "The conversation is no longer online.", "Could not prepare attachments",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            IReadOnlyList<AttachmentCandidate> files;
            using (var review = new AttachmentReviewDialog(candidates, destination.Title) { Icon = Icon })
            {
                if (review.ShowDialog(this) != DialogResult.OK) return;
                files = review.SelectedFiles;
            }
            int sent = 0;
            string? failure = null;
            foreach (AttachmentCandidate file in files)
            {
                if (!ReferenceEquals(connection, current) || !destination.Online)
                { failure = "The conversation is no longer online."; break; }
                attachmentStatus = $"Sending file {sent + 1} of {files.Count}…";
                RefreshControls();
                try
                {
                    ChatAttachment attachment = await ChatAttachment.FromFileAsync(file.Path, token);
                    if (!ReferenceEquals(connection, current) || !destination.Online)
                    { failure = "The conversation is no longer online."; break; }
                    if (!await SendAttachmentAsync(attachment, destination))
                    { failure = "Sending stopped. Remaining files were not sent."; break; }
                    sent++;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { failure = error.Message; break; }
            }
            if (failure is not null && !token.IsCancellationRequested && !IsDisposed)
                MessageBox.Show(this, $"{sent} of {files.Count} file(s) sent.\n{failure}", "Attachment sending stopped",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            preparingAttachments = false;
            attachmentStatus = null;
            if (!IsDisposed && !token.IsCancellationRequested) RefreshControls();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (imageViewer is not null && imageViewer.HandleViewerKey(keyData)) return true;
        if (keyData == Keys.Escape && attachmentDrop.Visible) { HideAttachmentDrop(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
