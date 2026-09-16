using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SnipIt.Services;
using Windows.ApplicationModel.DataTransfer;
using Drawing=System.Drawing;
namespace SnipIt.Views;
internal static class OcrDialog
{
 internal static async Task Show(Window owner,Drawing.Bitmap bitmap)
 {
  var result=await OcrService.ExtractTextWithRegionsAsync(bitmap);
  var panel=new StackPanel{Spacing=8};
  panel.Children.Add(Ui.Text(Ui.L("이미지에서 단어를 선택하거나 아래 텍스트를 편집하세요."),12));
  double scale=Math.Min(1,580.0/bitmap.Width);
  var canvas=new Canvas{Width=bitmap.Width*scale,Height=bitmap.Height*scale};
  canvas.Children.Add(new Image{Source=await Ui.ImageAsync(bitmap),Width=canvas.Width,Height=canvas.Height,Stretch=Stretch.Fill});
  var box=new TextBox{Text=result.FullText,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=140};
  var words=new List<(OcrWord word,ToggleButton button)>();
  foreach(var word in result.Lines.SelectMany(line=>line.Words))
  {
   var button=new ToggleButton{Width=Math.Max(8,word.BoundingRect.Width*scale),Height=Math.Max(8,word.BoundingRect.Height*scale),Padding=new Thickness(0),BorderBrush=Ui.Blue,BorderThickness=new Thickness(1),Background=new SolidColorBrush(Windows.UI.Color.FromArgb(30,49,130,246))};
   ToolTipService.SetToolTip(button,word.Text);Canvas.SetLeft(button,word.BoundingRect.X*scale);Canvas.SetTop(button,word.BoundingRect.Y*scale);canvas.Children.Add(button);words.Add((word,button));
   button.Click+=(_,_)=>box.Text=string.Join(" ",words.Where(x=>x.button.IsChecked==true).Select(x=>x.word.Text));
  }
  panel.Children.Add(new ScrollViewer{Content=canvas,MaxHeight=300,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto});
  panel.Children.Add(Ui.Button(Ui.L("전체 텍스트"),()=>{foreach(var entry in words)entry.button.IsChecked=false;box.Text=result.FullText;}));panel.Children.Add(box);
  var dialog=new ContentDialog{XamlRoot=((FrameworkElement)owner.Content).XamlRoot,Title=result.FullText.Length==0?Ui.L("인식된 텍스트가 없습니다"):Ui.L("텍스트 인식"),Content=panel,PrimaryButtonText=Ui.L("복사"),CloseButtonText=Ui.L("닫기")};
  if(await dialog.ShowAsync()==ContentDialogResult.Primary){var data=new DataPackage();data.SetText(box.Text);Clipboard.SetContent(data);Clipboard.Flush();}
 }
}
