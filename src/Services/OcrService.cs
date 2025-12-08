using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WinRect = Windows.Foundation.Rect;

namespace SnipIt.Services;

/// <summary>
/// Represents a recognized word with its bounding box
/// </summary>
public class OcrWord
{
    public string Text { get; set; } = "";
    public WinRect BoundingRect { get; set; }
}

/// <summary>
/// Represents a line of recognized text with its words
/// </summary>
public class OcrLine
{
    public string Text { get; set; } = "";
    public WinRect BoundingRect { get; set; }
    public List<OcrWord> Words { get; set; } = [];
}

/// <summary>
/// OCR result containing all recognized text with position information
/// </summary>
public class OcrResultWithRegions
{
    public string FullText { get; set; } = "";
    public List<OcrLine> Lines { get; set; } = [];
}

/// <summary>
/// Service for optical character recognition using Windows.Media.Ocr
/// Supports 26+ languages and works offline
/// </summary>
public static class OcrService
{
    /// <summary>
    /// Extracts text from a bitmap using Windows OCR with user's preferred language
    /// </summary>
    public static async Task<string> ExtractTextAsync(Bitmap bitmap)
    {
        var result = await ExtractTextWithRegionsAsync(bitmap);
        return result.FullText;
    }

    /// <summary>
    /// Extracts text with region information from a bitmap
    /// </summary>
    public static async Task<OcrResultWithRegions> ExtractTextWithRegionsAsync(Bitmap bitmap)
    {
        var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (ocrEngine == null)
        {
            throw new InvalidOperationException("OCR 엔진을 초기화할 수 없습니다. 시스템에 언어 팩이 설치되어 있는지 확인하세요.");
        }

        return await ExtractWithRegionsAsync(bitmap, ocrEngine);
    }

    /// <summary>
    /// Extracts text from a bitmap using a specific language
    /// </summary>
    public static async Task<string> ExtractTextAsync(Bitmap bitmap, string languageTag)
    {
        var language = new Windows.Globalization.Language(languageTag);

        if (!OcrEngine.IsLanguageSupported(language))
        {
            throw new ArgumentException($"언어 '{languageTag}'은(는) OCR에서 지원되지 않거나 설치되지 않았습니다.");
        }

        var ocrEngine = OcrEngine.TryCreateFromLanguage(language);
        if (ocrEngine == null)
        {
            throw new InvalidOperationException($"'{languageTag}' 언어로 OCR 엔진을 초기화할 수 없습니다.");
        }

        var result = await ExtractWithRegionsAsync(bitmap, ocrEngine);
        return result.FullText;
    }

    private static async Task<OcrResultWithRegions> ExtractWithRegionsAsync(Bitmap bitmap, OcrEngine ocrEngine)
    {
        // Convert System.Drawing.Bitmap to Windows.Graphics.Imaging.SoftwareBitmap
        using var softwareBitmap = await ConvertToSoftwareBitmapAsync(bitmap);

        // Perform OCR
        var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);

        var result = new OcrResultWithRegions();
        var fullText = new StringBuilder();

        foreach (var line in ocrResult.Lines)
        {
            var ocrLine = new OcrLine
            {
                Text = line.Text
            };

            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var word in line.Words)
            {
                var ocrWord = new OcrWord
                {
                    Text = word.Text,
                    BoundingRect = word.BoundingRect
                };
                ocrLine.Words.Add(ocrWord);

                // Calculate line bounding rect
                minX = Math.Min(minX, word.BoundingRect.X);
                minY = Math.Min(minY, word.BoundingRect.Y);
                maxX = Math.Max(maxX, word.BoundingRect.X + word.BoundingRect.Width);
                maxY = Math.Max(maxY, word.BoundingRect.Y + word.BoundingRect.Height);
            }

            ocrLine.BoundingRect = new WinRect(minX, minY, maxX - minX, maxY - minY);
            result.Lines.Add(ocrLine);
            fullText.AppendLine(line.Text);
        }

        result.FullText = fullText.ToString().TrimEnd();
        return result;
    }

    /// <summary>
    /// Converts System.Drawing.Bitmap to Windows.Graphics.Imaging.SoftwareBitmap
    /// </summary>
    private static Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
    {
        // Convert to 32bpp BGRA format which is compatible with SoftwareBitmap
        using var convertedBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(convertedBitmap))
        {
            g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
        }

        // Lock bits to get pixel data
        var rect = new Rectangle(0, 0, convertedBitmap.Width, convertedBitmap.Height);
        var bitmapData = convertedBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int stride = bitmapData.Stride;
            int size = Math.Abs(stride) * convertedBitmap.Height;
            byte[] pixels = new byte[size];

            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, pixels, 0, size);

            // Create SoftwareBitmap with BGRA8 format (matches Format32bppArgb)
            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                convertedBitmap.Width,
                convertedBitmap.Height,
                BitmapAlphaMode.Premultiplied);

            softwareBitmap.CopyFromBuffer(pixels.AsBuffer());

            return Task.FromResult(softwareBitmap);
        }
        finally
        {
            convertedBitmap.UnlockBits(bitmapData);
        }
    }

    /// <summary>
    /// Gets available OCR languages installed on the system
    /// </summary>
    public static IReadOnlyList<Windows.Globalization.Language> GetAvailableLanguages()
    {
        return OcrEngine.AvailableRecognizerLanguages;
    }

    /// <summary>
    /// Checks if OCR is available on this system
    /// </summary>
    public static bool IsOcrAvailable()
    {
        return OcrEngine.AvailableRecognizerLanguages.Count > 0;
    }
}
