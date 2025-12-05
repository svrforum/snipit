using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SnipIt.Views;

public partial class RecordingControlWindow : Window
{
    private DispatcherTimer? _blinkTimer;

    public event Action? StopRequested;

    public RecordingControlWindow()
    {
        InitializeComponent();
        StartBlinking();
    }

    private void StartBlinking()
    {
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (s, e) =>
        {
            RecordingDot.Visibility = RecordingDot.Visibility == Visibility.Visible
                ? Visibility.Hidden
                : Visibility.Visible;
        };
        _blinkTimer.Start();
    }

    public void UpdateTime(string timeText)
    {
        RecordingTimeText.Text = timeText;
    }

    private void StopButton_Click(object sender, MouseButtonEventArgs e)
    {
        StopRequested?.Invoke();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            StopRequested?.Invoke();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_blinkTimer != null)
        {
            _blinkTimer.Stop();
            _blinkTimer = null;
        }
        base.OnClosed(e);
    }
}
