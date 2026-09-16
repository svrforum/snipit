using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Windowing;
using Drawing=System.Drawing;
using SnipIt.Models;
namespace SnipIt.Views;
internal sealed class CaptureOverlay:Window
{
 private readonly TaskCompletionSource<Drawing.Rectangle?> result=new(TaskCreationOptions.RunContinuationsAsynchronously);
 private readonly ToolCanvas canvas=new();
 private readonly ContentControl input=new(){IsTabStop=true,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};
 private readonly Drawing.Rectangle bounds;
 private readonly Drawing.Bitmap snapshot;
 private readonly Rectangle outline=new(){Stroke=Ui.Blue,StrokeThickness=2,IsHitTestVisible=false};
 private readonly Rectangle[] shades=new Rectangle[4];
 private readonly TextBlock label=new(){Text=Ui.L("드래그해서 영역 선택 · Esc 취소"),Foreground=new SolidColorBrush(Microsoft.UI.Colors.White),FontSize=16,IsHitTestVisible=false};
 private readonly Image magnified=new(){Width=144,Height=144,Stretch=Stretch.Fill};
 private readonly Border magnifier;
 private readonly System.Diagnostics.Stopwatch throttle=System.Diagnostics.Stopwatch.StartNew();
 private Windows.Foundation.Point start;
 private bool selecting,closed;
 private CaptureOverlay(Drawing.Bitmap bitmap,Drawing.Rectangle physical)
 {
  canvas.SetToolCursor("Capture");snapshot=(Drawing.Bitmap)bitmap.Clone();bounds=physical;Title=Ui.L("SnipIt 영역 선택");input.Content=canvas;Content=input;canvas.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);
  var image=new Image{Stretch=Stretch.Fill,IsHitTestVisible=false};canvas.Children.Add(image);
  for(int i=0;i<4;i++){shades[i]=new Rectangle{Fill=new SolidColorBrush(Windows.UI.Color.FromArgb((byte)(255*Math.Clamp(AppSettingsConfig.Instance.CaptureDimmingOpacity,0,100)/100),0,0,0)),IsHitTestVisible=false};canvas.Children.Add(shades[i]);}
  canvas.Children.Add(outline);canvas.Children.Add(label);Canvas.SetLeft(label,20);Canvas.SetTop(label,20);
  magnifier=new Border{Child=magnified,BorderBrush=Ui.Blue,BorderThickness=new Thickness(2),IsHitTestVisible=false,Visibility=Visibility.Collapsed};canvas.Children.Add(magnifier);
  var cancel=Ui.Button(Ui.L("취소 · Esc"),Close);canvas.Children.Add(cancel);Canvas.SetLeft(cancel,20);Canvas.SetTop(cancel,52);
  canvas.Loaded+=async(_,_)=>{image.Source=await Ui.ImageAsync(snapshot);image.Width=canvas.ActualWidth;image.Height=canvas.ActualHeight;Shade(0,0,0,0);input.Focus(FocusState.Programmatic);};
  canvas.PointerPressed+=(_,e)=>{if(!e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)return;start=e.GetCurrentPoint(canvas).Position;selecting=true;canvas.CapturePointer(e.Pointer);e.Handled=true;};
  canvas.PointerMoved+=async(_,e)=>
  {
   var p=e.GetCurrentPoint(canvas).Position;
   if(selecting){var x=Math.Min(p.X,start.X);var y=Math.Min(p.Y,start.Y);outline.Width=Math.Abs(p.X-start.X);outline.Height=Math.Abs(p.Y-start.Y);Canvas.SetLeft(outline,x);Canvas.SetTop(outline,y);Shade(x,y,outline.Width,outline.Height);label.Text=$"{(int)(outline.Width*bounds.Width/canvas.ActualWidth)} × {(int)(outline.Height*bounds.Height/canvas.ActualHeight)} · " + Ui.L("Esc 취소");}
   if(throttle.ElapsedMilliseconds<33||closed)return;throttle.Restart();
   int size=Math.Min(36,Math.Min(snapshot.Width,snapshot.Height));
   int px=Math.Clamp((int)(p.X*bounds.Width/canvas.ActualWidth)-size/2,0,snapshot.Width-size),py=Math.Clamp((int)(p.Y*bounds.Height/canvas.ActualHeight)-size/2,0,snapshot.Height-size);
   using var crop=snapshot.Clone(new Drawing.Rectangle(px,py,size,size),Drawing.Imaging.PixelFormat.Format32bppArgb);
   magnified.Source=await Ui.ImageAsync(crop);magnifier.Visibility=Visibility.Visible;
   var position=AppSettingsConfig.Instance.MagnifierPosition;
   bool right=position is MagnifierPosition.TopRight or MagnifierPosition.BottomRight or MagnifierPosition.ScreenTopRight or MagnifierPosition.ScreenBottomRight;
   bool bottom=position is MagnifierPosition.BottomLeft or MagnifierPosition.BottomRight or MagnifierPosition.ScreenBottomLeft or MagnifierPosition.ScreenBottomRight;
   bool fixedPosition=(int)position>=4;
   double mx=fixedPosition?(right?canvas.ActualWidth-168:20):p.X+(right?20:-168);
   double my=fixedPosition?(bottom?canvas.ActualHeight-168:100):p.Y+(bottom?20:-168);
   Canvas.SetLeft(magnifier,Math.Clamp(mx,0,Math.Max(0,canvas.ActualWidth-148)));Canvas.SetTop(magnifier,Math.Clamp(my,0,Math.Max(0,canvas.ActualHeight-148)));
  };
  canvas.PointerReleased+=(_,e)=>{if(selecting)CompleteSelection(e.GetCurrentPoint(canvas).Position);};
  canvas.PointerCanceled+=(_,_)=>Close();
  input.KeyDown+=(_,e)=>
  {
   if(e.Key==Windows.System.VirtualKey.Escape){e.Handled=true;Close();return;}
   if(canvas.ActualWidth<=0||canvas.ActualHeight<=0||!SnipIt.Utils.NativeMethods.GetCursorPos(out var cursor))return;
   var step=Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)?10:1;
   var dx=e.Key==Windows.System.VirtualKey.Left?-step:e.Key==Windows.System.VirtualKey.Right?step:0;
   var dy=e.Key==Windows.System.VirtualKey.Up?-step:e.Key==Windows.System.VirtualKey.Down?step:0;
   if(dx!=0||dy!=0){SnipIt.Utils.NativeMethods.SetCursorPos(Math.Clamp(cursor.X+dx,bounds.Left,bounds.Right-1),Math.Clamp(cursor.Y+dy,bounds.Top,bounds.Bottom-1));e.Handled=true;return;}
   var point=new Windows.Foundation.Point((cursor.X-bounds.X)*canvas.ActualWidth/bounds.Width,(cursor.Y-bounds.Y)*canvas.ActualHeight/bounds.Height);
   if(e.Key==Windows.System.VirtualKey.Space){if(selecting)CompleteSelection(point);else{start=point;selecting=true;}e.Handled=true;}
   else if(e.Key==Windows.System.VirtualKey.Enter&&selecting){CompleteSelection(point);e.Handled=true;}
  };
  Closed+=(_,_)=>{closed=true;snapshot.Dispose();magnified.Source=null;result.TrySetResult(null);};
  if(AppWindow.Presenter is OverlappedPresenter presenter){presenter.SetBorderAndTitleBar(false,false);presenter.IsAlwaysOnTop=true;presenter.IsResizable=false;}
  AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(bounds.X,bounds.Y,bounds.Width,bounds.Height));
 }
 private void CompleteSelection(Windows.Foundation.Point end)
 {
  selecting=false;var rect=ToPhysical(start,end,bounds,canvas.ActualWidth,canvas.ActualHeight);
  if(rect.Width>1&&rect.Height>1){result.TrySetResult(rect);Close();}
 }
 internal static Drawing.Rectangle ToPhysical(Windows.Foundation.Point start,Windows.Foundation.Point end,Drawing.Rectangle bounds,double width,double height)
 {
  // Snap floating-point round-trip noise at integer pixel boundaries only.
  static double Pixel(double value){var nearest=Math.Round(value);return Math.Abs(value-nearest)<0.001?nearest:value;}
  int left=(int)Math.Floor(Pixel(Math.Min(start.X,end.X)*bounds.Width/width)),top=(int)Math.Floor(Pixel(Math.Min(start.Y,end.Y)*bounds.Height/height));
  int right=(int)Math.Ceiling(Pixel(Math.Max(start.X,end.X)*bounds.Width/width)),bottom=(int)Math.Ceiling(Pixel(Math.Max(start.Y,end.Y)*bounds.Height/height));
  return Drawing.Rectangle.Intersect(Drawing.Rectangle.FromLTRB(bounds.X+left,bounds.Y+top,bounds.X+right,bounds.Y+bottom),bounds);
 }
 private void Shade(double x,double y,double width,double height)
 {
  var w=canvas.ActualWidth;var h=canvas.ActualHeight;
  var areas=new[]{new Windows.Foundation.Rect(0,0,w,Math.Max(0,y)),new Windows.Foundation.Rect(0,y,Math.Max(0,x),Math.Max(0,height)),new Windows.Foundation.Rect(x+width,y,Math.Max(0,w-x-width),Math.Max(0,height)),new Windows.Foundation.Rect(0,y+height,w,Math.Max(0,h-y-height))};
  for(int i=0;i<4;i++){Canvas.SetLeft(shades[i],areas[i].X);Canvas.SetTop(shades[i],areas[i].Y);shades[i].Width=areas[i].Width;shades[i].Height=areas[i].Height;}
 }
 public static Task<Drawing.Rectangle?> Select(Drawing.Bitmap image,Drawing.Rectangle bounds){var window=new CaptureOverlay(image,bounds);window.Activate();return window.result.Task;}
}

