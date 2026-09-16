using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SnipIt.Services;
namespace SnipIt.Views;
internal sealed partial class EditorWindow
{
    private HistoryEntry? pendingHistory;
    private bool selectingHistory;
    private async void HistorySelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (selectingHistory || history.SelectedItem is not HistoryEntry entry || entry.Item.Id == currentHistory?.Item.Id) return;
        if (working && !loadingHistory) { SelectHistoryItem(currentHistory); return; }
        pendingHistory = entry;
        if (loadingHistory) return;
        loadingHistory = true;

        try
        {
            while (pendingHistory is { } requested && !closed)
            {
                pendingHistory = null;
                await CommitInlineText();
                await PersistHistory();
                if (closed) return;
                var loaded = await HistoryStore.Load(requested.Item);
                if (closed || pendingHistory != null) { loaded.Dispose(); continue; }
                ExitOcr(); CancelGesture(); CancelInlineText(); document.Dispose();
                document = new EditorDocument(loaded); currentHistory = requested; savedRevision = 0; historyRevision = 0; historyItem = requested.Item;
                SelectHistoryItem(requested);
                await Refresh(true);
            }
        }
        catch (Exception ex) { if (!closed) { status.Text = ex.Message; SelectHistoryItem(currentHistory); } }
        finally { loadingHistory = false; }
    }
    private void SelectHistoryItem(HistoryEntry? entry)
    {
        selectingHistory = true;
        try { history.SelectedItem = entry; }
        finally { selectingHistory = false; }
    }
    private void AttachHistoryMenu()
    {
        history.RightTapped += (_, e) =>
        {
            if (working || loadingHistory || e.OriginalSource is not DependencyObject source) return;
            while (source is not ListViewItem && source != history)
            {
                source = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(source);
                if (source == null) return;
            }
            if (source is not ListViewItem { Content: HistoryEntry entry }) return;
            var menu = new MenuFlyout();
            var delete = new MenuFlyoutItem { Text = Ui.L("선택 이력 삭제") };
            delete.Click += async (_, _) =>
            {
                try
                {
                    if (await Ui.Confirm(this, Ui.L("이력 삭제"), Ui.L("선택한 캡처를 삭제할까요?")))
                        await HistoryStore.Instance.Delete(entry.Item);
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            menu.Items.Add(delete); menu.ShowAt(history, e.GetPosition(history)); e.Handled = true;
        };
    }
}

