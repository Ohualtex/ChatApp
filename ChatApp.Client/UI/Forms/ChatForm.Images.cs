namespace ChatApp.Client;

internal sealed partial class ChatForm
{
    private ImageViewer? imageViewer;
    private Control? imageReturnFocus;
    private Control? imageCoveredLayout;

    internal void OpenImageViewer(IReadOnlyList<ImageGalleryItem> items, int index, Control source)
    {
        if (imageViewer is not null || IsDisposed || closing.IsCancellationRequested) return;
        var viewer = new ImageViewer(items, index);
        imageReturnFocus = source;
        imageCoveredLayout = chatRoot;
        imageViewer = viewer;
        viewer.CloseRequested += (_, _) => CloseImageViewer();
        HideAttachmentDrop();
        Controls.Add(viewer);
        viewer.BringToFront();
        // Keep the existing controls alive to preserve drafts and scroll position,
        // but remove the covered layout from mouse and keyboard interaction.
        imageCoveredLayout.Visible = false;
        imageCoveredLayout.Enabled = false;
        viewer.Select();
        viewer.SelectNextControl(null, true, true, true, true);
    }

    private void CloseImageViewer()
    {
        if (imageViewer is null) return;
        ImageViewer viewer = imageViewer;
        imageViewer = null;
        viewer.Visible = false;
        Controls.Remove(viewer);
        viewer.Dispose();
        if (imageCoveredLayout is { IsDisposed: false } layout)
        {
            layout.Enabled = true;
            layout.Visible = true;
        }
        // A disconnect may have removed the source card while the viewer was open.
        if (imageReturnFocus is { IsDisposed: false, CanFocus: true } source) source.Focus();
        else composer.Focus();
        imageCoveredLayout = null;
        imageReturnFocus = null;
    }
}
