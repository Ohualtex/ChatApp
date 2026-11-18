using System.Net.Sockets;
using System.Text.Json;

namespace ChatApp.Client;

internal sealed class ChatForm : Form
{
    private static readonly Icon WindowIcon = new(typeof(ChatForm), "Assets.app.ico");

    private static readonly Color Accent = Color.FromArgb(8, 111, 89);
    private static readonly Color Canvas = Color.FromArgb(243, 241, 237);
    private static readonly Color Ink = Color.FromArgb(23, 43, 37);
    private static readonly Color Muted = Color.FromArgb(79, 99, 90);
    private static readonly Bitmap Logo = WindowIcon.ToBitmap();
    private static readonly Font TitleFont = new("Segoe UI", 15, FontStyle.Bold);
    private static readonly Font BodyFont = new("Segoe UI", 10);
    private static readonly Font ActionFont = new("Segoe UI", 10, FontStyle.Bold);

    private readonly TextBox user = new() { PlaceholderText = "User name", Dock = DockStyle.Fill, MaxLength = 24 };
    private readonly TextBox host = new() { Text = "127.0.0.1", Dock = DockStyle.Fill };
    private readonly NumericUpDown port = new() { Minimum = 1, Maximum = 65535, Value = 5050, Width = 72 };
    private readonly Label status = new() { Text = "Disconnected", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly Button connect = new() { Text = "Connect", AutoSize = true };
    private readonly Button membersToggle = new() { Text = "General", AutoSize = true };
    private readonly Panel membersDrawer = new() { Dock = DockStyle.Left, Width = 205, BackColor = Color.White };
    private readonly Label memberCount = new() { AutoSize = true, ForeColor = Muted };
    private readonly ListBox memberList = new()
    {
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false,
        AccessibleName = "Online members"
    };
    private readonly Button closeMembers = new() { Text = "×", AccessibleName = "Close members", Size = new Size(36, 36) };
    private readonly Button disconnect = new() { Text = "Disconnect", AutoSize = true };
    private readonly MessageFeed messages = new();
    private readonly Label loginStatus = new() { AutoSize = true, MaximumSize = new Size(336, 0) };
    private Control login = null!;
    private Control composerBar = null!;
    private readonly TextBox composer = new() { Dock = DockStyle.Fill, MaxLength = 1000, PlaceholderText = "Type a message" };
    private readonly Button send = new() { Text = "Send", AutoSize = true };
    private readonly CancellationTokenSource closing = new();
    private ChatConnection? connection;
    private bool connecting;
    private bool sending;

    public ChatForm()
    {
        Text = "ChatApp";
        Icon = WindowIcon;
        MinimumSize = new Size(590, 385);
        ClientSize = new Size(780, 570);
        StartPosition = FormStartPosition.CenterScreen;
        Font = BodyFont;
        ForeColor = Ink;
        BackColor = Canvas;
        Controls.Add(BuildLayout());
        connect.Click += async (_, _) => await ConnectAndListenAsync();
        membersToggle.Click += (_, _) => SetMembersDrawer(!membersDrawer.Visible);
        closeMembers.Click += (_, _) => SetMembersDrawer(false);
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
            loginStatus.Text = "Enter a user name and server address.";
            return;
        }

        using var next = new ChatConnection();
        CancellationToken lifetime = closing.Token;
        connection = next;
        connecting = true;
        loginStatus.Text = "Connecting…";
        RefreshControls();
        try
        {
            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            handshake.CancelAfter(TimeSpan.FromSeconds(6));
            string joinedName = await next.ConnectAsync(address, (int)port.Value, name, handshake.Token);
            messages.ClearMessages();
            loginStatus.Text = "";
            connecting = false;
            RefreshControls();
            composer.Focus();
            while (await next.ReadAsync(lifetime) is { } packet && ReferenceEquals(connection, next) && !lifetime.IsCancellationRequested)
            {
                if (packet.Type == "chat")
                {
                    messages.AddMessage(packet.Name ?? "Unknown", packet.Message ?? "", packet.Timestamp, packet.Name == joinedName);
                }
                else if (packet is { Type: "presence", Users: { } users })
                {
                    UpdateMembers(users, joinedName);
                }
                else if (packet.Type == "error")
                {
                    messages.AddNotice(packet.Message ?? "The server rejected a message.");
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
        SetMembersDrawer(false);
        UpdateMembers([], "");
        loginStatus.Text = message;
        RefreshControls();
    }

    private void UpdateMembers(string[] names, string joinedName)
    {
        memberList.BeginUpdate();
        try
        {
            memberList.Items.Clear();
            memberList.Items.AddRange(names.Select(name => name == joinedName ? $"{name} (you)" : name).ToArray());
            memberCount.Text = $"{names.Length} online";
        }
        finally
        {
            memberList.EndUpdate();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && membersDrawer.Visible)
        {
            SetMembersDrawer(false);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SetMembersDrawer(bool open)
    {
        membersDrawer.Visible = open;
        membersToggle.AccessibleName = open ? "General, hide online members" : "General, show online members";
        if (!open && membersToggle.Enabled)
        {
            membersToggle.Focus();
        }
    }

    private void RefreshControls()
    {
        bool offline = connection is null && !connecting;
        bool connected = connection is not null && !connecting;
        user.Enabled = host.Enabled = port.Enabled = connect.Enabled = offline;
        status.Text = connected ? "Online" : connecting ? "Connecting…" : "Offline";
        status.ForeColor = connected ? Accent : Muted;
        login.Visible = !connected;
        messages.Visible = composerBar.Visible = disconnect.Visible = connected;
        membersToggle.Enabled = connected;
        composer.Enabled = connected;
        send.Enabled = connected && !sending && !string.IsNullOrWhiteSpace(composer.Text);
    }

    private Control BuildLayout()
    {
        StyleButton(connect, primary: true);
        StyleButton(send, primary: true);
        StyleButton(disconnect, primary: false);
        StyleButton(membersToggle, primary: false);
        membersToggle.Font = TitleFont;
        membersToggle.BackColor = Color.White;
        membersToggle.Padding = Padding.Empty;
        membersToggle.AccessibleName = "General, show online members";
        membersToggle.Anchor = AnchorStyles.Left;
        StyleButton(closeMembers, primary: false);
        closeMembers.Padding = Padding.Empty;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 4, Margin = Padding.Empty,
            Padding = new Padding(18, 8, 16, 8), BackColor = Color.White
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new PictureBox
        {
            Image = Logo, Size = new Size(34, 34), SizeMode = PictureBoxSizeMode.Zoom,
            Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0)
        }, 0, 0);
        header.Controls.Add(membersToggle, 1, 0);
        status.Margin = new Padding(8, 0, 12, 0);
        header.Controls.Add(status, 2, 0);
        disconnect.Anchor = AnchorStyles.Right;
        header.Controls.Add(disconnect, 3, 0);

        var body = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Canvas };
        login = BuildLogin();
        body.Controls.Add(messages);
        BuildMembersDrawer();
        body.Controls.Add(membersDrawer);
        body.Controls.Add(login);

        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = Padding.Empty,
            Padding = new Padding(14, 9, 14, 9), BackColor = Color.White
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        composer.Margin = new Padding(0, 5, 10, 0);
        composer.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        bar.Controls.Add(composer, 0, 0);
        bar.Controls.Add(send, 1, 0);
        composerBar = bar;
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(body, 0, 1);
        layout.Controls.Add(bar, 0, 2);
        return layout;
    }

    private void BuildMembersDrawer()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Members", AutoSize = true, Font = TitleFont, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(closeMembers, 1, 0);
        layout.Controls.Add(memberCount, 0, 1);
        layout.SetColumnSpan(memberCount, 2);
        memberList.Margin = new Padding(3, 9, 3, 0);
        layout.Controls.Add(memberList, 0, 2);
        layout.SetColumnSpan(memberList, 2);
        membersDrawer.Controls.Add(layout);
        membersDrawer.Visible = false;
    }

    private Control BuildLogin()
    {
        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = Canvas };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var card = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.None,
            Padding = new Padding(20), ColumnCount = 2, BackColor = Color.White
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
        var title = new Label { Text = "Join the chat", Font = TitleFont, AutoSize = true, Margin = new Padding(3, 0, 0, 12) };
        card.Controls.Add(title, 0, 0);
        card.SetColumnSpan(title, 2);
        AddField("User name", user, 1);
        AddField("Server address", host, 3, span: false);
        card.Controls.Add(new Label { Text = "Port", AutoSize = true }, 1, 3);
        port.Dock = DockStyle.Fill;
        port.AccessibleName = "Port";
        card.Controls.Add(port, 1, 4);
        connect.Dock = DockStyle.Fill;
        connect.Margin = new Padding(3, 12, 3, 0);
        card.Controls.Add(connect, 0, 5);
        card.SetColumnSpan(connect, 2);
        loginStatus.ForeColor = Muted;
        loginStatus.Margin = new Padding(3, 8, 3, 0);
        card.Controls.Add(loginStatus, 0, 6);
        card.SetColumnSpan(loginStatus, 2);
        center.Controls.Add(card, 0, 0);
        return center;

        void AddField(string label, Control field, int row, bool span = true)
        {
            var caption = new Label { Text = label, AutoSize = true };
            field.AccessibleName = label;
            card.Controls.Add(caption, 0, row);
            card.Controls.Add(field, 0, row + 1);
            if (span)
            {
                card.SetColumnSpan(caption, 2);
                card.SetColumnSpan(field, 2);
            }
        }
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 0;
        button.Font = ActionFont;
        button.Padding = new Padding(12, 4, 12, 4);
        button.BackColor = primary ? Accent : Color.FromArgb(234, 244, 239);
        button.ForeColor = primary ? Color.White : Ink;
    }
}
