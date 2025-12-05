using System.Windows;
using System.Windows.Input;
using SnipIt.Models;
using SnipIt.Services;

namespace SnipIt.Controls;

public class HotkeyTextBox : System.Windows.Controls.TextBox
{
    public static readonly DependencyProperty HotkeyConfigProperty =
        DependencyProperty.Register(
            nameof(HotkeyConfig),
            typeof(HotkeyConfig),
            typeof(HotkeyTextBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyConfigChanged));

    public HotkeyConfig? HotkeyConfig
    {
        get => (HotkeyConfig?)GetValue(HotkeyConfigProperty);
        set => SetValue(HotkeyConfigProperty, value);
    }

    private static void OnHotkeyConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyTextBox textBox && e.NewValue is HotkeyConfig config)
        {
            textBox.Text = config.ToString();
        }
    }

    public HotkeyTextBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = System.Windows.Input.Cursors.Hand;
        Background = System.Windows.Media.Brushes.White;

        PreviewKeyDown += HotkeyTextBox_PreviewKeyDown;
        GotFocus += HotkeyTextBox_GotFocus;
        LostFocus += HotkeyTextBox_LostFocus;
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(227, 242, 253)); // Light blue
        Text = "Press new hotkey...";
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        Background = System.Windows.Media.Brushes.White;
        if (HotkeyConfig != null)
        {
            Text = HotkeyConfig.ToString();
        }
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        // Get the actual key (handle system keys)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only keys
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Escape cancels
        if (key == Key.Escape)
        {
            if (HotkeyConfig != null)
            {
                Text = HotkeyConfig.ToString();
            }
            Keyboard.ClearFocus();
            return;
        }

        // Build modifiers
        var modifiers = Services.ModifierKeys.None;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers |= Services.ModifierKeys.Control;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers |= Services.ModifierKeys.Alt;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers |= Services.ModifierKeys.Shift;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers |= Services.ModifierKeys.Windows;

        // Convert WPF Key to WinForms Keys
        var formsKey = ConvertToFormsKey(key);
        if (formsKey == System.Windows.Forms.Keys.None)
        {
            return;
        }

        // Create new hotkey config
        HotkeyConfig = new HotkeyConfig(modifiers, formsKey);
        Text = HotkeyConfig.ToString();

        Keyboard.ClearFocus();
    }

    private static System.Windows.Forms.Keys ConvertToFormsKey(Key key)
    {
        return key switch
        {
            // Function keys
            Key.F1 => System.Windows.Forms.Keys.F1,
            Key.F2 => System.Windows.Forms.Keys.F2,
            Key.F3 => System.Windows.Forms.Keys.F3,
            Key.F4 => System.Windows.Forms.Keys.F4,
            Key.F5 => System.Windows.Forms.Keys.F5,
            Key.F6 => System.Windows.Forms.Keys.F6,
            Key.F7 => System.Windows.Forms.Keys.F7,
            Key.F8 => System.Windows.Forms.Keys.F8,
            Key.F9 => System.Windows.Forms.Keys.F9,
            Key.F10 => System.Windows.Forms.Keys.F10,
            Key.F11 => System.Windows.Forms.Keys.F11,
            Key.F12 => System.Windows.Forms.Keys.F12,

            // Special keys
            Key.PrintScreen => System.Windows.Forms.Keys.PrintScreen,
            Key.Scroll => System.Windows.Forms.Keys.Scroll,
            Key.Pause => System.Windows.Forms.Keys.Pause,
            Key.Insert => System.Windows.Forms.Keys.Insert,
            Key.Delete => System.Windows.Forms.Keys.Delete,
            Key.Home => System.Windows.Forms.Keys.Home,
            Key.End => System.Windows.Forms.Keys.End,
            Key.PageUp => System.Windows.Forms.Keys.PageUp,
            Key.PageDown => System.Windows.Forms.Keys.PageDown,
            Key.Back => System.Windows.Forms.Keys.Back,
            Key.Return => System.Windows.Forms.Keys.Return,
            Key.Space => System.Windows.Forms.Keys.Space,
            Key.Tab => System.Windows.Forms.Keys.Tab,
            Key.CapsLock => System.Windows.Forms.Keys.Capital,

            // Arrow keys
            Key.Up => System.Windows.Forms.Keys.Up,
            Key.Down => System.Windows.Forms.Keys.Down,
            Key.Left => System.Windows.Forms.Keys.Left,
            Key.Right => System.Windows.Forms.Keys.Right,

            // Number keys
            Key.D0 => System.Windows.Forms.Keys.D0,
            Key.D1 => System.Windows.Forms.Keys.D1,
            Key.D2 => System.Windows.Forms.Keys.D2,
            Key.D3 => System.Windows.Forms.Keys.D3,
            Key.D4 => System.Windows.Forms.Keys.D4,
            Key.D5 => System.Windows.Forms.Keys.D5,
            Key.D6 => System.Windows.Forms.Keys.D6,
            Key.D7 => System.Windows.Forms.Keys.D7,
            Key.D8 => System.Windows.Forms.Keys.D8,
            Key.D9 => System.Windows.Forms.Keys.D9,

            // Numpad keys
            Key.NumPad0 => System.Windows.Forms.Keys.NumPad0,
            Key.NumPad1 => System.Windows.Forms.Keys.NumPad1,
            Key.NumPad2 => System.Windows.Forms.Keys.NumPad2,
            Key.NumPad3 => System.Windows.Forms.Keys.NumPad3,
            Key.NumPad4 => System.Windows.Forms.Keys.NumPad4,
            Key.NumPad5 => System.Windows.Forms.Keys.NumPad5,
            Key.NumPad6 => System.Windows.Forms.Keys.NumPad6,
            Key.NumPad7 => System.Windows.Forms.Keys.NumPad7,
            Key.NumPad8 => System.Windows.Forms.Keys.NumPad8,
            Key.NumPad9 => System.Windows.Forms.Keys.NumPad9,

            // Letter keys
            Key.A => System.Windows.Forms.Keys.A,
            Key.B => System.Windows.Forms.Keys.B,
            Key.C => System.Windows.Forms.Keys.C,
            Key.D => System.Windows.Forms.Keys.D,
            Key.E => System.Windows.Forms.Keys.E,
            Key.F => System.Windows.Forms.Keys.F,
            Key.G => System.Windows.Forms.Keys.G,
            Key.H => System.Windows.Forms.Keys.H,
            Key.I => System.Windows.Forms.Keys.I,
            Key.J => System.Windows.Forms.Keys.J,
            Key.K => System.Windows.Forms.Keys.K,
            Key.L => System.Windows.Forms.Keys.L,
            Key.M => System.Windows.Forms.Keys.M,
            Key.N => System.Windows.Forms.Keys.N,
            Key.O => System.Windows.Forms.Keys.O,
            Key.P => System.Windows.Forms.Keys.P,
            Key.Q => System.Windows.Forms.Keys.Q,
            Key.R => System.Windows.Forms.Keys.R,
            Key.S => System.Windows.Forms.Keys.S,
            Key.T => System.Windows.Forms.Keys.T,
            Key.U => System.Windows.Forms.Keys.U,
            Key.V => System.Windows.Forms.Keys.V,
            Key.W => System.Windows.Forms.Keys.W,
            Key.X => System.Windows.Forms.Keys.X,
            Key.Y => System.Windows.Forms.Keys.Y,
            Key.Z => System.Windows.Forms.Keys.Z,

            // OEM keys
            Key.OemMinus => System.Windows.Forms.Keys.OemMinus,
            Key.OemPlus => System.Windows.Forms.Keys.Oemplus,
            Key.OemOpenBrackets => System.Windows.Forms.Keys.OemOpenBrackets,
            Key.OemCloseBrackets => System.Windows.Forms.Keys.OemCloseBrackets,
            Key.OemPipe => System.Windows.Forms.Keys.OemPipe,
            Key.OemSemicolon => System.Windows.Forms.Keys.OemSemicolon,
            Key.OemQuotes => System.Windows.Forms.Keys.OemQuotes,
            Key.OemComma => System.Windows.Forms.Keys.Oemcomma,
            Key.OemPeriod => System.Windows.Forms.Keys.OemPeriod,
            Key.OemQuestion => System.Windows.Forms.Keys.OemQuestion,
            Key.OemTilde => System.Windows.Forms.Keys.Oemtilde,

            _ => System.Windows.Forms.Keys.None
        };
    }
}
