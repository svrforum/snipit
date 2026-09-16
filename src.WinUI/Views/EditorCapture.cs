using SnipIt.Services;
using Drawing = System.Drawing;
namespace SnipIt.Views;

internal sealed partial class EditorWindow
{
    private async Task AutoCopyEdit()
    {
        if (closed || rendered == null || !SnipIt.Models.AppSettingsConfig.Instance.CopyToClipboard) return;
        using var snapshot = (Drawing.Bitmap)rendered.Clone();
        try { await App.Copy(snapshot); }
        catch (Exception ex) { if (!closed) status.Text = Ui.L("클립보드 복사 실패: ") + ex.Message; }
    }
#if SNIPIT_SMOKE_TESTS
    internal static async Task VerifyCaptureReuse()
    {
        await App.OpenEditor(new Drawing.Bitmap(120, 80));
        var editor = App.Editors.Single();
        for (var attempt = 0; attempt < 100 && (editor.rendered == null || editor.working); attempt++)
            await Task.Delay(50);
        if (editor.rendered == null || editor.working) throw new Exception("Editor did not finish loading");
        editor.SelectTool("Pen", Ui.L("펜"));
        for (var i = 0; i < 3; i++)
        {
            var next = new Drawing.Bitmap(160 + i, 100);
            next.SetPixel(0, 0, Drawing.Color.Blue);
            await App.OpenEditor(next);
            if (App.Editors.Count != 1 || !ReferenceEquals(App.Editors[0], editor)
                || editor.rendered?.Width != 160 + i
                || editor.rendered.GetPixel(0, 0).ToArgb() != Drawing.Color.Blue.ToArgb()
                || editor.dirty || editor.document.CanUndo)
                throw new Exception("Repeated capture did not reuse the editor with the new original pixels");
            if (editor.tool != "Pen") throw new Exception("Capture lost the selected drawing tool");
        }
        var config = SnipIt.Models.AppSettingsConfig.Instance;
        config.CopyToClipboard = true;
        editor.document.Add(new EditMark("Rectangle", new[] { new Drawing.PointF(1, 1), new Drawing.PointF(30, 30) }, Drawing.Color.Red, 3));
        await editor.Refresh();
        var editedItem = editor.historyItem ?? throw new Exception("Edited image missing from history");
        using (var editedHistory = await HistoryStore.Load(editedItem))
            if (editedHistory.GetPixel(1, 10).R < 200 || editedHistory.GetPixel(1, 10).G > 30)
                throw new Exception("Edited pixels were not persisted to history");
        await editor.AutoCopyEdit();
        var clipboard = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!clipboard.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Bitmap)) throw new Exception("Edit did not copy an image");
        await editor.UndoEdit();
        if (editor.historyItem?.Id != editedItem.Id) throw new Exception("Undo duplicated history entry");
        using (var undoneHistory = await HistoryStore.Load(editor.historyItem))
            if (undoneHistory.GetPixel(1, 10).A != 0) throw new Exception("Undo did not update saved history");
        editor.document.Add(new EditMark("Crop", new[] { new Drawing.PointF(0, 0), new Drawing.PointF(80, 60) }, Drawing.Color.Red, 1));
        await editor.Refresh();
        var croppedItem = editor.historyItem!;
        if (croppedItem.Width != 80 || croppedItem.Height != 60 || croppedItem.Id != editedItem.Id)
            throw new Exception("Crop did not update the same history item dimensions");
        await App.OpenEditor(new Drawing.Bitmap(162, 100));
        using (var persisted = await HistoryStore.Load(croppedItem))
            if (persisted.Width != 80 || persisted.Height != 60) throw new Exception("Capture switch lost edited history");
        var words = new[]
        {
            new OcrWord { Text = "one", BoundingRect = new Windows.Foundation.Rect(0, 0, 20, 10) },
            new OcrWord { Text = "two", BoundingRect = new Windows.Foundation.Rect(25, 0, 20, 10) },
            new OcrWord { Text = "three", BoundingRect = new Windows.Foundation.Rect(0, 15, 25, 10) }
        };
        editor.EnterOcr(new OcrResultWithRegions { Lines = new() { new OcrLine { Words = new() { words[0], words[1] } }, new OcrLine { Words = new() { words[2] } } } });
        var scale = editor.canvas.Width / editor.rendered!.Width;
        editor.SelectOcr(new(5 * scale, 5 * scale), new(5 * scale, 5 * scale), new());
        if (editor.SelectedOcrText() != "one") throw new Exception("OCR single selection");
        editor.SelectOcr(new(30 * scale, 5 * scale), new(30 * scale, 5 * scale), new(editor.ocrSelection));
        if (editor.SelectedOcrText() != "one two") throw new Exception("OCR Ctrl additive selection");
        editor.SelectOcr(new(0, 0), new(50 * scale, 28 * scale), new());
        if (editor.SelectedOcrText() != "one two" + Environment.NewLine + "three") throw new Exception("OCR drag selection preserves lines");
        editor.CopyOcr();
        if (await Windows.ApplicationModel.DataTransfer.Clipboard.GetContent().GetTextAsync() != editor.SelectedOcrText()) throw new Exception("OCR selection clipboard mismatch");
        editor.ExitOcr();
        if (editor.ocrLayer != null) throw new Exception("OCR overlay not released");
        config.CopyToClipboard = false;
        var originalHandle = Ui.Handle(editor);
        for (var repeat = 0; repeat < 3; repeat++)
        {
        var capture = App.CaptureAsync("Full");
        await Task.Delay(25);
        if (editor.AppWindow.IsVisible) throw new Exception("Editor remained visible during screen capture");
        await capture;
        if (App.Editors.Count != 1 || !editor.AppWindow.IsVisible || HistoryStore.Instance.Items.Count == 0) throw new Exception("Full capture did not reuse and restore editor/history");
        if (Ui.Handle(App.Editors.Single()) != originalHandle) throw new Exception("Repeated full capture replaced the editor window handle");
        }
        editor.Close();
        if (App.Editors.Count != 0) throw new Exception("Closed editor was retained");
        using (var recent = new Drawing.Bitmap(90, 60)) await HistoryStore.Instance.Add(recent);
        await App.EditRecentAsync();
        var reopened = App.Editors.Single();
        for (var attempt = 0; attempt < 100 && (reopened.rendered == null || reopened.working); attempt++)
            await Task.Delay(50);
        if (ReferenceEquals(reopened, editor) || reopened.rendered?.Width != 90)
            throw new Exception("Capture after closing did not create a working editor");
        if (reopened.tool != "Pen") throw new Exception("Reopening editor lost last drawing tool");
        using (var first = new Drawing.Bitmap(71, 50)) await HistoryStore.Instance.Add(first);
        using (var second = new Drawing.Bitmap(73, 50)) await HistoryStore.Instance.Add(second);
        reopened.ReloadHistory();
        reopened.history.SelectedIndex = 1;
        reopened.history.SelectedIndex = 0;
        for (var attempt = 0; attempt < 100 && (reopened.loadingHistory || reopened.working || reopened.rendered?.Width != 73); attempt++) await Task.Delay(50);
        if (reopened.rendered?.Width != 73 || reopened.currentHistory?.Item.Width != 73)
            throw new Exception("Rapid history selection showed a stale image");
        // Exercise the tray-exit path with an edit that has not been rendered or saved yet.
        reopened.document.Add(new EditMark("Rectangle", new[] { new Drawing.PointF(1, 1), new Drawing.PointF(30, 30) }, Drawing.Color.Red, 3));
        await reopened.CloseForAppExit();
        if (App.Editors.Count != 0 || !reopened.closed) throw new Exception("Tray exit did not close editor");
        using (var savedOnExit = await HistoryStore.Load(reopened.historyItem!))
            if (savedOnExit.GetPixel(1, 10).R < 200 || savedOnExit.GetPixel(1, 10).G > 30)
                throw new Exception("Tray exit lost unsaved edits");
    }
#endif
    // Consumes bitmap even when the user keeps the current document.
    internal async Task OpenCapture(Drawing.Bitmap bitmap, CaptureHistoryItem? item = null)
    {
        Drawing.Bitmap? incoming = bitmap;
        var switching = false;
        try
        {
            AppWindow.Show();
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter
                && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                presenter.Restore();
            Activate();
            if (closed || working || loadingHistory)
            {
                App.Notify(Ui.L("현재 편집 작업을 마친 뒤 캡처 이력에서 새 캡처를 열어 주세요."));
                return;
            }
            loadingHistory = true;
            switching = true;
            history.IsEnabled = false;
            await CommitInlineText();
            await PersistHistory();
            if (closed) return;
            ExitOcr();
            CancelGesture();
            CancelInlineText();
            document.Dispose();
            document = new EditorDocument(incoming);
            incoming = null;
            savedRevision = 0; historyRevision = 0; historyItem = item;
            currentHistory = null;
            history.SelectedItem = null;
            await Refresh(true);
        }
        finally
        {
            incoming?.Dispose();
            if (switching) { loadingHistory = false; if (!closed) history.IsEnabled = true; }
        }
    }
}

