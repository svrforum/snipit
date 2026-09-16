using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SnipIt.Services;
using Drawing=System.Drawing;
namespace SnipIt.Views;
internal sealed partial class EditorWindow
{
 private TextBox? inlineText;
 private Drawing.PointF textOrigin;
 private void BeginText(Drawing.PointF point)
 {
  if(rendered==null)return;textOrigin=point;
  var box=new TextBox{PlaceholderText=Ui.L("텍스트 입력 · Enter 완료 / Esc 취소"),MinWidth=220,MinHeight=34,FontFamily=new FontFamily(fonts.SelectedItem?.ToString()??"Malgun Gothic"),FontSize=Math.Max(10,fontSize.Value*canvas.Width/rendered.Width),Foreground=new SolidColorBrush(Windows.UI.Color.FromArgb(255,Color.R,Color.G,Color.B)),AcceptsReturn=false};
  inlineText=box;Canvas.SetLeft(box,point.X*canvas.Width/rendered.Width);Canvas.SetTop(box,point.Y*canvas.Height/rendered.Height);canvas.Children.Add(box);
  box.KeyDown+=async(_,e)=>{if(e.Key==Windows.System.VirtualKey.Escape){e.Handled=true;CancelInlineText();scroll.Focus(FocusState.Programmatic);}else if(e.Key==Windows.System.VirtualKey.Enter){e.Handled=true;await CommitInlineText();}};
  box.LostFocus+=async(_,_)=>{if(!closed&&ReferenceEquals(inlineText,box))await CommitInlineText();};
  App.Queue.TryEnqueue(()=>{if(!closed&&ReferenceEquals(inlineText,box))box.Focus(FocusState.Programmatic);});
 }
 private void CancelInlineText(){var box=inlineText;inlineText=null;if(box!=null)canvas.Children.Remove(box);}
 private Task pendingTextCommit=Task.CompletedTask;
 private Task CommitInlineText(){if(!pendingTextCommit.IsCompleted)return pendingTextCommit;return pendingTextCommit=CommitInlineTextCore();}
 private async Task CommitInlineTextCore()
 {
  var box=inlineText;if(box==null)return;var value=box.Text;CancelInlineText();if(closed||string.IsNullOrWhiteSpace(value))return;
  if(!double.IsFinite(fontSize.Value)){status.Text=Ui.L("글자 크기를 숫자로 입력해 주세요.");return;}
  document.Add(new EditMark("Text",new[]{textOrigin},Color,1,value,fonts.SelectedItem?.ToString()??"Malgun Gothic",(float)fontSize.Value,(bold.IsChecked==true?Drawing.FontStyle.Bold:0)|(italic.IsChecked==true?Drawing.FontStyle.Italic:0)));
  await Refresh(); await AutoCopyEdit();
 }
}
