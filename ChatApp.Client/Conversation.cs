namespace ChatApp.Client;

internal sealed class Conversation(string? peer)
{
    public string? Peer { get; } = peer;
    public string Title => Peer ?? "General";
    public List<ChatPacket> Messages { get; } = [];
    public int Unread { get; set; }
    public string Draft { get; set; } = "";
    public bool Online { get; set; } = peer is null;
    public string Preview => Messages.Count > 0
        ? (Messages[^1].Attachment is { ContentType: "audio/wav" } ? "Voice message" : Messages[^1].Attachment?.FileName ?? Messages[^1].Message ?? "").ReplaceLineEndings(" ")
        : Peer is null ? "Shared room" : Online ? "Online" : "Offline";

    public override string ToString() => Title;
}
