using System.Windows;
using SnipIt.Utils;

namespace SnipIt.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        SourceInitialized += HelpWindow_SourceInitialized;
    }

    private void HelpWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Apply Windows 11 styling
        Windows11Helper.ApplyWindows11Style(this, useMica: false, useDarkMode: Windows11Helper.IsSystemDarkTheme());
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
