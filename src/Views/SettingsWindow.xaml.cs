using System.Windows;
using System.Windows.Controls;
using SnipIt.Models;
using SnipIt.Services;
using SnipIt.Utils;

namespace SnipIt.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsConfig _config;

    public SettingsWindow()
    {
        InitializeComponent();
        _config = AppSettingsConfig.Instance;
        LoadSettings();
        SourceInitialized += SettingsWindow_SourceInitialized;
    }

    private void SettingsWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Apply Windows 11 styling
        Windows11Helper.ApplyWindows11Style(this, useMica: false, useDarkMode: Windows11Helper.IsSystemDarkTheme());
    }

    private void LoadSettings()
    {
        // General settings
        ChkCaptureCursor.IsChecked = _config.CaptureCursor;
        ChkCopyToClipboard.IsChecked = _config.CopyToClipboard;
        ChkPlaySound.IsChecked = _config.PlaySound;
        ChkStartMinimized.IsChecked = _config.StartMinimized;

        // Language
        CmbLanguage.SelectedIndex = _config.Language == Services.Language.Korean ? 0 : 1;

        // Save settings
        TxtSavePath.Text = _config.SavePath;
        CmbFormat.SelectedIndex = _config.DefaultFormat.ToLower() switch
        {
            "jpg" or "jpeg" => 1,
            "bmp" => 2,
            "gif" => 3,
            _ => 0 // PNG
        };

        // GIF settings
        CmbGifFps.SelectedIndex = _config.GifFps switch
        {
            15 => 0,
            60 => 2,
            _ => 1 // 30fps default
        };

        CmbGifQuality.SelectedIndex = _config.GifQuality switch
        {
            GifQualityPreset.Original => 0,
            GifQualityPreset.SkipFramesHalfSize => 2,
            _ => 1 // SkipFrames default
        };

        // Hotkeys
        HotkeyFullScreen.HotkeyConfig = _config.FullScreenHotkey;
        HotkeyActiveWindow.HotkeyConfig = _config.ActiveWindowHotkey;
        HotkeyRegion.HotkeyConfig = _config.RegionHotkey;
        HotkeyGif.HotkeyConfig = _config.GifHotkey;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select default save folder",
            SelectedPath = TxtSavePath.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtSavePath.Text = dialog.SelectedPath;
        }
    }

    private void BtnResetHotkeys_Click(object sender, RoutedEventArgs e)
    {
        HotkeyFullScreen.HotkeyConfig = new HotkeyConfig(ModifierKeys.None, System.Windows.Forms.Keys.PrintScreen);
        HotkeyActiveWindow.HotkeyConfig = new HotkeyConfig(ModifierKeys.Alt, System.Windows.Forms.Keys.PrintScreen);
        HotkeyRegion.HotkeyConfig = new HotkeyConfig(ModifierKeys.Control | ModifierKeys.Shift, System.Windows.Forms.Keys.C);
        HotkeyGif.HotkeyConfig = new HotkeyConfig(ModifierKeys.Control | ModifierKeys.Shift, System.Windows.Forms.Keys.G);
    }

    private void BtnReregisterHotkeys_Click(object sender, RoutedEventArgs e)
    {
        int count = HotkeyService.Instance.ReregisterAllHotkeys();
        System.Windows.MessageBox.Show(
            $"핫키 {count}개가 재등록되었습니다.",
            "핫키 재등록",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // General settings
        _config.CaptureCursor = ChkCaptureCursor.IsChecked ?? false;
        _config.CopyToClipboard = ChkCopyToClipboard.IsChecked ?? false;
        _config.PlaySound = ChkPlaySound.IsChecked ?? false;
        _config.StartMinimized = ChkStartMinimized.IsChecked ?? false;

        // Language
        var selectedLang = (CmbLanguage.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _config.Language = selectedLang == "English" ? Services.Language.English : Services.Language.Korean;
        LocalizationService.Instance.CurrentLanguage = _config.Language;

        // Save settings
        _config.SavePath = TxtSavePath.Text;
        _config.DefaultFormat = CmbFormat.SelectedIndex switch
        {
            1 => "jpg",
            2 => "bmp",
            3 => "gif",
            _ => "png"
        };

        // GIF settings
        var selectedFps = (CmbGifFps.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _config.GifFps = selectedFps switch
        {
            "15" => 15,
            "60" => 60,
            _ => 30
        };

        var selectedQuality = (CmbGifQuality.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _config.GifQuality = selectedQuality switch
        {
            "Original" => GifQualityPreset.Original,
            "SkipFramesHalfSize" => GifQualityPreset.SkipFramesHalfSize,
            _ => GifQualityPreset.SkipFrames
        };

        // Hotkeys
        if (HotkeyFullScreen.HotkeyConfig is not null)
            _config.FullScreenHotkey = HotkeyFullScreen.HotkeyConfig;
        if (HotkeyActiveWindow.HotkeyConfig is not null)
            _config.ActiveWindowHotkey = HotkeyActiveWindow.HotkeyConfig;
        if (HotkeyRegion.HotkeyConfig is not null)
            _config.RegionHotkey = HotkeyRegion.HotkeyConfig;
        if (HotkeyGif.HotkeyConfig is not null)
            _config.GifHotkey = HotkeyGif.HotkeyConfig;

        // Save to file
        _config.Save();

        // Apply to runtime settings
        _config.ApplyToAppSettings();

        // Re-register hotkeys
        ReregisterHotkeys();

        DialogResult = true;
        Close();
    }

    private void ReregisterHotkeys()
    {
        var hotkeyService = HotkeyService.Instance;

        // Unregister existing hotkeys
        hotkeyService.UnregisterHotkey("FullScreen");
        hotkeyService.UnregisterHotkey("ActiveWindow");
        hotkeyService.UnregisterHotkey("Region");
        hotkeyService.UnregisterHotkey("GifRecord");

        // Register new hotkeys
        hotkeyService.RegisterHotkey("FullScreen",
            _config.FullScreenHotkey.Modifiers,
            _config.FullScreenHotkey.Key,
            App.CaptureFullScreen);

        hotkeyService.RegisterHotkey("ActiveWindow",
            _config.ActiveWindowHotkey.Modifiers,
            _config.ActiveWindowHotkey.Key,
            App.CaptureActiveWindow);

        hotkeyService.RegisterHotkey("Region",
            _config.RegionHotkey.Modifiers,
            _config.RegionHotkey.Key,
            App.CaptureRegion);

        hotkeyService.RegisterHotkey("GifRecord",
            _config.GifHotkey.Modifiers,
            _config.GifHotkey.Key,
            App.CaptureGif);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
