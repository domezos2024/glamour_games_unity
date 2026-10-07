using System;

namespace GlamourGames
{
    /// <summary>Kamera-/Linseneffekte, die das URP-Modul bereitstellt (Bloom-/Linsenreflex-Impuls bei Siegen).</summary>
    public static class Lens { public static Action<float> Kick; }

    /// <summary>Edelmetall-Optik: Chrom-/Goldschrift mit Horizont, Band-Banner, Lichtstrahlen, Lens-Flare und Glitzer.</summary>
    public static class Metal
    {
        public static readonly Col Gold = new Col(255, 196, 64), GoldLight = new Col(255, 244, 196), GoldDeep = new Col(128, 74, 8);
        public static float H(int i) { float s = MathF.Sin(i * 12.9898f + 78.233f) * 43758.547f; return s - MathF.Floor(s); }

        /// <summary>Metallschrift um (0,0): Schlagschatten, Leuchten, dunkle Kontur, heller Kantenschliff, Verlauf mit Horizont und wandernder Glanzstreif (sheen 0..1, sonst aus).</summary>
        public static void Text(Canvas2D c, string s, float size, Col tint, float alpha = 1, float sheen = -1, bool serif = true, float glow = .55f)
        {
            if (alpha <= 0) return;
            float w = Gfx.TW(s, size, true, serif) * .5f + size, top = -size * .4f, mid = size * .03f, bot = size * .4f;
            c.SaveLayer(alpha);
            var sh = Gfx.Fill(Col.Black.A(.6f)); sh.Blur = size * .07f; Gfx.TextPaint(c, s, size * .025f, size * .08f, size, sh, Al.C, true, serif);
            if (glow > 0) { var g = Gfx.Fill(tint.A(glow)); g.Blur = size * .28f; g.Glow = 1.8f; Gfx.TextPaint(c, s, 0, 0, size, g, Al.C, true, serif); }
            Gfx.TextPaint(c, s, 0, 0, size, Gfx.Line(tint.Dark(.2f), size * .1f), Al.C, true, serif);
            var edge = Gfx.Line(tint.Light(.7f), size * .035f); edge.Glow = 1.25f; Gfx.TextPaint(c, s, 0, 0, size, edge, Al.C, true, serif);
            for (int k = 0; k < 2; k++)
            {
                c.Save(); c.ClipRect(k == 0 ? new Box(-w, -size * 2, w, mid) : new Box(-w, mid, w, size * 2));
                var p = Gfx.Fill(Col.White); p.Glow = 1.12f;
                p.Shader = k == 0 ? Grad.Linear(0, top, 0, mid, tint.Light(.9f), tint.Light(.22f)) : Grad.Linear(0, mid, 0, bot, tint.Dark(.4f), tint.Light(.18f));
                Gfx.TextPaint(c, s, 0, 0, size, p, Al.C, true, serif); c.Restore();
            }
            if (sheen >= 0 && sheen <= 1)
            {
                float sx = -w + sheen * w * 2, bw = size * .45f;
                for (int k = 0; k < 2; k++)
                {
                    c.Save(); c.ClipRect(k == 0 ? new Box(sx - bw, -size * 2, sx, size * 2) : new Box(sx, -size * 2, sx + bw, size * 2));
                    var p = Gfx.Fill(Col.White); p.Additive = true; p.Glow = 2.3f;
                    p.Shader = k == 0 ? Grad.Linear(sx - bw, 0, sx, 0, Col.White.A(0), Col.White.A(.9f)) : Grad.Linear(sx, 0, sx + bw, 0, Col.White.A(.9f), Col.White.A(0));
                    Gfx.TextPaint(c, s, 0, 0, size, p, Al.C, true, serif); c.Restore();
                }
            }
            c.Restore();
        }

