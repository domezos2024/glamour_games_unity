using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    /// <summary>Geometrie-Hilfen.</summary>
    public static class Geo
    {
        /// <summary>Schneidet eine Strecke auf ein Rechteck zu (Liang-Barsky). false = komplett ausserhalb.</summary>
        public static bool ClipSeg(Box r, ref float x0, ref float y0, ref float x1, ref float y1)
        {
            float t0 = 0, t1 = 1, dx = x1 - x0, dy = y1 - y0;
            float[] p = { -dx, dx, -dy, dy }, q = { x0 - r.Left, r.Right - x0, y0 - r.Top, r.Bottom - y0 };
            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0) { if (q[i] < 0) return false; continue; }
                float t = q[i] / p[i];
                if (p[i] < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
                else { if (t < t0) return false; if (t < t1) t1 = t; }
            }
            float nx0 = x0 + t0 * dx, ny0 = y0 + t0 * dy, nx1 = x0 + t1 * dx, ny1 = y0 + t1 * dy;
            x0 = nx0; y0 = ny0; x1 = nx1; y1 = ny1; return true;
        }
        /// <summary>Zeichnet eine Linie, die auf ein Rechteck begrenzt ist (funktioniert auch in gedrehten Karten).</summary>
        public static void LineIn(Canvas2D c, Box r, float x0, float y0, float x1, float y1, Paint p)
        {
            if (ClipSeg(r, ref x0, ref y0, ref x1, ref y1)) c.DrawLine(x0, y0, x1, y1, p);
        }
    }

    /// <summary>Spielkarten (Vorderseite, Rueckseite, Wenden).</summary>
    public static class CardArt
    {
        public static readonly string[] Ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
        public static Col SuitCol(int s) => s == 1 || s == 2 ? new Col(214, 30, 60) : new Col(24, 20, 40);
        public static void Face(Canvas2D c, Box r, string rank, int suit, bool hl = false)
        {
            float w = r.Width, rad = w * .09f;
            Gfx.RectGrad(c, r, rad, new Col(255, 255, 255), new Col(222, 216, 238));
            Gfx.RectGrad(c, new Box(r.Left + 3, r.Top + 3, r.Right - 3, r.Top + r.Height * .45f), rad * .8f, Col.White.A(.6f), Col.White.A(0));
            Gfx.Stroke(c, r, rad, hl ? C.Gold : new Col(120, 100, 160), hl ? 4 : 1.5f);
            var col = SuitCol(suit); float fs = w * (rank == "10" ? .27f : .3f);
            Gfx.Text(c, rank, r.Left + w * .2f, r.Top + w * .26f, fs, col, Al.C);
            Gfx.Suit(c, suit, r.Left + w * .2f, r.Top + w * .5f, w * .09f, col);
            Gfx.Suit(c, suit, r.MidX, r.MidY + w * .05f, w * .27f, col);
            c.Save(); c.RotateDegrees(180, r.MidX, r.MidY);
            Gfx.Text(c, rank, r.Left + w * .2f, r.Top + w * .26f, fs, col, Al.C); Gfx.Suit(c, suit, r.Left + w * .2f, r.Top + w * .5f, w * .09f, col); c.Restore();
            if (hl) Gfx.Glow(c, r, rad, C.Gold, 10, .9f);
        }
        public static void Back(Canvas2D c, Box r)
        {
            float rad = r.Width * .09f;
            Gfx.RectGrad(c, r, rad, new Col(78, 24, 150), new Col(26, 6, 66)); Gfx.Stroke(c, r, rad, C.Gold, 2.5f);
            var inner = Gfx.Inflate(r, -r.Width * .09f);
            var p = Gfx.Line(C.Gold.A(.35f), 1.5f); float s = r.Width * .16f;
            for (float k = -r.Height; k < r.Width + r.Height; k += s)
            {
                Geo.LineIn(c, inner, inner.Left + k, inner.Top, inner.Left + k + inner.Height, inner.Bottom, p);
                Geo.LineIn(c, inner, inner.Left + k, inner.Bottom, inner.Left + k + inner.Height, inner.Top, p);
            }
            Gfx.Stroke(c, inner, rad * .5f, C.Gold.A(.8f), 2);
            Gfx.Radial(c, r.MidX, r.MidY, r.Width * .45f, C.Gold, .18f);
            Gfx.Suit(c, 2, r.MidX, r.MidY, r.Width * .16f, C.Gold.A(.95f));
        }
        /// <summary>Karte mit Mittelpunkt (cx,cy), Breite w. flip 0 = Rueckseite, 1 = Vorderseite (dazwischen dreht sie).</summary>
        public static void Card(Canvas2D c, float cx, float cy, float w, string rank, int suit, float flip, float rot = 0, float lift = 0, bool hl = false)
        {
            float h = w * 1.4f, sx = MathF.Abs(MathF.Cos(flip * MathF.PI)); bool face = flip > .5f;
            float turn = MathF.Sin(flip * MathF.PI), rad = w * .09f;
            c.Save(); c.Translate(cx, cy - lift); c.RotateDegrees(rot);
            // Kontaktschatten (eng + weich), waechst mit dem Anheben
            var sh = Gfx.Fill(Col.Black.A(.32f)); sh.Blur = 18 + lift * .15f; c.DrawRoundRect(Gfx.Ctr(8 + lift * .15f, 14 + lift * .3f, w * sx, h), rad, rad, sh);
            var sh2 = Gfx.Fill(Col.Black.A(Math.Max(0, .5f - lift * .01f))); sh2.Blur = 3; c.DrawRoundRect(Gfx.Ctr(2, 3, w * sx, h), rad, rad, sh2);
            // beim Umdrehen leichte Perspektive (die nahe Kante wirkt groesser)
            c.Scale(Math.Max(sx, .02f), 1 + .06f * turn);
            var r = Gfx.Ctr(0, 0, w, h);
            // Kartendicke
            c.DrawRoundRect(r.Offset(0, 2.2f), rad, rad, Gfx.Fill(face ? new Col(170, 160, 190) : new Col(40, 12, 80)));
            if (face) Face(c, r, rank, suit, hl); else Back(c, r);
            Gfx.Paper(c, r, rad, face ? .07f : .1f);
            c.Save(); c.ClipRoundRect(r, rad);
            var gl = Gfx.Fill(Col.White.A(.10f + .14f * turn)); gl.Additive = true; gl.Blur = w * .18f;
            c.DrawOval(-w * .3f + w * .6f * flip, -h * .32f, w * .55f, h * .16f, gl);
            c.Restore();
            c.Restore();
        }
    }

    /// <summary>3D-Wuerfel mit Perspektive, Schattierung und Augen.</summary>
    public static class Die3D
    {
        static readonly int[] faceVal = { 1, 6, 3, 4, 2, 5 };
        static readonly float[][] nrm = { new float[] { 0, 0, 1 }, new float[] { 0, 0, -1 }, new float[] { 1, 0, 0 }, new float[] { -1, 0, 0 }, new float[] { 0, 1, 0 }, new float[] { 0, -1, 0 } };
        static readonly float[][] uax = { new float[] { 1, 0, 0 }, new float[] { -1, 0, 0 }, new float[] { 0, 0, -1 }, new float[] { 0, 0, 1 }, new float[] { 1, 0, 0 }, new float[] { 1, 0, 0 } };
        static readonly float[][] vax = { new float[] { 0, 1, 0 }, new float[] { 0, 1, 0 }, new float[] { 0, 1, 0 }, new float[] { 0, 1, 0 }, new float[] { 0, 0, -1 }, new float[] { 0, 0, 1 } };
        public static float[] Mul(float[] a, float[] b)
        {
            var r = new float[9]; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) r[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j]; return r;
        }
        public static float[] RX(float a) { float c = MathF.Cos(a), s = MathF.Sin(a); return new float[] { 1, 0, 0, 0, c, -s, 0, s, c }; }
        public static float[] RY(float a) { float c = MathF.Cos(a), s = MathF.Sin(a); return new float[] { c, 0, s, 0, 1, 0, -s, 0, c }; }
        public static float[] RZ(float a) { float c = MathF.Cos(a), s = MathF.Sin(a); return new float[] { c, -s, 0, s, c, 0, 0, 0, 1 }; }
        public static float[] RAxis(float x, float y, float z, float a)
        {
            float l = MathF.Sqrt(x * x + y * y + z * z); x /= l; y /= l; z /= l; float c = MathF.Cos(a), s = MathF.Sin(a), t = 1 - c;
            return new float[] { t * x * x + c, t * x * y - s * z, t * x * z + s * y, t * x * y + s * z, t * y * y + c, t * y * z - s * x, t * x * z - s * y, t * y * z + s * x, t * z * z + c };
        }
        /// <summary>Rotationsmatrix, bei der Augenzahl v nach vorne zeigt.</summary>
        public static float[] Face(int v)
        {
            switch (v) { case 1: return RX(0); case 6: return RX(MathF.PI); case 3: return RY(-MathF.PI / 2); case 4: return RY(MathF.PI / 2); case 2: return RX(MathF.PI / 2); default: return RX(-MathF.PI / 2); }
        }
        static readonly (float, float)[][] pips = {
            new[]{(.5f,.5f)}, new[]{(.27f,.27f),(.73f,.73f)}, new[]{(.27f,.27f),(.5f,.5f),(.73f,.73f)}, new[]{(.27f,.27f),(.73f,.27f),(.27f,.73f),(.73f,.73f)},
            new[]{(.27f,.27f),(.73f,.27f),(.5f,.5f),(.27f,.73f),(.73f,.73f)}, new[]{(.27f,.27f),(.73f,.27f),(.27f,.5f),(.73f,.5f),(.27f,.73f),(.73f,.73f)} };
        /// <summary>Echte 3D-Darstellung (URP-Modul): zeichnet den Wuerfel und liefert true, sonst wird die 2D-Variante genutzt.</summary>
        public static Func<Canvas2D, float, float, float, float[], Col, Col, bool> Render3D;
        public static Action FrameBegin, FrameEnd;
        /// <summary>Wuerfel einer echten 3D-Tischszene (Unity-Achsen, Kantenlaenge 1, Tisch bei y = 0).</summary>
        public struct TrayDie { public System.Numerics.Vector3 Pos; public System.Numerics.Quaternion Rot; public Col Body, Pip; }
        /// <summary>3D-Tischszene (URP-Modul): (Canvas, Bildbereich, Designpunkt der Weltmitte, Designeinheiten je Welteinheit, Wuerfel) -> true wenn gezeichnet.</summary>
        public static Func<Canvas2D, Box, Pt, float, TrayDie[], bool> RenderTray;
        public static readonly int[] FaceValues = faceVal;
        public static float[] Normal(int i) => nrm[i];
        public static float[] UAxis(int i) => uax[i];
        public static float[] VAxis(int i) => vax[i];
        public static (float, float)[] Pips(int value) => pips[value - 1];
        public static void Draw(Canvas2D c, float cx, float cy, float size, float[] m, Col body, Col pip, bool glow = false)
        {
            if (glow) Gfx.Light(c, cx, cy, size * 1.2f, body, .45f, 1.4f);
            var sh = Gfx.Fill(Col.Black.A(.45f)); sh.Blur = size * .12f; c.DrawOval(cx + size * .08f, cy + size * .62f, size * .55f, size * .14f, sh);
            if (Render3D != null && Render3D(c, cx, cy, size, m, body, pip)) return;
            float f = 5.5f;
            Pt P(float[] v)
            {
                float x = m[0] * v[0] + m[1] * v[1] + m[2] * v[2], y = m[3] * v[0] + m[4] * v[1] + m[5] * v[2], z = m[6] * v[0] + m[7] * v[1] + m[8] * v[2];
                float k2 = f / (f - z); return new Pt(cx + x * k2 * size * .5f, cy - y * k2 * size * .5f);
            }
            var order = Enumerable.Range(0, 6).Select(i => (i, z: m[6] * nrm[i][0] + m[7] * nrm[i][1] + m[8] * nrm[i][2])).Where(t => t.z > .001f).OrderBy(t => t.z).ToList();
            // Geschlossener Koerper: exakte Seitenflaechen (keine Luecken an Kanten/Ecken), Kanten als runde Fase
            foreach (var (i, z) in order)
            {
                var n = nrm[i]; var u = uax[i]; var v = vax[i];
                float[] corner(float a, float b) => new float[] { n[0] + u[0] * a + v[0] * b, n[1] + u[1] * a + v[1] * b, n[2] + u[2] * a + v[2] * b };
                var q0 = P(corner(-1, 1)); var q1 = P(corner(1, 1)); var q2 = P(corner(1, -1)); var q3 = P(corner(-1, -1));
                float lx = m[0] * n[0] + m[1] * n[1] + m[2] * n[2], ly = m[3] * n[0] + m[4] * n[1] + m[5] * n[2];
                float shade = Math.Clamp(.55f + .25f * z + .2f * (-lx * .3f + ly * .8f), .35f, 1.05f);
                using var face = new Path2D(); face.MoveTo(q0); face.LineTo(q1); face.LineTo(q2); face.LineTo(q3); face.Close();
                var p = Gfx.Fill(Col.White); p.Shader = Grad.Linear(q0.X, q0.Y, q2.X, q2.Y, body.Dark(Math.Min(1, shade * 1.05f)), body.Dark(shade * .8f)); c.DrawPath(face, p);
                var ex = ((q1 - q0) + (q2 - q3)) * .5f; var ey = ((q3 - q0) + (q2 - q1)) * .5f;
                var o = (q0 + q1 + q2 + q3) * .25f - ex * .5f - ey * .5f;
                c.Save(); c.Concat(new Aff(ex.X, ex.Y, ey.X, ey.Y, o.X, o.Y));
                c.DrawRoundRect(new Box(.06f, .06f, .94f, .94f), .14f, .14f, Gfx.Line(Col.White.A(.22f * shade), .025f));
                foreach (var (px, py) in pips[faceVal[i] - 1])
                {
                    c.DrawCircle(px, py, .105f, Gfx.Fill(Col.Black.A(.35f))); c.DrawCircle(px, py, .095f, Gfx.Fill(pip.Dark(Math.Min(1, shade + .2f))));
                    c.DrawCircle(px - .025f, py - .03f, .03f, Gfx.Fill(Col.White.A(.35f)));
                }
                c.Restore();
            }
            float ew = Math.Max(1.5f, size * .035f);
            foreach (var (i, z) in order)
            {
                var n = nrm[i]; var u = uax[i]; var v = vax[i];
                float[] corner(float a, float b) => new float[] { n[0] + u[0] * a + v[0] * b, n[1] + u[1] * a + v[1] * b, n[2] + u[2] * a + v[2] * b };
                using var e = new Path2D(); e.MoveTo(P(corner(-1, 1))); e.LineTo(P(corner(1, 1))); e.LineTo(P(corner(1, -1))); e.LineTo(P(corner(-1, -1))); e.Close();
                c.DrawPath(e, Gfx.Line(body.Light(.35f).A(.3f), ew));
            }
        }
    }

    /// <summary>3D-Kriegsschiff von oben (URP-Modul): (c, Mitte x, Mitte y, Zellgroesse, Laenge in Zellen, senkrecht, beschaedigt) -> true wenn gezeichnet.</summary>
    public static class Ship3D { public static Func<Canvas2D, float, float, float, int, bool, bool, bool> Render3D; }

    /// <summary>Casino-Chipstapel (echtes 3D ueber das URP-Modul, sonst 2D-Ellipsen).</summary>
    public static class Chip3D
    {
        /// <summary>(c, x, y, Radius, Anzahl, Farbe gerade, Farbe ungerade) -> true wenn gezeichnet.</summary>
        public static Func<Canvas2D, float, float, float, int, Col, Col, bool> Render3D;
        /// <summary>Stapel mit n Chips; (x, y) = Mitte des untersten Chips, r = Chip-Radius in Designeinheiten.</summary>
        public static void Stack(Canvas2D c, float x, float y, float r, int n, Col a, Col b)
        {
            if (n <= 0) return;
            var sh = Gfx.Fill(Col.Black.A(.45f)); sh.Blur = r * .25f; c.DrawOval(x + r * .12f, y + r * .32f, r * 1.12f, r * .45f, sh);
            if (Render3D != null && Render3D(c, x, y, r, n, a, b)) return;
            for (int k = 0; k < n; k++)
            {
                var col = k % 2 == 0 ? a : b; float cy = y - k * r * .23f;
                c.DrawOval(x, cy + 3, r, r * .41f, Gfx.Fill(col.Dark(.4f))); c.DrawOval(x, cy, r, r * .41f, Gfx.Fill(col));
                c.DrawOval(x, cy, r * .55f, r * .23f, Gfx.Line(Col.White.A(.6f), 1.2f));
            }
        }
    }

    /// <summary>3D-Muenze fuer den Muenzwurf.</summary>
    public static class Coin3D
    {
        /// <summary>Echte 3D-Muenze (URP-Modul): (c, cx, cy, r, angle) -> true wenn gezeichnet.</summary>
        public static Func<Canvas2D, float, float, float, float, bool> Render3D;
        public static void Draw(Canvas2D c, float cx, float cy, float r, float angle, float lift = 0)
        {
            float cs = MathF.Cos(angle), sn = MathF.Abs(MathF.Sin(angle)); bool head = cs >= 0; float sy = Math.Max(.03f, MathF.Abs(cs)), th = r * .16f;
            var sh = Gfx.Fill(Col.Black.A(.4f - lift * .0015f)); sh.Blur = 14; c.DrawOval(cx, cy + r * 1.05f + lift * .3f, r * (.85f - lift * .001f), r * .2f, sh);
            cy -= lift;
            if (Render3D != null && Render3D(c, cx, cy, r, angle)) return;
            for (int i = 12; i >= 0; i--)
            {
                float off = (i / 12f - .5f) * th * 2 * sn * (head ? 1 : -1); var col = C.Gold.Dark(.55f + .25f * (1 - i / 12f));
                c.DrawOval(cx, cy + off, r, r * sy, Gfx.Fill(col));
            }
            c.Save(); c.Translate(cx, cy); c.Scale(1, sy);
            c.DrawCircle(0, 0, r, Gfx.Fill(C.Gold.Dark(.6f)));
            Gfx.Image(c, Assets.Img(head ? "coin_H" : "coin_T"), Gfx.Ctr(0, 0, r * 2, r * 2), 1, r * .96f);
            c.DrawCircle(0, 0, r * .97f, Gfx.Line(C.Gold.Light(.5f).A(.8f), 3));
            c.Restore();
            float gl = .5f + .5f * MathF.Sin(angle * 2);
            var spec = Gfx.Fill(Col.White.A(.22f * gl * sy)); spec.Additive = true; spec.Glow = 1.6f; spec.Blur = 6;
            c.DrawOval(cx - r * .3f, cy - r * .35f * sy, r * .5f, r * .18f * sy, spec);
        }
    }
}
