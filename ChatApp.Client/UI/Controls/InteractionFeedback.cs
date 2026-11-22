namespace ChatApp.Client;

internal sealed class InteractionFeedback
{
    private readonly Control owner;
    private bool hovered, pointerDown, keyDown;
    internal bool Hovered => owner.Enabled && hovered;
    internal bool Pressed => owner.Enabled && ((pointerDown && hovered) || keyDown);

    internal InteractionFeedback(Control owner)
    {
        this.owner = owner;
        owner.MouseEnter += (_, _) => { hovered = true; owner.Invalidate(); };
        owner.MouseLeave += (_, _) => { hovered = false; owner.Invalidate(); };
        owner.MouseMove += (_, e) =>
        {
            bool inside = owner.ClientRectangle.Contains(e.Location);
            if (inside != hovered) { hovered = inside; owner.Invalidate(); }
        };
        owner.MouseDown += (_, e) =>
        {
            if (owner.Enabled && e.Button == MouseButtons.Left)
            {
                pointerDown = true;
                hovered = owner.ClientRectangle.Contains(e.Location);
                owner.Invalidate();
            }
        };
        owner.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) { pointerDown = false; owner.Invalidate(); }
        };
        // Clear the pressed state when capture is lost before a mouse-up event arrives.
        owner.MouseCaptureChanged += (_, _) =>
        {
            if (!owner.Capture) { pointerDown = false; owner.Invalidate(); }
        };
        owner.KeyDown += (_, e) =>
        {
            if (owner.Enabled && e.KeyCode == Keys.Space) { keyDown = true; owner.Invalidate(); }
        };
        owner.KeyUp += (_, e) =>
        {
            if (e.KeyCode == Keys.Space) { keyDown = false; owner.Invalidate(); }
        };
        owner.LostFocus += (_, _) => { pointerDown = keyDown = false; owner.Invalidate(); };
        owner.GotFocus += (_, _) => owner.Invalidate();
        owner.EnabledChanged += (_, _) =>
        {
            if (!owner.Enabled) hovered = pointerDown = keyDown = false;
            owner.Cursor = owner.Enabled ? Cursors.Hand : Cursors.Default;
            owner.Invalidate();
        };
        owner.VisibleChanged += (_, _) =>
        {
            if (!owner.Visible) hovered = pointerDown = keyDown = false;
            owner.Invalidate();
        };
    }
}
