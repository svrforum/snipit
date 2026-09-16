using System.Runtime.InteropServices;
namespace SnipIt.Services;
internal static class CaptureVisibility
{
 [DllImport("user32.dll",SetLastError=true)]
 [return:MarshalAs(UnmanagedType.Bool)]
 private static extern bool SetWindowDisplayAffinity(nint window,uint affinity);
 internal static bool Exclude(Microsoft.UI.Xaml.Window window)=>SetWindowDisplayAffinity(Ui.Handle(window),0x11);
}
