using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SnipIt.Services;

namespace SnipIt.Models;

/// <summary>
/// Hotkey configuration using C# 12 primary constructor
/// </summary>
public sealed class HotkeyConfig(ModifierKeys modifiers = ModifierKeys.None, System.Windows.Forms.Keys key = System.Windows.Forms.Keys.None)
{
    public ModifierKeys Modifiers { get; set; } = modifiers;
    public System.Windows.Forms.Keys Key { get; set; } = key;

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
        PropertyNameCaseInsensitive = true
    };

    private static AppSettingsConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppSettingsConfig>(json, JsonOptions) ?? new();
            }
        }
        catch
        {
            // Use defaults on error
        }

        return new AppSettingsConfig();
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
