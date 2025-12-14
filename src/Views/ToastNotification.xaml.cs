using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SnipIt.Views;

public partial class ToastNotification : Window
{
    private readonly DispatcherTimer _closeTimer;
    private readonly Action? _clickAction;

    public ToastNotification(string title, string message, int durationMs, Action? onClick = null)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        _clickAction = onClick;

        // Position at bottom-right of screen
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;

        // Auto-close timer
        _closeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(durationMs)
        };
        _closeTimer.Tick += (s, e) =>
        {
            _closeTimer.Stop();
            Close();
        };
        _closeTimer.Start();

        // Fade in animation
        Opacity = 0;
        Loaded += (s, e) =>
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            BeginAnimation(OpacityProperty, animation);
        };
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _closeTimer.Stop();
        _clickAction?.Invoke();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _closeTimer.Stop();
        base.OnClosing(e);
    }
}
