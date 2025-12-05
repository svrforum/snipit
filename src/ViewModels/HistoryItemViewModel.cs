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

    [ObservableProperty]
    private BitmapImage? _thumbnail;

    public required CaptureHistoryItem Item { get; init; }
    public required int Index { get; init; }

    public string DisplayNumber => $"#{Index}";
    public string DisplayDateTime => Item.CapturedAt.ToString("MM/dd HH:mm:ss");
    public string DisplaySize => Item.DisplaySize;
}
