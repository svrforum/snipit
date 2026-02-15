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
    private readonly LinkedList<Bitmap> _undoStack = new();
    private readonly LinkedList<Bitmap> _redoStack = new();
    private const int MaxUndoStackSize = 10;

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

    // OCR overlay mode
    private bool _isOcrMode = false;
    private OcrResultWithRegions? _ocrResult;
    private List<Border> _ocrWordBorders = [];
    private Point _ocrSelectionStart;
    private bool _isOcrSelecting = false;
    private HashSet<OcrWord> _selectedOcrWords = [];

    public EditorWindow(Bitmap bitmap)
    {
        _originalBitmap = bitmap;
        InitializeComponent();
        HistoryList.ItemsSource = _historyItems;
        Loaded += EditorWindow_Loaded;
        KeyDown += EditorWindow_KeyDown;
        SourceInitialized += EditorWindow_SourceInitialized;
    }

    private void EditorWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Apply Windows 11 styling
        Windows11Helper.ApplyWindows11Style(this, useMica: false, useDarkMode: Windows11Helper.IsSystemDarkTheme());

        // Position window on primary monitor
        PositionOnPrimaryMonitor();
    }

    private void PositionOnPrimaryMonitor()
    {
        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        if (primaryScreen == null) return;

        // Get DPI scale factor
        var source = PresentationSource.FromVisual(this);
        double dpiScale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        // WorkingArea is in physical pixels, convert to logical units for WPF
        var workArea = primaryScreen.WorkingArea;
        double logicalWorkAreaLeft = workArea.Left / dpiScale;
        double logicalWorkAreaTop = workArea.Top / dpiScale;
        double logicalWorkAreaWidth = workArea.Width / dpiScale;
        double logicalWorkAreaHeight = workArea.Height / dpiScale;

        // Center on primary monitor's working area
        double windowWidth = Width;
        double windowHeight = Height;

        // If window size not set yet, use defaults
        if (double.IsNaN(windowWidth) || windowWidth <= 0) windowWidth = 1150;
        if (double.IsNaN(windowHeight) || windowHeight <= 0) windowHeight = 700;

        // Ensure window fits within primary monitor (in logical units)
        windowWidth = Math.Min(windowWidth, logicalWorkAreaWidth * 0.95);
        windowHeight = Math.Min(windowHeight, logicalWorkAreaHeight * 0.95);

        Left = logicalWorkAreaLeft + (logicalWorkAreaWidth - windowWidth) / 2;
        Top = logicalWorkAreaTop + (logicalWorkAreaHeight - windowHeight) / 2;
        Width = windowWidth;
        Height = windowHeight;
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

        // OCR mode handling
        if (_isOcrMode)
        {
            if (e.Key == Key.Escape)
            {
                ExitOcrMode();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.C && Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                CopySelectedOcrText();
                e.Handled = true;
                return;
            }
        }

        // ESC to close editor
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

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

        // Tool shortcuts (no modifiers) - use configurable shortcuts
        if (modifiers == System.Windows.Input.ModifierKeys.None)
        {
            var shortcuts = AppSettingsConfig.Instance.EditorShortcuts;

            if (e.Key == shortcuts.Select)
            {
                SelectTool("Select");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Pen)
            {
                SelectTool("Pen");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Arrow)
            {
                SelectTool("Arrow");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Line)
            {
                SelectTool("Line");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Rectangle)
            {
                SelectTool("Rectangle");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Ellipse)
            {
                SelectTool("Ellipse");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Text)
            {
                SelectTool("Text");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Highlight)
            {
                SelectTool("Highlight");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Blur)
            {
                SelectTool("Blur");
                e.Handled = true;
            }
            else if (e.Key == shortcuts.Crop)
            {
                SelectTool("Crop");
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Cancel current operation or deselect tool
                SelectTool("Select");
                e.Handled = true;
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
        var shortcuts = AppSettingsConfig.Instance.EditorShortcuts;
        string GetKeyStr(Key key) => EditorToolShortcuts.GetKeyDisplayName(key);

        return toolName switch
        {
            "Select" => $"선택 ({GetKeyStr(shortcuts.Select)})",
            "Pen" => $"펜 ({GetKeyStr(shortcuts.Pen)})",
            "Arrow" => $"화살표 ({GetKeyStr(shortcuts.Arrow)})",
            "Line" => $"직선 ({GetKeyStr(shortcuts.Line)})",
            "Rectangle" => $"사각형 ({GetKeyStr(shortcuts.Rectangle)})",
            "Ellipse" => $"타원 ({GetKeyStr(shortcuts.Ellipse)})",
            "Text" => $"텍스트 ({GetKeyStr(shortcuts.Text)})",
            "Highlight" => $"형광펜 ({GetKeyStr(shortcuts.Highlight)})",
            "Blur" => $"모자이크 ({GetKeyStr(shortcuts.Blur)})",
            "Crop" => $"자르기 ({GetKeyStr(shortcuts.Crop)})",
            _ => toolName
        };
    }

    private void Undo()
    {
        if (_undoStack.Count > 0)
        {
            _redoStack.AddFirst((Bitmap)_originalBitmap.Clone());
            _originalBitmap.Dispose();
            _originalBitmap = _undoStack.First!.Value;
            _undoStack.RemoveFirst();
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
            _undoStack.AddFirst((Bitmap)_originalBitmap.Clone());
            _originalBitmap.Dispose();
            _originalBitmap = _redoStack.First!.Value;
            _redoStack.RemoveFirst();
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

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItemViewModel viewModel)
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

        // Apply initial zoom setting
        ApplyInitialZoom(bitmap.Width, bitmap.Height);
    }

    private void ApplyInitialZoom(int imageWidth, int imageHeight)
    {
        var config = AppSettingsConfig.Instance;
        if (config.EditorInitialZoom == EditorInitialZoom.Original)
        {
            SetZoom(1.0);
        }
        else
        {
            // Fit to window - calculate zoom to fit image in canvas area
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var canvasArea = ImageScrollViewer;
                    if (canvasArea != null && canvasArea.ActualWidth > 0 && canvasArea.ActualHeight > 0)
                    {
                        double availableWidth = canvasArea.ActualWidth - 40;
                        double availableHeight = canvasArea.ActualHeight - 40;

                        double scaleX = availableWidth / imageWidth;
                        double scaleY = availableHeight / imageHeight;
                        double scale = Math.Min(scaleX, scaleY);

                        // Cap at 100% max
                        scale = Math.Min(scale, 1.0);
                        // Ensure minimum zoom
                        scale = Math.Max(scale, ZoomMin);

                        SetZoom(scale);
                    }
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void SaveState()
    {
        _undoStack.AddFirst((Bitmap)_originalBitmap.Clone());

        // Limit undo stack size to prevent excessive memory usage
        while (_undoStack.Count > MaxUndoStackSize)
        {
            _undoStack.Last!.Value.Dispose();
            _undoStack.RemoveLast();
        }

        // Clear redo stack
        foreach (var bitmap in _redoStack) bitmap.Dispose();
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
            FileName = $"SnipIt_Capture_{DateTime.Now:yyyyMMdd_HHmmss}"
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

    private async void BtnOcr_Click(object sender, RoutedEventArgs e)
    {
        // Toggle OCR mode
        if (_isOcrMode)
        {
            ExitOcrMode();
            return;
        }

        try
        {
            BtnOcr.IsEnabled = false;
            StatusText.Text = "텍스트 인식 중...";

            _ocrResult = await OcrService.ExtractTextWithRegionsAsync(_originalBitmap);

            if (_ocrResult.Lines.Count == 0)
            {
                StatusText.Text = "인식된 텍스트가 없습니다";
                return;
            }

            EnterOcrMode();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"OCR 오류: {ex.Message}";
        }
        finally
        {
            BtnOcr.IsEnabled = true;
        }
    }

    // OCR overlay colors
    private static readonly Color OcrNormalBg = Color.FromArgb(60, 255, 213, 79);      // Yellow background
    private static readonly Color OcrNormalBorder = Color.FromArgb(200, 255, 193, 7);  // Yellow border
    private static readonly Color OcrHoverBg = Color.FromArgb(100, 255, 193, 7);       // Hover yellow
    private static readonly Color OcrSelectedBg = Color.FromArgb(150, 49, 130, 246);   // Selected blue
    private static readonly Color OcrSelectedBorder = Color.FromArgb(255, 49, 130, 246); // Selected blue border

    private void EnterOcrMode()
    {
        _isOcrMode = true;
        _selectedOcrWords.Clear();
        _ocrWordBorders.Clear();

        // Create overlay borders for each word
        if (_ocrResult != null)
        {
            // Calculate scale factor between bitmap and displayed image
            double scaleX = DrawingCanvas.Width / _originalBitmap.Width;
            double scaleY = DrawingCanvas.Height / _originalBitmap.Height;

            foreach (var line in _ocrResult.Lines)
            {
                foreach (var word in line.Words)
                {
                    // Apply scale to coordinates
                    double x = word.BoundingRect.X * scaleX;
                    double y = word.BoundingRect.Y * scaleY;
                    double width = word.BoundingRect.Width * scaleX;
                    double height = word.BoundingRect.Height * scaleY;

                    var border = new Border
                    {
                        Width = width,
                        Height = height,
                        Background = new SolidColorBrush(OcrNormalBg),
                        BorderBrush = new SolidColorBrush(OcrNormalBorder),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand,
                        Tag = word,
                        SnapsToDevicePixels = true,
                        UseLayoutRounding = true
                    };

                    border.MouseLeftButtonDown += OcrWord_MouseLeftButtonDown;
                    border.MouseEnter += OcrWord_MouseEnter;
                    border.MouseLeave += OcrWord_MouseLeave;

                    Canvas.SetLeft(border, x);
                    Canvas.SetTop(border, y);
                    DrawingCanvas.Children.Add(border);
                    _ocrWordBorders.Add(border);
                }
            }
        }

        // Update UI
        StatusText.Text = "텍스트를 클릭하거나 드래그하여 선택하세요 (ESC: 취소, Ctrl+C: 복사)";
        DrawingCanvas.Cursor = Cursors.IBeam;

        // Add selection rectangle event handlers
        DrawingCanvas.MouseLeftButtonDown += OcrCanvas_MouseLeftButtonDown;
        DrawingCanvas.MouseMove += OcrCanvas_MouseMove;
        DrawingCanvas.MouseLeftButtonUp += OcrCanvas_MouseLeftButtonUp;
    }

    private void ExitOcrMode()
    {
        _isOcrMode = false;
        _ocrResult = null;
        _selectedOcrWords.Clear();

        // Remove OCR borders
        foreach (var border in _ocrWordBorders)
        {
            DrawingCanvas.Children.Remove(border);
        }
        _ocrWordBorders.Clear();

        // Remove selection rectangle event handlers
        DrawingCanvas.MouseLeftButtonDown -= OcrCanvas_MouseLeftButtonDown;
        DrawingCanvas.MouseMove -= OcrCanvas_MouseMove;
        DrawingCanvas.MouseLeftButtonUp -= OcrCanvas_MouseLeftButtonUp;

        // Remove selection rectangle if exists
        var selRect = DrawingCanvas.Children.OfType<Rectangle>().FirstOrDefault(r => r.Tag as string == "OcrSelection");
        if (selRect != null)
            DrawingCanvas.Children.Remove(selRect);

        UpdateCursor();
        StatusText.Text = "준비됨";
    }

    private void OcrWord_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is OcrWord word)
        {
            if (Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                // Toggle selection with Ctrl
                if (_selectedOcrWords.Contains(word))
                {
                    _selectedOcrWords.Remove(word);
                    border.Background = new SolidColorBrush(OcrNormalBg);
                    border.BorderBrush = new SolidColorBrush(OcrNormalBorder);
                }
                else
                {
                    _selectedOcrWords.Add(word);
                    border.Background = new SolidColorBrush(OcrSelectedBg);
                    border.BorderBrush = new SolidColorBrush(OcrSelectedBorder);
                }
            }
            else
            {
                // Single selection - clear others first
                ClearOcrSelection();
                _selectedOcrWords.Add(word);
                border.Background = new SolidColorBrush(OcrSelectedBg);
                border.BorderBrush = new SolidColorBrush(OcrSelectedBorder);
            }

            // Auto copy on selection
            CopySelectedOcrText();
            e.Handled = true;
        }
    }

    private void OcrWord_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border && border.Tag is OcrWord word && !_selectedOcrWords.Contains(word))
        {
            border.Background = new SolidColorBrush(OcrHoverBg);
        }
    }

    private void OcrWord_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border && border.Tag is OcrWord word && !_selectedOcrWords.Contains(word))
        {
            border.Background = new SolidColorBrush(OcrNormalBg);
        }
    }

    private Rectangle? _ocrSelectionRect;

    private void OcrCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isOcrMode) return;

        // Check if clicking on a word border (handled by OcrWord_MouseLeftButtonDown)
        if (e.OriginalSource is Border) return;

        _ocrSelectionStart = e.GetPosition(DrawingCanvas);
        _isOcrSelecting = true;

        // Clear previous selection unless Ctrl is held
        if (Keyboard.Modifiers != System.Windows.Input.ModifierKeys.Control)
        {
            ClearOcrSelection();
        }

        // Create selection rectangle
        _ocrSelectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(49, 130, 246)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(30, 49, 130, 246)),
            Tag = "OcrSelection"
        };
        Canvas.SetLeft(_ocrSelectionRect, _ocrSelectionStart.X);
        Canvas.SetTop(_ocrSelectionRect, _ocrSelectionStart.Y);
        DrawingCanvas.Children.Add(_ocrSelectionRect);

        DrawingCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void OcrCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isOcrMode || !_isOcrSelecting || _ocrSelectionRect == null) return;

        var currentPoint = e.GetPosition(DrawingCanvas);

        double x = Math.Min(_ocrSelectionStart.X, currentPoint.X);
        double y = Math.Min(_ocrSelectionStart.Y, currentPoint.Y);
        double width = Math.Abs(currentPoint.X - _ocrSelectionStart.X);
        double height = Math.Abs(currentPoint.Y - _ocrSelectionStart.Y);

        Canvas.SetLeft(_ocrSelectionRect, x);
        Canvas.SetTop(_ocrSelectionRect, y);
        _ocrSelectionRect.Width = width;
        _ocrSelectionRect.Height = height;

        // Highlight words that intersect with selection
        var selectionRect = new Rect(x, y, width, height);
        foreach (var border in _ocrWordBorders)
        {
            if (border.Tag is OcrWord word)
            {
                var wordRect = new Rect(
                    word.BoundingRect.X, word.BoundingRect.Y,
                    word.BoundingRect.Width, word.BoundingRect.Height);

                if (selectionRect.IntersectsWith(wordRect))
                {
                    border.Background = new SolidColorBrush(OcrSelectedBg);
                    border.BorderBrush = new SolidColorBrush(OcrSelectedBorder);
                }
                else if (!_selectedOcrWords.Contains(word))
                {
                    border.Background = new SolidColorBrush(OcrNormalBg);
                    border.BorderBrush = new SolidColorBrush(OcrNormalBorder);
                }
            }
        }
    }

    private void OcrCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isOcrMode || !_isOcrSelecting) return;

        _isOcrSelecting = false;
        DrawingCanvas.ReleaseMouseCapture();

        if (_ocrSelectionRect != null)
        {
            // Select all words that intersect with the selection rectangle
            double x = Canvas.GetLeft(_ocrSelectionRect);
            double y = Canvas.GetTop(_ocrSelectionRect);
            var selectionRect = new Rect(x, y, _ocrSelectionRect.Width, _ocrSelectionRect.Height);

            foreach (var border in _ocrWordBorders)
            {
                if (border.Tag is OcrWord word)
                {
                    var wordRect = new Rect(
                        word.BoundingRect.X, word.BoundingRect.Y,
                        word.BoundingRect.Width, word.BoundingRect.Height);

                    if (selectionRect.IntersectsWith(wordRect))
                    {
                        _selectedOcrWords.Add(word);
                        border.Background = new SolidColorBrush(OcrSelectedBg);
                        border.BorderBrush = new SolidColorBrush(OcrSelectedBorder);
                    }
                }
            }

            // Remove selection rectangle
            DrawingCanvas.Children.Remove(_ocrSelectionRect);
            _ocrSelectionRect = null;
        }

        // Auto copy on selection
        if (_selectedOcrWords.Count > 0)
        {
            CopySelectedOcrText();
        }
        e.Handled = true;
    }

    private void ClearOcrSelection()
    {
        _selectedOcrWords.Clear();
        foreach (var border in _ocrWordBorders)
        {
            border.Background = new SolidColorBrush(OcrNormalBg);
            border.BorderBrush = new SolidColorBrush(OcrNormalBorder);
        }
    }

    private void UpdateOcrStatusText()
    {
        if (_selectedOcrWords.Count > 0)
        {
            StatusText.Text = $"{_selectedOcrWords.Count}개 단어 선택됨 - Ctrl+C로 복사";
        }
        else
        {
            StatusText.Text = "텍스트를 클릭하거나 드래그하여 선택하세요 (ESC: 취소)";
        }
    }

    private void CopySelectedOcrText()
    {
        if (_selectedOcrWords.Count == 0) return;

        // Sort words by position (top to bottom, left to right)
        var sortedWords = _selectedOcrWords
            .OrderBy(w => w.BoundingRect.Y)
            .ThenBy(w => w.BoundingRect.X)
            .ToList();

        // Group words by line (words with similar Y position)
        var lines = new List<List<OcrWord>>();
        List<OcrWord>? currentLine = null;
        double lastY = -1;

        foreach (var word in sortedWords)
        {
            if (currentLine == null || Math.Abs(word.BoundingRect.Y - lastY) > word.BoundingRect.Height * 0.5)
            {
                currentLine = [word];
                lines.Add(currentLine);
            }
            else
            {
                currentLine.Add(word);
            }
            lastY = word.BoundingRect.Y;
        }

        // Build text with proper spacing
        var text = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            var lineWords = line.OrderBy(w => w.BoundingRect.X).Select(w => w.Text);
            text.AppendLine(string.Join(" ", lineWords));
        }

        try
        {
            System.Windows.Clipboard.SetText(text.ToString().TrimEnd());
            StatusText.Text = $"텍스트 복사됨 ({_selectedOcrWords.Count}개 단어)";
        }
        catch
        {
            StatusText.Text = "클립보드 복사 실패 - 다시 시도해주세요";
        }
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
