using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
namespace SnipIt.Views;
internal sealed class QuickCaptureWindow:Window
{
 private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer timer;
 private bool saving;
 public QuickCaptureWindow()
 {
  Title=Ui.L("SnipIt 캡처 완료");var panel=new StackPanel{Padding=new Thickness(12),Spacing=6,Background=Ui.Background};Content=panel;
  panel.Children.Add(Ui.Text(Ui.L("캡처 완료 · 클립보드에 복사됨"),14));var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};panel.Children.Add(buttons);
  buttons.Children.Add(Ui.Button(Ui.L("편집 (E)"),()=>{App.EditRecent();Close();},true));buttons.Children.Add(Ui.AsyncButton(Ui.L("저장 (S)"),Save));buttons.Children.Add(Ui.Button(Ui.L("닫기"),()=>{if(!saving)Close();}));
  panel.KeyDown+=async(_,e)=>{if(saving)return;if(e.Key==Windows.System.VirtualKey.E){App.EditRecent();Close();}else if(e.Key==Windows.System.VirtualKey.S)await Save();else if(e.Key==Windows.System.VirtualKey.Escape)Close();};
  Ui.Size(this,340,120);if(AppWindow.Presenter is OverlappedPresenter presenter){presenter.SetBorderAndTitleBar(false,false);presenter.IsAlwaysOnTop=true;presenter.IsResizable=false;}
  var area=System.Windows.Forms.Screen.FromHandle(Ui.Handle(this)).WorkingArea;AppWindow.Move(new Windows.Graphics.PointInt32(area.Right-AppWindow.Size.Width-16,area.Bottom-AppWindow.Size.Height-16));
  timer=App.Queue.CreateTimer();timer.Interval=TimeSpan.FromSeconds(6);timer.IsRepeating=false;timer.Tick+=(_,_)=>{if(!saving)Close();};timer.Start();
  AppWindow.Closing+=(_,e)=>{if(saving)e.Cancel=true;};Closed+=(_,_)=>timer.Stop();panel.Loaded+=(_,_)=>((Button)buttons.Children[0]).Focus(FocusState.Programmatic);
 }
 private async Task Save(){if(saving)return;saving=true;timer.Stop();try{await App.SaveRecent(this);}finally{saving=false;Close();}}
}
