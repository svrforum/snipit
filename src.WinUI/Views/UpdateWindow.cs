using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SnipIt.Services;
namespace SnipIt.Views;
internal sealed class UpdateWindow:Window
{
 public UpdateWindow()
 {
  Title=Ui.L("SnipIt 업데이트");var panel=new StackPanel{Padding=new Thickness(24),Spacing=14};Content=panel;panel.Children.Add(Ui.Text(Ui.L("더 좋아진 SnipIt"),24));var service=Updater.Instance;panel.Children.Add(Ui.Text(Ui.L("현재 버전 v")+service.CurrentVersion));var status=new TextBlock{TextWrapping=TextWrapping.Wrap};panel.Children.Add(status);var progress=new ProgressBar{Maximum=100};panel.Children.Add(progress);var notes=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=200};panel.Children.Add(notes);
  var check=Ui.AsyncButton(Ui.L("새 버전 확인"),()=>service.CheckAsync());var download=Ui.AsyncButton(Ui.L("다운로드"),service.DownloadAsync);var install=Ui.AsyncButton(Ui.L("업데이트 후 재시작"),()=>service.InstallAsync(this),true);panel.Children.Add(check);panel.Children.Add(download);panel.Children.Add(install);panel.Children.Add(Ui.Button(Ui.L("취소"),service.Cancel));
  void Refresh(){status.Text=service.Status;notes.Text=service.ReleaseNotes;progress.Value=service.Progress;check.IsEnabled=service.CanCheck;download.IsEnabled=service.CanDownload;install.IsEnabled=service.CanInstall;}
  System.ComponentModel.PropertyChangedEventHandler changed=(_,_)=>Refresh();service.PropertyChanged+=changed;Closed+=(_,_)=>service.PropertyChanged-=changed;Refresh();Ui.Size(this,500,650);
 }
}
