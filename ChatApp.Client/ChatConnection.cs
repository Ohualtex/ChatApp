using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ChatApp.Client;

internal sealed record ChatPacket(string Type, string? Name = null, string? Message = null, DateTimeOffset? Timestamp = null);

internal sealed class ChatConnection : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly TcpClient tcp = new();
    private StreamReader? reader;
    private StreamWriter? writer;

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
        StreamWriter output = writer ?? throw new InvalidOperationException("Connect before sending messages.");
        await output.WriteLineAsync(JsonSerializer.Serialize(packet, Json).AsMemory(), token);
        await output.FlushAsync(token);
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
