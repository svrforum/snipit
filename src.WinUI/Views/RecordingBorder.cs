using System.Drawing;
using Forms = System.Windows.Forms;
namespace SnipIt.Views;

// Four physical-pixel strips leave the recording area unobstructed and clickable.
internal sealed class RecordingBorder : IDisposable
{
    private readonly List<Strip> strips = new();
    internal static Rectangle[] BoundsFor(Rectangle region) => new[]
    {
        new Rectangle(region.Left - 6, region.Top - 6, region.Width + 12, 4),
        new Rectangle(region.Left - 6, region.Bottom + 2, region.Width + 12, 4),
        new Rectangle(region.Left - 6, region.Top - 2, 4, region.Height + 4),
        new Rectangle(region.Right + 2, region.Top - 2, 4, region.Height + 4)
    };
    internal RecordingBorder(Rectangle region)
    {
        try
        {
            foreach (var bounds in BoundsFor(region))
            {
                var strip = new Strip { Bounds = bounds };
                strips.Add(strip);
                strip.Show();
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { foreach (var strip in strips) strip.Dispose(); strips.Clear(); }
    private sealed class Strip : Forms.Form
    {
        internal Strip()
        {
            FormBorderStyle = Forms.FormBorderStyle.None;
            StartPosition = Forms.FormStartPosition.Manual;
            AutoScaleMode = Forms.AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(255, 87, 34);
        }
        protected override bool ShowWithoutActivation => true;
        protected override Forms.CreateParams CreateParams
        {
            get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000 | 0x80 | 0x20; return parameters; }
        }
    }
}
