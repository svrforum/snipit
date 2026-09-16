using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SnipIt.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;

namespace SnipIt.Views;
internal sealed partial class EditorWindow
{
    private OcrResultWithRegions? ocrResult;
    private readonly HashSet<OcrWord> ocrSelection = new();
    private readonly List<(OcrWord Word, Border Box)> ocrBoxes = new();
    private Canvas? ocrLayer;
    private StackPanel? ocrActions;
    private Point? ocrStart;
    private Microsoft.UI.Xaml.Shapes.Rectangle? ocrDrag;
    private HashSet<OcrWord> ocrBeforeDrag = new();
    private static bool ControlDown => Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private async Task Ocr()
    {
        if (ocrLayer != null) { ExitOcr(); return; }
        await CommitInlineText();
        if (rendered == null || working) return;
        working = true;
        interaction.IsHitTestVisible = false;
        status.Text = Ui.L("텍스트 인식 중…");
        using var source = (System.Drawing.Bitmap)rendered.Clone();
        try
        {
            var result = await OcrService.ExtractTextWithRegionsAsync(source);
            if (closed) return;
            EnterOcr(result);
        }
        finally { working = false; if (!closed) interaction.IsHitTestVisible = true; }
    }
    private void EnterOcr(OcrResultWithRegions result)
    {
        ExitOcr();
        ocrResult = result;
        ocrLayer = new Canvas { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        canvas.Children.Add(ocrLayer);
        foreach (var word in result.Lines.SelectMany(line => line.Words))
        {
            var box = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, word.Text);
            ocrBoxes.Add((word, box)); ocrLayer.Children.Add(box);
        }
        ocrActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        ocrActions.Children.Add(Ui.Text(Ui.L("OCR · 클릭 / Ctrl 다중 선택 / 드래그"), 12));
        ocrActions.Children.Add(Ui.Button(Ui.L("전체 선택·복사"), () => { foreach (var word in result.Lines.SelectMany(line => line.Words)) ocrSelection.Add(word); PaintOcr(); CopyOcr(); }));
        ocrActions.Children.Add(Ui.Button(Ui.L("선택 복사"), CopyOcr));
        ocrActions.Children.Add(Ui.Button(Ui.L("닫기 (Esc)"), ExitOcr));
        Grid.SetRow(ocrActions, 1); Grid.SetColumnSpan(ocrActions, 2); root.Children.Add(ocrActions);
        ocrLayer.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(ocrLayer).Properties.IsLeftButtonPressed) return;
            ocrStart = e.GetCurrentPoint(ocrLayer).Position;
            ocrBeforeDrag = ControlDown ? new(ocrSelection) : new();
            ocrDrag = new() { Stroke = Ui.Blue, StrokeThickness = 1, IsHitTestVisible = false };
            ocrLayer.Children.Add(ocrDrag); ocrLayer.CapturePointer(e.Pointer); e.Handled = true;
        };
        ocrLayer.PointerMoved += (_, e) =>
        {
            if (ocrStart is not { } start || ocrDrag == null) return;
            var end = e.GetCurrentPoint(ocrLayer).Position;
            var bounds = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(start.X - end.X), Math.Abs(start.Y - end.Y));
            Canvas.SetLeft(ocrDrag, bounds.X); Canvas.SetTop(ocrDrag, bounds.Y); ocrDrag.Width = bounds.Width; ocrDrag.Height = bounds.Height;
            e.Handled = true;
        };
        ocrLayer.PointerReleased += (_, e) =>
        {
            if (ocrStart is not { } start) return;
            var end = e.GetCurrentPoint(ocrLayer).Position;
            SelectOcr(start, end, ocrBeforeDrag);
            CancelOcrDrag(); ocrLayer.ReleasePointerCapture(e.Pointer); CopyOcr(); e.Handled = true;
        };
        ocrLayer.PointerCanceled += (_, _) => CancelOcrDrag();
        ocrLayer.PointerCaptureLost += (_, _) => CancelOcrDrag();
        LayoutOcr(); PaintOcr();
        status.Text = result.Lines.Count == 0 ? Ui.L("인식된 텍스트가 없습니다.") : Ui.L("텍스트를 클릭하거나 드래그해 선택하세요.");
        scroll.Focus(FocusState.Programmatic);
    }
    private void SelectOcr(Point start, Point end, HashSet<OcrWord> previous)
    {
        if (rendered == null) return;
        var scale = canvas.Width / rendered.Width;
        var click = Math.Abs(start.X - end.X) < 4 && Math.Abs(start.Y - end.Y) < 4;
        var area = new Rect(Math.Min(start.X, end.X) / scale, Math.Min(start.Y, end.Y) / scale, Math.Abs(start.X - end.X) / scale, Math.Abs(start.Y - end.Y) / scale);
        ocrSelection.Clear(); ocrSelection.UnionWith(previous);
        foreach (var (word, _) in ocrBoxes)
        {
            var b = word.BoundingRect;
            if (click ? b.Contains(new Point(end.X / scale, end.Y / scale)) : b.Left < area.Right && b.Right > area.Left && b.Top < area.Bottom && b.Bottom > area.Top)
            {
                if (click && previous.Contains(word)) ocrSelection.Remove(word); else ocrSelection.Add(word);
            }
        }
        PaintOcr();
    }
    private void LayoutOcr()
    {
        if (ocrLayer == null || rendered == null) return;
        ocrLayer.Width = canvas.Width; ocrLayer.Height = canvas.Height;
        var scale = canvas.Width / rendered.Width;
        foreach (var (word, box) in ocrBoxes)
        {
            Canvas.SetLeft(box, word.BoundingRect.X * scale); Canvas.SetTop(box, word.BoundingRect.Y * scale);
            box.Width = word.BoundingRect.Width * scale; box.Height = word.BoundingRect.Height * scale;
        }
    }
    private void PaintOcr()
    {
        foreach (var (word, box) in ocrBoxes)
        {
            var selected = ocrSelection.Contains(word);
            box.Background = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(150, 49, 130, 246) : Windows.UI.Color.FromArgb(60, 255, 213, 79));
            box.BorderBrush = selected ? Ui.Blue : new SolidColorBrush(Windows.UI.Color.FromArgb(200, 255, 193, 7));
        }
    }
    private string SelectedOcrText() => ocrResult == null ? "" : string.Join(Environment.NewLine,
        ocrResult.Lines.Select(line => string.Join(" ", line.Words.Where(ocrSelection.Contains).Select(word => word.Text))).Where(line => line.Length > 0));
    private void CopyOcr()
    {
        var text = SelectedOcrText(); if (text.Length == 0) return;
        try { var data = new DataPackage(); data.SetText(text); Clipboard.SetContent(data); Clipboard.Flush(); status.Text = Ui.L("텍스트 복사됨") + $" ({ocrSelection.Count} " + Ui.L("개 단어") + ")"; }
        catch (Exception ex) { status.Text = Ui.L("클립보드 복사 실패: ") + ex.Message; }
    }
    private void CancelOcrDrag() { if (ocrDrag != null) ocrLayer?.Children.Remove(ocrDrag); ocrDrag = null; ocrStart = null; }
    private void ExitOcr()
    {
        CancelOcrDrag();
        if (ocrLayer != null) { ocrLayer.ReleasePointerCaptures(); canvas.Children.Remove(ocrLayer); }
        if (ocrActions != null) root.Children.Remove(ocrActions);
        ocrLayer = null; ocrActions = null; ocrResult = null; ocrBoxes.Clear(); ocrSelection.Clear(); ocrBeforeDrag.Clear();
    }
}
