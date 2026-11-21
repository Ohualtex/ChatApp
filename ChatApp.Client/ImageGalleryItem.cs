namespace ChatApp.Client;

internal sealed class ImageGalleryItem(ChatAttachment attachment, byte[] bytes, string sender, DateTimeOffset? timestamp)
{
    public ChatAttachment Attachment { get; } = attachment;
    public byte[] Bytes { get; } = bytes;
    public string Sender { get; } = sender;
    public DateTimeOffset? Timestamp { get; } = timestamp;
}
