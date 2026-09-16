using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SnipIt.Models;
namespace SnipIt.Views;
internal sealed class MainWindow:Window
{
 private readonly List<Action> refreshShortcuts=new();
 internal void RefreshHotkeys(){foreach(var refresh in refreshShortcuts)refresh();}
 public MainWindow()
 {
  Title="SnipIt";
  var root=new Grid{Padding=new Thickness(16,10,16,12),Background=Ui.Background};
  root.RowDefinitions.Add(new(){Height=new GridLength(46)});
  root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
  root.RowDefinitions.Add(new(){Height=new GridLength(36)});Content=root;
  var header=new Grid();var identity=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
  identity.Children.Add(new Image{Source=new BitmapImage(new Uri("ms-appx:///Assets/brand-icon.png")),Width=30,Height=30});
  var name=Ui.Text("SnipIt",20);name.FontWeight=Microsoft.UI.Text.FontWeights.Bold;name.Foreground=Ui.Ink;identity.Children.Add(name);header.Children.Add(identity);
  var ready=Ui.Text(Ui.L("●  캡처 준비"),11);ready.Foreground=Ui.Blue;header.Children.Add(new Border{Child=ready,Background=Ui.SoftBlue,CornerRadius=new CornerRadius(12),Padding=new Thickness(10,5,10,5),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center});root.Children.Add(header);
  var actions=new StackPanel{Spacing=6};Grid.SetRow(actions,1);root.Children.Add(actions);
  foreach(var (label,mode,symbol) in new[]{(Ui.L("영역 캡처"),"Region",Symbol.Crop),(Ui.L("전체 화면"),"Full",Symbol.FullScreen),(Ui.L("활성 창"),"Window",Symbol.Document),(Ui.L("GIF 녹화"),"Gif",Symbol.Video)})
  {
   bool primary=mode=="Region";
   var button=Ui.Button(label,()=>App.Capture(mode),primary);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
   button.Height=primary?76:42;button.Padding=new Thickness(12,6,12,6);button.CornerRadius=new CornerRadius(primary?14:10);
   var row=new Grid{ColumnSpacing=10};row.ColumnDefinitions.Add(new(){Width=new GridLength(26)});row.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
   var icon=new SymbolIcon(symbol){Foreground=primary?new SolidColorBrush(Microsoft.UI.Colors.White):Ui.Blue};row.Children.Add(icon);
   var words=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Spacing=3};var title=Ui.Text(label,primary?17:13);title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;words.Children.Add(title);
   if(primary){var description=Ui.Text(Ui.L("필요한 부분만, 선명하게"),11);description.Opacity=.85;words.Children.Add(description);}
   Grid.SetColumn(words,1);row.Children.Add(words);
   var shortcut=Ui.Text("",10);shortcut.Opacity=primary?.88:.7;shortcut.MaxWidth=142;shortcut.TextTrimming=TextTrimming.CharacterEllipsis;
   if(primary){words.Children.Add(shortcut);}else{Grid.SetColumn(shortcut,2);row.Children.Add(shortcut);}
   refreshShortcuts.Add(()=>{var config=AppSettingsConfig.Instance;var key=(mode switch{"Region"=>config.RegionHotkey,"Full"=>config.FullScreenHotkey,"Window"=>config.ActiveWindowHotkey,_=>config.GifHotkey}).ToString();shortcut.Text=key;ToolTipService.SetToolTip(button,key);});
   button.Content=row;actions.Children.Add(button);
  }
  var footer=new Grid{ColumnSpacing=6};for(int i=0;i<3;i++)footer.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
  var links=new[]{Ui.Button(Ui.L("업데이트"),App.ShowUpdates),Ui.Button(Ui.L("도움말"),()=>new HelpWindow().Activate()),Ui.Button(Ui.L("설정"),App.OpenSettings)};
  for(int i=0;i<links.Length;i++){links[i].Padding=new Thickness(8,5,8,5);links[i].Background=Ui.Background;links[i].BorderThickness=new Thickness(0);Grid.SetColumn(links[i],i);footer.Children.Add(links[i]);}
  Grid.SetRow(footer,2);root.Children.Add(footer);RefreshHotkeys();
  Ui.Size(this,400,370);AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets/icon.ico"));AppWindow.Closing+=(_,e)=>{if(!App.Exiting){e.Cancel=true;AppWindow.Hide();}};
 }
}
internal sealed class HelpWindow:Window
{
 public HelpWindow(){Title=Ui.L("SnipIt 도움말");Content=new ScrollViewer{Content=new TextBlock{Margin=new Thickness(24),TextWrapping=TextWrapping.Wrap,Text=Ui.L("단축키로 화면을 캡처하고 편집할 수 있습니다.\n\n영역 선택: 드래그 / Esc 취소\n편집: 펜·화살표·선·도형·텍스트·형광펜·모자이크·자르기\nCtrl+Z 실행 취소 / Ctrl+Y 다시 실행\nCtrl+S 저장 / Ctrl+C 복사\n마우스 휠+Ctrl 확대·축소\n\n설정에서 캡처 단축키, 편집 단축키, GIF 품질, 업데이트를 바꿀 수 있습니다.")}};Ui.Size(this,460,390);}
}

