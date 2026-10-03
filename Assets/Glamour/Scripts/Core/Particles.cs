using System;
using System.Collections.Generic;

namespace GlamourGames
{
    /// <summary>Partikelsystem: Funken, Konfetti, Sterne, Rauch, Feuer, Wasser, Feuerwerk, Blitze, Schockwellen.</summary>
    public class Particles
    {
        struct P { public float x, y, vx, vy, life, max, size, rot, vr, grav, drag, aux; public Col col; public int kind; }
        struct Bolt { public Pt[] pts; public Pt[][] br; public float life, max; public Col col; }
        struct Shock { public float x, y, life, max, r; public Col col; }
        readonly List<P> l = new List<P>(); readonly List<Bolt> bolts = new List<Bolt>(); readonly List<Shock> shocks = new List<Shock>();
        static readonly Random R = new Random();
        static float Rf(float a, float b) => a + (float)R.NextDouble() * (b - a);
        public const int MaxParticles = 2600;
        static readonly Col[] Party = { C.Cyan, C.Pink, C.Gold, C.Green, C.Purple, C.Yellow, C.Magenta, C.Orange };
        static readonly Col[] Fire = { C.Orange, C.Yellow, C.Red };
        static readonly Col[] Water = { new Col(150, 210, 255), Col.White, new Col(90, 170, 255) };
        const int K_SPARK = 0, K_CONF = 1, K_STAR = 2, K_SMOKE = 3, K_FIRE = 4, K_DROP = 5, K_BUB = 6, K_DEB = 7, K_PETAL = 8, K_STREAK = 9, K_RIPPLE = 10, K_FLASH = 11;

