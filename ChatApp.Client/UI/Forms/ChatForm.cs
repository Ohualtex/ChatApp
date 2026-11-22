using System.Drawing.Drawing2D;
using System.Net.Sockets;
using System.Text.Json;

namespace ChatApp.Client;

// Coordinates the chat session and the active conversation. Companion files build the layout and host media workflows.
internal sealed partial class ChatForm : Form
{
    private static readonly Icon WindowIcon = new(typeof(ChatForm), "Assets.app.ico");

    private static readonly Color Accent = ChatTheme.Accent;
    private static readonly Color Canvas = ChatTheme.Canvas;
    private static readonly Color Ink = ChatTheme.Ink;
    private static readonly Color Muted = ChatTheme.Muted;
    private static readonly Font HeaderFont = new("Segoe UI", 12, FontStyle.Bold);
    private static readonly Font ComposerFont = new("Segoe UI", 11);
    private static readonly Font TitleFont = new("Segoe UI", 15, FontStyle.Bold);
    private static readonly Font SidebarTitleFont = new("Segoe UI", 16, FontStyle.Regular);
    private static readonly Font ConversationFont = new("Segoe UI", 10, FontStyle.Regular);
    private static readonly Font PreviewFont = new("Segoe UI", 9);
    private static readonly Font SearchIconFont = new("Segoe MDL2 Assets", 12);
    private static readonly Font SidebarIconFont = new("Segoe MDL2 Assets", 14);
    private static readonly Font BodyFont = new("Segoe UI", 10);
    private static readonly Font ActionFont = new("Segoe UI", 10, FontStyle.Bold);

    private readonly TextBox user = new() { PlaceholderText = "User name", Dock = DockStyle.Fill, MaxLength = 24 };
    private readonly TextBox host = new() { Text = "127.0.0.1", Dock = DockStyle.Fill };
    private readonly NumericUpDown port = new() { Minimum = 1, Maximum = 65535, Value = 5050, Width = 72 };
    private readonly Label status = new() { Text = "Disconnected", AutoSize = false, AutoEllipsis = true };
    private readonly ConversationAvatar conversationAvatar = new();
    private readonly InteractiveButton connect = new() { Text = "Connect", AutoSize = true };
    private readonly Label membersToggle = new() { Text = "General", AutoSize = false, AutoEllipsis = true };
    private readonly InteractiveButton backToChats = new() { Text = "←", AccessibleName = "Back to chats", AutoSize = true };
    private readonly Panel chatDivider = new() { Cursor = Cursors.VSplit, BackColor = ChatTheme.Raised };
    private readonly Panel membersDrawer = new() { Dock = DockStyle.Left, Width = 320, BackColor = ChatTheme.Canvas };
    private readonly Label memberCount = new() { AutoSize = true, ForeColor = Muted };
    private readonly ListBox memberList = new()
    {
        Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false,
        BackColor = ChatTheme.Canvas, ForeColor = Ink, AccessibleName = "Conversations", DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 76
    };
    private readonly TextBox search = new() { PlaceholderText = "Search chats", Dock = DockStyle.Fill };
    private readonly NavigationButton chatsNav = new() { Text = "\uE8F2", AccessibleName = "Chats" };
    private readonly NavigationButton accountNav = new() { Text = "\uE77B", AccessibleName = "Account" };
    private readonly NavigationButton settingsNav = new() { Text = "\uE713", AccessibleName = "Settings" };
    private readonly ToolTip tips = new();
    private Control chatsContent = null!;
    private Control settingsContent = null!;
    private readonly Label accountSummary = new() { AutoSize = true, MaximumSize = new Size(240, 0) };
    private readonly InteractiveButton disconnect = new() { Text = "Disconnect", AutoSize = true };
    private readonly MessageFeed messages = new();
    private readonly Label loginStatus = new() { AutoSize = true, MaximumSize = new Size(336, 0) };
    private Control login = null!;
    private Control composerBar = null!;
    private readonly TextBox composer = new() { Dock = DockStyle.Fill, MaxLength = 1000, AccessibleName = "Type a message" };
    private readonly Button attach = new RecordingButton(RecordingAction.Attach) { AccessibleName = "Attach a file" };
    private readonly Button voice = new RecordingButton(RecordingAction.Microphone) { AccessibleName = "Record a voice message" };
    private readonly VoiceRecordingBar recordingBar = new();
    private Conversation? recordingDestination;
    private ChatConnection? recordingConnection;
    private Control normalComposerBar = null!;
    private Control voiceComposerHost = null!;
    private readonly Button send = new RecordingButton(RecordingAction.Send) { AccessibleName = "Send message" };
    private readonly CancellationTokenSource closing = new();
    private ChatConnection? connection;
    private bool connecting;
    private bool sending;
    private string joinedName = "";
    private readonly Dictionary<string, Conversation> conversations = new(StringComparer.Ordinal);
    private Conversation selected = new(null);
    private bool refreshingChats;
    private int hoveredConversation = -1;
    private int pressedConversation = -1;
    private Panel? chatArea;
    private Control? chatContent;
    private bool compactLayout;
    private bool showingList = true;
    private int preferredSidebarWidth = 320;
    private bool layingOutColumns;
    private int dividerGrabOffset;

