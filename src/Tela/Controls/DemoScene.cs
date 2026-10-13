using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Tela.Controls;

/// <summary>
/// Uma paisagem desenhada (céu, sol ou lua, montanhas, lago) que faz de vídeo nas capturas do
/// README e nos cartões de demonstração: nada de imagens de terceiros.
/// </summary>
public sealed class DemoScene : Control
{
    public static readonly StyledProperty<int> VariantProperty =
        AvaloniaProperty.Register<DemoScene, int>(nameof(Variant));

    static DemoScene()
    {
        AffectsRender<DemoScene>(VariantProperty);
    }

    public int Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    private sealed record Palette(string SkyTop, string SkyMid, string Horizon, string Sun, string Far, string Mid, string Near, string Water, bool Moon);

    private static readonly Palette[] Palettes =
    [
        new("#141a46", "#9b4a6e", "#f6a85b", "#ffd88a", "#7a4f7d", "#3f2f5c", "#1b1532", "#2a2448", false),
        new("#2d6cb5", "#79b4e0", "#e9f2f5", "#fff6d8", "#7ea6c6", "#4f7f73", "#2c4a3a", "#3d6f8f", false),
        new("#03050f", "#0d1838", "#23366b", "#e8ecf7", "#1c2a52", "#121c3a", "#080d1e", "#0b1430", true),
        new("#5a2a1a", "#c4602d", "#f3b05a", "#fff0b8", "#b86a3e", "#8a4527", "#4d2414", "#7a3c22", false),
    ];

    private static Color C(string hex) => Color.Parse(hex);

    private static Color A(string hex, double alpha)
    {
        var c = C(hex);
        return Color.FromArgb((byte)(alpha * 255), c.R, c.G, c.B);
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;
        var p = Palettes[Math.Abs(Variant) % Palettes.Length];
        var horizon = h * 0.70;

        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(C(p.SkyTop), 0), new GradientStop(C(p.SkyMid), 0.48), new GradientStop(C(p.Horizon), 0.70) },
        }, new Rect(0, 0, w, horizon + 1));

        // Sol (ou lua) com halo.
        var sun = new Point(w * 0.66, p.Moon ? h * 0.24 : horizon - h * 0.09);
        var r = h * (p.Moon ? 0.055 : 0.085);
        context.DrawEllipse(new RadialGradientBrush
        {
            GradientStops = { new GradientStop(A(p.Sun, 0.55), 0), new GradientStop(A(p.Sun, 0), 1) },
        }, null, sun, r * 3.2, r * 3.2);
        context.DrawEllipse(new SolidColorBrush(C(p.Sun)), null, sun, r, r);
        if (p.Moon)
        {
            context.DrawEllipse(new SolidColorBrush(C(p.SkyMid)), null, new Point(sun.X + r * 0.45, sun.Y - r * 0.2), r * 0.92, r * 0.92);
            var star = new SolidColorBrush(A("#ffffff", 0.8));
            foreach (var (sx, sy) in new[] { (0.12, 0.12), (0.22, 0.30), (0.35, 0.08), (0.48, 0.22), (0.82, 0.10), (0.9, 0.32), (0.06, 0.4), (0.58, 0.05) })
                context.DrawEllipse(star, null, new Point(w * sx, h * sy), 1.3, 1.3);
        }

        void Ridge(Color color, double baseY, double amp, double seed)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(0, horizon + 1), true);
                for (var x = 0.0; x <= w + 1; x += w / 48)
                {
                    var t = x / w;
                    var y = baseY - amp * (0.55 * Math.Sin(t * 6.1 + seed) + 0.3 * Math.Sin(t * 13.7 + seed * 2) + 0.15 * Math.Sin(t * 29 + seed * 3));
                    c.LineTo(new Point(x, y));
                }
                c.LineTo(new Point(w, horizon + 1));
                c.EndFigure(true);
            }
            context.DrawGeometry(new SolidColorBrush(color), null, g);
        }

        Ridge(C(p.Far), horizon - h * 0.10, h * 0.08, 1.3);
        Ridge(C(p.Mid), horizon - h * 0.045, h * 0.06, 4.1);
        Ridge(C(p.Near), horizon, h * 0.035, 2.2);

        // Lago com o reflexo do sol em riscos.
        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(C(p.Water), 0), new GradientStop(C(p.Near), 1) },
        }, new Rect(0, horizon, w, h - horizon));
        var glint = new SolidColorBrush(A(p.Sun, 0.55));
        for (var i = 0; i < 9; i++)
        {
            var y = horizon + (h - horizon) * (0.08 + i * 0.1);
            var half = r * (1.6 - i * 0.12);
            context.FillRectangle(glint, new Rect(sun.X - half, y, half * 2, Math.Max(1, h * 0.004)));
        }
    }
}
