using System.Drawing.Drawing2D;

namespace ChatApp.Client;

internal sealed partial class ChatForm
{
    private static readonly Font WelcomeFont = new("Segoe UI", 24, FontStyle.Bold);
    private static readonly Font FieldFont = new("Segoe UI", 9, FontStyle.Bold);
    private readonly Label loginStatus = new() { AutoEllipsis = true };
    private Control chatRoot = null!;
    private Control login = null!;

    private Control BuildLogin()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, AutoScroll = true };
        var card = new RoundedPanel { BackColor = ChatTheme.Surface };
        page.Controls.Add(card);
        card.Paint += (_, e) =>
        {
            var bounds = new Rectangle(1, 1, card.Width - 3, card.Height - 3);
            if (bounds.Width < 20 || bounds.Height < 20) return;
            using var outline = new GraphicsPath();
            outline.AddRoundedRectangle(bounds, new Size(10, 10));
            using var pen = new Pen(ChatTheme.Raised);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, outline);
        };

        var mark = new PictureBox { Image = WindowIcon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
        mark.Disposed += (_, _) => mark.Image?.Dispose();
        var title = new Label { Text = "ChatApp", Font = WelcomeFont, TextAlign = ContentAlignment.MiddleCenter };
        var intro = new Label
        {
            Text = "Connect to a chat server to get started.", ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleCenter
        };
        card.Controls.AddRange([mark, title, intro]);

        var nameField = CreateField("Display name", user, 0);
        var addressField = CreateField("Server address", host, 1);
        var portField = CreateField("Port", port, 2);
        StyleButton(connect, primary: true);
        connect.AutoSize = false;
        connect.TabIndex = 3;
        loginStatus.TabStop = false;
        card.Controls.AddRange([connect, loginStatus]);

        page.SizeChanged += (_, _) => Arrange();
        page.DpiChangedAfterParent += (_, _) => Arrange();
        Arrange();
        return page;

        Panel CreateField(string label, TextBox input, int tab)
        {
            var field = new Panel { TabIndex = tab };
            var caption = new Label { Text = label, Font = FieldFont, ForeColor = Ink, Dock = DockStyle.Top };
            var surface = new RoundedPanel { BackColor = ChatTheme.Raised, Dock = DockStyle.Fill };
            input.AccessibleName = label;
            input.BackColor = ChatTheme.Raised;
            input.ForeColor = Ink;
            input.Dock = DockStyle.None;
            input.TabIndex = 0;
            input.BorderStyle = BorderStyle.None;
            surface.Controls.Add(input);
            field.Controls.Add(surface);
            field.Controls.Add(caption);
            card.Controls.Add(field);

            surface.SizeChanged += (_, _) => PositionInput();
            input.FontChanged += (_, _) => PositionInput();
            page.DpiChangedAfterParent += (_, _) => PositionInput();
            field.SizeChanged += (_, _) => caption.Height = ScaleLayout(23);
            return field;

            void PositionInput()
            {
                int inset = ScaleLayout(12);
                input.SetBounds(inset, Math.Max(0, (surface.Height - input.PreferredSize.Height) / 2),
                    Math.Max(1, surface.Width - inset * 2), input.PreferredSize.Height);
            }
        }

        void Arrange()
        {
            int S(int n) => ScaleLayout(n);
            int margin = S(12), cardHeight = S(387);
            var available = page.ClientSize;
            int width = Math.Min(S(428), Math.Max(S(230), available.Width - margin * 2));
            page.AutoScrollMinSize = new Size(width + margin * 2, cardHeight + margin * 2);
            // Center when the viewport fits, and let the page scroll on short or high-DPI windows.
            if (page.VerticalScroll.Visible) width = Math.Min(S(428), Math.Max(S(230), page.ClientSize.Width - margin * 2));
            Point scroll = page.AutoScrollPosition;
            card.SetBounds(Math.Max(margin, (page.ClientSize.Width - width) / 2) + scroll.X,
                Math.Max(margin, (page.ClientSize.Height - cardHeight) / 2) + scroll.Y, width, cardHeight);
            int inset = S(28), innerWidth = width - inset * 2;
            int portWidth = S(98), fieldGap = S(12), fieldHeight = S(63);
            mark.SetBounds((width - S(44)) / 2, S(22), S(44), S(44));
            title.SetBounds(inset, S(70), innerWidth, S(48));
            intro.SetBounds(inset, S(118), innerWidth, S(26));
            nameField.SetBounds(inset, S(160), innerWidth, fieldHeight);
            addressField.SetBounds(inset, S(234), innerWidth - portWidth - fieldGap, fieldHeight);
            portField.SetBounds(width - inset - portWidth, S(234), portWidth, fieldHeight);
            connect.SetBounds(inset, S(312), innerWidth, S(40));
            loginStatus.SetBounds(inset, S(356), innerWidth, S(27));
        }
    }

    private void SetLoginStatus(string message, bool error = false)
    {
        loginStatus.Text = message;
        loginStatus.ForeColor = error ? Color.FromArgb(255, 164, 155) : Muted;
        tips.SetToolTip(loginStatus, message);
    }

    private void RefreshLoginView(bool connected)
    {
        connect.Text = connecting ? "Connecting\u2026" : "Connect";
        connect.BackColor = connecting ? ChatTheme.Raised : Accent;
        login.Visible = !connected;
        chatRoot.Visible = connected && imageViewer is null;
        AcceptButton = connection is null && !connecting ? connect : null;
    }
}
