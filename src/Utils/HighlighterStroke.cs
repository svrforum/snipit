using System;
using System.Drawing;
using System.Linq;
namespace SnipIt.Utils;
internal static class HighlighterStroke
{
    internal static PointF[] Align(PointF[] points, bool horizontal)
    {
        if (points.Length < 2) return points;
        var start = points[0];
        var distance = points.Max(p => Math.Abs(p.X - start.X));
        var nearHorizontal = distance >= 12 && points.All(p => Math.Abs(p.Y - start.Y) <= Math.Min(6, distance * .08));
        return horizontal || nearHorizontal ? points.Select(p => new PointF(p.X, start.Y)).ToArray() : points;
    }
}