    public ChatForm()
    {
        Text = "ChatApp";
        Icon = WindowIcon;
        MinimumSize = new Size(650, 440);
        ClientSize = new Size(1120, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = BodyFont;
        ForeColor = Ink;
        BackColor = Canvas;
        Controls.Add(BuildLayout());
        InitializeAttachmentDrop();
        chatsNav.Click += (_, _) =>
        {
            settingsContent.Visible = false;
            chatsContent.Visible = true;
            SetMembersDrawer(true);
            SelectNavigation(chatsNav);
        };
        backToChats.Click += (_, _) => SetMembersDrawer(true);
        chatDivider.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            dividerGrabOffset = e.X;
            chatDivider.Capture = true;
        };
        chatDivider.MouseMove += (_, _) =>
        {
            if (!chatDivider.Capture || chatArea is null) return;
            int desired = chatArea.PointToClient(Cursor.Position).X - dividerGrabOffset;
            preferredSidebarWidth = (int)Math.Round(ClampSidebarWidth(desired) * 96d / DeviceDpi);
            LayoutChatColumns();
        };
        chatDivider.MouseUp += (_, _) => chatDivider.Capture = false;
        settingsNav.Click += (_, _) => { ShowSettings(); SelectNavigation(settingsNav); };
        accountNav.Click += (_, _) => { ShowSettings(); SelectNavigation(accountNav); };
        search.TextChanged += (_, _) => RefreshConversationList();
        Shown += (_, _) => ChatTheme.DarkTitle(this);
        connect.Click += async (_, _) => await ConnectAndListenAsync();
        memberList.DrawItem += DrawConversation;
        memberList.MouseMove += (_, e) =>
        {
            int index = memberList.IndexFromPoint(e.Location);
            if (index == hoveredConversation) return;
            int previous = hoveredConversation;
            hoveredConversation = index;
            InvalidateConversation(previous);
            InvalidateConversation(index);
        };
        memberList.MouseLeave += (_, _) =>
        {
            int previous = hoveredConversation;
            hoveredConversation = -1;
            InvalidateConversation(previous);
        };
        memberList.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || !memberList.Visible || !memberList.Enabled) return;
            pressedConversation = hoveredConversation = memberList.IndexFromPoint(e.Location);
            InvalidateConversation(pressedConversation);
        };
        memberList.MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) ClearPressedConversation(); };
        memberList.MouseCaptureChanged += (_, _) => { if (!memberList.Capture) ClearPressedConversation(); };
        memberList.VisibleChanged += (_, _) => { if (!memberList.Visible) ClearPressedConversation(); };
        memberList.EnabledChanged += (_, _) => { if (!memberList.Enabled) ClearPressedConversation(); };
        memberList.SizeChanged += (_, _) => memberList.Invalidate();
        memberList.DpiChangedAfterParent += (_, _) => memberList.ItemHeight = ScaleLayout(80);
        memberList.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshingChats && memberList.SelectedItem is Conversation chat)
            {
                SelectConversation(chat);
            }
        };
        memberList.MouseClick += (_, _) =>
        {
            if (compactLayout && memberList.SelectedItem is Conversation chat && !recordingBar.Active)
                SelectConversation(chat);
        };
        disconnect.Click += (_, _) => Disconnect("Disconnected");
        attach.Click += async (_, _) => await PickAttachmentAsync();
        voice.Click += (_, _) => StartVoiceRecording();
        recordingBar.Changed += () => { if (!IsDisposed) RefreshControls(); };
        recordingBar.SendRequested += SendVoiceRecordingAsync;
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
        composer.HandleCreated += (_, _) => SetComposerHint();
        composer.GotFocus += (_, _) => SetComposerHint();
        FormClosed += (_, _) =>
        {
            recordingBar.Cancel();
            ClearVoiceHistory();
            closing.Cancel();
            connection?.Dispose();
            closing.Dispose();
            tips.Dispose();
        };
        SelectNavigation(chatsNav);
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
            joinedName = await next.ConnectAsync(address, (int)port.Value, name, handshake.Token);
            ClearVoiceHistory();
            conversations.Clear();
            selected = new Conversation(null);
            conversations.Add("", selected);
            membersToggle.Text = selected.Title;
            search.Clear();
            messages.ClearMessages();
            composer.Clear();
            RefreshConversationList();
            loginStatus.Text = "";
            connecting = false;
            RefreshControls();
            SetMembersDrawer(true);
            composer.Focus();
            while (await next.ReadAsync(lifetime) is { } packet)
            {
                if (!ReferenceEquals(connection, next) || lifetime.IsCancellationRequested)
                {
                    packet.Recording?.Dispose();
                    break;
                }
                if (packet.Type is "chat" or "file" or "voice")
                {
                    ReceiveMessage(packet);
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
        if (preparingAttachments) return;
        if (connection is not { } current || connecting || sending || string.IsNullOrWhiteSpace(composer.Text))
        {
            return;
        }

        if (!selected.Online)
        {
            return;
        }

        Conversation destination = selected;
        string text = composer.Text;
        sending = true;
        RefreshControls();
        CancellationToken lifetime = closing.Token;
        try
        {
            await current.SendAsync(new ChatPacket("chat", Message: text.Trim(), Recipient: destination.Peer), lifetime);
            if (ReferenceEquals(connection, current) && ReferenceEquals(selected, destination) && composer.Text == text)
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

    private void StartVoiceRecording()
    {
        if (preparingAttachments) return;
        if (connection is null || connecting || sending || !selected.Online || recordingBar.Active) return;
        // Bind the recording to the conversation and connection present when capture starts.
        recordingDestination = selected;
        recordingConnection = connection;
        recordingBar.Start();
    }

    private async Task<bool> SendVoiceRecordingAsync(VoiceClip clip)
    {
        if (connection is not { } current || !ReferenceEquals(current, recordingConnection)
            || recordingDestination is not { Online: true } destination || sending)
        {
            messages.AddNotice("The recording could not be sent because the conversation is offline.");
            return false;
        }
        sending = true;
        RefreshControls();
        CancellationToken token = closing.Token;
        try
        {
            await current.SendVoiceAsync(clip, destination.Peer, token);
            return true;
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(connection, current)) Disconnect(error.Message);
            return false;
        }
        finally
        {
            sending = false;
            if (!IsDisposed && !token.IsCancellationRequested) RefreshControls();
        }
    }

    private void ClearVoiceHistory()
    {
        // Dispose playback controls before deleting the temporary files owned by history clips.
        messages.ClearMessages();
        foreach (Conversation chat in conversations.Values)
            foreach (ChatPacket packet in chat.Messages) packet.Recording?.Dispose();
    }

    private async Task PickAttachmentAsync()
    {
        if (preparingAttachments) return;
        if (connection is not { } current || connecting || sending || !selected.Online) return;
        using var picker = new OpenFileDialog { Title = "Attach photo, video or file",
            Filter = "All files (*.*)|*.*|Photos|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Videos|*.mp4;*.mov;*.webm" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        Conversation destination = selected;
        sending = true;
        RefreshControls();
        try
        {
            ChatAttachment attachment = await ChatAttachment.FromFileAsync(picker.FileName, closing.Token);
            sending = false;
            if (ReferenceEquals(connection, current))
                await SendAttachmentAsync(attachment, destination);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            if (!closing.IsCancellationRequested) messages.AddNotice(error.Message);
        }
        finally
        {
            sending = false;
            if (!IsDisposed) RefreshControls();
        }
    }

    private async Task<bool> SendAttachmentAsync(ChatAttachment attachment, Conversation destination)
    {
        if (connection is not { } current || connecting || sending || !destination.Online) return false;
        sending = true;
        RefreshControls();
        CancellationToken token = closing.Token;
        try
        {
            await current.SendAsync(new ChatPacket("file", Recipient: destination.Peer, Attachment: attachment), token);
            return true;
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(connection, current)) Disconnect(error.Message);
            return false;
        }
        finally
        {
            sending = false;
            if (!token.IsCancellationRequested) RefreshControls();
        }
    }

    private void Disconnect(string message)
    {
        ChatConnection? previous = connection;
        connection = null;
        connecting = false;
        previous?.Dispose();
        recordingBar.Cancel();
        settingsContent.Visible = false;
        chatsContent.Visible = true;
        SelectNavigation(chatsNav);
        SetMembersDrawer(false);
        UpdateMembers([], "");
        loginStatus.Text = message;
        RefreshControls();
    }

    private void UpdateMembers(string[] names, string currentName)
    {
        foreach (Conversation chat in conversations.Values)
        {
            chat.Online = chat.Peer is null || names.Contains(chat.Peer, StringComparer.Ordinal);
        }
        foreach (string name in names.Where(name => name != currentName))
        {
            GetConversation(name).Online = true;
        }
        memberCount.Text = $"{names.Length} online";
        RefreshConversationList();
        RefreshControls();
    }

    private Conversation GetConversation(string? peer)
    {
        string key = peer ?? "";
        if (!conversations.TryGetValue(key, out Conversation? chat))
        {
            chat = new Conversation(peer);
            conversations.Add(key, chat);
        }
        return chat;
    }

    // Store each packet in its conversation. Only the selected conversation updates the visible feed.
    private void ReceiveMessage(ChatPacket packet)
    {
        bool outgoing = packet.Name == joinedName;
        string? peer = packet.Recipient is null ? null : outgoing ? packet.Recipient : packet.Name;
        Conversation chat = GetConversation(peer);
        chat.Messages.Add(packet);
        if (ReferenceEquals(chat, selected))
        {
            messages.AddPacket(packet, outgoing, showSender: selected.Peer is null);
        }
        else if (!outgoing)
        {
            chat.Unread++;
        }
        RefreshConversationList();
    }

    // Save the current draft before restoring the next conversation's history and draft.
    private void SelectConversation(Conversation chat)
    {
        if (recordingBar.Active && !ReferenceEquals(chat, selected))
        {
            RefreshConversationList();
            return;
        }
        if (ReferenceEquals(chat, selected))
        {
            selected.Unread = 0;
            SetMembersDrawer(false);
            return;
        }
        selected.Draft = composer.Text;
        selected = chat;
        selected.Unread = 0;
        membersToggle.Text = selected.Title;
        composer.Text = selected.Draft;
        messages.ClearMessages();
        foreach (ChatPacket packet in selected.Messages)
        {
            messages.AddPacket(packet, packet.Name == joinedName, showSender: selected.Peer is null);
        }
        RefreshConversationList();
        RefreshControls();
        SetMembersDrawer(false);
    }

    private void ShowSettings()
    {
        accountSummary.Text = connection is not null && !connecting
            ? $"Signed in as {joinedName}\n\nServer\n{host.Text}:{port.Value}\n\nAppearance\nDark"
            : "Connect to a server to start chatting.\n\nAppearance\nDark";
        chatsContent.Visible = false;
        settingsContent.Visible = true;
        SetMembersDrawer(true);
    }

    private void RefreshConversationList()
    {
        refreshingChats = true;
        hoveredConversation = pressedConversation = -1;
        memberList.BeginUpdate();
        try
        {
            memberList.Items.Clear();
            foreach (Conversation chat in conversations.Values.OrderBy(chat => chat.Peer is not null)
                .ThenBy(chat => chat.Title, StringComparer.OrdinalIgnoreCase))
            {
                if (chat.Title.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                    || chat.Preview.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    memberList.Items.Add(chat);
                }
            }
            memberList.SelectedItem = selected;
        }
        finally
        {
            memberList.EndUpdate();
            refreshingChats = false;
        }
    }

    private void SelectNavigation(NavigationButton active)
    {
        foreach (NavigationButton button in new[] { chatsNav, accountNav, settingsNav })
            button.SetSelected(ReferenceEquals(button, active));
    }

    private void InvalidateConversation(int index)
    {
        if (index >= 0 && index < memberList.Items.Count)
            memberList.Invalidate(memberList.GetItemRectangle(index));
    }

    private void ClearPressedConversation()
    {
        int previous = pressedConversation;
        pressedConversation = -1;
        InvalidateConversation(previous);
    }

    private void DrawConversation(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var chat = (Conversation)memberList.Items[e.Index];
        using (var background = new SolidBrush(Canvas)) e.Graphics.FillRectangle(background, e.Bounds);
        var card = Rectangle.Inflate(e.Bounds, -ScaleLayout(2), -ScaleLayout(3));
        bool active = ReferenceEquals(chat, selected);
        if (active || e.Index == hoveredConversation)
        {
            int diameter = ScaleLayout(20);
            using var path = new GraphicsPath();
            path.AddArc(card.Left, card.Top, diameter, diameter, 180, 90);
            path.AddArc(card.Right - diameter, card.Top, diameter, diameter, 270, 90);
            path.AddArc(card.Right - diameter, card.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(card.Left, card.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(e.Index == pressedConversation && e.Index == hoveredConversation
                ? Color.FromArgb(34, 37, 35) : active ? ChatTheme.Raised : ChatTheme.Surface);
            e.Graphics.FillPath(fill, path);
        }
        int avatarSize = ScaleLayout(48);
        var avatar = new Rectangle(card.Left + ScaleLayout(12), card.Top + (card.Height - avatarSize) / 2,
            avatarSize, avatarSize);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var circle = new SolidBrush(chat.Peer is null ? Color.FromArgb(40, 48, 46) : Color.FromArgb(48, 50, 54));
        e.Graphics.FillEllipse(circle, avatar);
        string avatarText = chat.Peer is null ? "\uE902"
            : System.Globalization.StringInfo.GetNextTextElement(chat.Title).ToUpperInvariant();
        const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(e.Graphics, avatarText, chat.Peer is null ? SidebarIconFont : ActionFont,
            avatar, Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        int left = avatar.Right + ScaleLayout(12);
        int right = card.Right - ScaleLayout(12);
        int lineHeight = ScaleLayout(24);
        int top = card.Top + (card.Height - lineHeight * 2) / 2;
        string time = chat.Messages.LastOrDefault()?.Timestamp?.ToLocalTime().ToString("HH:mm") ?? "";
        int timeWidth = time.Length == 0 ? 0 : TextRenderer.MeasureText(time, PreviewFont).Width;
        var title = new Rectangle(left, top, Math.Max(1, right - left - timeWidth - ScaleLayout(8)), lineHeight);
        TextRenderer.DrawText(e.Graphics, chat.Title, ConversationFont, title, Ink, flags);
        TextRenderer.DrawText(e.Graphics, time, PreviewFont,
            new Rectangle(right - timeWidth, top, timeWidth, lineHeight),
            chat.Unread > 0 ? Accent : Muted, flags | TextFormatFlags.Right);
        int badgeWidth = chat.Unread > 99 ? ScaleLayout(30) : ScaleLayout(22);
        int previewRight = chat.Unread > 0 ? right - badgeWidth - ScaleLayout(8) : right;
        var preview = new Rectangle(left, top + lineHeight, Math.Max(1, previewRight - left), lineHeight);
        TextRenderer.DrawText(e.Graphics, chat.Preview, PreviewFont, preview, Muted, flags);
        if (chat.Unread > 0)
        {
            var badge = new Rectangle(right - badgeWidth, top + lineHeight + ScaleLayout(1), badgeWidth, ScaleLayout(22));
            using var green = new SolidBrush(Accent);
            e.Graphics.FillEllipse(green, badge);
            TextRenderer.DrawText(e.Graphics, chat.Unread > 99 ? "99+" : chat.Unread.ToString(), PreviewFont,
                badge, Canvas, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    private void SetMembersDrawer(bool open)
    {
        showingList = open;
        LayoutChatColumns();
    }

    private void RefreshControls()
    {
        bool offline = connection is null && !connecting;
        bool connected = connection is not null && !connecting;
        user.Enabled = host.Enabled = port.Enabled = connect.Enabled = offline;
        status.Text = connected ? selected.Peer is null
            ? (memberCount.Text.Length > 0 ? memberCount.Text : "Shared room")
            : selected.Online ? "Online" : "Offline"
            : connecting ? "Connecting…" : "Offline";
        status.ForeColor = Muted;
        conversationAvatar.ShowConversation(selected.Title, selected.Peer is null);
        login.Visible = !connected;
        if (preparingAttachments && attachmentStatus is not null) status.Text = attachmentStatus;
        messages.Visible = composerBar.Visible = disconnect.Visible = connected;
        membersToggle.Enabled = connected;
        LayoutChatColumns();
        chatsNav.Enabled = connected && !preparingAttachments;
        settingsNav.Enabled = accountNav.Enabled = !preparingAttachments;
        backToChats.Enabled = connected && !preparingAttachments;
        if (normalComposerBar is not null) normalComposerBar.Visible = !recordingBar.Active;
        if (voiceComposerHost is not null) voiceComposerHost.Visible = recordingBar.Active;
        memberList.Enabled = !recordingBar.Active && !preparingAttachments;
        composer.Enabled = connected && selected.Online && !recordingBar.Active;
        attach.Enabled = voice.Enabled = connected && selected.Online && !sending && !preparingAttachments;

        bool hasMessage = !string.IsNullOrWhiteSpace(composer.Text);
        voice.Visible = !hasMessage;
        send.Visible = hasMessage;
        send.Enabled = connected && selected.Online && !sending && !preparingAttachments && hasMessage;
        if (!CanAcceptFileDrop()) HideAttachmentDrop();
    }
}
