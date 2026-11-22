using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed partial class ChatForm
{
    private enum SettingsPage { Closed, Overview, Profile, Connection, Shortcuts }

    private readonly TextBox settingsSearch = new() { PlaceholderText = "Search settings", AccessibleName = "Search settings" };
    private readonly ConversationAvatar settingsAvatar = new() { BackColor = Canvas };
    private readonly Label settingsName = new() { Font = SidebarTitleFont, AutoEllipsis = true, Dock = DockStyle.Fill };
    private readonly Label settingsEmpty = new() { Text = "No matching settings", ForeColor = Muted, Dock = DockStyle.Top };
    private readonly InteractiveButton settingsBack = new() { Text = "\u2190", AccessibleName = "Back to settings" };
    private readonly Label settingsDetailTitle = new() { Font = TitleFont, Dock = DockStyle.Top };
    private readonly Label settingsDetailBody = new() { ForeColor = Muted, Dock = DockStyle.Top };
    private readonly Dictionary<SettingsPage, SettingsMenuButton> settingsRows = new();
    private Control settingsContent = null!;
    private Control settingsDetails = null!;
    private SettingsPage settingsPage;

    private Control BuildSettingsMenu()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14, 0, 14, 10) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleLayout(54)));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleLayout(44)));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleLayout(46)));
        settingsName.TextAlign = ContentAlignment.MiddleLeft;
        settingsName.Margin = new Padding(10, 0, 10, 0);
        panel.Controls.Add(settingsName, 0, 0);
        panel.Controls.Add(BuildSidebarSearch(settingsSearch), 0, 1);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        var menu = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Margin = Padding.Empty
        };
        menu.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var avatarHost = new Panel { Dock = DockStyle.Top, Height = ScaleLayout(82), Margin = Padding.Empty };
        settingsAvatar.Size = new Size(ScaleLayout(60), ScaleLayout(60));
        avatarHost.Controls.Add(settingsAvatar);
        avatarHost.SizeChanged += (_, _) => CenterAvatar();
        settingsAvatar.SizeChanged += (_, _) => CenterAvatar();
        menu.Controls.Add(avatarHost, 0, 0);
        AddSetting(SettingsPage.Profile, "\uE77B", "Profile", "Display name and current session");
        AddSetting(SettingsPage.Connection, "\uE968", "Connection", "Server address and port");
        AddSetting(SettingsPage.Shortcuts, "\uE765", "Keyboard shortcuts", "Chat and gallery controls");
        settingsEmpty.Visible = false;
        settingsEmpty.TextAlign = ContentAlignment.MiddleCenter;
        settingsEmpty.Height = ScaleLayout(48);
        menu.Controls.Add(settingsEmpty, 0, 4);
        scroll.Controls.Add(menu);
        panel.Controls.Add(scroll, 0, 2);

        disconnect.Dock = DockStyle.Fill;
        disconnect.AutoSize = false;
        disconnect.BackColor = Color.FromArgb(43, 32, 34);
        disconnect.ForeColor = Color.FromArgb(250, 152, 151);
        disconnect.Margin = new Padding(8, ScaleLayout(6), 8, 0);
        panel.Controls.Add(disconnect, 0, 3);
        settingsSearch.TextChanged += (_, _) => FilterSettings();
        settingsSearch.HandleCreated += (_, _) => ShowSearchHint();
        settingsSearch.GotFocus += (_, _) => ShowSearchHint();
        scroll.SizeChanged += (_, _) => FitMenu();
        FitMenu();
        return panel;

        void ShowSearchHint() => SendMessage(settingsSearch.Handle, 0x1501, new IntPtr(1), settingsSearch.PlaceholderText);

        void CenterAvatar() => settingsAvatar.Location = new Point(
            (avatarHost.Width - settingsAvatar.Width) / 2, (avatarHost.Height - settingsAvatar.Height) / 2);

        void FitMenu()
        {
            // Give the list room at compact heights before falling back to the scrollable menu.
            int fullHeight = ScaleLayout(82) + settingsRows.Count * (ScaleLayout(53) + 4);
            bool dense = scroll.ClientSize.Height < fullHeight;
            int avatarSize = ScaleLayout(dense ? 36 : 60);
            settingsAvatar.Size = new Size(avatarSize, avatarSize);
            int rowHeight = ScaleLayout(dense ? 48 : 53);
            foreach (SettingsMenuButton row in settingsRows.Values) row.Height = rowHeight;
            int remaining = scroll.ClientSize.Height - settingsRows.Count * (rowHeight + 4);
            avatarHost.Height = Math.Clamp(remaining, ScaleLayout(42), ScaleLayout(dense ? 50 : 82));
        }

        void AddSetting(SettingsPage page, string icon, string title, string summary)
        {
            var row = new SettingsMenuButton(icon, title, summary)
            {
                Dock = DockStyle.Top, Height = ScaleLayout(53), Margin = new Padding(6, 2, 6, 2),
                AccessibleName = title + " - " + summary, AccessibleDescription = summary
            };
            row.Click += (_, _) => ShowSettingsPage(page);
            settingsRows.Add(page, row);
            menu.Controls.Add(row, 0, settingsRows.Count);
        }
    }

    private Control BuildSettingsDetails()
    {
        var details = new Panel { BackColor = ChatTheme.Surface, Visible = false };
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top, Height = ScaleLayout(64), ColumnCount = 2, RowCount = 1,
            Padding = new Padding(ScaleLayout(18), 0, ScaleLayout(18), 0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        StyleButton(settingsBack, primary: false);
        settingsBack.AutoSize = false;
        settingsBack.Size = new Size(ScaleLayout(44), ScaleLayout(38));
        settingsBack.Anchor = AnchorStyles.Left;
        settingsBack.Click += (_, _) => OpenSettings();
        settingsDetailTitle.Dock = DockStyle.Fill;
        settingsDetailTitle.TextAlign = ContentAlignment.MiddleLeft;
        settingsDetailTitle.AutoEllipsis = true;
        header.Controls.Add(settingsBack, 0, 0);
        header.Controls.Add(settingsDetailTitle, 1, 0);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(ScaleLayout(24), 0, ScaleLayout(24), 0), AutoScroll = true };
        settingsDetailBody.Height = ScaleLayout(190);
        body.Controls.Add(settingsDetailBody);
        details.Controls.Add(body);
        details.Controls.Add(header);
        settingsDetails = details;
        return details;
    }

    private void OpenSettings()
    {
        settingsName.Text = joinedName;
        settingsAvatar.ShowConversation(string.IsNullOrWhiteSpace(joinedName) ? "?" : joinedName, shared: false);
        ShowSettingsPage(SettingsPage.Overview);
        settingsSearch.Focus();
    }

    private void ReturnToChats()
    {
        bool returning = settingsPage != SettingsPage.Closed;
        settingsPage = SettingsPage.Closed;
        SelectNavigation(chatsNav);
        if (!returning) showingList = true;
        LayoutChatColumns();
        if (returning && chatContent is { Visible: true }) composer.Focus();
        else if (compactLayout) search.Focus();
    }

    private void ShowSettingsPage(SettingsPage page)
    {
        settingsPage = page;
        SelectNavigation(settingsNav);
        foreach (var (item, button) in settingsRows)
            button.SetCurrent(item == page);
        (settingsDetailTitle.Text, settingsDetailBody.Text) = page switch
        {
            SettingsPage.Profile => ("Profile", $"Display name\n{joinedName}\n\nSession\nActive on this server"),
            SettingsPage.Connection => ("Connection", $"Server address\n{host.Text.Trim()}\n\nPort\n{port.Text.Trim()}"),
            SettingsPage.Shortcuts => ("Keyboard shortcuts", "Enter   Send a message\n\nEsc   Close the image viewer\n\nPage Up / Page Down   Browse gallery images\n\nHome / Ctrl+0   Fit the image to the viewer"),
            _ => ("Settings", "Select a section on the left.")
        };
        HideAttachmentDrop();
        LayoutChatColumns();
        if (compactLayout && page != SettingsPage.Overview) settingsBack.Focus();
    }

    private void FilterSettings()
    {
        string query = settingsSearch.Text.Trim();
        bool any = false;
        foreach (SettingsMenuButton row in settingsRows.Values)
        {
            bool matches = row.AccessibleName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            row.Visible = matches;
            any |= matches;
        }
        settingsEmpty.Visible = !any;
    }

    private sealed class SettingsMenuButton(string glyph, string title, string summary) : InteractiveButton
    {
        private bool current;

        public void SetCurrent(bool value)
        {
            current = value;
            AccessibleDescription = value ? "Current section" : summary;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Canvas);
            if (Width < 6 || Height < 6) return;
            float scale = DeviceDpi / 96f;
            int S(int value) => (int)Math.Round(value * scale);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (current || Hovered || Pressed || KeyboardFocused)
            {
                using var shape = new GraphicsPath();
                shape.AddRoundedRectangle(new Rectangle(1, 1, Width - 3, Height - 3), new Size(S(8), S(8)));
                using var fill = new SolidBrush(Pressed ? ChatTheme.Surface : KeyboardFocused ? ChatTheme.FocusSurface : ChatTheme.Raised);
                e.Graphics.FillPath(fill, shape);
            }

            const TextFormatFlags line = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
            var iconBounds = new Rectangle(S(10), 0, S(32), Height);
            TextRenderer.DrawText(e.Graphics, glyph, SearchIconFont, iconBounds, Muted, line | TextFormatFlags.HorizontalCenter);
            int left = S(50), available = Math.Max(1, Width - left - S(6)), middle = Height / 2;
            TextRenderer.DrawText(e.Graphics, title, ActionFont, new Rectangle(left, middle - S(18), available, S(18)), Ink, line);
            TextRenderer.DrawText(e.Graphics, summary, PreviewFont, new Rectangle(left, middle + S(2), available, S(18)), Muted, line);
        }
    }
}
