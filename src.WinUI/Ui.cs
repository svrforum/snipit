using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Color = Windows.UI.Color;
namespace SnipIt;
internal static class Ui
{
    public static string L(string text) => NativeText.Get(text);
    public static string ToolLabel(string key)=>key switch {"Select"=>Ui.L("선택"),"Pen"=>Ui.L("펜"),"Arrow"=>Ui.L("화살표"),"Line"=>Ui.L("선"),"Rectangle"=>Ui.L("사각형"),"Ellipse"=>Ui.L("타원"),"Text"=>Ui.L("텍스트"),"Highlight"=>Ui.L("형광펜"),"Blur"=>Ui.L("모자이크"),"Crop"=>Ui.L("자르기"),_=>key};
    public static SolidColorBrush Blue => new(Color.FromArgb(255, 49, 130, 246));
    public static SolidColorBrush Ink => new(Color.FromArgb(255,25,42,65));
    public static SolidColorBrush Muted => new(Color.FromArgb(255,102,119,140));
    public static SolidColorBrush SoftBlue => new(Color.FromArgb(255,233,242,255));
    public static void Emphasis(Button button,bool selected)
    {
        button.Background=selected?Blue:new SolidColorBrush(Microsoft.UI.Colors.White);
        button.Foreground=selected?new SolidColorBrush(Microsoft.UI.Colors.White):Ink;
        button.BorderBrush=selected?Blue:new SolidColorBrush(Color.FromArgb(255,225,232,241));
        button.Resources["ButtonBackgroundPointerOver"]=selected?new SolidColorBrush(Color.FromArgb(255,35,109,225)):SoftBlue;
        button.Resources["ButtonForegroundPointerOver"]=selected?new SolidColorBrush(Microsoft.UI.Colors.White):Ink;
        button.Resources["ButtonBackgroundPressed"]=selected?new SolidColorBrush(Color.FromArgb(255,25,88,191)):new SolidColorBrush(Color.FromArgb(255,215,232,255));
        button.Resources["ButtonForegroundPressed"]=button.Foreground;
    }
    public static SolidColorBrush Background => new(Color.FromArgb(255, 247, 249, 252));
    public static TextBlock Text(string text, double size = 14) => new() { Text = text, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
    public static Button Button(string text, Action click, bool primary = false)
    {
        var button = new Button { Content = text, Padding = new Thickness(12, 8, 12, 8), CornerRadius = new CornerRadius(10), FontSize=13, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, BorderThickness=new Thickness(1), HorizontalAlignment = HorizontalAlignment.Stretch };
        Emphasis(button,primary);Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,text);
        button.Click += (_, _) => click(); return button;
    }
    public static Button AsyncButton(string text, Func<Task> click, bool primary = false)
    {
        bool running = false;
        return Button(text, async () =>
        {
            if (running) return;
            running = true;
            try { await click(); }
            catch (Exception ex) { App.Notify(ex.Message); }
            finally { running = false; }
        }, primary);
    }
    public static nint Handle(Window window) => WinRT.Interop.WindowNative.GetWindowHandle(window);
    public static void Size(Window window, int width, int height)
    {
        var scale = SnipIt.Utils.NativeMethods.GetDpiForWindow(Handle(window)) / 96.0;
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(width * scale), (int)(height * scale)));
    }
    public static Task<BitmapSource> ImageAsync(Bitmap bitmap)
    {
        var image = new WriteableBitmap(bitmap.Width, bitmap.Height);
        var row = System.Buffers.ArrayPool<byte>.Shared.Rent(bitmap.Width * 4);
        BitmapData? data = null;
        try
        {
            data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            using var stream = image.PixelBuffer.AsStream();
            for (int y = 0; y < bitmap.Height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, bitmap.Width * 4);
                stream.Write(row, 0, bitmap.Width * 4);
            }
        }
        finally { if (data != null) bitmap.UnlockBits(data); System.Buffers.ArrayPool<byte>.Shared.Return(row); }
        image.Invalidate(); return Task.FromResult<BitmapSource>(image);
    }
    public static Task<T> OnUi<T>(Func<T> callback)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!App.Queue.TryEnqueue(() => { try { result.SetResult(callback()); } catch (Exception ex) { result.SetException(ex); } })) result.SetException(new InvalidOperationException(Ui.L("앱이 종료 중입니다.")));
        return result.Task;
    }
    public static async Task<bool> Confirm(Window owner, string title, string message)
    {
        var dialog = new ContentDialog { XamlRoot = ((FrameworkElement)owner.Content).XamlRoot, Title = title, Content = message, PrimaryButtonText = Ui.L("확인"), CloseButtonText = Ui.L("취소"), DefaultButton = ContentDialogButton.Close };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    public static string? SaveFile(Window owner, string filter, string name)
    {
        using var picker = new System.Windows.Forms.SaveFileDialog { Filter = filter, FileName = name, AddExtension = true, FilterIndex = Path.GetExtension(name).ToLowerInvariant() switch { ".jpg" or ".jpeg" => 2, ".bmp" => 3, ".gif" when filter == SnipIt.Services.ImageExport.Filter => 4, _ => 1 }, InitialDirectory = SnipIt.Models.AppSettingsConfig.Instance.SavePath };
        return picker.ShowDialog(new Owner(Handle(owner))) == System.Windows.Forms.DialogResult.OK ? picker.FileName : null;
    }
    public static string? SelectFolder(Window owner, string initial)
    {
        using var picker = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = initial, Description = Ui.L("기본 저장 폴더 선택") };
        return picker.ShowDialog(new Owner(Handle(owner))) == System.Windows.Forms.DialogResult.OK ? picker.SelectedPath : null;
    }
    private sealed record Owner(nint Handle) : System.Windows.Forms.IWin32Window;
}
