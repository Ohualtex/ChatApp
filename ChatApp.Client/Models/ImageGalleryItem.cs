namespace ChatApp.Client;

internal sealed class ImageGalleryItem(ChatAttachment attachment, byte[] bytes, string sender, DateTimeOffset? timestamp, Image preview)
{
    public ChatAttachment Attachment { get; } = attachment;
    public Image Preview { get; } = preview;
    public byte[] Bytes { get; } = bytes;
    public string Sender { get; } = sender;
    public DateTimeOffset? Timestamp { get; } = timestamp;
}
