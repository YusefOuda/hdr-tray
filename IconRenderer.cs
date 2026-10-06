using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace HdrTray;

static class IconRenderer
{
    // Drawn at the system small-icon size (16 px at 100% scaling, larger at higher DPI) so the text stays sharp.
    // White text on a colored badge reads on both light and dark taskbars.
    public static Icon ForState(HdrState state)
    {
        using var bmp = Draw(state, SystemInformation.SmallIconSize.Width);
        return ToIcon(bmp);
    }

    internal static Bitmap Draw(HdrState state, int size)
    {
        var (text, color) = state switch
        {
            HdrState.Hdr => ("HDR", Color.FromArgb(200, 120, 0)),
            HdrState.Sdr => ("SDR", Color.FromArgb(70, 90, 120)),
            _ => ("SDR", Color.FromArgb(90, 90, 90)),
        };

        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using (var badge = new SolidBrush(color))
        using (var path = RoundedRect(new RectangleF(0, 0, size - 1, size - 1), size / 5f))
            g.FillPath(badge, path);

        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = StringAlignment.Center;
        format.LineAlignment = StringAlignment.Center;
        using var font = FitFont(g, text, size, format);
        g.DrawString(text, font, Brushes.White, new RectangleF(0, 0, size, size), format);

        return bmp;
    }

    // Largest bold font whose text fits the icon width with a pixel of margin on each side
    static Font FitFont(Graphics g, string text, int size, StringFormat format)
    {
        for (float em = size * 0.8f; ; em -= 0.5f)
        {
            var font = new Font("Segoe UI", em, FontStyle.Bold, GraphicsUnit.Pixel);
            if (em <= 5f || g.MeasureString(text, font, PointF.Empty, format).Width <= size - 2)
                return font;
            font.Dispose();
        }
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static Icon ToIcon(Bitmap bmp)
    {
        var hIcon = bmp.GetHicon();
        var copy = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return copy;
    }

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);
}