        /// <summary>Satinband mit gefalteten, eingekerbten Enden und Goldkanten um (0,0).</summary>
        public static void Ribbon(Canvas2D c, float w, float h, Col col, float alpha = 1)
        {
            if (alpha <= 0) return;
            c.SaveLayer(alpha);
            var body = col.Mix(new Col(40, 4, 46), .5f); float hw = w / 2, hh = h / 2, tl = h * 1.05f, drop = h * .32f;
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float x0 = sd * (hw - h * .35f), x1 = sd * (hw + tl);
                using (var t = new Path2D())
                {
                    t.MoveTo(x0, -hh + drop); t.LineTo(x1, -hh + drop); t.LineTo(x1 - sd * h * .38f, drop); t.LineTo(x1, hh + drop); t.LineTo(x0, hh + drop); t.Close();
                    var p = Gfx.Fill(Col.White); p.Shader = Grad.Linear(0, -hh + drop, 0, hh + drop, body.Dark(.62f), body.Dark(.32f)); c.DrawPath(t, p);
                }
                using (var f = new Path2D())
                {
                    f.MoveTo(sd * (hw - h * .35f), hh); f.LineTo(sd * hw, hh); f.LineTo(sd * (hw - h * .35f), hh + drop); f.Close(); c.DrawPath(f, Gfx.Fill(body.Dark(.18f)));
                }
                c.DrawLine(x0, -hh + drop + 5, x1 - 4, -hh + drop + 5, Gfx.Line(Gold.A(.55f), 2));
                c.DrawLine(x0, hh + drop - 5, x1 - 4, hh + drop - 5, Gfx.Line(Gold.A(.55f), 2));
            }
            var r = new Box(-hw, -hh, hw, hh);
            Gfx.Shadow(c, r, 6, 18, .55f, 0, 12);
            Gfx.RectGrad(c, r, 6, body.Light(.12f), body.Dark(.55f));
            Gfx.RectGrad(c, new Box(-hw, -hh, hw, -hh + h * .42f), 6, Col.White.A(.2f), Col.White.A(0));
            for (int k = 0; k < 2; k++)
            {
                float y = k == 0 ? -hh + 7 : hh - 7; var gl = Gfx.Line(GoldLight, 3); gl.Glow = 1.5f;
                c.DrawLine(-hw + 4, y, hw - 4, y, Gfx.Line(GoldDeep, 6)); c.DrawLine(-hw + 4, y, hw - 4, y, gl);
            }
            c.Restore();
        }

