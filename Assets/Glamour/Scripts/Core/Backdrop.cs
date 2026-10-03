using System;
using System.Linq;

namespace GlamourGames
{
    /// <summary>
    /// Hintergrund: Nebel, Sterne und Polarlicht kommen aus dem Shader "Glamour/Backdrop" (App setzt die Parameter),
    /// die leuchtende Blumenwiese mit Bluetenblaettern und Gluehwuermchen wird hier per Canvas gezeichnet.
    /// </summary>
    public static class Backdrop
    {
        struct Fl { public float x, h, size, ph, delay; public int type, ci; }
        struct Pe { public float x, y, sp, sw, ph, size; public int ci; }
        struct Ff { public float x, y, fx, fy, ph, size; }
        static readonly Random R = new Random(11);
        static readonly Fl[] fl = Enumerable.Range(0, 34).Select(i => new Fl { x = (i + (float)R.NextDouble() * .8f) / 34f, h = 60 + (float)R.NextDouble() * 110, size = 16 + (float)R.NextDouble() * 20, ph = (float)R.NextDouble() * 6.3f, delay = (float)R.NextDouble() * 1.6f, type = R.Next(3), ci = R.Next(6) }).ToArray();
        static readonly Pe[] pe = Enumerable.Range(0, 28).Select(_ => new Pe { x = (float)R.NextDouble(), y = (float)R.NextDouble(), sp = 22 + (float)R.NextDouble() * 34, sw = 20 + (float)R.NextDouble() * 40, ph = (float)R.NextDouble() * 6.3f, size = 5 + (float)R.NextDouble() * 6, ci = R.Next(6) }).ToArray();
        static readonly Ff[] ff = Enumerable.Range(0, 26).Select(_ => new Ff { x = (float)R.NextDouble(), y = .35f + (float)R.NextDouble() * .6f, fx = .1f + (float)R.NextDouble() * .3f, fy = .1f + (float)R.NextDouble() * .4f, ph = (float)R.NextDouble() * 6.3f, size = 2 + (float)R.NextDouble() * 2.5f }).ToArray();
        static readonly float[] blades = Enumerable.Range(0, 170).Select(_ => (float)R.NextDouble()).ToArray();
        static Col Pal(int i, Col a1, Col a2) => i switch { 0 => a1, 1 => a2, 2 => C.Pink, 3 => C.Gold, 4 => C.Purple, _ => C.Magenta };
        static readonly Path2D stemP = new Path2D(), hill = new Path2D();

