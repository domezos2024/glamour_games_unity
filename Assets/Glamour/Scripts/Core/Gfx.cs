using System;

namespace GlamourGames
{
    /// <summary>Neon-Palette und Farb-Hilfsfunktionen (identisch zum Original).</summary>
    public static class C
    {
        public static readonly Col Bg = new Col(6, 1, 15), Cyan = new Col(0, 255, 242), Pink = new Col(255, 32, 121), Magenta = new Col(255, 0, 229), Gold = new Col(255, 215, 0),
            Purple = new Col(160, 32, 255), Green = new Col(0, 255, 106), Yellow = new Col(250, 255, 0), Orange = new Col(255, 136, 0), Blue = new Col(77, 166, 255), Red = new Col(255, 68, 68),
            White = Col.White, Black = Col.Black, Dim = new Col(170, 160, 200), Panel = new Col(20, 8, 40);
        /// <summary>Gleiche Farbe mit Deckkraft a (0..1).</summary>
        public static Col A(this Col c, float a) => c.WithAlpha((byte)Math.Clamp(a * 255, 0, 255));
        public static Col Mix(this Col a, Col b, float t) => new Col((byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t), (byte)(a.Blue + (b.Blue - a.Blue) * t), (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
        public static Col Dark(this Col c, float f) => new Col((byte)Math.Clamp(c.Red * f, 0, 255), (byte)Math.Clamp(c.Green * f, 0, 255), (byte)Math.Clamp(c.Blue * f, 0, 255), c.Alpha);
        public static Col Light(this Col c, float f) => c.Mix(Col.White, f);
    }

    /// <summary>Zeichen-Hilfen im Stil des Originals (Gfx.Text, Gfx.Rect, Gfx.Glow ...), GPU-beschleunigt.</summary>
    public static class Gfx
    {
        // Ringpuffer statt einer einzigen statischen Paint: zwei nacheinander geholte Paints ueberschreiben sich nicht.
        static readonly Paint[] ring = new Paint[32]; static int ringI;
        static Gfx() { for (int i = 0; i < ring.Length; i++) ring[i] = new Paint(); }
        static Paint Next() { ringI = (ringI + 1) % ring.Length; return ring[ringI].Reset(); }

        /// <summary>Neue Fuell-Paint mit Farbe.</summary>
        public static Paint Fill(Col c) { var p = Next(); p.Color = c; return p; }
        /// <summary>Neue Strich-Paint (runde Enden) mit Farbe und Breite.</summary>
        public static Paint Line(Col c, float w) { var p = Next(); p.Color = c; p.Stroke = true; p.StrokeWidth = w; return p; }
        /// <summary>Kompatibilitaet zu Skia MaskFilter: liefert nur das Sigma (Paint.Blur = Gfx.Blur(x)).</summary>
        public static float Blur(float s) => s;

        public static Box R(float x, float y, float w, float h) => new Box(x, y, x + w, y + h);
        public static Box Ctr(float cx, float cy, float w, float h) => new Box(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
        public static Box Inflate(Box r, float d) => new Box(r.Left - d, r.Top - d, r.Right + d, r.Bottom + d);

        public static void Rect(Canvas2D c, Box r, float rad, Col col) => c.DrawRoundRect(r, rad, rad, Fill(col));
        public static void RectGrad(Canvas2D c, Box r, float rad, Col top, Col bot)
        {
            var p = Fill(Col.White); p.Shader = Grad.Linear(r.Left, r.Top, r.Left, r.Bottom, top, bot); c.DrawRoundRect(r, rad, rad, p);
        }
        /// <summary>Verlauf in beliebiger Richtung (von (x0,y0) nach (x1,y1)).</summary>
        public static void RectGradDir(Canvas2D c, Box r, float rad, float x0, float y0, float x1, float y1, Col a, Col b)
        {
            var p = Fill(Col.White); p.Shader = Grad.Linear(x0, y0, x1, y1, a, b); c.DrawRoundRect(r, rad, rad, p);
        }
        /// <summary>Radialer Verlauf als Flaechenfuellung (z.B. Spieltisch-Filz).</summary>
        public static void RectRadial(Canvas2D c, Box r, float rad, float cx, float cy, float radius, Col inner, Col outer)
        {
            var p = Fill(Col.White); p.Shader = Grad.Radial(cx, cy, radius, inner, outer); c.DrawRoundRect(r, rad, rad, p);
        }
        public static void Stroke(Canvas2D c, Box r, float rad, Col col, float w) => c.DrawRoundRect(r, rad, rad, Line(col, w));
        /// <summary>Weicher Neon-Schein entlang des Rands (HDR, wird vom Bloom verstaerkt).</summary>
        public static void Glow(Canvas2D c, Box r, float rad, Col col, float sigma, float a = 1)
        {
            var p = Line(col.A(a * col.Alpha / 255f), sigma * .8f); p.Blur = sigma; p.Glow = 1.6f; c.DrawRoundRect(r, rad, rad, p);
        }
        public static void GlowFill(Canvas2D c, Box r, float rad, Col col, float sigma, float a = 1)
        {
            var p = Fill(col.A(a * col.Alpha / 255f)); p.Blur = sigma; p.Glow = 1.4f; c.DrawRoundRect(r, rad, rad, p);
        }
        /// <summary>Weicher Lichthof (Farbe -&gt; transparent).</summary>
        public static void Radial(Canvas2D c, float x, float y, float rad, Col col, float a = 1) => c.DrawRadial(x, y, rad, Fill(col.A(a * col.Alpha / 255f)));
        /// <summary>Wie Radial, aber additiv und mit HDR-Leuchtkraft (fuer Lichter, Funken, Glanz).</summary>
        public static void Light(Canvas2D c, float x, float y, float rad, Col col, float a = 1, float glow = 1.6f)
        {
            var p = Fill(col.A(a * col.Alpha / 255f)); p.Additive = true; p.Glow = glow; c.DrawRadial(x, y, rad, p);
        }
        /// <summary>Beleuchtete 3D-Kugel.</summary>
        public static void Ball(Canvas2D c, float x, float y, float r, Col col) => c.DrawBall(x, y, r, Fill(col));

        // ------------------------------------------------------------------ Text
        static float Bump(float s) { if (s >= 32) return s; float b = s * 1.15f; return s >= 16 && b < 24 ? 24 : b; }
        static int FontIdx(bool bold, bool serif) => serif ? 2 : bold ? 1 : 0;
        /// <summary>Textbreite in Design-Einheiten.</summary>
        public static float TW(string s, float size, bool bold = true, bool serif = false) => FontAtlas.Measure(s, Bump(size), FontIdx(bold, serif));

        /// <summary>Text, vertikal mittig um y. glow = Weichzeichnungs-Sigma des Leuchtens (0 = keins).</summary>
        public static void Text(Canvas2D c, string s, float x, float y, float size, Col col, Al al = Al.C, bool bold = true, float glow = 0, bool serif = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            float sz = Bump(size); int fi = FontIdx(bold, serif);
            float w = FontAtlas.Measure(s, sz, fi), by = y + (FontAtlas.Ascent(fi, sz) - FontAtlas.Descent(fi, sz)) / 2;
            float x0 = al == Al.C ? x - w / 2 : al == Al.R ? x - w : x;
            if (glow > 0)
            {
                var g = Fill(col.A(.45f * col.Alpha / 255f)); g.Blur = glow; g.Glow = 1.5f;
                FontAtlas.Draw(c, s, x0, by, sz, fi, g);
            }
            var p = Fill(col);
            FontAtlas.Draw(c, s, x0, by, sz, fi, p);
        }
        /// <summary>Text mit beliebiger Paint (z.B. mit Farbverlauf-Shader).</summary>
        public static void TextPaint(Canvas2D c, string s, float x, float y, float size, Paint p, Al al = Al.C, bool bold = true, bool serif = false)
        {
            float sz = Bump(size); int fi = FontIdx(bold, serif);
            float w = FontAtlas.Measure(s, sz, fi), by = y + (FontAtlas.Ascent(fi, sz) - FontAtlas.Descent(fi, sz)) / 2;
            FontAtlas.Draw(c, s, al == Al.C ? x - w / 2 : al == Al.R ? x - w : x, by, sz, fi, p);
        }
        public static void TextOutline(Canvas2D c, string s, float x, float y, float size, Col col, float w, bool serif = true)
        {
            var p = Line(col, w); TextPaint(c, s, x, y, size, p, Al.C, true, serif);
        }
        /// <summary>Grosser Sieger-Schriftzug mit kraeftigem Neon-Schein.</summary>
        public static void BannerText(Canvas2D c, string s, float size, Col col, float alpha)
        {
            c.SaveLayer(alpha);
            var g = Fill(col.A(.85f)); g.Blur = 34; g.Glow = 2.2f; TextPaint(c, s, 0, 0, size, g, Al.C, true, true);
            Text(c, s, 0, 0, size, col, Al.C, true, 14, true);
            var p = Fill(col.Light(.6f)); TextPaint(c, s, 0, 0, size, p, Al.C, true, true);
            c.Restore();
        }
        public static void TextShadow(Canvas2D c, string s, float x, float y, float size, Col col, Al al = Al.C, bool serif = false)
        {
            Text(c, s, x + 2, y + 3, size, Col.Black.A(.6f), al, true, 0, serif); Text(c, s, x, y, size, col, al, true, 0, serif);
        }

        // ------------------------------------------------------------------ Bilder & Formen
        public static void Image(Canvas2D c, Img img, Box dst, float a = 1, float rad = 0)
        {
            if (img == null) return;
            c.DrawImage(img, dst, Fill(Col.White.A(a)), rad);
        }
        public static Path2D Heart(float cx, float cy, float s)
        {
            var p = new Path2D(); p.MoveTo(cx, cy + s * .9f);
            p.CubicTo(cx - s * 1.5f, cy - s * .1f, cx - s * .8f, cy - s * 1.1f, cx, cy - s * .35f);
            p.CubicTo(cx + s * .8f, cy - s * 1.1f, cx + s * 1.5f, cy - s * .1f, cx, cy + s * .9f); p.Close(); return p;
        }
        /// <summary>Spielkartenfarbe: 0 Pik, 1 Herz, 2 Karo, 3 Kreuz.</summary>
        public static void Suit(Canvas2D c, int suit, float cx, float cy, float s, Col col)
        {
            var p = Fill(col);
            switch (suit)
            {
                case 1: using (var h = Heart(cx, cy, s)) c.DrawPath(h, p); break;
                case 2: { using var d = new Path2D(); d.MoveTo(cx, cy - s); d.LineTo(cx + s * .75f, cy); d.LineTo(cx, cy + s); d.LineTo(cx - s * .75f, cy); d.Close(); c.DrawPath(d, p); break; }
                case 0:
                    {
                        using var h = Heart(cx, cy, s); c.Save(); c.Scale(1, -1, cx, cy); c.DrawPath(h, p); c.Restore();
                        using var st = new Path2D(); st.MoveTo(cx, cy + s * .1f); st.LineTo(cx + s * .35f, cy + s); st.LineTo(cx - s * .35f, cy + s); st.Close(); c.DrawPath(st, p); break;
                    }
                default:
                    {
                        c.DrawCircle(cx, cy - s * .45f, s * .42f, p); c.DrawCircle(cx - s * .48f, cy + s * .2f, s * .42f, p); c.DrawCircle(cx + s * .48f, cy + s * .2f, s * .42f, p);
                        using var st = new Path2D(); st.MoveTo(cx, cy); st.LineTo(cx + s * .3f, cy + s); st.LineTo(cx - s * .3f, cy + s); st.Close(); c.DrawPath(st, p); break;
                    }
            }
        }
        public static void Gear(Canvas2D c, float cx, float cy, float r, Col col)
        {
            using var p = new Path2D();
            for (int i = 0; i < 16; i++)
            {
                float a0 = i * MathF.PI / 8, a1 = a0 + MathF.PI / 16, rr = (i % 2 == 0) ? r : r * .78f;
                if (i == 0) p.MoveTo(cx + MathF.Cos(a0) * rr, cy + MathF.Sin(a0) * rr); else p.LineTo(cx + MathF.Cos(a0) * rr, cy + MathF.Sin(a0) * rr);
                p.LineTo(cx + MathF.Cos(a1) * rr, cy + MathF.Sin(a1) * rr);
            }
            p.Close(); c.DrawPath(p, Fill(col)); c.DrawCircle(cx, cy, r * .38f, Fill(C.Panel));
        }
        static Path2D starPath;
        public static void Star(Canvas2D c, float cx, float cy, float r, Col col, float rot = 0)
        {
            if (starPath == null)
            {
                starPath = new Path2D();
                for (int i = 0; i < 10; i++) { float a = i * MathF.PI / 5 - MathF.PI / 2, rr = i % 2 == 0 ? 1f : .45f; if (i == 0) starPath.MoveTo(MathF.Cos(a) * rr, MathF.Sin(a) * rr); else starPath.LineTo(MathF.Cos(a) * rr, MathF.Sin(a) * rr); }
                starPath.Close();
            }
            c.Save(); c.Translate(cx, cy); c.RotateRadians(rot); c.Scale(r, r); c.DrawPath(starPath, Fill(col)); c.Restore();
        }
        /// <summary>Weicher Schlagschatten unter einem Rechteck (Karten, Panels).</summary>
        public static void Shadow(Canvas2D c, Box r, float rad, float sigma = 12, float a = .45f, float dx = 6, float dy = 10)
        {
            var p = Fill(Col.Black.A(a)); p.Blur = sigma; c.DrawRoundRect(r.Offset(dx, dy), rad, rad, p);
        }
    }
}
