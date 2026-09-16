using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace SnipIt.Views;
internal sealed class ToolCanvas : Canvas
{
    internal void SetToolCursor(string tool) => ProtectedCursor = InputSystemCursor.Create(tool switch
    {
        "Text" => InputSystemCursorShape.IBeam,
        "Select" => InputSystemCursorShape.Arrow,
        _ => InputSystemCursorShape.Cross
    });
}
