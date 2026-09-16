using System.Drawing;
using System.Drawing.Imaging;
namespace SnipIt.Services;
internal static class ImageExport
{
 public const string Filter="PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp|GIF|*.gif";
 public static void Save(Bitmap bitmap,string path)
 {
  var format=Path.GetExtension(path).ToLowerInvariant() switch{".jpg" or ".jpeg"=>ImageFormat.Jpeg,".bmp"=>ImageFormat.Bmp,".gif"=>ImageFormat.Gif,_=>ImageFormat.Png};
  bitmap.Save(path,format);
 }
}
