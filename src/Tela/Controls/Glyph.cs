using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Tela.Controls;

public enum GlyphKind
{
    Play, Pause, Prev, Next, Menu, Minimize, Maximize, Restore, Close, Logo,
    Fullscreen, ExitFullscreen, Subtitles, AudioTrack, Volume, VolumeLow, Mute, Pip, Replay, Note,
}

/// <summary>Ícones desenhados numa grelha de 16×16, na cor do texto (os botões só de ícone).</summary>
public sealed class Glyph : Control
{
    public static readonly StyledProperty<GlyphKind> KindProperty =
        AvaloniaProperty.Register<Glyph, GlyphKind>(nameof(Kind));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Glyph>();

    static Glyph()
    {
        AffectsRender<Glyph>(KindProperty, ForegroundProperty);
    }

    public Glyph()
    {
        IsHitTestVisible = false;
    }

    public GlyphKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public override void Render(DrawingContext context)
    {
        var brush = Foreground ?? Brushes.White;
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
            return;
        var s = size / 16.0;
        var ox = (Bounds.Width - size) / 2;
        var oy = (Bounds.Height - size) / 2;
        Point P(double x, double y) => new(ox + x * s, oy + y * s);
        Rect R(double x, double y, double w, double h) => new(ox + x * s, oy + y * s, w * s, h * s);
        var pen = new Pen(brush, Math.Max(1, 1.5 * s), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var thin = new Pen(brush, Math.Max(1, 1.25 * s), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        void Poly(params Point[] points)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(points[0], true);
                for (var i = 1; i < points.Length; i++)
                    c.LineTo(points[i]);
                c.EndFigure(true);
            }
            context.DrawGeometry(brush, null, g);
        }

        void Lines(Pen p, params Point[] points)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(points[0], false);
                for (var i = 1; i < points.Length; i++)
                    c.LineTo(points[i]);
                c.EndFigure(false);
            }
            context.DrawGeometry(null, p, g);
        }

        // Arco de circunferência de a0 a a1 graus (0 = direita, sentido dos ponteiros).
        void Arc(Pen p, double cx, double cy, double r, double a0, double a1)
        {
            static double Rad(double d) => d * Math.PI / 180;
            var start = P(cx + r * Math.Cos(Rad(a0)), cy + r * Math.Sin(Rad(a0)));
            var end = P(cx + r * Math.Cos(Rad(a1)), cy + r * Math.Sin(Rad(a1)));
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(start, false);
                c.ArcTo(end, new Size(r * s, r * s), 0, Math.Abs(a1 - a0) > 180, SweepDirection.Clockwise);
                c.EndFigure(false);
            }
            context.DrawGeometry(null, p, g);
        }

        void Speaker() => Poly(P(1.5, 6), P(4.5, 6), P(8.5, 2.5), P(8.5, 13.5), P(4.5, 10), P(1.5, 10));

        switch (Kind)
        {
            case GlyphKind.Play:
                Poly(P(4, 2), P(14, 8), P(4, 14));
                break;
            case GlyphKind.Pause:
                context.FillRectangle(brush, R(3, 2.5, 3.6, 11));
                context.FillRectangle(brush, R(9.4, 2.5, 3.6, 11));
                break;
            case GlyphKind.Prev:
                context.FillRectangle(brush, R(2, 2.5, 2.4, 11));
                Poly(P(14, 2.5), P(5, 8), P(14, 13.5));
                break;
            case GlyphKind.Next:
                Poly(P(2, 2.5), P(11, 8), P(2, 13.5));
                context.FillRectangle(brush, R(11.6, 2.5, 2.4, 11));
                break;
            case GlyphKind.Menu:
                context.DrawLine(pen, P(3, 4.5), P(13, 4.5));
                context.DrawLine(pen, P(3, 8), P(13, 8));
                context.DrawLine(pen, P(3, 11.5), P(13, 11.5));
                break;
            case GlyphKind.Minimize:
                context.DrawLine(pen, P(3.5, 11.5), P(12.5, 11.5));
                break;
            case GlyphKind.Maximize:
                context.DrawRectangle(null, thin, R(3.5, 3.5, 9, 9));
                break;
            case GlyphKind.Restore:
                context.DrawRectangle(null, thin, R(3, 5.5, 7.5, 7.5));
                Lines(thin, P(5.5, 5.5), P(5.5, 3), P(13, 3), P(13, 10.5), P(10.5, 10.5));
                break;
            case GlyphKind.Close:
                context.DrawLine(pen, P(4, 4), P(12, 12));
                context.DrawLine(pen, P(12, 4), P(4, 12));
                break;
            case GlyphKind.Logo:
                // Um ecrã com o triângulo de tocar e o pé.
                context.DrawRectangle(null, thin, new RoundedRect(R(1, 2, 14, 9.5), 1.4 * s));
                Poly(P(6.6, 4.4), P(10.6, 6.75), P(6.6, 9.1));
                context.DrawLine(thin, P(8, 11.5), P(8, 13.6));
                context.DrawLine(thin, P(5, 14), P(11, 14));
                break;
            case GlyphKind.Fullscreen:
                Lines(pen, P(2, 6), P(2, 2), P(6, 2));
                Lines(pen, P(10, 2), P(14, 2), P(14, 6));
                Lines(pen, P(14, 10), P(14, 14), P(10, 14));
                Lines(pen, P(6, 14), P(2, 14), P(2, 10));
                break;
            case GlyphKind.ExitFullscreen:
                Lines(pen, P(6, 2), P(6, 6), P(2, 6));
                Lines(pen, P(10, 2), P(10, 6), P(14, 6));
                Lines(pen, P(14, 10), P(10, 10), P(10, 14));
                Lines(pen, P(2, 10), P(6, 10), P(6, 14));
                break;
            case GlyphKind.Subtitles:
                context.DrawRectangle(null, thin, new RoundedRect(R(1.5, 3, 13, 10), 1.6 * s));
                context.DrawLine(thin, P(4.5, 8.2), P(7, 8.2));
                context.DrawLine(thin, P(8.8, 8.2), P(11.5, 8.2));
                context.DrawLine(thin, P(4.5, 10.6), P(10, 10.6));
                break;
            case GlyphKind.AudioTrack:
                context.DrawLine(pen, P(3, 6.5), P(3, 9.5));
                context.DrawLine(pen, P(6, 3), P(6, 13));
                context.DrawLine(pen, P(9, 5), P(9, 11));
                context.DrawLine(pen, P(12, 7), P(12, 9));
                break;
            case GlyphKind.Volume:
                Speaker();
                Arc(thin, 8.5, 8, 3, -50, 50);
                Arc(thin, 8.5, 8, 5.8, -50, 50);
                break;
            case GlyphKind.VolumeLow:
                Speaker();
                Arc(thin, 8.5, 8, 3, -50, 50);
                break;
            case GlyphKind.Mute:
                Speaker();
                context.DrawLine(thin, P(11, 5.8), P(15, 10.2));
                context.DrawLine(thin, P(15, 5.8), P(11, 10.2));
                break;
            case GlyphKind.Pip:
                context.DrawRectangle(null, thin, new RoundedRect(R(1.5, 2.5, 13, 11), 1.2 * s));
                context.FillRectangle(brush, R(8, 8, 5, 4));
                break;
            case GlyphKind.Replay:
                Arc(pen, 8, 8.5, 5.5, -70, 210);
                Poly(P(9.6, 0.8), P(9.9, 5.6), P(5.9, 3.4));
                break;
            case GlyphKind.Note:
                context.DrawEllipse(brush, null, P(5, 12), 2.6 * s, 2.1 * s);
                context.DrawLine(pen, P(7.4, 12), P(7.4, 2.5));
                Lines(pen, P(7.4, 2.5), P(13, 4.2), P(13, 6.6), P(7.4, 5));
                break;
        }
    }
}
