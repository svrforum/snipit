using SnipIt.Services;
namespace SnipIt.Views;
internal sealed partial class EditorWindow
{
    private CaptureHistoryItem? historyItem;
    private long historyRevision;
    private async Task PersistHistory()
    {
        if (document.Revision == historyRevision) return;
        using var snapshot = await Task.Run(() => document.Render());
        historyItem = historyItem == null
            ? await HistoryStore.Instance.Add(snapshot)
            : await HistoryStore.Instance.Update(historyItem, snapshot);
        historyRevision = document.Revision;
        ReloadHistory();
    }
    private async Task CloseWithHistory()
    {
        loadingHistory = true;
        try { await CommitInlineText(); await PersistHistory(); allowClose = true; Close(); }
        catch (Exception ex) { status.Text = "이력 저장 실패: " + ex.Message; }
        finally { loadingHistory = false; }
    }
    internal async Task CloseForAppExit()
    {
        while (!closed && (working || loadingHistory)) await Task.Delay(50);
        if (closed) return;
        loadingHistory = true;
        interaction.IsHitTestVisible = false;
        try
        {
            await CommitInlineText();
            await PersistHistory();
            allowClose = true;
            Close();
        }
        finally
        {
            loadingHistory = false;
            if (!closed) interaction.IsHitTestVisible = true;
        }
    }
}
