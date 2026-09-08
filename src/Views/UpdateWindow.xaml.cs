using System.Windows;
using SnipIt.Services;
namespace SnipIt.Views;
public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();
        DataContext = UpdateService.Instance;
    }
    private async void Check_Click(object sender, RoutedEventArgs e) => await UpdateService.Instance.CheckAsync();
    private async void Download_Click(object sender, RoutedEventArgs e) => await UpdateService.Instance.DownloadAsync();
    private void Cancel_Click(object sender, RoutedEventArgs e) => UpdateService.Instance.Cancel();
    private async void Install_Click(object sender, RoutedEventArgs e) => await UpdateService.Instance.InstallAsync(this);
}
