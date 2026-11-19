namespace ChatApp.Client;

internal sealed class VoiceRecordingBar : RoundedPanel
{
    private readonly RecordingButton discard = new(RecordingAction.Delete) { AccessibleName = "Discard recording" };
    private readonly RecordingButton pause = new(RecordingAction.Pause) { AccessibleName = "Pause recording" };
    private readonly RecordingButton send = new(RecordingAction.Send) { AccessibleName = "Send voice message" };
    private readonly Label clock = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = ChatTheme.Ink, Font = new Font("Segoe UI", 13), Text = "0:00" };
    private readonly Label dot = new() { Text = "●", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.FromArgb(255, 153, 174) };
    private readonly VoiceLevel waveform = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private MicrophoneRecorder? recorder;
    private VoiceClip? clip;
    private bool uploading;
    public bool Active => recorder is not null || clip is not null;
    public event Action? Changed;
    public event Func<VoiceClip, Task<bool>>? SendRequested;

    public VoiceRecordingBar()
    {
        Dock = DockStyle.Fill;
        Margin = new Padding(14, 9, 14, 9);
        BackColor = ChatTheme.Raised;
        Padding = new Padding(8, 3, 4, 3);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 7, RowCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var style in new[] {
            new ColumnStyle(SizeType.Percent, 100), new ColumnStyle(SizeType.Absolute, 40),
            new ColumnStyle(SizeType.Absolute, 20), new ColumnStyle(SizeType.Absolute, 90),
            new ColumnStyle(SizeType.Absolute, 90), new ColumnStyle(SizeType.Absolute, 44), new ColumnStyle(SizeType.Absolute, 44) })
            layout.ColumnStyles.Add(style);
        layout.Controls.Add(discard, 1, 0); layout.Controls.Add(dot, 2, 0); layout.Controls.Add(clock, 3, 0);
        layout.Controls.Add(waveform, 4, 0); layout.Controls.Add(pause, 5, 0); layout.Controls.Add(send, 6, 0);
        Controls.Add(layout);
        discard.Click += (_, _) => Cancel();
        pause.Click += (_, _) =>
        {
            if (recorder is null) return;
            try
            {
                recorder.TogglePause();
                pause.Action = recorder.Paused ? RecordingAction.Resume : RecordingAction.Pause;
                pause.AccessibleName = recorder.Paused ? "Resume recording" : "Pause recording";
                dot.ForeColor = recorder.Paused ? ChatTheme.Muted : Color.FromArgb(255, 153, 174);
            }
            catch (IOException error) { Fail(error); }
        };
        send.Click += async (_, _) => await SendAsync();
        timer.Tick += (_, _) =>
        {
            if (recorder is null) return;
            clock.Text = WaveAudio.FormatTime(recorder.Elapsed);
            waveform.Push(recorder.Paused ? 0 : recorder.Level);
        };
    }

    public void Start()
    {
        if (Active) return;
        recorder = new MicrophoneRecorder();
        recorder.Failed += Fail;
        try
        {
            recorder.Start();
            pause.Action = RecordingAction.Pause;
            pause.AccessibleName = "Pause recording";
            dot.ForeColor = Color.FromArgb(255, 153, 174);
            clock.Text = "0:00";
            pause.Enabled = true;
            waveform.Clear();
            timer.Start();
            Changed?.Invoke();
        }
        catch (IOException error) { Fail(error); }
    }

    private async Task SendAsync()
    {
        if (!Active || uploading || SendRequested is null) return;
        uploading = true;
        discard.Enabled = pause.Enabled = send.Enabled = false;
        try
        {
            if (recorder is not null)
            {
                timer.Stop();
                clip = recorder.Finish();
                recorder.Dispose(); recorder = null;
                clock.Text = WaveAudio.FormatTime(clip.Duration);
                pause.Action = RecordingAction.Resume;
            }
            if (clip is not null && await SendRequested(clip)) Cancel();
        }
        catch (IOException error) { Fail(error); }
        finally
        {
            uploading = false;
            if (!IsDisposed)
            {
                discard.Enabled = send.Enabled = true;
                pause.Enabled = recorder is not null;
            }
        }
    }

    private void Fail(Exception error)
    {
        Cancel();
        if (!IsDisposed) MessageBox.Show(FindForm(), error.Message, "Voice message", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    public void Cancel()
    {
        timer.Stop();
        recorder?.Dispose(); recorder = null;
        clip?.Dispose(); clip = null;
        Changed?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); recorder?.Dispose(); clip?.Dispose(); }
        base.Dispose(disposing);
    }
}
