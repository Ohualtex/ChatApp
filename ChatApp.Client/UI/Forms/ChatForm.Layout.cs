using System.Runtime.InteropServices;

namespace ChatApp.Client;

internal sealed partial class ChatForm
{
    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        LayoutChatColumns();
    }

    private int ScaleLayout(int value) => (int)Math.Round(value * DeviceDpi / 96d);

    private int ClampSidebarWidth(int desired)
    {
        int available = chatArea?.ClientSize.Width ?? 0;
        // Keep the list readable while reserving space for the divider and conversation.
        int minimum = ScaleLayout(280);
        int maximum = Math.Max(minimum, Math.Min(available / 2, available - ScaleLayout(365)));
        return Math.Clamp(desired, minimum, maximum);
    }

    private void LayoutChatColumns()
    {
        if (chatArea is null || chatContent is null || layingOutColumns) return;
        layingOutColumns = true;
        chatArea.SuspendLayout();
        try
        {
            int width = chatArea.ClientSize.Width;
            int height = chatArea.ClientSize.Height;
            bool compact = width < ScaleLayout(280 + 5 + 360);
            // Choose the initial narrow view only when entering compact mode; later resizes
            // must preserve the user's choice between the list and the conversation.
            if (compact && !compactLayout)
                showingList = settingsContent.Visible || connection is null || connecting;
            compactLayout = compact;
            bool connected = connection is not null && !connecting;
            bool listOnly = compact && (settingsContent.Visible || (connected && showingList));
            membersDrawer.Visible = !compact || listOnly;
            chatContent.Visible = !compact || !listOnly;
            chatDivider.Visible = !compact;
            backToChats.Visible = compact && !listOnly && connected;
            if (compact)
            {
                chatDivider.Capture = false;
                membersDrawer.Bounds = new Rectangle(0, 0, width, height);
                chatContent.Bounds = new Rectangle(0, 0, width, height);
            }
            else
            {
                int sidebarWidth = ClampSidebarWidth(ScaleLayout(preferredSidebarWidth));
                int dividerWidth = ScaleLayout(5);
                membersDrawer.Bounds = new Rectangle(0, 0, sidebarWidth, height);
                chatDivider.Bounds = new Rectangle(sidebarWidth, 0, dividerWidth, height);
                chatContent.Bounds = new Rectangle(sidebarWidth + dividerWidth, 0,
                    Math.Max(0, width - sidebarWidth - dividerWidth), height);
            }
        }
        finally
        {
            chatArea.ResumeLayout(performLayout: false);
            layingOutColumns = false;
        }
    }

    private Control BuildLayout()
    {
        foreach (Button action in new[] { attach, voice, send })
        {
            StyleButton(action, primary: false);
            action.Padding = Padding.Empty;
            action.BackColor = ChatTheme.Raised;
        }
        tips.SetToolTip(attach, "Attach photo, video or file (up to 10 MB)");
        tips.SetToolTip(voice, "Record a voice message");
        tips.SetToolTip(send, "Send message");
        StyleButton(disconnect, primary: false);
        StyleButton(backToChats, primary: false);
        backToChats.Padding = Padding.Empty;
        backToChats.Margin = new Padding(0, 0, 8, 0);
        backToChats.Visible = false;
        backToChats.Anchor = AnchorStyles.Left;
        membersToggle.Font = HeaderFont;
        membersToggle.BackColor = ChatTheme.Surface;
        membersToggle.Padding = Padding.Empty;
        membersToggle.AccessibleName = "Conversation name";
        membersToggle.Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var rail = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
            BackColor = ChatTheme.Surface, Padding = new Padding(8, 12, 8, 12), Margin = Padding.Empty };
        rail.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        rail.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rail.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        rail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (Button button in new[] { chatsNav, settingsNav })
        {
            StyleButton(button, primary: false);
            button.Font = SidebarIconFont;
            button.Padding = Padding.Empty;
            button.Margin = new Padding(0, 2, 0, 2);
            button.AutoSize = false;
            button.MinimumSize = Size.Empty;
            button.Size = new Size(44, 44);
            button.Dock = DockStyle.Fill;
            tips.SetToolTip(button, button.AccessibleName);
            rail.Controls.Add(button, 0, button == chatsNav ? 0 : 2);
        }

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Margin = Padding.Empty };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 10), BackColor = ChatTheme.Surface
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(backToChats, 0, 0);
        conversationAvatar.Size = new Size(44, 44);
        conversationAvatar.Anchor = AnchorStyles.Left;
        conversationAvatar.Margin = new Padding(0, 0, 12, 0);
        header.Controls.Add(conversationAvatar, 1, 0);
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty
        };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        membersToggle.Margin = Padding.Empty;
        membersToggle.TextAlign = ContentAlignment.BottomLeft;
        status.Dock = DockStyle.Fill;
        status.Font = PreviewFont;
        status.TextAlign = ContentAlignment.TopLeft;
        status.Margin = new Padding(0, 2, 0, 0);
        heading.Controls.Add(membersToggle, 0, 0);
        heading.Controls.Add(status, 0, 1);
        header.Controls.Add(heading, 2, 0);

        var body = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Canvas };
        body.Controls.Add(messages);
        BuildMembersDrawer();

        var normalHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 10), BackColor = Color.Transparent };
        var inputShell = new RoundedPanel(capsule: true)
        {
            Dock = DockStyle.Fill, Padding = new Padding(16, 8, 16, 8), BackColor = ChatTheme.Raised
        };
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        composer.Font = ComposerFont;
        composer.BorderStyle = BorderStyle.None;
        composer.Dock = DockStyle.None;
        composer.Margin = Padding.Empty;
        var textHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0) };
        textHost.Controls.Add(composer);
        textHost.SizeChanged += (_, _) => CenterComposer();
        composer.FontChanged += (_, _) => CenterComposer();
        void CenterComposer() => composer.SetBounds(0, Math.Max(0, (textHost.Height - composer.PreferredHeight) / 2), textHost.Width, composer.PreferredHeight);
        var actionHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        foreach (Button action in new[] { attach, voice, send })
        {
            action.AutoSize = false;
            action.Dock = DockStyle.Fill;
            action.Margin = Padding.Empty;
        }
        actionHost.Controls.Add(voice);
        actionHost.Controls.Add(send);
        bar.Controls.Add(attach, 0, 0);
        bar.Controls.Add(textHost, 1, 0);
        bar.Controls.Add(actionHost, 2, 0);
        inputShell.Controls.Add(bar);
        normalHost.Controls.Add(inputShell);
        normalComposerBar = normalHost;
        var footer = new WallpaperPanel(() => messages.Height) { Dock = DockStyle.Fill, Margin = Padding.Empty };
        footer.Controls.Add(normalHost);
        var recordingHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 10), BackColor = Color.Transparent, Visible = false };
        recordingHost.Controls.Add(recordingBar);
        voiceComposerHost = recordingHost;
        footer.Controls.Add(recordingHost);
        composerBar = footer;
        right.Controls.Add(header, 0, 0);
        right.Controls.Add(body, 0, 1);
        right.Controls.Add(footer, 0, 2);
        var conversationView = new Panel { Margin = Padding.Empty };
        conversationView.Controls.Add(right);
        conversationView.Controls.Add(attachmentDrop);
        attachmentDropHost = conversationView;
        attachmentHeader = header;
        right.Layout += (_, _) => UpdateAttachmentDropBounds();
        var main = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        chatArea = main;
        chatContent = conversationView;
        main.SizeChanged += (_, _) => LayoutChatColumns();
        membersDrawer.Dock = DockStyle.None;
        main.Controls.Add(conversationView);
        main.Controls.Add(membersDrawer);
        main.Controls.Add(chatDivider);
        layout.Controls.Add(rail, 0, 0);
        layout.Controls.Add(main, 1, 0);
        StyleInput(composer);
        composer.BorderStyle = BorderStyle.None;
        return layout;
    }

    // Use the native edit control's cue banner so the composer can show a hint while focused.
    private void SetComposerHint() => SendMessage(composer.Handle, 0x1501, new IntPtr(1), "Type a message");

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr parameter, string text);

    private static void StyleInput(TextBox input)
    {
        input.BackColor = ChatTheme.Raised;
        input.ForeColor = Ink;
        input.BorderStyle = BorderStyle.FixedSingle;
    }

    private void BuildMembersDrawer()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(14, 0, 14, 0), Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Chats", AutoSize = true, Font = SidebarTitleFont, Anchor = AnchorStyles.Left, Margin = new Padding(10, 0, 0, 0) }, 0, 0);

        var searchShell = new RoundedPanel(capsule: true)
        {
            Dock = DockStyle.Fill, Margin = new Padding(8, 5, 8, 5),
            Padding = new Padding(12, 8, 12, 8), BackColor = ChatTheme.Raised
        };
        var searchIcon = new Label
        {
            Text = "\uE721", Font = SearchIconFont, ForeColor = Muted, Dock = DockStyle.Left,
            Width = 28, TextAlign = ContentAlignment.MiddleCenter
        };
        search.BorderStyle = BorderStyle.None;
        search.BackColor = ChatTheme.Raised;
        search.ForeColor = Ink;
        search.AccessibleName = "Search chats";
        search.Dock = DockStyle.None;
        var searchTextHost = new Panel { Dock = DockStyle.Fill, Padding = Padding.Empty };
        searchTextHost.Controls.Add(search);
        void PositionSearch()
        {
            int gap = ScaleLayout(10);
            search.SetBounds(gap, Math.Max(0, (searchTextHost.Height - search.PreferredHeight) / 2),
                Math.Max(1, searchTextHost.Width - gap), search.PreferredHeight);
        }
        searchTextHost.SizeChanged += (_, _) => PositionSearch();
        search.FontChanged += (_, _) => PositionSearch();
        searchShell.Controls.Add(searchTextHost);
        searchShell.Controls.Add(searchIcon);
        layout.Controls.Add(searchShell, 0, 1);
        layout.SetColumnSpan(searchShell, 2);
        memberCount.Margin = new Padding(12, 4, 0, 0);
        layout.Controls.Add(memberCount, 0, 2);
        layout.SetColumnSpan(memberCount, 2);
        memberList.ItemHeight = ScaleLayout(80);
        memberList.Margin = new Padding(0, 4, 0, 0);
        layout.Controls.Add(memberList, 0, 3);
        layout.SetColumnSpan(memberList, 2);
        chatsContent = layout;
        var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(18), Visible = false };
        settings.Controls.Add(new Label { Text = "Settings", Font = TitleFont, AutoSize = true });
        settings.Controls.Add(accountSummary);
        disconnect.Margin = new Padding(0, 18, 0, 0);
        settings.Controls.Add(disconnect);
        settingsContent = settings;
        membersDrawer.Controls.Add(layout);
        membersDrawer.Controls.Add(settings);
        membersDrawer.Visible = false;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = 0;
        button.Font = ActionFont;
        button.Padding = new Padding(12, 4, 12, 4);
        button.BackColor = primary ? Accent : ChatTheme.Surface;
        button.ForeColor = primary ? Canvas : Ink;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(52, 225, 119) : ChatTheme.Raised;
    }
}