        public static void DrawMeadow(Canvas2D c, float t, Col a1, Col a2, float amount = 1)
        {
            if (amount <= 0) return;
            float x0 = App.VX0 - 50, x1 = App.VX1 + 50, y0 = App.VY0 - 50, y1 = App.VY1, w = x1 - x0, h = y1 - y0, grow = Ease.OutCubic(t / 1.8f);
            c.SaveLayer(amount);
            hill.Reset(); hill.MoveTo(x0, y1 + 10); for (float x = 0; x <= w + 20; x += 20) hill.LineTo(x0 + x, y1 - 46 - 20 * MathF.Sin(x * .006f + 1)); hill.LineTo(x1, y1 + 10); hill.Close();
            { var p = Gfx.Fill(Col.White); p.Shader = Grad.Linear(0, y1 - 90, 0, y1, new Col(30, 8, 60, 0), new Col(18, 4, 40, 215)); c.DrawPath(hill, p); }
            for (int i = 0; i < blades.Length; i++)
            {
                float bx = x0 + blades[i] * w, bh = (18 + blades[(i * 7) % blades.Length] * 34) * grow, sway = MathF.Sin(t * 1.3f + bx * .02f) * 6, gy = y1 - 30 - 20 * MathF.Sin((bx - x0) * .006f + 1);
                c.DrawLine(bx, gy + 30, bx + sway, gy + 30 - bh, Gfx.Line(C.Green.Dark(.35f).A(.5f), 2.2f));
            }
            foreach (var f in fl)
            {
                float g = Ease.OutBack(Ease.Clamp((t - f.delay) / 1.4f)), pulse = 1 + .07f * MathF.Sin(t * 1.6f + f.ph);
                float bx = x0 + f.x * w, gy = y1 - 26 - 20 * MathF.Sin((bx - x0) * .006f + 1), hh = f.h * g, sway = MathF.Sin(t * 1.1f + f.ph) * 9 * g;
                var col = Pal(f.ci, a1, a2); var stem = C.Green.Dark(.45f);
                stemP.Reset(); stemP.MoveTo(bx, gy + 26); stemP.QuadTo(bx + sway * .3f, gy + 26 - hh * .5f, bx + sway, gy + 26 - hh);
                c.DrawPath(stemP, Gfx.Line(stem.A(.75f), 3.5f));
                float lx = bx + sway * .35f, ly = gy + 26 - hh * .4f; c.Save(); c.Translate(lx, ly); c.RotateDegrees(-35 + sway * 2); c.DrawOval(14 * g, 0, 15 * g, 5 * g, Gfx.Fill(stem.Light(.15f).A(.7f))); c.Restore();
                Head(c, bx + sway, gy + 26 - hh, f.size * g * pulse, col, f.type, t + f.ph);
            }
            foreach (var p in pe)
            {
                float py = y0 + (p.y * h + t * p.sp) % h, px = x0 + ((p.x * w + MathF.Sin(t * .8f + p.ph) * p.sw + t * 10) % w + w) % w;
                c.Save(); c.Translate(px, py); c.RotateDegrees(t * 60 + p.ph * 50); c.Scale(1, MathF.Abs(MathF.Cos(t * 1.8f + p.ph)) * .7f + .3f); c.DrawOval(0, 0, p.size * .55f, p.size, Gfx.Fill(Pal(p.ci, a1, a2).Light(.25f).A(.55f))); c.Restore();
            }
            foreach (var f in ff)
            {
                float fx = x0 + ((f.x + .06f * MathF.Sin(t * f.fx + f.ph)) % 1f + 1) % 1 * w, fy = y0 + (f.y + .05f * MathF.Cos(t * f.fy + f.ph)) * h, a = .5f + .5f * MathF.Sin(t * 2.2f + f.ph);
                Gfx.Light(c, fx, fy, f.size * 7, C.Yellow, a * .5f, 1.8f); var core = Gfx.Fill(Col.White.A(.4f + a * .5f)); core.Glow = 2.2f; c.DrawCircle(fx, fy, f.size * .6f, core);
            }
            c.Restore();
        }

        static void Head(Canvas2D c, float x, float y, float r, Col col, int type, float t)
        {
            if (r < 1) return; Gfx.Light(c, x, y, r * 2.6f, col, .32f, 1.4f);
            c.Save(); c.Translate(x, y); c.RotateDegrees(MathF.Sin(t * .5f) * 8);
            if (type == 0)
            {
                for (int k = 0; k < 6; k++) { c.Save(); c.RotateDegrees(k * 60); c.DrawOval(0, -r * .62f, r * .34f, r * .62f, Gfx.Fill(col.Light(.08f * (k % 2)).A(.85f))); c.Restore(); }
                var g = Gfx.Fill(C.Gold); g.Glow = 1.3f; c.DrawCircle(0, 0, r * .3f, g);
            }
            else if (type == 1)
            {
                using var tp = new Path2D(); tp.MoveTo(-r * .7f, -r * .3f); tp.LineTo(-r * .45f, -r * 1.05f); tp.LineTo(-r * .15f, -r * .55f); tp.LineTo(0, -r * 1.2f); tp.LineTo(r * .15f, -r * .55f); tp.LineTo(r * .45f, -r * 1.05f); tp.LineTo(r * .7f, -r * .3f); tp.QuadTo(0, r * .55f, -r * .7f, -r * .3f); tp.Close();
                c.DrawPath(tp, Gfx.Fill(col.A(.9f))); c.DrawPath(tp, Gfx.Line(col.Light(.5f).A(.7f), 1.5f));
            }
            else
            {
                for (int k = 0; k < 11; k++) { c.Save(); c.RotateDegrees(k * 32.7f); c.DrawOval(0, -r * .7f, r * .14f, r * .5f, Gfx.Fill(Col.White.Mix(col, .35f).A(.85f))); c.Restore(); }
                c.DrawCircle(0, 0, r * .26f, Gfx.Fill(C.Yellow));
            }
            c.Restore();
        }
    }
}
