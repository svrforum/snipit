using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SnipIt.Utils;
namespace SnipIt.Services;
internal sealed record EditMark(string Tool, PointF[] Points, Color Color, float Width, string Text = "", string Font = "Malgun Gothic", float FontSize = 20, FontStyle Style = FontStyle.Regular);
internal sealed class EditorDocument : IDisposable
{
    private Bitmap original;
    private readonly object gate = new();
    private readonly List<EditMark> edits = new();
    private readonly Stack<EditMark> redo = new();
    private readonly List<long> revisions = new();
    private readonly Stack<long> redoRevisions = new();
    private long nextRevision, baseRevision;
    public long Revision { get { lock (gate) return revisions.Count > 0 ? revisions[^1] : baseRevision; } }
    public bool CanUndo => edits.Count > 0; public bool CanRedo => redo.Count > 0;
    public EditorDocument(Bitmap bitmap) { original = bitmap; }
    public void Add(EditMark mark) { lock (gate) { if (edits.Count >= 200) { var checkpoint = RenderMarks((Bitmap)original.Clone(), edits.Take(100).ToArray()); original.Dispose(); original = checkpoint; baseRevision = revisions[99]; revisions.RemoveRange(0, 100); edits.RemoveRange(0, 100); } edits.Add(mark); revisions.Add(++nextRevision); redo.Clear(); redoRevisions.Clear(); } }
    public void Undo() { lock (gate) { if (CanUndo) { redo.Push(edits[^1]); redoRevisions.Push(revisions[^1]); revisions.RemoveAt(revisions.Count - 1); edits.RemoveAt(edits.Count - 1); } } }
    public void Redo() { lock (gate) { if (CanRedo) { edits.Add(redo.Pop()); revisions.Add(redoRevisions.Pop()); } } }
    public Bitmap Render()
    {
        Bitmap result; EditMark[] snapshot;
        lock (gate) { result = (Bitmap)original.Clone(); snapshot = edits.ToArray(); }
        return RenderMarks(result, snapshot);
    }
    private static Bitmap RenderMarks(Bitmap result, EditMark[] snapshot)
    {
        try
        {
            foreach (var mark in snapshot)
            {
                var a = mark.Points[0]; var b = mark.Points[^1]; var bounds = Rectangle.Intersect(Rectangle.FromLTRB((int)Math.Min(a.X, b.X), (int)Math.Min(a.Y, b.Y), (int)Math.Ceiling(Math.Max(a.X, b.X)), (int)Math.Ceiling(Math.Max(a.Y, b.Y))), new Rectangle(0, 0, result.Width, result.Height));
                if (mark.Tool == "Crop") { if (bounds.Width > 0 && bounds.Height > 0) { var cropped = result.Clone(bounds, PixelFormat.Format32bppArgb); result.Dispose(); result = cropped; } continue; }
                if (mark.Tool == "Blur") { if (bounds.Width > 0 && bounds.Height > 0) ImageProcessingHelper.ApplyMosaic(result, bounds); continue; }
                if (mark.Tool == "SoftBlur") { if (bounds.Width > 0 && bounds.Height > 0) ImageProcessingHelper.ApplyBlur(result, bounds); continue; }
                using var graphics = Graphics.FromImage(result); graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(mark.Color, mark.Width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                switch (mark.Tool)
                {
                    case "Pen": if (mark.Points.Length > 1) graphics.DrawLines(pen, mark.Points); else graphics.DrawEllipse(pen, a.X, a.Y, 1, 1); break;
                    case "Arrow": using (var cap = new AdjustableArrowCap(4, 6, true)) { pen.CustomEndCap = cap; graphics.DrawLine(pen, a, b); } break;
                    case "Line": graphics.DrawLine(pen, a, b); break;
                    case "Rectangle": if (bounds.Width > 0 && bounds.Height > 0) graphics.DrawRectangle(pen, bounds); break;
                    case "Ellipse": if (bounds.Width > 0 && bounds.Height > 0) graphics.DrawEllipse(pen, bounds); break;
                    case "Highlight":
                        using (var brush = new SolidBrush(Color.FromArgb(128, mark.Color)))
                        using (var path = new GraphicsPath(FillMode.Winding))
                        {
                            var stroke = mark.Points.Distinct().ToArray();
                            if (stroke.Length < 2) graphics.FillEllipse(brush, a.X-mark.Width/2, a.Y-mark.Width/2, mark.Width, mark.Width);
                            else { path.AddLines(mark.Points); path.Widen(pen); path.FillMode = FillMode.Winding; graphics.FillPath(brush, path); }
                        }
                        break;
                    case "Text": using (var font = new Font(mark.Font, mark.FontSize, mark.Style, GraphicsUnit.Pixel)) using (var brush = new SolidBrush(mark.Color)) { graphics.DrawString(mark.Text, font, brush, a); } break;
                }
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    public void Dispose() { lock (gate) { original.Dispose(); edits.Clear(); redo.Clear(); } }
}
