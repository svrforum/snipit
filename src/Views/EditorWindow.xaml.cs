using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SnipIt.Models;
using SnipIt.Services;
using SnipIt.Utils;
using SnipIt.ViewModels;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Cursors = System.Windows.Input.Cursors;
using Panel = System.Windows.Controls.Panel;

namespace SnipIt.Views;

public partial class EditorWindow : Window
{
    private Bitmap _originalBitmap;
    private readonly Stack<Bitmap> _undoStack = new();
    private readonly Stack<Bitmap> _redoStack = new();

    private string _currentTool = "Select";
    private Color _currentColor = Colors.Red;
    private double _strokeWidth = 3;

    // Text tool options
    private string _fontFamily = "Malgun Gothic";
    private double _fontSize = 16;
    private bool _isBold = false;
    private bool _isItalic = false;

    // Highlight tool - default yellow color
    private Color _highlightColor = Color.FromArgb(128, 255, 235, 59); // Semi-transparent yellow

    private Point _startPoint;
    private bool _isDrawing;
    private Shape? _currentShape;
    private System.Windows.Controls.TextBox? _currentTextBox;

    private readonly ObservableCollection<HistoryItemViewModel> _historyItems = new();
    private static string? _lastUsedTool;

    // Zoom
    private double _zoomLevel = 1.0;
    private const double ZoomMin = 0.1;
    private const double ZoomMax = 5.0;
    private const double ZoomStep = 0.1;

    public EditorWindow(Bitmap bitmap)
    {
        _originalBitmap = bitmap;
        InitializeComponent();
        Loaded += EditorWindow_Loaded;
        KeyDown += EditorWindow_KeyDown;
        SourceInitialized += EditorWindow_SourceInitialized;
    }

