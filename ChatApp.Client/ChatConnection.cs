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
    private readonly Dictionary<(string, string), IncomingVoice> incoming = [];

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
        while (true)
        {
            string? line = await input.ReadLineAsync(token);
            if (line is null) { ClearIncoming(); return null; }
            ChatPacket packet = JsonSerializer.Deserialize<ChatPacket>(line, Json)
                ?? throw new IOException("The server sent an empty message.");
            if (packet.Type is not ("voice_start" or "voice_chunk" or "voice_end" or "voice_abort")) return packet;
            if (packet.Voice is not { } voice || packet.Name is null) continue;
            var key = (packet.Name, voice.Id);
            try
            {
                if (packet.Type == "voice_abort")
                {
                    if (incoming.Remove(key, out IncomingVoice? abandoned)) abandoned.Dispose();
                }
                else if (packet.Type == "voice_start")
                {
                    // Each sender uploads one recording at a time; discard an abandoned prefix.
                    foreach (var stale in incoming.Keys.Where(k => k.Item1 == packet.Name).ToArray())
                    { incoming[stale].Dispose(); incoming.Remove(stale); }
                    if (voice.Length is not >= 44 or > 4_294_967_303)
                        throw new IOException("The recording length is invalid.");
                    incoming[key] = new IncomingVoice(packet, voice.Length.Value);
                }
                else if (incoming.TryGetValue(key, out IncomingVoice? transfer))
                {
                    if (packet.Type == "voice_chunk") await transfer.AppendAsync(voice.Data, token);
                    else
                    {
                        incoming.Remove(key);
                        using (transfer)
                        {
                            VoiceClip clip = transfer.Complete();
                            return transfer.Started with { Type = "voice", Voice = null, Recording = clip };
                        }
                    }
                }
            }
            catch (Exception error) when (error is IOException or FormatException or UnauthorizedAccessException)
            {
                if (incoming.Remove(key, out IncomingVoice? failed)) failed.Dispose();
                return new ChatPacket("error", Message: "Could not receive voice message: " + error.Message);
            }
        }
    }

    private void ClearIncoming()
    {
        foreach (IncomingVoice transfer in incoming.Values) transfer.Dispose();
        incoming.Clear();
    }

    public void Dispose()
    {
        tcp.Dispose();
        ClearIncoming();
    }

    private sealed class IncomingVoice : IDisposable
    {
        private readonly string path = VoiceClip.NewPath();
        private readonly FileStream file;
        private readonly long expected;
        private bool retained;
        public ChatPacket Started { get; }

        public IncomingVoice(ChatPacket started, long expected)
        {
            Started = started;
            this.expected = expected;
            file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
        }

        public async Task AppendAsync(string? data, CancellationToken token)
        {
            if (data is null || data.Length > 87384) throw new IOException("Invalid audio chunk.");
            byte[] bytes = Convert.FromBase64String(data);
            if (bytes.Length is 0 or > 65536 || file.Position + bytes.Length > expected)
                throw new IOException("Invalid audio chunk length.");
            await file.WriteAsync(bytes, token);
        }

        public VoiceClip Complete()
        {
            if (file.Position != expected) throw new IOException("The recording is incomplete.");
            file.Dispose();
            var clip = new VoiceClip(path);
            retained = true;
            return clip;
        }

        public void Dispose()
        {
            file.Dispose();
            if (!retained) VoiceClip.DeleteTemporary(path);
        }
    }
}
