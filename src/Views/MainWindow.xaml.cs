using System.ComponentModel;
using System.Windows;
using SnipIt.Services;
using SnipIt.Utils;

namespace SnipIt.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Apply Windows 11 styling with rounded corners
        Windows11Helper.ApplyWindows11Style(this, useMica: false, useDarkMode: Windows11Helper.IsSystemDarkTheme());
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize hotkey service with this window
        HotkeyService.Instance.Initialize(this);
    }

    private void BtnFullScreen_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        // Small delay to ensure window is hidden
        System.Threading.Thread.Sleep(200);
        App.CaptureFullScreen();
    }

    private void BtnActiveWindow_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        System.Threading.Thread.Sleep(200);
        App.CaptureActiveWindow();
    }

    private void BtnRegion_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        App.CaptureRegion();
    }

    private void BtnGifRecord_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        App.CaptureGif();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        var helpWindow = new HelpWindow();
        helpWindow.Owner = this;
        helpWindow.ShowDialog();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            TrayIconService.Instance.ShowNotification("SnipIt", "시스템 트레이에서 실행 중", 1500);
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Just hide instead of closing when X is clicked
        e.Cancel = true;
        Hide();
        TrayIconService.Instance.ShowNotification("SnipIt", "시스템 트레이에서 실행 중. 종료하려면 우클릭하세요.", 1500);
    }
}