        public void Burst(float x, float y, int n = 50, Col[] cols = null, float speed = 420, int kind = -1, float grav = 700)
        {
            cols ??= Party;
            for (int i = 0; i < n; i++)
            {
                float a = Rf(0, MathF.PI * 2), s = Rf(.15f, 1f) * speed;
                l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s - speed * .25f, max = Rf(.7f, 1.6f), size = Rf(4, 10), rot = Rf(0, 6), vr = Rf(-9, 9), grav = grav, drag = 1.4f, col = cols[R.Next(cols.Length)], kind = kind >= 0 ? kind : R.Next(3) });
            }
        }
        public void Confetti(float w, int n = 120)
        {
            for (int i = 0; i < n; i++)
                l.Add(new P { x = Rf(0, w), y = Rf(-200, -10), vx = Rf(-60, 60), vy = Rf(120, 320), max = Rf(2.5f, 4.5f), size = Rf(6, 12), rot = Rf(0, 6), vr = Rf(-6, 6), grav = 120, drag = .3f, col = Party[R.Next(Party.Length)], kind = K_CONF });
        }
        public void Spark(float x, float y, Col col, int n = 3, float speed = 90)
        {
            for (int i = 0; i < n; i++) { float a = Rf(0, MathF.PI * 2), s = Rf(.2f, 1) * speed; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = Rf(.4f, .9f), size = Rf(3, 7), drag = 2.5f, col = col, kind = K_SPARK }); }
        }
        public void Ring(float x, float y, Col col, int n = 36, float speed = 320)
        {
            for (int i = 0; i < n; i++) { float a = i * MathF.PI * 2 / n; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * speed, vy = MathF.Sin(a) * speed, max = .8f, size = 5, drag = 3.2f, col = col, kind = K_SPARK }); }
        }
        public void Shockwave(float x, float y, Col col, float r = 200, float dur = .6f) => shocks.Add(new Shock { x = x, y = y, max = dur, r = r, col = col });
        public void Smoke(float x, float y, int n = 1, float size = 14, float rise = 60, float life = 1.8f)
        {
            for (int i = 0; i < n; i++) { byte g = (byte)R.Next(50, 110); l.Add(new P { x = x + Rf(-6, 6), y = y, vx = Rf(-14, 14), vy = -Rf(.6f, 1.2f) * rise, max = Rf(.7f, 1f) * life, size = size * Rf(.7f, 1.2f), grav = -8, drag = .5f, col = new Col(g, g, (byte)(g + 8)), kind = K_SMOKE }); }
        }
        public void Flames(float x, float y, int n = 2, float size = 12)
        {
            for (int i = 0; i < n; i++) l.Add(new P { x = x + Rf(-size, size), y = y + Rf(-2, 6), vx = Rf(-12, 12), vy = -Rf(50, 120), max = Rf(.4f, .8f), size = size * Rf(.6f, 1.1f), grav = -40, drag = 1, aux = Rf(0, 6), col = C.Orange, kind = K_FIRE });
        }
        public void Explosion(float x, float y, float power = 1)
        {
            for (int i = 0; i < 14 * power; i++) { float a = Rf(0, MathF.PI * 2), s = Rf(20, 200) * power; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = Rf(.4f, .9f), size = Rf(14, 30) * power, grav = -30, drag = 2.2f, col = C.Orange, kind = K_FIRE, aux = Rf(0, 6) }); }
            for (int i = 0; i < 26 * power; i++) { float a = Rf(0, MathF.PI * 2), s = Rf(.3f, 1) * 520 * power; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = Rf(.4f, .9f), size = Rf(3, 6), grav = 300, drag = 2.2f, col = Fire[R.Next(3)], kind = K_STREAK }); }
            for (int i = 0; i < 12 * power; i++) { float a = Rf(0, MathF.PI * 2), s = Rf(.3f, 1) * 420 * power; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s - 150, max = Rf(.8f, 1.5f), size = Rf(4, 9), rot = Rf(0, 6), vr = Rf(-12, 12), grav = 900, drag = .6f, col = new Col(R.Next(40, 90), R.Next(30, 60), 30), kind = K_DEB }); }
            Smoke(x, y, (int)(6 * power), 22 * power, 50, 2.2f); Shockwave(x, y, C.Yellow, 130 * power, .45f); Shockwave(x, y, C.Orange, 90 * power, .35f);
            l.Add(new P { x = x, y = y, max = .3f, size = 70 * power, col = C.Yellow.Light(.4f), kind = K_FLASH });
        }
        public void Splash(float x, float y, float power = 1)
        {
            for (int i = 0; i < 22 * power; i++) { float a = -MathF.PI / 2 + Rf(-.55f, .55f), s = Rf(180, 460) * power; l.Add(new P { x = x + Rf(-6, 6), y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = Rf(.6f, 1.1f), size = Rf(3, 6.5f), grav = 900, drag = .15f, col = Water[R.Next(3)], kind = K_DROP }); }
            for (int i = 0; i < 9; i++) l.Add(new P { x = x + Rf(-14, 14), y = y + Rf(-4, 8), vx = Rf(-25, 25), vy = -Rf(30, 90), max = Rf(.9f, 1.7f), size = Rf(4, 9), grav = -50, drag = .5f, aux = Rf(0, 6), col = new Col(200, 235, 255), kind = K_BUB });
            Ripple(x, y, 70 * power); Ripple(x, y, 110 * power, .18f);
        }
        public void Ripple(float x, float y, float r, float delay = 0) => l.Add(new P { x = x, y = y, max = 1.1f, size = r, life = -delay, col = new Col(200, 235, 255), kind = K_RIPPLE, aux = .38f });
        public void Bubbles(float x, float y, int n = 6, float spread = 20)
        {
            for (int i = 0; i < n; i++) l.Add(new P { x = x + Rf(-spread, spread), y = y + Rf(-spread, spread), vy = -Rf(30, 80), max = Rf(1.2f, 2.4f), size = Rf(3, 9), grav = -20, drag = .4f, aux = Rf(0, 6), col = new Col(200, 235, 255), kind = K_BUB });
        }
        public void Petals(float x, float y, int n, Col[] cols, float speed = 200)
        {
            for (int i = 0; i < n; i++) { float a = Rf(0, MathF.PI * 2), s = Rf(.2f, 1) * speed; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s - speed * .4f, max = Rf(1.8f, 3.2f), size = Rf(7, 13), rot = Rf(0, 6), vr = Rf(-5, 5), grav = 90, drag = 1.1f, col = cols[R.Next(cols.Length)], kind = K_PETAL, aux = Rf(0, 6) }); }
        }
        public void Throw(float x, float y, float tx, float ty, int n = 14, float flight = .7f)
        {
            for (int i = 0; i < n; i++)
            {
                float T = flight * Rf(.85f, 1.15f), g = 700;
                l.Add(new P { x = x, y = y, vx = (tx - x) / T + Rf(-30, 30), vy = (ty - y) / T - .5f * g * T + Rf(-40, 40), max = T + Rf(.5f, 1.1f), size = Rf(6, 11), rot = Rf(0, 6), vr = Rf(-10, 10), grav = g, col = Party[R.Next(Party.Length)], kind = i % 4 == 0 ? K_STAR : K_CONF });
            }
        }
        public void Cannon(float x, float y, float ang, int n = 60, float speed = 900)
        {
            for (int i = 0; i < n; i++) { float a = ang + Rf(-.28f, .28f), s = Rf(.4f, 1) * speed; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = Rf(2f, 3.6f), size = Rf(6, 12), rot = Rf(0, 6), vr = Rf(-8, 8), grav = 520, drag = 1.3f, col = Party[R.Next(Party.Length)], kind = i % 5 == 0 ? K_STAR : K_CONF }); }
        }
        public void Rocket(float x, float ty, Col col)
        {
            float T = Rf(.8f, 1.1f); l.Add(new P { x = x, y = 940, vx = Rf(-40, 40), vy = -(940 - ty) / T, max = T, size = 4, col = col, kind = K_STREAK, aux = 1 });
            Sfx.Play(S.Launch, .5f, Rf(.9f, 1.15f));
        }
        void Firework(float x, float y, Col col)
        {
            int type = R.Next(4), n = type == 1 ? 48 : type == 3 ? 110 : 85; var alt = Party[R.Next(Party.Length)]; Sfx.Play(S.FwPop, .55f, Rf(.85f, 1.2f));
            for (int i = 0; i < n; i++)
            {
                float a = type == 1 ? i * MathF.PI * 2 / n : Rf(0, MathF.PI * 2), s = type == 1 ? 380 : type == 3 ? 420 * MathF.Pow(Rf(0, 1), .4f) : Rf(.25f, 1) * 460;
                l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = type == 2 ? Rf(1.4f, 2.2f) : Rf(.9f, 1.5f), size = Rf(3, 5.5f), grav = type == 2 ? 240 : 110, drag = type == 2 ? 1.6f : 1.9f, col = i % 3 == 0 ? alt : col, kind = K_STREAK });
            }
            if (type == 3) for (int i = 0; i < 30; i++) { float a = Rf(0, MathF.PI * 2), s = Rf(40, 160); l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = Rf(1.2f, 2f), size = Rf(2, 4), grav = 60, drag = 1.2f, col = Col.White, kind = K_SPARK }); }
            l.Add(new P { x = x, y = y, max = .35f, size = 90, col = col.Light(.5f), kind = K_FLASH });
            Shockwave(x, y, col.Light(.3f), 120, .5f);
        }
        public void Lightning(float x0, float y0, float x1, float y1, Col col)
        {
            Pt[] Jag(Pt a, Pt b, float off, int lvl)
            {
                var pts = new List<Pt> { a, b };
                for (int k = 0; k < lvl; k++)
                {
                    var n = new List<Pt>();
                    for (int i = 0; i < pts.Count - 1; i++) { var m = new Pt((pts[i].X + pts[i + 1].X) / 2 + Rf(-off, off), (pts[i].Y + pts[i + 1].Y) / 2 + Rf(-off * .3f, off * .3f)); n.Add(pts[i]); n.Add(m); }
                    n.Add(pts[pts.Count - 1]); pts = n; off *= .55f;
                }
                return pts.ToArray();
            }
            var main = Jag(new Pt(x0, y0), new Pt(x1, y1), 90, 6); var br = new List<Pt[]>();
            for (int i = 0; i < 4; i++) { var s = main[R.Next(6, main.Length - 8)]; br.Add(Jag(s, new Pt(s.X + Rf(-220, 220), s.Y + Rf(80, 260)), 40, 4)); }
            bolts.Add(new Bolt { pts = main, br = br.ToArray(), max = .42f, col = col });
            Shockwave(x1, y1, col, 160, .45f); Spark(x1, y1, col.Light(.5f), 18, 320);
            Sfx.Play(S.Zap, .8f, Rf(.9f, 1.1f)); App.Flash(col.Light(.6f), .35f);
        }
        public void Clear() { l.Clear(); bolts.Clear(); shocks.Clear(); }
        public int Count => l.Count;

        public void Update(float dt)
        {
            if (l.Count > MaxParticles) l.RemoveRange(0, l.Count - MaxParticles);
            for (int i = l.Count - 1; i >= 0; i--)
            {
                var p = l[i]; p.life += dt;
                if (p.life < 0) { l[i] = p; continue; }
                if (p.life >= p.max) { bool boom = p.kind == K_STREAK && p.aux == 1; l.RemoveAt(i); if (boom) Firework(p.x, p.y, p.col); continue; }
                p.vx -= p.vx * p.drag * dt; p.vy -= p.vy * p.drag * dt; p.vy += p.grav * dt; p.x += p.vx * dt; p.y += p.vy * dt; p.rot += p.vr * dt;
                if (p.kind == K_BUB) p.x += MathF.Sin(p.life * 7 + p.aux) * 22 * dt;
                if (p.kind == K_STREAK && p.aux == 1 && R.NextDouble() < dt * 90) l.Add(new P { x = p.x, y = p.y, vx = Rf(-20, 20), vy = Rf(20, 60), max = .55f, size = 3, drag = 2, col = p.col.Light(.4f), kind = K_SPARK });
                l[i] = p;
            }
            for (int i = bolts.Count - 1; i >= 0; i--) { var b = bolts[i]; b.life += dt; if (b.life >= b.max) bolts.RemoveAt(i); else bolts[i] = b; }
            for (int i = shocks.Count - 1; i >= 0; i--) { var s = shocks[i]; s.life += dt; if (s.life >= s.max) shocks.RemoveAt(i); else shocks[i] = s; }
        }

        static Paint Add(Paint p, float glow = 1.8f) { p.Additive = true; p.Glow = glow; return p; }

        public void Draw(Canvas2D c)
        {
            for (int pass = 0; pass < 2; pass++)
                foreach (var p in l)
                {
                    if (p.life < 0) continue;
                    bool addK = p.kind == K_SPARK || p.kind == K_FIRE || p.kind == K_STREAK || p.kind == K_FLASH; if (addK != (pass == 1)) continue;
                    float t = p.life / p.max, a = t < .15f ? t / .15f : 1 - (t - .15f) / .85f; a = Math.Clamp(a, 0, 1);
                    switch (p.kind)
                    {
                        case K_SPARK:
                            c.DrawRadial(p.x, p.y, p.size * 2.6f, Add(Gfx.Fill(p.col.A(a * .5f)))); c.DrawCircle(p.x, p.y, p.size * .6f, Add(Gfx.Fill(p.col.A(a).Light(.4f)), 2.2f)); break;
                        case K_CONF:
                            {
                                c.Save(); c.Translate(p.x, p.y); c.RotateDegrees(p.rot * 57.3f); float sh = MathF.Cos(p.rot * 1.7f); c.Scale(1, MathF.Abs(sh) * .9f + .1f);
                                var cc = sh > 0 ? p.col : p.col.Dark(.7f);
                                c.DrawRoundRect(new Box(-p.size / 2, -p.size / 4, p.size / 2, p.size / 4), 1.5f, 1.5f, Gfx.Fill(cc.A(a))); c.Restore(); break;
                            }
                        case K_STAR: { var sp = p.col.A(a).Light(.2f); Gfx.Light(c, p.x, p.y, p.size * 2.2f, p.col, a * .35f); Gfx.Star(c, p.x, p.y, p.size * (1.2f - t * .6f), sp, p.rot); break; }
                        case K_SMOKE: { var sp = Gfx.Fill(p.col.A(a * .38f)); sp.Blur = p.size * .35f; c.DrawCircle(p.x, p.y, p.size * (1 + t * 1.8f), sp); break; }
                        case K_FIRE:
                            {
                                float f = 1 - t; var col = f > .6f ? C.Yellow.Mix(C.Orange, (1 - f) / .4f) : C.Orange.Mix(C.Red, (.6f - f) / .6f); float r = p.size * (.5f + f * .7f);
                                c.DrawRadial(p.x, p.y, r * 2.2f, Add(Gfx.Fill(col.A(a * .45f)))); c.DrawRadial(p.x, p.y, r, Add(Gfx.Fill(col.A(a * .9f)), 2.2f));
                                if (f > .5f) c.DrawRadial(p.x, p.y, r * .5f, Add(Gfx.Fill(Col.White.A(a * .9f)), 2.6f)); break;
                            }
                        case K_DROP: c.DrawLine(p.x, p.y, p.x - p.vx * .022f, p.y - p.vy * .022f, Gfx.Line(p.col.A(a), p.size)); break;
                        case K_BUB: c.DrawCircle(p.x, p.y, p.size, Gfx.Line(p.col.A(a * .8f), 1.6f)); c.DrawCircle(p.x - p.size * .35f, p.y - p.size * .35f, p.size * .22f, Gfx.Fill(Col.White.A(a * .9f))); break;
                        case K_DEB: c.Save(); c.Translate(p.x, p.y); c.RotateDegrees(p.rot * 57.3f); c.DrawRect(-p.size / 2, -p.size / 3, p.size, p.size * .66f, Gfx.Fill(p.col.A(a))); c.Restore(); break;
                        case K_PETAL: c.Save(); c.Translate(p.x, p.y); c.RotateDegrees(p.rot * 57.3f); c.Scale(1, MathF.Abs(MathF.Cos(p.life * 3 + p.aux)) * .7f + .3f); c.DrawOval(0, 0, p.size * .55f, p.size, Gfx.Fill(p.col.A(a))); c.Restore(); break;
                        case K_STREAK:
                            { float k = .045f; c.DrawLine(p.x, p.y, p.x - p.vx * k, p.y - p.vy * k, Add(Gfx.Line(p.col.A(a), p.size * .7f), 2.2f)); c.DrawRadial(p.x, p.y, p.size * 2.4f, Add(Gfx.Fill(p.col.A(a * .45f)))); break; }
                        case K_RIPPLE:
                            {
                                float r = p.size * Ease.OutCubic(t); c.DrawOval(p.x, p.y, r, r * p.aux, Gfx.Line(p.col.A((1 - t) * .7f), 3 * (1 - t) + 1));
                                if (t > .15f) c.DrawOval(p.x, p.y, r * .6f, r * .6f * p.aux, Gfx.Line(p.col.A((1 - t) * .4f), 2)); break;
                            }
                        case K_FLASH: c.DrawRadial(p.x, p.y, p.size * (.6f + t * 1.2f), Add(Gfx.Fill(p.col.A((1 - t) * .7f)), 2.4f)); break;
                    }
                }
            foreach (var s in shocks)
            {
                float t = s.life / s.max, r = s.r * Ease.OutCubic(t);
                var g = Add(Gfx.Line(s.col.A((1 - t) * .5f), 18 * (1 - t) + 2), 1.6f); g.Blur = 6; c.DrawCircle(s.x, s.y, r, g);
                c.DrawCircle(s.x, s.y, r, Add(Gfx.Line(s.col.A((1 - t) * .85f), 8 * (1 - t) + 1), 2f));
            }
            foreach (var b in bolts)
            {
                float t = b.life / b.max, a = t < .1f ? 1 : (1 - t) * (.6f + .4f * MathF.Sin(t * 60)); a = Math.Clamp(a, 0, 1);
                void Line(Pt[] pts, float w)
                {
                    using var path = new Path2D(); path.MoveTo(pts[0]); for (int i = 1; i < pts.Length; i++) path.LineTo(pts[i]);
                    var g = Add(Gfx.Line(b.col.A(a * .55f), w * 5)); g.Blur = 8; c.DrawPath(path, g);
                    c.DrawPath(path, Add(Gfx.Line(b.col.Light(.4f).A(a), w * 1.6f), 2.4f)); c.DrawPath(path, Add(Gfx.Line(Col.White.A(a), w * .7f), 3f));
                }
                Line(b.pts, 3); foreach (var br in b.br) Line(br, 1.5f);
            }
        }
    }
}
