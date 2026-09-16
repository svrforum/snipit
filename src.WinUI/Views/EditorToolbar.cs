using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
namespace SnipIt.Views;
internal sealed class ToolbarWrapPanel:Panel
{
 protected override Size MeasureOverride(Size available)
 {
  double x=0,y=0,line=0,maxWidth=0;
  foreach(UIElement child in Children){child.Measure(new Size(available.Width,double.PositiveInfinity));var size=child.DesiredSize;if(x>0&&x+size.Width>available.Width){maxWidth=Math.Max(maxWidth,x);y+=line;x=0;line=0;}x+=size.Width;line=Math.Max(line,size.Height);}
  return new Size(Math.Max(maxWidth,x),y+line);
 }
 protected override Size ArrangeOverride(Size available)
 {
  double x=0,y=0,line=0;
  foreach(UIElement child in Children){var size=child.DesiredSize;if(x>0&&x+size.Width>available.Width){y+=line;x=0;line=0;}child.Arrange(new Rect(x,y,size.Width,size.Height));x+=size.Width;line=Math.Max(line,size.Height);}
  return available;
 }
}
internal sealed partial class EditorWindow
{
 private System.Drawing.Color selectedColor=System.Drawing.Color.FromArgb(240,68,82);
 private readonly StackPanel textOptions=new(){Orientation=Orientation.Horizontal,Spacing=6,Visibility=Visibility.Collapsed,Margin=new Thickness(4)};
 private readonly Border colorSample=new(){Width=22,Height=22,CornerRadius=new CornerRadius(5)};
 private FrameworkElement BuildToolbar()
 {
  var wrap=new ToolbarWrapPanel();
  StackPanel Group(){var group=new StackPanel{Orientation=Orientation.Horizontal,Spacing=4,Margin=new Thickness(0,3,12,3),VerticalAlignment=VerticalAlignment.Center};wrap.Children.Add(group);return group;}
  var files=Group();files.Children.Add(Ui.AsyncButton(Ui.L("저장"),Save,true));files.Children.Add(Ui.AsyncButton(Ui.L("복사"),Copy));files.Children.Add(Ui.AsyncButton("OCR",Ocr));files.Children.Add(Ui.Button(Ui.L("도움말"),()=>new HelpWindow().Activate()));
  var undo=Group();undo.Children.Add(Ui.AsyncButton("↶",UndoEdit));undo.Children.Add(Ui.AsyncButton("↷",RedoEdit));
  ToolTipService.SetToolTip(undo.Children[0],Ui.L("실행 취소 (Ctrl+Z)"));ToolTipService.SetToolTip(undo.Children[1],Ui.L("다시 실행 (Ctrl+Y)"));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(undo.Children[0],Ui.L("실행 취소"));Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(undo.Children[1],Ui.L("다시 실행"));
  var drawing=Group();
  foreach(var (label,key,glyph) in new[]{(Ui.L("선택"),"Select","\uE8B0"),(Ui.L("펜"),"Pen","\uE70F"),(Ui.L("화살표"),"Arrow","➔"),(Ui.L("직선"),"Line","╱"),(Ui.L("사각형"),"Rectangle","▢"),(Ui.L("타원"),"Ellipse","◯"),(Ui.L("텍스트"),"Text","A"),(Ui.L("형광펜"),"Highlight","\uE7E6"),(Ui.L("모자이크"),"Blur","▦"),(Ui.L("블러"),"SoftBlur","◌"),(Ui.L("자르기"),"Crop","\uE7A8")})
  {
   var button=Ui.Button(label,()=>SelectTool(key,label));button.Width=34;button.Height=34;button.Padding=new Thickness(4);button.CornerRadius=new CornerRadius(7);
   button.Content=new TextBlock{Text=glyph,FontFamily=new FontFamily(key is "Select" or "Pen" or "Highlight" or "Crop"?"Segoe MDL2 Assets":"Segoe UI"),FontSize=18,HorizontalAlignment=HorizontalAlignment.Center};
   var shortcut=typeof(SnipIt.Models.EditorToolShortcuts).GetProperty(key)?.GetValue(SnipIt.Models.AppSettingsConfig.Instance.EditorShortcuts)?.ToString();ToolTipService.SetToolTip(button,shortcut==null?label:$"{label} ({shortcut})");drawing.Children.Add(button);toolButtons.Add(key,button);
  }
  var palette=Group();palette.Children.Add(Ui.Text(Ui.L("색상"),12));var choose=Ui.AsyncButton(Ui.L("사용자 지정 색상"),PickColor);choose.Padding=new Thickness(4);choose.Content=colorSample;choose.Height=34;ToolTipService.SetToolTip(choose,Ui.L("사용자 지정 색상"));palette.Children.Add(choose);
  foreach(var (name,value) in new[]{(Ui.L("빨강"),0xF04452),(Ui.L("초록"),0x00C471),(Ui.L("파랑"),0x3182F6),(Ui.L("노랑"),0xFFC107),(Ui.L("검정"),0x191F28),(Ui.L("흰색"),0xFFFFFF)})
  {var color=System.Drawing.Color.FromArgb(255,(value>>16)&255,(value>>8)&255,value&255);var button=Ui.Button(name,()=>SetColor(color));button.Content=null;button.Width=24;button.Height=24;button.Padding=new Thickness(0);button.CornerRadius=new CornerRadius(6);button.Background=new SolidColorBrush(Windows.UI.Color.FromArgb(255,color.R,color.G,color.B));ToolTipService.SetToolTip(button,name);palette.Children.Add(button);}
  var stroke=Group();stroke.Children.Add(Ui.Text(Ui.L("두께"),12));var slider=new Slider{Minimum=1,Maximum=40,Value=thickness.Value,Width=90,VerticalAlignment=VerticalAlignment.Center};slider.ValueChanged+=(_,_)=>thickness.Value=slider.Value;thickness.ValueChanged+=(_,_)=>{if(double.IsFinite(thickness.Value))slider.Value=thickness.Value;};stroke.Children.Add(slider);stroke.Children.Add(thickness);
  foreach(var font in new[]{"Malgun Gothic","NanumGothic","Gulim","Dotum","Batang","Arial","Times New Roman","Consolas","Segoe UI"})fonts.Items.Add(font);fonts.SelectedIndex=0;
  foreach(var child in new UIElement[]{Ui.Text(Ui.L("글꼴"),12),fonts,fontSize,bold,italic})textOptions.Children.Add(child);
  var panel=new StackPanel();panel.Children.Add(wrap);panel.Children.Add(textOptions);SetColor(selectedColor);SelectTool(lastTool,Ui.ToolLabel(lastTool));
  return new Border{Child=panel,Background=new SolidColorBrush(Microsoft.UI.Colors.White),BorderBrush=Ui.SoftBlue,BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(4,4,4,8),Margin=new Thickness(0,0,0,8)};
 }
 private async Task UndoEdit(){await CommitInlineText();if(closed||working)return;ExitOcr();document.Undo();await Refresh();}
 private async Task RedoEdit(){await CommitInlineText();if(closed||working)return;ExitOcr();document.Redo();await Refresh();}
 private void SetColor(System.Drawing.Color color){selectedColor=color;colorSample.Background=new SolidColorBrush(Windows.UI.Color.FromArgb(color.A,color.R,color.G,color.B));}
 private async Task PickColor()
 {
  var picker=new ColorPicker{Color=Windows.UI.Color.FromArgb(255,selectedColor.R,selectedColor.G,selectedColor.B),IsAlphaEnabled=false};
  var dialog=new ContentDialog{XamlRoot=root.XamlRoot,Title=Ui.L("색상 선택"),Content=picker,PrimaryButtonText=Ui.L("적용"),CloseButtonText=Ui.L("취소")};
  if(await dialog.ShowAsync()==ContentDialogResult.Primary)SetColor(System.Drawing.Color.FromArgb(picker.Color.R,picker.Color.G,picker.Color.B));
 }
}
