using System.Windows;
using System.Windows.Input;

namespace SnipIt.Controls;

/// <summary>
/// 에디터 도구 단축키 입력용 텍스트 박스 (단일 키만 허용)
/// </summary>
public class EditorShortcutTextBox : System.Windows.Controls.TextBox
{
    public static readonly DependencyProperty ShortcutKeyProperty =
        DependencyProperty.Register(
            nameof(ShortcutKey),
            typeof(Key),
            typeof(EditorShortcutTextBox),
            new FrameworkPropertyMetadata(Key.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnShortcutKeyChanged));

    public Key ShortcutKey
    {
        get => (Key)GetValue(ShortcutKeyProperty);
        set => SetValue(ShortcutKeyProperty, value);
    }

    private static void OnShortcutKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EditorShortcutTextBox textBox && e.NewValue is Key key)
        {
            textBox.Text = GetKeyDisplayName(key);
        }
    }

    public EditorShortcutTextBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Cursor = System.Windows.Input.Cursors.Hand;
        FontSize = 14;
        TextAlignment = TextAlignment.Center;
        VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
        Padding = new Thickness(8, 6, 8, 6);
        BorderThickness = new Thickness(1);
        BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(220, 220, 220));
        Background = System.Windows.Media.Brushes.White;
        Foreground = System.Windows.Media.Brushes.Black;

        PreviewKeyDown += EditorShortcutTextBox_PreviewKeyDown;
        GotFocus += EditorShortcutTextBox_GotFocus;
        LostFocus += EditorShortcutTextBox_LostFocus;
        Loaded += EditorShortcutTextBox_Loaded;
    }

    private void EditorShortcutTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        // Set initial text from ShortcutKey
        Text = GetKeyDisplayName(ShortcutKey);
    }

    private void EditorShortcutTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(227, 242, 253));
        BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(49, 130, 246));
        Text = "...";
    }

    private void EditorShortcutTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        Background = System.Windows.Media.Brushes.White;
        BorderBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(220, 220, 220));
        Text = GetKeyDisplayName(ShortcutKey);
    }

    private void EditorShortcutTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 수정자 키는 무시
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Escape는 취소
        if (key == Key.Escape)
        {
            Text = GetKeyDisplayName(ShortcutKey);
            Keyboard.ClearFocus();
            return;
        }

        // 단일 문자/숫자 키만 허용 (수정자 없이)
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        // A-Z, 0-9 키만 허용
        if ((key >= Key.A && key <= Key.Z) || (key >= Key.D0 && key <= Key.D9))
        {
            ShortcutKey = key;
            Text = GetKeyDisplayName(key);
            Keyboard.ClearFocus();
        }
    }

    private static string GetKeyDisplayName(Key key) => key switch
    {
        Key.None => "",
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
        _ => key.ToString()
    };
}
