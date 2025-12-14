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
        Loaded += SettingsWindow_Loaded;
        SourceInitialized += SettingsWindow_SourceInitialized;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsWindow] LoadSettings failed: {ex.Message}");
        }
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        // Hide all panels first
        if (PanelGeneral != null) PanelGeneral.Visibility = Visibility.Collapsed;
        if (PanelGif != null) PanelGif.Visibility = Visibility.Collapsed;
        if (PanelHotkeys != null) PanelHotkeys.Visibility = Visibility.Collapsed;
        if (PanelEditor != null) PanelEditor.Visibility = Visibility.Collapsed;

        // Show selected panel
        if (sender == NavGeneral && PanelGeneral != null)
            PanelGeneral.Visibility = Visibility.Visible;
        else if (sender == NavGif && PanelGif != null)
            PanelGif.Visibility = Visibility.Visible;
        else if (sender == NavHotkeys && PanelHotkeys != null)
            PanelHotkeys.Visibility = Visibility.Visible;
        else if (sender == NavEditor && PanelEditor != null)
            PanelEditor.Visibility = Visibility.Visible;
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
        ChkSilentMode.IsChecked = _config.SilentMode;

        // Editor initial zoom
        CmbEditorZoom.SelectedIndex = _config.EditorInitialZoom == EditorInitialZoom.Original ? 1 : 0;

        // Magnifier position
        CmbMagnifierPosition.SelectedIndex = _config.MagnifierPosition switch
        {
            MagnifierPosition.TopLeft => 0,
            MagnifierPosition.TopRight => 1,
            MagnifierPosition.BottomLeft => 2,
            MagnifierPosition.BottomRight => 3,
            MagnifierPosition.ScreenTopLeft => 4,
            MagnifierPosition.ScreenTopRight => 5,
            MagnifierPosition.ScreenBottomLeft => 6,
            MagnifierPosition.ScreenBottomRight => 7,
            _ => 0
        };

        // Capture dimming opacity
        SliderDimming.Value = _config.CaptureDimmingOpacity;
        TxtDimmingValue.Text = $"{_config.CaptureDimmingOpacity}%";

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

        CmbGifMaxDuration.SelectedIndex = _config.GifMaxDurationSeconds switch
        {
            30 => 0,
            120 => 2,
            180 => 3,
            _ => 1 // 60s default
        };

        // Hotkeys
        HotkeyFullScreen.HotkeyConfig = _config.FullScreenHotkey;
        HotkeyActiveWindow.HotkeyConfig = _config.ActiveWindowHotkey;
        HotkeyRegion.HotkeyConfig = _config.RegionHotkey;
        HotkeyGif.HotkeyConfig = _config.GifHotkey;

        // Editor tool shortcuts
        var editorShortcuts = _config.EditorShortcuts;
        EditorKeySelect.ShortcutKey = editorShortcuts.Select;
        EditorKeyPen.ShortcutKey = editorShortcuts.Pen;
        EditorKeyArrow.ShortcutKey = editorShortcuts.Arrow;
        EditorKeyLine.ShortcutKey = editorShortcuts.Line;
        EditorKeyRectangle.ShortcutKey = editorShortcuts.Rectangle;
        EditorKeyEllipse.ShortcutKey = editorShortcuts.Ellipse;
        EditorKeyText.ShortcutKey = editorShortcuts.Text;
        EditorKeyHighlight.ShortcutKey = editorShortcuts.Highlight;
        EditorKeyBlur.ShortcutKey = editorShortcuts.Blur;
        EditorKeyCrop.ShortcutKey = editorShortcuts.Crop;
    }

    private void SliderDimming_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtDimmingValue != null)
        {
            TxtDimmingValue.Text = $"{(int)e.NewValue}%";
        }
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

    private void BtnResetEditorShortcuts_Click(object sender, RoutedEventArgs e)
    {
        EditorKeySelect.ShortcutKey = System.Windows.Input.Key.V;
        EditorKeyPen.ShortcutKey = System.Windows.Input.Key.P;
        EditorKeyArrow.ShortcutKey = System.Windows.Input.Key.A;
        EditorKeyLine.ShortcutKey = System.Windows.Input.Key.L;
        EditorKeyRectangle.ShortcutKey = System.Windows.Input.Key.R;
        EditorKeyEllipse.ShortcutKey = System.Windows.Input.Key.E;
        EditorKeyText.ShortcutKey = System.Windows.Input.Key.T;
        EditorKeyHighlight.ShortcutKey = System.Windows.Input.Key.H;
        EditorKeyBlur.ShortcutKey = System.Windows.Input.Key.M;
        EditorKeyCrop.ShortcutKey = System.Windows.Input.Key.C;
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
        _config.SilentMode = ChkSilentMode.IsChecked ?? false;

        // Editor initial zoom
        var selectedZoom = (CmbEditorZoom.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _config.EditorInitialZoom = selectedZoom == "Original" ? EditorInitialZoom.Original : EditorInitialZoom.FitToWindow;

        // Magnifier position
        var selectedMagPos = (CmbMagnifierPosition.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _config.MagnifierPosition = selectedMagPos switch
        {
            "TopLeft" => MagnifierPosition.TopLeft,
            "TopRight" => MagnifierPosition.TopRight,
            "BottomLeft" => MagnifierPosition.BottomLeft,
            "BottomRight" => MagnifierPosition.BottomRight,
            "ScreenTopLeft" => MagnifierPosition.ScreenTopLeft,
            "ScreenTopRight" => MagnifierPosition.ScreenTopRight,
            "ScreenBottomLeft" => MagnifierPosition.ScreenBottomLeft,
            "ScreenBottomRight" => MagnifierPosition.ScreenBottomRight,
            _ => MagnifierPosition.TopLeft
        };

        // Capture dimming opacity
        _config.CaptureDimmingOpacity = (int)SliderDimming.Value;

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

        var selectedMaxDuration = (CmbGifMaxDuration.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _config.GifMaxDurationSeconds = selectedMaxDuration switch
        {
            "30" => 30,
            "120" => 120,
            "180" => 180,
            _ => 60
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

        // Editor tool shortcuts
        _config.EditorShortcuts.Select = EditorKeySelect.ShortcutKey;
        _config.EditorShortcuts.Pen = EditorKeyPen.ShortcutKey;
        _config.EditorShortcuts.Arrow = EditorKeyArrow.ShortcutKey;
        _config.EditorShortcuts.Line = EditorKeyLine.ShortcutKey;
        _config.EditorShortcuts.Rectangle = EditorKeyRectangle.ShortcutKey;
        _config.EditorShortcuts.Ellipse = EditorKeyEllipse.ShortcutKey;
        _config.EditorShortcuts.Text = EditorKeyText.ShortcutKey;
        _config.EditorShortcuts.Highlight = EditorKeyHighlight.ShortcutKey;
        _config.EditorShortcuts.Blur = EditorKeyBlur.ShortcutKey;
        _config.EditorShortcuts.Crop = EditorKeyCrop.ShortcutKey;

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
