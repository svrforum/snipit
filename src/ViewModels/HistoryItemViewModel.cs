using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SnipIt.Services;

namespace SnipIt.ViewModels;

/// <summary>
/// ViewModel for history items using CommunityToolkit.Mvvm source generators
/// </summary>
public partial class HistoryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    private BitmapImage? _thumbnail;
    private bool _thumbnailRequested;

    // WPF asks for this property only when a virtualized row becomes visible.
    public BitmapImage? Thumbnail
    {
        get
        {
            if (!_thumbnailRequested)
            {
                _thumbnailRequested = true;
                _ = LoadThumbnailAsync();
            }
            return _thumbnail;
        }
    }

    private async Task LoadThumbnailAsync()
    {
        var thumbnail = await Task.Run(() => CaptureHistoryService.Instance.LoadThumbnail(Item));
        SetProperty(ref _thumbnail, thumbnail, nameof(Thumbnail));
    }

    public required CaptureHistoryItem Item { get; init; }
    public required int Index { get; init; }

    public string DisplayNumber => $"#{Index}";
    public string DisplayDateTime => Item.CapturedAt.ToString("MM/dd HH:mm:ss");
    public string DisplaySize => Item.DisplaySize;
}
