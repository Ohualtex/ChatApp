using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;

namespace ChatApp.Client;

internal sealed record VoiceChunk(string Id, string? Data = null, long? Length = null);

internal sealed record ChatPacket(string Type, string? Name = null, string? Message = null, DateTimeOffset? Timestamp = null,
    string[]? Users = null, string? Recipient = null, ChatAttachment? Attachment = null, VoiceChunk? Voice = null,
    [property: JsonIgnore] VoiceClip? Recording = null);

internal sealed class ChatConnection : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly TcpClient tcp = new();
    private StreamReader? reader;
    private StreamWriter? writer;
    private readonly SemaphoreSlim writeGate = new(1, 1);

    public async Task<string> ConnectAsync(string host, int port, string name, CancellationToken token)
    {
        await tcp.ConnectAsync(host, port, token);
        NetworkStream stream = tcp.GetStream();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        reader = new StreamReader(stream, utf8, leaveOpen: true);
        writer = new StreamWriter(stream, utf8, leaveOpen: true);
        await SendAsync(new ChatPacket("join", Name: name), token);
        ChatPacket? response = await ReadAsync(token);
        if (response is not { Type: "joined", Name: { Length: > 0 } })
        {
            throw new IOException(response?.Message ?? "The server did not accept the connection.");
        }

        return response.Name;
    }

    public async Task SendAsync(ChatPacket packet, CancellationToken token)
    {
        await writeGate.WaitAsync(token);
        try { await WritePacketAsync(packet, token); }
        finally { writeGate.Release(); }
    }

    private async Task WritePacketAsync(ChatPacket packet, CancellationToken token)
    {
        StreamWriter output = writer ?? throw new InvalidOperationException("Connect before sending messages.");
        await output.WriteLineAsync(JsonSerializer.Serialize(packet, Json).AsMemory(), token);
        await output.FlushAsync(token);
    }

    public async Task SendVoiceAsync(VoiceClip clip, string? recipient, CancellationToken token)
    {
        await writeGate.WaitAsync(token);
        try
        {
            await using var file = new FileStream(clip.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536, true);
            string id = Guid.NewGuid().ToString("N");
            await WritePacketAsync(new ChatPacket("voice_start", Recipient: recipient,
                Voice: new VoiceChunk(id, Length: file.Length)), token);
            byte[] buffer = new byte[65536];
            int count;
            while ((count = await file.ReadAsync(buffer, token)) > 0)
                await WritePacketAsync(new ChatPacket("voice_chunk", Voice: new VoiceChunk(id,
                    Convert.ToBase64String(buffer, 0, count))), token);
            await WritePacketAsync(new ChatPacket("voice_end", Voice: new VoiceChunk(id)), token);
        }
        finally { writeGate.Release(); }
    }

    public async Task<ChatPacket?> ReadAsync(CancellationToken token)
    {
        StreamReader input = reader ?? throw new InvalidOperationException("Connect before reading messages.");
        string? line = await input.ReadLineAsync(token);
        return line is null ? null : JsonSerializer.Deserialize<ChatPacket>(line, Json)
            ?? throw new IOException("The server sent an empty message.");
    }

    public void Dispose() => tcp.Dispose();
}
