using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SnipIt.Services;

namespace SnipIt.Models;

/// <summary>
/// GIF recording quality preset
/// </summary>
public enum GifQualityPreset
{
    /// <summary>
    /// 원본: 모든 프레임 저장 (용량 큼)
    /// </summary>
    Original,

    /// <summary>
    /// 최적화: 중복 프레임 스킵 (기본값)
    /// </summary>
    SkipFrames,

    /// <summary>
    /// 최적화+: 중복 프레임 스킵 + 50% 해상도
    /// </summary>
    SkipFramesHalfSize
}

/// <summary>
/// Hotkey configuration using C# 12 primary constructor
/// </summary>
public sealed class HotkeyConfig
{
    public ModifierKeys Modifiers { get; set; }
    public System.Windows.Forms.Keys Key { get; set; }

    // Parameterless constructor for JSON deserialization
    public HotkeyConfig() : this(ModifierKeys.None, System.Windows.Forms.Keys.None) { }

    public HotkeyConfig(ModifierKeys modifiers, System.Windows.Forms.Keys key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public override string ToString()
    {
        List<string> parts = [];

        if (Modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");

        parts.Add(GetKeyDisplayName(Key));

        return string.Join(" + ", parts);
    }

    private static string GetKeyDisplayName(System.Windows.Forms.Keys key) => key switch
    {
        System.Windows.Forms.Keys.PrintScreen => "PrtSc",
        System.Windows.Forms.Keys.Scroll => "ScrLk",
        System.Windows.Forms.Keys.Pause => "Pause",
        System.Windows.Forms.Keys.Insert => "Ins",
        System.Windows.Forms.Keys.Delete => "Del",
        System.Windows.Forms.Keys.Home => "Home",
        System.Windows.Forms.Keys.End => "End",
        System.Windows.Forms.Keys.PageUp => "PgUp",
        System.Windows.Forms.Keys.PageDown => "PgDn",
        System.Windows.Forms.Keys.Back => "Backspace",
        System.Windows.Forms.Keys.Return => "Enter",
        System.Windows.Forms.Keys.Escape => "Esc",
        System.Windows.Forms.Keys.Space => "Space",
        System.Windows.Forms.Keys.Tab => "Tab",
        System.Windows.Forms.Keys.Capital => "CapsLock",
        System.Windows.Forms.Keys.OemMinus => "-",
        System.Windows.Forms.Keys.Oemplus => "=",
        System.Windows.Forms.Keys.OemOpenBrackets => "[",
        System.Windows.Forms.Keys.OemCloseBrackets => "]",
        System.Windows.Forms.Keys.OemPipe => "\\",
        System.Windows.Forms.Keys.OemSemicolon => ";",
        System.Windows.Forms.Keys.OemQuotes => "'",
        System.Windows.Forms.Keys.Oemcomma => ",",
        System.Windows.Forms.Keys.OemPeriod => ".",
        System.Windows.Forms.Keys.OemQuestion => "/",
        System.Windows.Forms.Keys.Oemtilde => "`",
        >= System.Windows.Forms.Keys.D0 and <= System.Windows.Forms.Keys.D9
            => ((int)key - (int)System.Windows.Forms.Keys.D0).ToString(),
        >= System.Windows.Forms.Keys.NumPad0 and <= System.Windows.Forms.Keys.NumPad9
            => $"Num{(int)key - (int)System.Windows.Forms.Keys.NumPad0}",
        >= System.Windows.Forms.Keys.F1 and <= System.Windows.Forms.Keys.F24
            => $"F{(int)key - (int)System.Windows.Forms.Keys.F1 + 1}",
        _ => key.ToString()
    };
}

/// <summary>
/// Application settings configuration with modern C# patterns
/// </summary>
public sealed class AppSettingsConfig
{
    // General settings
    public bool CaptureCursor { get; set; } = true;
    public bool CopyToClipboard { get; set; } = true;
    public bool PlaySound { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public string SavePath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string DefaultFormat { get; set; } = "png";
    public Language Language { get; set; } = Language.Korean;

    // GIF settings
    public int GifFps { get; set; } = 30; // 15, 30, or 60
    public GifQualityPreset GifQuality { get; set; } = GifQualityPreset.SkipFrames; // 기본값: 중복 프레임 스킵
    public int GifMaxDurationSeconds { get; set; } = 60; // 최대 녹화 시간 (초), 기본 60초

    // Hotkey settings with collection expression
    public HotkeyConfig FullScreenHotkey { get; set; } = new(ModifierKeys.None, System.Windows.Forms.Keys.PrintScreen);
    public HotkeyConfig ActiveWindowHotkey { get; set; } = new(ModifierKeys.Alt, System.Windows.Forms.Keys.PrintScreen);
    public HotkeyConfig RegionHotkey { get; set; } = new(ModifierKeys.Control | ModifierKeys.Shift, System.Windows.Forms.Keys.C);
    public HotkeyConfig GifHotkey { get; set; } = new(ModifierKeys.Control | ModifierKeys.Shift, System.Windows.Forms.Keys.G);

    // Static configuration path
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnipIt",
        "settings.json");

    // Thread-safe lazy singleton
    private static readonly Lazy<AppSettingsConfig> _lazy = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    public static AppSettingsConfig Instance => _lazy.Value;

    // JSON serialization options
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static AppSettingsConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppSettingsConfig>(json, JsonOptions);
                if (config != null)
                {
                    // Ensure hotkey configs have valid defaults if they're null or have no key
                    config.EnsureHotkeyDefaults();
                    return config;
                }
            }
        }
        catch
        {
            // Use defaults on error
        }

        return new AppSettingsConfig();
    }

    /// <summary>
    /// Ensures all hotkey configurations have valid defaults.
    /// This handles cases where settings.json is from an older version
    /// or was partially corrupted.
    /// </summary>
    private void EnsureHotkeyDefaults()
    {
        if (FullScreenHotkey == null || FullScreenHotkey.Key == System.Windows.Forms.Keys.None)
            FullScreenHotkey = new HotkeyConfig(ModifierKeys.None, System.Windows.Forms.Keys.PrintScreen);

        if (ActiveWindowHotkey == null || ActiveWindowHotkey.Key == System.Windows.Forms.Keys.None)
            ActiveWindowHotkey = new HotkeyConfig(ModifierKeys.Alt, System.Windows.Forms.Keys.PrintScreen);

        if (RegionHotkey == null || RegionHotkey.Key == System.Windows.Forms.Keys.None)
            RegionHotkey = new HotkeyConfig(ModifierKeys.Control | ModifierKeys.Shift, System.Windows.Forms.Keys.C);

        if (GifHotkey == null || GifHotkey.Key == System.Windows.Forms.Keys.None)
            GifHotkey = new HotkeyConfig(ModifierKeys.Control | ModifierKeys.Shift, System.Windows.Forms.Keys.G);
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, JsonOptions);
            await File.WriteAllTextAsync(ConfigPath, json, cancellationToken);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public void ApplyToAppSettings()
    {
        var settings = AppSettings.Instance;
        settings.CaptureCursor = CaptureCursor;
        settings.CopyToClipboard = CopyToClipboard;
        settings.PlaySound = PlaySound;
        settings.SavePath = SavePath;
        settings.DefaultFormat = DefaultFormat;
    }
}