    private void EditorWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Apply Windows 11 styling
        Windows11Helper.ApplyWindows11Style(this, useMica: false, useDarkMode: Windows11Helper.IsSystemDarkTheme());
    }

    private void EditorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadImage(_originalBitmap);
            UpdateImageSizeText();
            LoadHistory();
            RestoreLastTool();

            // Subscribe to history changes
            CaptureHistoryService.Instance.HistoryChanged += OnHistoryChanged;

            // Delay clipboard copy to ensure UI is fully loaded
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    CopyToClipboard();
                    StatusText.Text = "클립보드에 복사됨";
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"오류: {ex.Message}";
        }
    }

    private void EditorWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Skip if typing in a textbox
        if (e.OriginalSource is System.Windows.Controls.TextBox)
            return;

        var modifiers = Keyboard.Modifiers;

        // Ctrl shortcuts
        if (modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.C:
                    CopyToClipboard();
                    e.Handled = true;
                    break;
                case Key.S:
                    BtnSave_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Key.Z:
                    Undo();
                    e.Handled = true;
                    break;
                case Key.Y:
                    Redo();
                    e.Handled = true;
                    break;
            }
            return;
        }

        // Tool shortcuts (no modifiers)
        if (modifiers == System.Windows.Input.ModifierKeys.None)
        {
            switch (e.Key)
            {
                case Key.V:
                    SelectTool("Select");
                    e.Handled = true;
                    break;
                case Key.P:
                    SelectTool("Pen");
                    e.Handled = true;
                    break;
                case Key.A:
                    SelectTool("Arrow");
                    e.Handled = true;
                    break;
                case Key.L:
                    SelectTool("Line");
                    e.Handled = true;
                    break;
                case Key.R:
                    SelectTool("Rectangle");
                    e.Handled = true;
                    break;
                case Key.E:
                    SelectTool("Ellipse");
                    e.Handled = true;
                    break;
                case Key.T:
                    SelectTool("Text");
                    e.Handled = true;
                    break;
                case Key.H:
                    SelectTool("Highlight");
                    e.Handled = true;
                    break;
                case Key.M:
                    SelectTool("Blur");
                    e.Handled = true;
                    break;
                case Key.C:
                    SelectTool("Crop");
                    e.Handled = true;
                    break;
                case Key.Escape:
                    // Cancel current operation or deselect tool
                    SelectTool("Select");
                    e.Handled = true;
                    break;
            }
        }
    }

    private void SelectTool(string toolName)
    {
        _currentTool = toolName;

        // Update toggle buttons
        BtnSelect.IsChecked = toolName == "Select";
        BtnPen.IsChecked = toolName == "Pen";
        BtnArrow.IsChecked = toolName == "Arrow";
        BtnLine.IsChecked = toolName == "Line";
        BtnRectangle.IsChecked = toolName == "Rectangle";
        BtnEllipse.IsChecked = toolName == "Ellipse";
        BtnText.IsChecked = toolName == "Text";
        BtnHighlight.IsChecked = toolName == "Highlight";
        BtnBlur.IsChecked = toolName == "Blur";

        // Show/hide text options panel
        TextOptionsPanel.Visibility = toolName == "Text" ? Visibility.Visible : Visibility.Collapsed;
        HighlightOptionsPanel.Visibility = toolName == "Highlight" ? Visibility.Visible : Visibility.Collapsed;
        TextOptionsSeparator.Visibility = (toolName == "Text" || toolName == "Highlight") ? Visibility.Visible : Visibility.Collapsed;

        UpdateCursor();

        // Remember last used drawing tool
        if (toolName != "Select")
        {
            _lastUsedTool = toolName;
        }

        StatusText.Text = $"도구: {GetToolDisplayName(toolName)}";
    }

    private string GetToolDisplayName(string toolName)
    {
        return toolName switch
        {
            "Select" => "선택 (V)",
            "Pen" => "펜 (P)",
            "Arrow" => "화살표 (A)",
            "Line" => "직선 (L)",
            "Rectangle" => "사각형 (R)",
            "Ellipse" => "타원 (E)",
            "Text" => "텍스트 (T)",
            "Highlight" => "형광펜 (H)",
            "Blur" => "모자이크 (M)",
            "Crop" => "자르기 (C)",
            _ => toolName
        };
    }

    private void Undo()
    {
        if (_undoStack.Count > 0)
        {
            _redoStack.Push((Bitmap)_originalBitmap.Clone());
            _originalBitmap.Dispose();
            _originalBitmap = _undoStack.Pop();
            DrawingCanvas.Children.Clear();
            LoadImage(_originalBitmap);
            CopyToClipboard();
            StatusText.Text = "실행 취소됨";
        }
    }

    private void Redo()
    {
        if (_redoStack.Count > 0)
        {
            _undoStack.Push((Bitmap)_originalBitmap.Clone());
            _originalBitmap.Dispose();
            _originalBitmap = _redoStack.Pop();
            DrawingCanvas.Children.Clear();
            LoadImage(_originalBitmap);
            CopyToClipboard();
            StatusText.Text = "다시 실행됨";
        }
    }

    private void CopyToClipboard()
    {
        try
        {
            var finalBitmap = RenderFinalImage();
            var bitmapSource = BitmapToImageSource(finalBitmap);
            if (bitmapSource != null)
            {
                System.Windows.Clipboard.SetImage(bitmapSource);
                StatusText.Text = "클립보드에 복사됨";
            }
            finalBitmap.Dispose();
        }
        catch
        {
            StatusText.Text = "클립보드 복사 실패";
        }
    }

    private void LoadHistory()
    {
        _historyItems.Clear();
        var history = CaptureHistoryService.Instance.History.ToList();
        for (int i = 0; i < history.Count; i++)
        {
            var item = history[i];
            var thumbnail = CaptureHistoryService.Instance.LoadThumbnail(item);
            _historyItems.Add(new HistoryItemViewModel
            {
                Item = item,
                Thumbnail = thumbnail,
                Index = history.Count - i, // 최신 항목이 #1
            });
        }
        HistoryList.ItemsSource = _historyItems;
    }

    private void OnHistoryChanged()
    {
        // Use BeginInvoke to avoid deadlock when called from UI thread
        Dispatcher.BeginInvoke(LoadHistory);
    }

    private void RestoreLastTool()
    {
        // Simplified: just restore the tool name, button states are handled by SelectTool
        if (!string.IsNullOrEmpty(_lastUsedTool) && _lastUsedTool != "Select")
        {
            SelectTool(_lastUsedTool);
        }
    }

    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is HistoryItemViewModel viewModel)
        {
            var bitmap = CaptureHistoryService.Instance.LoadImage(viewModel.Item);
            if (bitmap != null)
            {
                // Clear previous selection
                foreach (var item in _historyItems)
                {
                    item.IsSelected = false;
                }

                // Set current selection
                viewModel.IsSelected = true;

                _originalBitmap?.Dispose();
                _originalBitmap = bitmap;
                DrawingCanvas.Children.Clear();
                LoadImage(_originalBitmap);
                UpdateImageSizeText();
                StatusText.Text = $"#{viewModel.Index} 캡쳐 불러옴 ({viewModel.DisplayDateTime})";
            }
        }
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to clear all capture history?",
            "Clear History",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            CaptureHistoryService.Instance.ClearHistory();
            StatusText.Text = "History cleared";
        }
    }

    private void LoadImage(Bitmap bitmap)
    {
        BaseImage.Source = BitmapToImageSource(bitmap);
        DrawingCanvas.Width = bitmap.Width;
        DrawingCanvas.Height = bitmap.Height;
    }

    private void SaveState()
    {
        _undoStack.Push((Bitmap)_originalBitmap.Clone());
        _redoStack.Clear();
    }

    #region Tool Selection
    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn)
        {
            // Uncheck all other tool buttons
            foreach (var child in ((Panel)btn.Parent).Children)
            {
                if (child is ToggleButton otherBtn && otherBtn != btn)
                {
                    otherBtn.IsChecked = false;
                }
            }

            btn.IsChecked = true;
            _currentTool = btn.Tag?.ToString() ?? "Select";

            // Show/hide text options panel
            TextOptionsPanel.Visibility = _currentTool == "Text" ? Visibility.Visible : Visibility.Collapsed;
            HighlightOptionsPanel.Visibility = _currentTool == "Highlight" ? Visibility.Visible : Visibility.Collapsed;
            TextOptionsSeparator.Visibility = (_currentTool == "Text" || _currentTool == "Highlight") ? Visibility.Visible : Visibility.Collapsed;

            UpdateCursor();

            // Remember last used drawing tool (not Select)
            if (_currentTool != "Select")
            {
                _lastUsedTool = _currentTool;
            }

            StatusText.Text = $"도구: {GetToolDisplayName(_currentTool)}";
        }
    }

    private void UpdateCursor()
    {
        DrawingCanvas.Cursor = _currentTool switch
        {
            "Pen" => Cursors.Pen,
            "Highlight" => Cursors.Hand, // Different cursor for highlighter
            "Text" => Cursors.IBeam,
            "Select" => Cursors.Arrow,
            _ => Cursors.Cross
        };
    }
    #endregion

    #region Drawing
    private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't start new drawing if textbox is active
        if (_currentTextBox != null)
            return;

        _startPoint = e.GetPosition(DrawingCanvas);

        if (_currentTool == "Text")
        {
            CreateTextBox(_startPoint);
            return;
        }

        _isDrawing = true;
        DrawingCanvas.CaptureMouse();

        _currentShape = CreateShape();
        if (_currentShape != null)
        {
            // Set position for shapes that use Canvas positioning (Rectangle, Ellipse)
            // Polyline uses Points collection with absolute coordinates, no Canvas position needed
            // Line uses X1,Y1,X2,Y2 properties, no Canvas position needed
            if (_currentShape is Rectangle || _currentShape is Ellipse)
            {
                Canvas.SetLeft(_currentShape, _startPoint.X);
                Canvas.SetTop(_currentShape, _startPoint.Y);
            }
            DrawingCanvas.Children.Add(_currentShape);
        }
    }

    private void DrawingCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDrawing || _currentShape == null) return;

        Point currentPoint = e.GetPosition(DrawingCanvas);
        UpdateShape(_currentShape, _startPoint, currentPoint);
    }

    private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            DrawingCanvas.ReleaseMouseCapture();

            if (_currentTool == "Blur" && _currentShape is Rectangle blurRect)
            {
                ApplyBlurEffect(blurRect);
                DrawingCanvas.Children.Remove(blurRect);
            }
            else if (_currentTool == "Crop" && _currentShape is Rectangle cropRect)
            {
                ApplyCropEffect(cropRect);
                DrawingCanvas.Children.Remove(cropRect);
            }
            else if (_currentShape != null && _currentTool != "Select")
            {
                // Save state and merge drawing to bitmap for undo/redo support
                SaveAndMergeDrawing();
            }

            _currentShape = null;

            // Auto-copy to clipboard after any edit
            if (_currentTool != "Select")
            {
                CopyToClipboard();
            }
        }
    }

    private void SaveAndMergeDrawing()
    {
        try
        {
            // Save current state for undo
            SaveState();

            // Render canvas to bitmap and merge with original
            var finalBitmap = RenderFinalImage();

            // Replace original bitmap
            _originalBitmap.Dispose();
            _originalBitmap = finalBitmap;

            // Clear canvas and reload merged image
            DrawingCanvas.Children.Clear();
            LoadImage(_originalBitmap);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"편집 오류: {ex.Message}";
        }
    }

    private Shape? CreateShape()
    {
        var stroke = new SolidColorBrush(_currentColor);

        return _currentTool switch
        {
            "Pen" => new Polyline
            {
                Stroke = stroke,
                StrokeThickness = _strokeWidth,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Points = new PointCollection { _startPoint }
            },
            "Highlight" => new Polyline
            {
                Stroke = new SolidColorBrush(_highlightColor),
                StrokeThickness = 20, // Fixed thick stroke for highlighter
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap = PenLineCap.Square,
                Points = new PointCollection { _startPoint }
            },
            "Arrow" or "Line" => new Line
            {
                Stroke = stroke,
                StrokeThickness = _strokeWidth,
                X1 = _startPoint.X,
                Y1 = _startPoint.Y
            },
            "Rectangle" => new Rectangle
            {
                Stroke = stroke,
                StrokeThickness = _strokeWidth,
                Fill = System.Windows.Media.Brushes.Transparent
            },
            "Ellipse" => new Ellipse
            {
                Stroke = stroke,
                StrokeThickness = _strokeWidth,
                Fill = System.Windows.Media.Brushes.Transparent
            },
            "Blur" => new Rectangle
            {
                Stroke = new SolidColorBrush(Colors.Blue),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 255))
            },
            "Crop" => new Rectangle
            {
                Stroke = new SolidColorBrush(Colors.Blue),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 0, 0, 255)),
                Tag = "CropRect"
            },
            _ => null
        };
    }

    private void UpdateShape(Shape shape, Point start, Point current)
    {
        switch (shape)
        {
            case Polyline polyline:
                polyline.Points.Add(current);
                break;

            case Line line:
                line.X2 = current.X;
                line.Y2 = current.Y;

                if (_currentTool == "Arrow")
                {
                    // Arrow head will be added on mouse up
                }
                break;

            case Rectangle rect:
                double x = Math.Min(start.X, current.X);
                double y = Math.Min(start.Y, current.Y);
                double width = Math.Abs(current.X - start.X);
                double height = Math.Abs(current.Y - start.Y);

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                rect.Width = width;
                rect.Height = height;
                break;

            case Ellipse ellipse:
                x = Math.Min(start.X, current.X);
                y = Math.Min(start.Y, current.Y);
                width = Math.Abs(current.X - start.X);
                height = Math.Abs(current.Y - start.Y);

                Canvas.SetLeft(ellipse, x);
                Canvas.SetTop(ellipse, y);
                ellipse.Width = width;
                ellipse.Height = height;
                break;
        }
    }

    private void CreateTextBox(Point position)
    {
        _isDrawing = false;
        DrawingCanvas.ReleaseMouseCapture();

        _currentTextBox = new System.Windows.Controls.TextBox
        {
            MinWidth = 100,
            MinHeight = 30,
            FontFamily = new System.Windows.Media.FontFamily(_fontFamily),
            FontSize = _fontSize,
            FontWeight = _isBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = _isItalic ? FontStyles.Italic : FontStyles.Normal,
            Foreground = new SolidColorBrush(_currentColor),
            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
            Padding = new Thickness(4),
            AcceptsReturn = false,
            Focusable = true,
            IsTabStop = true
        };

        Canvas.SetLeft(_currentTextBox, position.X);
        Canvas.SetTop(_currentTextBox, position.Y);
        DrawingCanvas.Children.Add(_currentTextBox);

        _currentTextBox.LostFocus += TextBox_LostFocus;
        _currentTextBox.KeyDown += TextBox_KeyDown;

        // Delay focus to ensure TextBox is fully rendered
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _currentTextBox?.Focus();
            Keyboard.Focus(_currentTextBox);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (e.Key == Key.Escape)
                {
                    textBox.Text = ""; // Clear text on escape
                }
                // Move focus away to trigger LostFocus
                Keyboard.ClearFocus();
                Focus();
            }
            e.Handled = true;
        }
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.LostFocus -= TextBox_LostFocus;
            textBox.KeyDown -= TextBox_KeyDown;

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                DrawingCanvas.Children.Remove(textBox);
            }
            else
            {
                // Convert to TextBlock for final display
                var textBlock = new TextBlock
                {
                    Text = textBox.Text,
                    FontFamily = textBox.FontFamily,
                    FontSize = textBox.FontSize,
                    FontWeight = textBox.FontWeight,
                    FontStyle = textBox.FontStyle,
                    Foreground = textBox.Foreground
                };

                double left = Canvas.GetLeft(textBox);
                double top = Canvas.GetTop(textBox);

                DrawingCanvas.Children.Remove(textBox);
                DrawingCanvas.Children.Add(textBlock);
                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);

                // Save state and merge for undo/redo support
                SaveAndMergeDrawing();

                // Auto-copy to clipboard
                CopyToClipboard();
            }

            _currentTextBox = null;
        }
    }

    private void ApplyBlurEffect(Rectangle rect)
    {
        SaveState();

        int x = (int)Canvas.GetLeft(rect);
        int y = (int)Canvas.GetTop(rect);
        int width = (int)rect.Width;
        int height = (int)rect.Height;

        // Ensure bounds are valid
        x = Math.Max(0, x);
        y = Math.Max(0, y);
        width = Math.Min(width, _originalBitmap.Width - x);
        height = Math.Min(height, _originalBitmap.Height - y);

        if (width <= 0 || height <= 0) return;

        // Use high-performance mosaic effect - ~100x faster than GetPixel/SetPixel
        var region = new System.Drawing.Rectangle(x, y, width, height);
        Utils.ImageProcessingHelper.ApplyMosaic(_originalBitmap, region, blockSize: 16);

        LoadImage(_originalBitmap);
    }
    #endregion

    #region Color and Stroke
    private void ColorPicker_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new System.Windows.Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(_currentColor.R, _currentColor.G, _currentColor.B)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _currentColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            CurrentColorBrush.Color = _currentColor;
        }
    }

    private void QuickColor_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Background is SolidColorBrush brush)
        {
            _currentColor = brush.Color;
            CurrentColorBrush.Color = _currentColor;
        }
    }

    private void StrokeWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _strokeWidth = e.NewValue;
        if (StrokeWidthText != null)
        {
            StrokeWidthText.Text = ((int)_strokeWidth).ToString();
        }
    }
    #endregion

    #region File Operations
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Image|*.png|JPEG Image|*.jpg|BMP Image|*.bmp|GIF Image|*.gif",
            DefaultExt = ".png",
            FileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            var finalBitmap = RenderFinalImage();
            var format = System.IO.Path.GetExtension(dialog.FileName).ToLower() switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".bmp" => ImageFormat.Bmp,
                ".gif" => ImageFormat.Gif,
                _ => ImageFormat.Png
            };

            finalBitmap.Save(dialog.FileName, format);
            StatusText.Text = $"Saved: {dialog.FileName}";
            finalBitmap.Dispose();
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard();
    }

    private Bitmap RenderFinalImage()
    {
        int width = _originalBitmap.Width;
        int height = _originalBitmap.Height;

        // Ensure valid dimensions
        if (width <= 0 || height <= 0)
        {
            return (Bitmap)_originalBitmap.Clone();
        }

        try
        {
            // Render canvas to bitmap using 32-bit for compatibility
            var renderBitmap = new RenderTargetBitmap(
                width, height,
                96, 96,
                PixelFormats.Pbgra32);

            // Create a visual with the base image and canvas
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Draw base image
                if (BaseImage.Source != null)
                {
                    context.DrawImage(BaseImage.Source, new Rect(0, 0, width, height));
                }
            }
            renderBitmap.Render(visual);

            // Render canvas elements
            renderBitmap.Render(DrawingCanvas);

            // Convert to System.Drawing.Bitmap
            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Copy pixels
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            renderBitmap.CopyPixels(pixels, stride, 0);

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }
        catch
        {
            // Fallback: return a copy of the original
            return (Bitmap)_originalBitmap.Clone();
        }
    }
    #endregion

    #region Undo/Redo
    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        Undo();
    }

    private void BtnRedo_Click(object sender, RoutedEventArgs e)
    {
        Redo();
    }
    #endregion

    #region Crop
    private void BtnCrop_Click(object sender, RoutedEventArgs e)
    {
        SelectTool("Crop");
        StatusText.Text = "드래그하여 자를 영역을 선택하세요";
    }

    private void ApplyCropEffect(Rectangle cropRect)
    {
        int x = (int)Canvas.GetLeft(cropRect);
        int y = (int)Canvas.GetTop(cropRect);
        int width = (int)cropRect.Width;
        int height = (int)cropRect.Height;

        if (width > 5 && height > 5)
        {
            SaveState();

            x = Math.Max(0, x);
            y = Math.Max(0, y);
            width = Math.Min(width, _originalBitmap.Width - x);
            height = Math.Min(height, _originalBitmap.Height - y);

            if (width <= 0 || height <= 0) return;

            var cropRegion = new System.Drawing.Rectangle(x, y, width, height);
            var croppedBitmap = _originalBitmap.Clone(cropRegion, _originalBitmap.PixelFormat);

            _originalBitmap.Dispose();
            _originalBitmap = croppedBitmap;

            DrawingCanvas.Children.Clear();
            LoadImage(_originalBitmap);
            UpdateImageSizeText();
            CopyToClipboard();

            StatusText.Text = "이미지가 잘렸습니다";
        }
        else
        {
            StatusText.Text = "선택 영역이 너무 작습니다";
        }

        // Return to Select tool after crop
        SelectTool("Select");
    }
    #endregion

    private void UpdateImageSizeText()
    {
        ImageSizeText.Text = $"{_originalBitmap.Width} x {_originalBitmap.Height} px";
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        var helpWindow = new HelpWindow();
        helpWindow.Owner = this;
        helpWindow.ShowDialog();
    }

    #region Text Options
    private void CmbFontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbFontFamily.SelectedItem is ComboBoxItem item && item.Tag is string fontName)
        {
            _fontFamily = fontName;

            // Update current textbox if editing
            if (_currentTextBox != null)
            {
                _currentTextBox.FontFamily = new System.Windows.Media.FontFamily(_fontFamily);
            }
        }
    }

    private void CmbFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbFontSize.SelectedItem is ComboBoxItem item && item.Tag is string sizeStr)
        {
            if (double.TryParse(sizeStr, out double size))
            {
                _fontSize = size;

                // Update current textbox if editing
                if (_currentTextBox != null)
                {
                    _currentTextBox.FontSize = _fontSize;
                }
            }
        }
    }

    private void BtnBold_Click(object sender, RoutedEventArgs e)
    {
        _isBold = BtnBold.IsChecked ?? false;

        // Update current textbox if editing
        if (_currentTextBox != null)
        {
            _currentTextBox.FontWeight = _isBold ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    private void BtnItalic_Click(object sender, RoutedEventArgs e)
    {
        _isItalic = BtnItalic.IsChecked ?? false;

        // Update current textbox if editing
        if (_currentTextBox != null)
        {
            _currentTextBox.FontStyle = _isItalic ? FontStyles.Italic : FontStyles.Normal;
        }
    }
    #endregion

    #region Zoom
    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            if (e.Delta > 0)
                ZoomIn();
            else
                ZoomOut();
            e.Handled = true;
        }
    }

    private void ZoomIn()
    {
        SetZoom(_zoomLevel + ZoomStep);
    }

    private void ZoomOut()
    {
        SetZoom(_zoomLevel - ZoomStep);
    }

    private void SetZoom(double level)
    {
        _zoomLevel = Math.Max(ZoomMin, Math.Min(ZoomMax, level));
        CanvasScale.ScaleX = _zoomLevel;
        CanvasScale.ScaleY = _zoomLevel;
        UpdateZoomText();
    }

    private void UpdateZoomText()
    {
        ZoomText.Text = $"{(int)(_zoomLevel * 100)}%";
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        ZoomIn();
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        ZoomOut();
    }

    private void BtnZoom100_Click(object sender, RoutedEventArgs e)
    {
        SetZoom(1.0);
    }

    private void BtnZoomFit_Click(object sender, RoutedEventArgs e)
    {
        // Calculate zoom to fit image in viewport
        double viewportWidth = ImageScrollViewer.ActualWidth - 60; // Account for padding
        double viewportHeight = ImageScrollViewer.ActualHeight - 60;

        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        double imageWidth = _originalBitmap.Width;
        double imageHeight = _originalBitmap.Height;

        double scaleX = viewportWidth / imageWidth;
        double scaleY = viewportHeight / imageHeight;
        double fitZoom = Math.Min(scaleX, scaleY);

        SetZoom(Math.Min(fitZoom, 1.0)); // Don't zoom in beyond 100%
    }
    #endregion

    private static BitmapSource BitmapToImageSource(Bitmap bitmap)
    {
        // Use CreateBitmapSourceFromHBitmap for much faster conversion
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            hBitmap = bitmap.GetHbitmap();
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();
            return bitmapSource;
        }
        catch
        {
            // Fallback to MemoryStream method if GetHbitmap fails
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Bmp);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    protected override void OnClosed(EventArgs e)
    {
        CaptureHistoryService.Instance.HistoryChanged -= OnHistoryChanged;
        _originalBitmap?.Dispose();
        foreach (var bitmap in _undoStack) bitmap.Dispose();
        foreach (var bitmap in _redoStack) bitmap.Dispose();
        base.OnClosed(e);
    }
}
