using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using SnipIt.Services;
using SnipIt.Models;
using Drawing = System.Drawing;
using Path = System.IO.Path;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
namespace SnipIt.Views;
internal sealed partial class EditorWindow : Window
{
    private EditorDocument document;
    private Drawing.Bitmap? rendered;
    private readonly Grid root = new() { Background = Ui.Background, Padding = new Thickness(12) };
    private readonly ContentControl interaction = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly ToolCanvas canvas = new();
    private readonly Image image = new() { Stretch = Stretch.Fill };
    private readonly ScrollViewer scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, ZoomMode = ZoomMode.Disabled, IsTabStop=true };
    private readonly ListView history = new();
    private readonly TextBlock status = Ui.Text(Ui.L("준비"), 12);
    private readonly NumberBox thickness = new() { Value = 3, Minimum = 1, Maximum = 40, Width = 70 };
    private readonly NumberBox fontSize = new() { Value = 16, Minimum = 8, Maximum = 160, Width = 70 };
    private readonly ComboBox fonts = new() { Width = 140 };
    private readonly ToggleButtonLike bold = new(Ui.L("굵게")), italic = new(Ui.L("기울임"));
    private readonly Dictionary<string, Button> toolButtons = new();
    private bool allowClose;
    private long savedRevision;
    private bool dirty => document.Revision != savedRevision || !string.IsNullOrEmpty(inlineText?.Text);
    private string tool = "Select";
    private static string lastTool = "Select";
    private double zoom = 1;
    private double rasterScale=1;
    private XamlRoot? observedRoot;
    private bool fitMode;
    private readonly List<Drawing.PointF> points = new();
    private Polyline? feedback;
    private bool closed, working,loadingHistory;
    private HistoryEntry? currentHistory;
    private int generation;
    public EditorWindow(Drawing.Bitmap bitmap, CaptureHistoryItem? item = null)
    {
        historyItem = item; document = new(bitmap); Title = Ui.L("SnipIt 편집"); interaction.Content = root; Content = interaction;
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(200) });
        AttachHistoryMenu();var toolbar=BuildToolbar();Grid.SetColumnSpan(toolbar,2);root.Children.Add(toolbar);
        canvas.Children.Add(image); canvas.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); scroll.Content = canvas; Grid.SetRow(scroll, 2); root.Children.Add(scroll);
        var side = new Grid { Margin = new Thickness(10, 0, 0, 0) }; side.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); side.RowDefinitions.Add(new() { Height = GridLength.Auto }); side.Children.Add(history);
        var delete = Ui.AsyncButton(Ui.L("선택 이력 삭제"), async () => { if (history.SelectedItem is HistoryEntry e && await Ui.Confirm(this, Ui.L("이력 삭제"), Ui.L("선택한 캡처를 삭제할까요?"))) await HistoryStore.Instance.Delete(e.Item); }); var historyActions=new StackPanel{Spacing=4};historyActions.Children.Add(delete);historyActions.Children.Add(Ui.AsyncButton(Ui.L("전체 이력 삭제"),async()=>{if(await Ui.Confirm(this,Ui.L("전체 이력 삭제"),Ui.L("저장된 캡처 이력을 모두 삭제할까요?")))await HistoryStore.Instance.Delete(null);}));Grid.SetRow(historyActions,1);side.Children.Add(historyActions); Grid.SetRow(side, 2); Grid.SetColumn(side, 1); root.Children.Add(side);
        history.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Margin='4'><Image Source='{Binding Thumbnail}' Width='160' Height='100' Stretch='Uniform'/><TextBlock Text='{Binding Label}' FontSize='11' TextWrapping='Wrap'/></StackPanel></DataTemplate>");
        history.SelectionChanged += HistorySelectionChanged;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6,Margin=new Thickness(0,8,0,0) };var brand=Ui.Text("SnipIt",13);brand.Foreground=Ui.Blue;brand.FontWeight=Microsoft.UI.Text.FontWeights.Bold;brand.Margin=new Thickness(4,0,10,0);footer.Children.Add(brand); footer.Children.Add(Ui.Button("−", () => SetZoom(zoom - .1))); footer.Children.Add(Ui.Button("+", () => SetZoom(zoom + .1))); footer.Children.Add(Ui.Button("1:1", () => SetZoom(1))); footer.Children.Add(Ui.Button(Ui.L("화면 맞춤"), Fit)); footer.Children.Add(status); Grid.SetRow(footer, 3); Grid.SetColumnSpan(footer, 2); root.Children.Add(footer);
        canvas.PointerPressed += Pressed; canvas.PointerMoved += Moved; canvas.PointerReleased += Released; canvas.PointerCanceled += (_, _) => CancelGesture(); canvas.PointerCaptureLost += (_, _) => CancelGesture();
        root.KeyDown += KeyDown; scroll.PointerWheelChanged += (_, e) => { if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) { SetZoom(zoom + Math.Sign(e.GetCurrentPoint(scroll).Properties.MouseWheelDelta) * .1); e.Handled = true; } };
        AppWindow.Closing += async (_, e) => { if (allowClose) return; if (working||loadingHistory) { e.Cancel = true; return; } if (dirty || document.Revision != historyRevision) { e.Cancel = true; await CloseWithHistory(); } }; root.Loaded += async (_, _) => { observedRoot=root.XamlRoot;rasterScale=observedRoot.RasterizationScale;observedRoot.Changed+=DpiChanged;await Refresh(true); ReloadHistory(); };scroll.SizeChanged+=(_,_)=>{if(fitMode&&!working)Fit();}; HistoryStore.Instance.Changed += ReloadHistory;
        Closed += (_, _) => { closed = true;ExitOcr();CancelInlineText();if(observedRoot!=null)observedRoot.Changed-=DpiChanged; generation++; HistoryStore.Instance.Changed -= ReloadHistory; document.Dispose(); rendered?.Dispose(); image.Source = null; history.ItemsSource = null; }; Ui.Size(this, 1150, 700); AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/icon.ico"));
    }
    private void SelectTool(string key, string label) { ExitOcr(); tool = key; canvas.SetToolCursor(key); if(key != "Select") lastTool = key; foreach (var pair in toolButtons) { Ui.Emphasis(pair.Value,pair.Key==key);Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(pair.Value,pair.Key==key?Ui.L("선택한 도구"):Ui.L("편집 도구")); } textOptions.Visibility=key=="Text"?Visibility.Visible:Visibility.Collapsed;status.Text = label + Ui.L(" 도구"); }
    private Drawing.Color Color => selectedColor;
    private Drawing.PointF Point(PointerRoutedEventArgs e) { var p = e.GetCurrentPoint(canvas).Position; return new((float)Math.Clamp(p.X / canvas.Width * (rendered?.Width ?? 1), 0, rendered?.Width ?? 1), (float)Math.Clamp(p.Y / canvas.Height * (rendered?.Height ?? 1), 0, rendered?.Height ?? 1)); }
    private void Pressed(object sender, PointerRoutedEventArgs e)
    {
        if (ocrLayer != null || working || rendered == null || tool == "Select" || inlineText!=null || !e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed) return;if(tool=="Text"){BeginText(Point(e));e.Handled=true;return;} scroll.Focus(FocusState.Pointer); points.Clear(); points.Add(Point(e)); canvas.CapturePointer(e.Pointer); feedback = new Polyline { Stroke = Ui.Blue, StrokeThickness = 2, IsHitTestVisible = false }; if(tool=="Highlight"){feedback.Stroke=new SolidColorBrush(Windows.UI.Color.FromArgb(255,255,235,59));feedback.Opacity=.5;feedback.StrokeThickness=Math.Max(8,thickness.Value*6)*canvas.Width/rendered.Width;} canvas.Children.Add(feedback); e.Handled = true;
    }
    private void Moved(object sender, PointerRoutedEventArgs e)
    {
        if (feedback == null) return; var point = Point(e); if (tool is "Pen" or "Highlight") points.Add(point); else { if (points.Count == 1) points.Add(point); else points[^1] = point; }
        feedback.Points.Clear(); IEnumerable<Drawing.PointF> preview=tool=="Highlight"?SnipIt.Utils.HighlighterStroke.Align(points.ToArray(), Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)):points; if(tool is "Rectangle" or "Blur" or "SoftBlur" or "Crop"){var a=points[0];var b=points[^1];preview=new[]{a,new Drawing.PointF(b.X,a.Y),b,new Drawing.PointF(a.X,b.Y),a};}else if(tool=="Ellipse"){var a=points[0];var b=points[^1];preview=Enumerable.Range(0,49).Select(i=>new Drawing.PointF((a.X+b.X)/2+(b.X-a.X)/2*(float)Math.Cos(i*Math.PI/24),(a.Y+b.Y)/2+(b.Y-a.Y)/2*(float)Math.Sin(i*Math.PI/24)));} foreach (var p in preview) feedback.Points.Add(new(p.X * canvas.Width / rendered!.Width, p.Y * canvas.Height / rendered.Height));
    }
    private async void Released(object sender, PointerRoutedEventArgs e)
    {
        if (feedback == null) return; var end = Point(e); if (points.Count == 1) points.Add(end); else points[^1] = end; if(!double.IsFinite(thickness.Value)||!double.IsFinite(fontSize.Value)){CancelGesture();status.Text=Ui.L("두께와 글자 크기를 숫자로 입력해 주세요.");return;} var mark = new EditMark(tool, tool=="Highlight"?SnipIt.Utils.HighlighterStroke.Align(points.ToArray(), Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)):points.ToArray(), tool == "Highlight" ? Drawing.Color.FromArgb(255,235,59) : Color, (float)(tool=="Highlight"?Math.Max(8,thickness.Value*6):thickness.Value), string.Empty, fonts.SelectedItem?.ToString() ?? "Malgun Gothic", (float)fontSize.Value, (bold.IsChecked == true ? Drawing.FontStyle.Bold : 0) | (italic.IsChecked == true ? Drawing.FontStyle.Italic : 0)); CancelGesture(); canvas.ReleasePointerCapture(e.Pointer);
        document.Add(mark); await Refresh(); await AutoCopyEdit();
    }
    private void CancelGesture() { if (feedback != null) canvas.Children.Remove(feedback); feedback = null; points.Clear(); }
    private async Task Refresh(bool fit = false)
    {
        if (closed || working) return; working = true; interaction.IsHitTestVisible = false; status.Text = Ui.L("이미지 처리 중…"); try { var bitmap = await Task.Run(() => document.Render()); try { var source = await Ui.ImageAsync(bitmap); if (closed) return; rendered?.Dispose(); rendered = bitmap; bitmap = null!; image.Source = source; } finally { bitmap?.Dispose(); } await PersistHistory(); if (fit && AppSettingsConfig.Instance.EditorInitialZoom == EditorInitialZoom.FitToWindow) Fit(); else SetZoom(fit ? 1 : zoom); } catch (Exception ex) { status.Text = ex.Message; } finally { working = false; if (!closed){interaction.IsHitTestVisible = true;App.Queue.TryEnqueue(()=>{if(!closed)scroll.Focus(FocusState.Programmatic);});} }
    }
    private void DpiChanged(XamlRoot sender,XamlRootChangedEventArgs args){if(closed||sender.RasterizationScale==rasterScale)return;rasterScale=sender.RasterizationScale;if(fitMode)Fit();else SetZoom(zoom);}
    private void SetZoom(double value) { if (rendered == null) return;fitMode=false; zoom = Math.Clamp(value, .1, 5); var scale = root.XamlRoot?.RasterizationScale ?? 1; canvas.Width = image.Width = rendered.Width * zoom / scale; canvas.Height = image.Height = rendered.Height * zoom / scale; LayoutOcr(); status.Text = $"{rendered.Width}×{rendered.Height} · {zoom:P0}"; }
    private void Fit() { if (rendered == null) return; SetZoom(Math.Min(1, Math.Min(Math.Max(1, scroll.ActualWidth - 24) / rendered.Width, Math.Max(1, scroll.ActualHeight - 24) / rendered.Height) * (root.XamlRoot?.RasterizationScale ?? 1)));fitMode=true; }
    private void ReloadHistory() { if(!closed){selectingHistory=true;try{var entries=HistoryStore.Instance.Items.Select(x=>new HistoryEntry(x)).ToList();history.ItemsSource=entries;currentHistory=entries.FirstOrDefault(x=>x.Item.Id==historyItem?.Id);history.SelectedItem=currentHistory;}finally{selectingHistory=false;}} }
    private async Task Copy() { await CommitInlineText();if (rendered != null) { await App.Copy(rendered); status.Text = Ui.L("클립보드에 복사했습니다."); } }
    private async Task Save() { await CommitInlineText();if (rendered == null) return; var path = Ui.SaveFile(this, ImageExport.Filter, $"SnipIt_{DateTime.Now:yyyyMMdd_HHmmss}.{AppSettingsConfig.Instance.DefaultFormat}"); if (path == null) return; var savingDocument = document; var savingRevision = document.Revision; using var copy = (Drawing.Bitmap)rendered.Clone(); await Task.Run(() => ImageExport.Save(copy,path)); if (ReferenceEquals(document, savingDocument)) savedRevision = savingRevision; status.Text = Ui.L("저장했습니다."); }
    private async void KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (working || e.OriginalSource is TextBox) return;
        if (ocrLayer != null) { if (e.Key == VirtualKey.Escape) ExitOcr(); else if (ControlDown && e.Key == VirtualKey.C) CopyOcr(); else return; e.Handled = true; return; }
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) { switch (e.Key) { case VirtualKey.Z: await UndoEdit(); break; case VirtualKey.Y: await RedoEdit(); break; case VirtualKey.S: await Save(); break; case VirtualKey.C: await Copy(); break; default: return; } e.Handled = true; return; }
        if (e.Key == VirtualKey.Escape) { CancelGesture(); Close(); e.Handled=true; return; }
        if (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) || Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)) return;
        if (e.OriginalSource is TextBox) return; foreach (var property in typeof(EditorToolShortcuts).GetProperties()) if (property.GetValue(AppSettingsConfig.Instance.EditorShortcuts) is System.Windows.Forms.Keys key && (int)key == (int)e.Key) { SelectTool(property.Name, toolButtons.TryGetValue(property.Name, out var button) ? Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button) : property.Name); e.Handled = true; break; }
    }
}
internal sealed class ToggleButtonLike : Microsoft.UI.Xaml.Controls.Primitives.ToggleButton { public ToggleButtonLike(string label) { Content = label; } }
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class HistoryEntry(CaptureHistoryItem item)
{
    public CaptureHistoryItem Item { get; } = item; public string Label => Item.Label;
    public Microsoft.UI.Xaml.Media.Imaging.BitmapImage Thumbnail { get { var ratio=Math.Min(1,Math.Min(480.0/Math.Max(1,Item.Width),300.0/Math.Max(1,Item.Height)));return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage{DecodePixelWidth=Math.Max(1,(int)(Item.Width*ratio)),DecodePixelHeight=Math.Max(1,(int)(Item.Height*ratio)),UriSource=new Uri(Item.ThumbnailPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase)&&File.Exists(Item.ThumbnailPath)?Item.ThumbnailPath:Item.ImagePath)}; } }
}




