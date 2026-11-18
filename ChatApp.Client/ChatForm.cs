using System.Net.Sockets;
using System.Text.Json;

namespace ChatApp.Client;

internal sealed class ChatForm : Form
{
    private readonly TextBox user = new() { PlaceholderText = "User name", Width = 125, MaxLength = 24 };
    private readonly TextBox host = new() { Text = "127.0.0.1", Width = 125 };
    private readonly NumericUpDown port = new() { Minimum = 1, Maximum = 65535, Value = 5050, Width = 72 };
    private readonly Label status = new() { Text = "Disconnected", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button connect = new() { Text = "Connect", AutoSize = true };
    private readonly Button disconnect = new() { Text = "Disconnect", AutoSize = true };
    private readonly ListBox messages = new() { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Messages" };
    private readonly TextBox composer = new() { Dock = DockStyle.Fill, MaxLength = 1000, PlaceholderText = "Type a message" };
    private readonly Button send = new() { Text = "Send", AutoSize = true };
    private readonly CancellationTokenSource closing = new();
    private ChatConnection? connection;
    private bool connecting;
    private bool sending;

    public ChatForm()
    {
        Text = "ChatApp";
        MinimumSize = new Size(590, 385);
        ClientSize = new Size(690, 480);
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 4 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var settings = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        settings.Controls.AddRange([user, host, port, connect, disconnect]);
        layout.Controls.Add(settings, 0, 0);
        layout.SetColumnSpan(settings, 2);
        layout.Controls.Add(status, 0, 1);
        layout.SetColumnSpan(status, 2);
        layout.Controls.Add(messages, 0, 2);
        layout.SetColumnSpan(messages, 2);
        layout.Controls.Add(composer, 0, 3);
        layout.Controls.Add(send, 1, 3);
        Controls.Add(layout);
        connect.Click += async (_, _) => await ConnectAndListenAsync();
        disconnect.Click += (_, _) => Disconnect("Disconnected");
        send.Click += async (_, _) => await SendAsync();
        composer.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };
        composer.TextChanged += (_, _) => RefreshControls();
        FormClosed += (_, _) =>
        {
            closing.Cancel();
            connection?.Dispose();
            closing.Dispose();
        };
        RefreshControls();
    }

    private async Task ConnectAndListenAsync()
    {
        if (connection is not null || connecting)
        {
            return;
        }

        string name = user.Text.Trim();
        string address = host.Text.Trim();
        if (name.Length == 0 || address.Length == 0)
        {
            status.Text = "Enter a user name and server address.";
            return;
        }

        using var next = new ChatConnection();
        CancellationToken lifetime = closing.Token;
        connection = next;
        connecting = true;
        status.Text = "Connecting…";
        RefreshControls();
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            handshake.CancelAfter(TimeSpan.FromSeconds(6));
            await next.ConnectAsync(address, (int)port.Value, name, handshake.Token);
            connecting = false;
            status.Text = $"Connected as {name}";
            RefreshControls();
            composer.Focus();
            while (await next.ReadAsync(lifetime) is { } packet && ReferenceEquals(connection, next) && !lifetime.IsCancellationRequested)
            {
                if (packet.Type == "chat")
                {
                    string time = packet.Timestamp?.ToLocalTime().ToString("HH:mm:ss") ?? "--:--:--";
                    AddLine($"{time}  {packet.Name}: {packet.Message}");
                }
                else if (packet.Type == "error")
                {
                    AddLine(packet.Message ?? "The server rejected a message.");
                }
            }

            if (ReferenceEquals(connection, next))
            {
                Disconnect("The server closed the connection.");
            }
        }
        catch (Exception error) when (error is IOException or SocketException or JsonException or OperationCanceledException or ObjectDisposedException)
        {
            if (!lifetime.IsCancellationRequested && ReferenceEquals(connection, next))
            {
                Disconnect(error is OperationCanceledException ? "The connection timed out." : error.Message);
            }
        }
    }

    private async Task SendAsync()
    {
        if (connection is not { } current || connecting || sending || string.IsNullOrWhiteSpace(composer.Text))
        {
            return;
        }

        string text = composer.Text;
        sending = true;
        RefreshControls();
        CancellationToken lifetime = closing.Token;
        try
        {
            await current.SendAsync(new ChatPacket("chat", Message: text.Trim()), lifetime);
            if (ReferenceEquals(connection, current) && composer.Text == text)
            {
                composer.Clear();
            }
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            if (!lifetime.IsCancellationRequested && ReferenceEquals(connection, current))
            {
                Disconnect(error.Message);
            }
        }
        finally
        {
            sending = false;
            if (!lifetime.IsCancellationRequested)
            {
                RefreshControls();
            }
        }
    }

    private void Disconnect(string message)
    {
        ChatConnection? previous = connection;
        connection = null;
        connecting = false;
        previous?.Dispose();
        status.Text = message;
        RefreshControls();
    }

    private void AddLine(string text)
    {
        messages.Items.Add(text);
        messages.TopIndex = messages.Items.Count - 1;
    }

    private void RefreshControls()
    {
        bool offline = connection is null && !connecting;
        user.Enabled = host.Enabled = port.Enabled = connect.Enabled = offline;
        bool connected = connection is not null && !connecting;
        disconnect.Enabled = composer.Enabled = connected;
        send.Enabled = connected && !sending && !string.IsNullOrWhiteSpace(composer.Text);
    }
}