        /// <summary>Weiche, sich drehende Lichtstrahlen (zwei gegenlaeufige Lagen).</summary>
        public static void Rays(Canvas2D c, float cx, float cy, float t, Col a, Col b, float alpha, float len = 1800, int n = 22)
        {
            if (alpha <= 0) return;
            c.Save(); c.Translate(cx, cy);
            for (int layer = 0; layer < 2; layer++)
            {
                c.Save(); c.RotateRadians(t * (layer == 0 ? .11f : -.07f) + layer * .4f);
                for (int i = 0; i < n; i++)
                {
                    float ang = (i + H(i + layer * 57) * .7f) * MathF.PI * 2 / n, wid = .015f + H(i * 3 + layer * 11) * .055f, k = .55f + .45f * MathF.Sin(t * (1.1f + H(i) * .8f) + i * 1.7f);
                    var col = (i + layer) % 2 == 0 ? a : b;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        float ww = pass == 0 ? wid * 2.2f : wid * .7f, aa = alpha * k * (pass == 0 ? .045f : .1f) * (layer == 0 ? 1 : .65f);
                        using var p = new Path2D(); p.MoveTo(0, 0); p.LineTo(MathF.Cos(ang - ww) * len, MathF.Sin(ang - ww) * len); p.LineTo(MathF.Cos(ang + ww) * len, MathF.Sin(ang + ww) * len); p.Close();
                        var pt = Gfx.Fill(Col.White); pt.Shader = Grad.Radial(0, 0, len * .85f, col.A(aa), col.A(0)); pt.Additive = true; pt.Glow = 1.35f; c.DrawPath(p, pt);
                    }
                }
                c.Restore();
            }
            c.Restore();
        }

        /// <summary>Anamorphotischer Linsenreflex: horizontaler Streifen, Kern und Geisterbilder gespiegelt um die Bildmitte.</summary>
        public static void Flare(Canvas2D c, float x, float y, Col col, float a, float width = 1500)
        {
            if (a <= 0) return;
            var s1 = Gfx.Fill(col.Light(.55f).A(a * .45f)); s1.Additive = true; s1.Glow = 2.4f; s1.Blur = 3; c.DrawOval(x, y, width / 2, 3f, s1);
            var s2 = Gfx.Fill(col.A(a * .22f)); s2.Additive = true; s2.Glow = 1.8f; s2.Blur = 22; c.DrawOval(x, y, width * .32f, 24, s2);
            Gfx.Light(c, x, y, 46, Col.White, a * .75f, 2.8f); Gfx.Light(c, x, y, 200, col.Light(.3f), a * .2f, 1.5f);
            float[] f = { .35f, .8f, 1.25f, 1.6f }; Col[] gc = { new Col(120, 200, 255), new Col(255, 140, 220), new Col(160, 255, 190), col };
            for (int k = 0; k < f.Length; k++)
            {
                float gx = 800 + (800 - x) * f[k], gy = 450 + (450 - y) * f[k], r = 26 + k * 22;
                Gfx.Light(c, gx, gy, r, gc[k], a * .16f, 1.4f);
                var ring = Gfx.Line(gc[k].A(a * .14f), 2.5f); ring.Additive = true; ring.Blur = 2; c.DrawCircle(gx, gy, r * .8f, ring);
            }
        }

        /// <summary>Vierstrahliger Glitzerstern (Sternfilter-Look).</summary>
        public static void Glint(Canvas2D c, float x, float y, float r, Col col, float a, float rot = .35f)
        {
            if (a <= 0 || r <= 0) return;
            Gfx.Light(c, x, y, r * .8f, col, a * .5f, 2f);
            c.Save(); c.Translate(x, y); c.RotateRadians(rot);
            for (int k = 0; k < 2; k++)
            {
                float L = k == 0 ? r * 1.9f : r * 1.15f;
                var p = Gfx.Fill(col.Light(.65f).A(a)); p.Additive = true; p.Glow = 3f; p.Blur = r * .05f; c.DrawOval(0, 0, L, r * .055f, p);
                var q = Gfx.Fill(col.A(a * .45f)); q.Additive = true; q.Glow = 2f; q.Blur = r * .12f; c.DrawOval(0, 0, L * .55f, r * .16f, q);
                c.RotateDegrees(90);
            }
            var core = Gfx.Fill(Col.White.A(a)); core.Additive = true; core.Glow = 3.2f; c.DrawCircle(0, 0, r * .1f, core);
            c.Restore();
        }

        /// <summary>Waagerechter Metallverlauf mit Glanzlinie bei hl (0..1) fuer Rechtecke.</summary>
        public static void Bar(Canvas2D c, Box r, float rad, Col baseCol, float hl = .35f)
        {
            float xm = r.Left + r.Width * hl;
            for (int k = 0; k < 2; k++)
            {
                c.Save(); c.ClipRect(k == 0 ? new Box(r.Left - 2, r.Top - 2, xm, r.Bottom + 2) : new Box(xm, r.Top - 2, r.Right + 2, r.Bottom + 2));
                var p = Gfx.Fill(Col.White); p.Shader = k == 0 ? Grad.Linear(r.Left, 0, xm, 0, baseCol.Dark(.45f), baseCol.Light(.75f)) : Grad.Linear(xm, 0, r.Right, 0, baseCol.Light(.75f), baseCol.Dark(.3f));
                c.DrawRoundRect(r, rad, rad, p); c.Restore();
            }
        }
    }

    /// <summary>Siegerpokal: echtes 3D-Modell (URP-Modul, PBR-Gold) oder 2D-Ersatz mit Metallverlaeufen.</summary>
    public static class Trophy3D
    {
        /// <summary>(c, Zielrechteck, Drehung um die Hochachse in rad, Deckkraft) -> true wenn in 3D gezeichnet.</summary>
        public static Func<Canvas2D, Box, float, float, bool> Render3D;
        /// <summary>Seitenverhaeltnis Breite/Hoehe des Pokal-Rechtecks.</summary>
        public const float Aspect = .8f;
        public static Box Rect(float cx, float cy, float h) => Gfx.Ctr(cx, cy, h * Aspect, h);

        public static void Draw(Canvas2D c, Box b, float spin, float alpha = 1)
        {
            if (alpha <= 0) return;
            var sh = Gfx.Fill(Col.Black.A(.55f * alpha)); sh.Blur = b.Height * .035f; c.DrawOval(b.MidX, b.Bottom - b.Height * .035f, b.Width * .42f, b.Height * .04f, sh);
            if (Render3D != null && Render3D(c, b, spin, alpha)) return;
            Draw2D(c, b, spin, alpha);
        }

        static void Draw2D(Canvas2D c, Box b, float spin, float alpha)
        {
            float H = b.Height, W = b.Width, cx = b.MidX, y0 = b.Top, hl = .5f + .32f * MathF.Sin(spin);
            Col g = Metal.Gold; c.SaveLayer(alpha);
            Box P(float l, float t, float r, float btm) => new Box(cx + l * W, y0 + t * H, cx + r * W, y0 + btm * H);
            var low = P(-.33f, .86f, .33f, .975f); Gfx.RectGrad(c, low, 6, new Col(40, 36, 46), new Col(6, 5, 9));
            c.DrawLine(low.Left + 6, low.Top + 2, low.Right - 6, low.Top + 2, Gfx.Line(Col.White.A(.35f), 2));
            Metal.Bar(c, P(-.3f, .835f, .3f, .862f), 3, g, hl);
            var up = P(-.25f, .755f, .25f, .838f); Gfx.RectGrad(c, up, 5, new Col(46, 40, 52), new Col(8, 6, 12));
            c.DrawLine(up.Left + 5, up.Top + 2, up.Right - 5, up.Top + 2, Gfx.Line(Col.White.A(.3f), 2));
            Metal.Bar(c, P(-.16f, .79f, .16f, .815f), 3, g.Light(.2f), hl);
            using (var foot = new Path2D())
            {
                foot.MoveTo(cx - .2f * W, y0 + .755f * H); foot.CubicTo(cx - .17f * W, y0 + .7f * H, cx - .06f * W, y0 + .69f * H, cx - .045f * W, y0 + .64f * H);
                foot.LineTo(cx + .045f * W, y0 + .64f * H); foot.CubicTo(cx + .06f * W, y0 + .69f * H, cx + .17f * W, y0 + .7f * H, cx + .2f * W, y0 + .755f * H); foot.Close();
                MetalPath(c, foot, new Box(cx - .2f * W, y0 + .64f * H, cx + .2f * W, y0 + .755f * H), g, hl);
            }
            Metal.Bar(c, P(-.045f, .5f, .045f, .65f), 4, g, hl);
            c.DrawBall(cx, y0 + .565f * H, W * .075f, Gfx.Fill(g), 1, .25f);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                var hp = Gfx.Line(g.Dark(.55f), W * .05f); var hq = Gfx.Line(g.Light(.45f), W * .018f); hq.Glow = 1.3f;
                using var h = new Path2D(); h.MoveTo(cx + sd * .27f * W, y0 + .14f * H); h.CubicTo(cx + sd * .52f * W, y0 + .12f * H, cx + sd * .5f * W, y0 + .38f * H, cx + sd * .16f * W, y0 + .43f * H);
                c.DrawPath(h, hp); c.DrawPath(h, hq);
            }
            var bowlBox = new Box(cx - .33f * W, y0 + .08f * H, cx + .33f * W, y0 + .52f * H);
            using (var bowl = new Path2D())
            {
                bowl.MoveTo(cx - .33f * W, y0 + .1f * H); bowl.CubicTo(cx - .33f * W, y0 + .36f * H, cx - .2f * W, y0 + .47f * H, cx - .05f * W, y0 + .5f * H);
                bowl.LineTo(cx + .05f * W, y0 + .5f * H); bowl.CubicTo(cx + .2f * W, y0 + .47f * H, cx + .33f * W, y0 + .36f * H, cx + .33f * W, y0 + .1f * H); bowl.Close();
                MetalPath(c, bowl, bowlBox, g, hl);
            }
            c.DrawOval(cx, y0 + .1f * H, .33f * W, .045f * H, Gfx.Fill(g.Dark(.32f)));
            var ip = Gfx.Fill(Col.White); ip.Shader = Grad.Linear(0, y0 + .06f * H, 0, y0 + .14f * H, g.Dark(.2f), g.Dark(.6f)); c.DrawOval(cx, y0 + .105f * H, .3f * W, .035f * H, ip);
            var rim = Gfx.Line(g.Light(.6f), H * .012f); rim.Glow = 1.4f; c.DrawOval(cx, y0 + .1f * H, .33f * W, .045f * H, rim);
            Gfx.Star(c, cx, y0 + .29f * H, W * .085f, g.Dark(.5f), 0); Gfx.Star(c, cx, y0 + .284f * H, W * .08f, g.Light(.35f), 0);
            var spec = Gfx.Fill(Col.White.A(.55f)); spec.Additive = true; spec.Glow = 2.2f; spec.Blur = W * .015f;
            c.DrawOval(bowlBox.Left + bowlBox.Width * hl, y0 + .26f * H, W * .022f, H * .13f, spec);
            c.Restore();
        }

        static void MetalPath(Canvas2D c, Path2D p, Box r, Col g, float hl)
        {
            float xm = r.Left + r.Width * hl;
            for (int k = 0; k < 2; k++)
            {
                c.Save(); c.ClipRect(k == 0 ? new Box(r.Left - 4, r.Top - 4, xm, r.Bottom + 4) : new Box(xm, r.Top - 4, r.Right + 4, r.Bottom + 4));
                var f = Gfx.Fill(Col.White); f.Shader = k == 0 ? Grad.Linear(r.Left, 0, xm, 0, g.Dark(.35f), g.Light(.7f)) : Grad.Linear(xm, 0, r.Right, 0, g.Light(.7f), g.Dark(.25f));
                c.DrawPath(p, f); c.Restore();
            }
        }
    }

    /// <summary>Kinoreife Siegesinszenierung: Abdunklung, Lichtstrahlen, aufsteigender 3D-Pokal, Linsenreflex, Glitzer, Metall-Banner.</summary>
    public sealed class RewardShow
    {
        public readonly Col Col; public readonly float Dur, Power; public readonly string Banner; public readonly bool Trophy;
        public float T { get; private set; }
        public bool Done => T > Dur + 1.6f;
        readonly (float x, float y, float t0, float r, float rot)[] glints = new (float, float, float, float, float)[14];
        int gi; float gAcc;

        public RewardShow(Col col, float dur, float power, string banner, bool trophyAllowed)
        {
            Col = col; Dur = Math.Max(1.5f, dur); Power = power; Banner = banner;
            Trophy = trophyAllowed && (banner != null && dur >= 3 || power >= 1.4f); // Sieger-Parade (1.3) bleibt ohne Pokal
            for (int i = 0; i < glints.Length; i++) glints[i].t0 = -10;
        }

        float In => Ease.Clamp(T / .45f);
        float Out => 1 - Ease.Clamp((T - Dur) / 1.4f);
        float Env => In * Out;
        // Pokal (ca. 30 cm -> 1570 px/m) faellt auf den Sockel, springt mit kleiner Restitution nach und kippelt danach um die
        // Bodenkanten aus (Housner-Kippmodell); Kippstoss beim ersten Aufprall unterhalb der Umkippgrenze
        static readonly float Gt = Phys.Gpx(1570); const float DropH = 640, TRest = .25f;
        readonly Rocker rock = new Rocker(470 * Trophy3D.Aspect * .3f, 470 * .45f, Phys.Gpx(1570)); int hops;
        float Rise => Trophy ? -Phys.Drop(T, DropH, Gt, TRest, out _) : 0;
        float Spin => (1 - MathF.Exp(-T * 1.5f)) * MathF.PI * 4 + T * .55f;
        public Box TrophyBox { get { float h = 470 * (.72f + .28f * Ease.OutCubic(Ease.Clamp(T / 1.1f))) * (1 + .06f * (1 - Out)); return Trophy3D.Rect(800, 525 + Rise, h); } }
        float BannerY => Trophy ? 158 : 190;

        public void Update(float dt)
        {
            T += dt;
            if (Trophy)
            {
                Phys.Drop(T, DropH, Gt, TRest, out int hp);
                if (hp > hops)
                {
                    float k = MathF.Pow(TRest, hops); Impact.Play(Mat.Metal, k, 2.2f); App.Shake(10 * k);
                    if (hops == 0) rock.Kick((Rng.F() < .5f ? -1 : 1) * rock.Critical * .55f); hops = hp;
                }
                rock.Update(dt);
            }
            if (!Trophy || T < .9f || T > Dur) return;
            gAcc += dt * 7;
            while (gAcc >= 1)
            {
                gAcc -= 1; var b = TrophyBox; var r = Rng.Shared;
                float x = b.Left + b.Width * (.12f + .76f * (float)r.NextDouble()), y = b.Top + b.Height * (.05f + .72f * (float)r.NextDouble());
                glints[gi] = (x, y, T, 14 + 26 * (float)r.NextDouble(), (float)r.NextDouble() * .6f); gi = (gi + 1) % glints.Length;
            }
        }

        /// <summary>Hinter dem Spielfeld: weiche Strahlen (auch ohne Pokal).</summary>
        public void DrawBack(Canvas2D c)
        {
            float a = Env; if (a <= 0) return;
            if (!Trophy) Metal.Rays(c, 800, 450, T, Col, Metal.Gold, a * .8f * Math.Min(1.2f, .6f + Power * .4f));
        }

        /// <summary>Ueber dem Spielfeld, unter den Partikeln: Abdunklung, Lichtkegel und Pokal.</summary>
        public void DrawMid(Canvas2D c, bool hideTrophy)
        {
            if (!Trophy) return;
            float a = Env * (hideTrophy ? 0 : 1); if (a <= 0) return;
            c.DrawRect(App.VX0 - 10, App.VY0 - 10, App.VX1 - App.VX0 + 20, App.VY1 - App.VY0 + 20, Gfx.Fill(new Col(4, 0, 10).A(.62f * a)));
            var v = Gfx.Fill(Col.White); v.Shader = Grad.Radial(800, 470, 950, Col.Black.A(0), Col.Black.A(.6f * a)); c.DrawRect(App.VX0 - 10, App.VY0 - 10, App.VX1 - App.VX0 + 20, App.VY1 - App.VY0 + 20, v);
            var b = TrophyBox;
            Metal.Rays(c, b.MidX, b.Top + b.Height * .3f, T * 1.3f, Metal.Gold, Col.Light(.4f), a * .75f, 1150, 18);
            Gfx.Light(c, b.MidX, b.Top + b.Height * .4f, b.Height * .7f, Col.Mix(Metal.Gold, .6f), .22f * a, 1.3f);
            using (var cone = new Path2D())
            {
                cone.MoveTo(b.MidX - 40, App.VY0 - 10); cone.LineTo(b.MidX + 40, App.VY0 - 10); cone.LineTo(b.MidX + b.Width * .75f, b.Bottom); cone.LineTo(b.MidX - b.Width * .75f, b.Bottom); cone.Close();
                var cp = Gfx.Fill(Col.White); cp.Shader = Grad.Linear(0, App.VY0, 0, b.Bottom, Metal.GoldLight.A(0), Metal.GoldLight.A(.07f * a)); cp.Additive = true; cp.Glow = 1.2f; c.DrawPath(cone, cp);
            }
            c.Save(); rock.Apply(c, b.MidX, b.Bottom, b.Width * .3f); Trophy3D.Draw(c, b, Spin, Ease.Clamp(T / .25f) * Out); c.Restore();
        }

        /// <summary>Ueber allem: Linsenreflex, Glitzer und Metall-Banner.</summary>
        public void DrawTop(Canvas2D c, bool hideTrophy)
        {
            float a = Env;
            if (Trophy && !hideTrophy && a > 0)
            {
                var b = TrophyBox; float land = Ease.Clamp((T - .95f) / .25f) * (1 - Ease.Clamp((T - 1.6f) / 1.2f));
                Metal.Flare(c, b.MidX + b.Width * .3f, b.Top + b.Height * .1f, Metal.Gold, (land * .8f + .2f) * a, 1100);
                foreach (var g in glints)
                {
                    float u = (T - g.t0) / .7f; if (u < 0 || u > 1) continue;
                    Metal.Glint(c, g.x, g.y, g.r * MathF.Sin(u * MathF.PI), Metal.GoldLight, MathF.Sin(u * MathF.PI) * a, g.rot);
                }
            }
            if (Banner == null) return;
            float ba = 1 - Ease.Clamp((T - Dur + .8f) / .8f); if (ba <= 0 || T < .15f) return;
            float u2 = Ease.OutElastic(Ease.Clamp((T - .15f) / 1f)) * (1 + .02f * MathF.Sin(T * 6));
            float fs = Math.Min(84, 1150f / Math.Max(6, Banner.Length) * 1.6f), w = Gfx.TW(Banner, fs, true, true) + fs * 1.6f;
            c.Save(); c.Translate(800, BannerY); c.Scale(u2);
            Metal.Ribbon(c, w, fs * 1.45f, Col, ba);
            float sheen = ((T - 1.1f) % 2.6f) / 1.1f;
            Metal.Text(c, Banner, fs, Metal.Gold, ba, T > 1.1f ? sheen : -1, true, .35f);
            c.Restore();
        }
    }
}
