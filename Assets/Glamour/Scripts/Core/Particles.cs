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
        const int K_SPARK = 0, K_CONF = 1, K_STAR = 2, K_SMOKE = 3, K_FIRE = 4, K_DROP = 5, K_BUB = 6, K_DEB = 7, K_PETAL = 8, K_STREAK = 9, K_RIPPLE = 10, K_FLASH = 11, K_COIN = 12, K_GLINT = 13, K_RIBBON = 14;
        static readonly Col[] Foil = { new Col(255, 205, 80), new Col(235, 238, 248), new Col(255, 170, 60) };

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
                l.Add(new P { x = Rf(0, w), y = Rf(-200, -10), vx = Rf(-60, 60), vy = Rf(120, 320), max = Rf(2.5f, 4.5f), size = Rf(6, 12), rot = Rf(0, 6), vr = Rf(-6, 6), grav = 120, drag = .3f, col = Party[R.Next(Party.Length)], kind = K_CONF, aux = R.Next(4) == 0 ? 1 : 0 });
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
            for (int i = 0; i < n; i++) { float a = ang + Rf(-.28f, .28f), s = Rf(.4f, 1) * speed; l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = Rf(2f, 3.6f), size = Rf(6, 12), rot = Rf(0, 6), vr = Rf(-8, 8), grav = 520, drag = 1.3f, col = i % 4 == 1 ? Foil[R.Next(Foil.Length)] : Party[R.Next(Party.Length)], kind = i % 5 == 0 ? K_STAR : K_CONF, aux = i % 4 == 1 ? 1 : 0 }); }
        }
        public void Rocket(float x, float ty, Col col)
        {
            float T = Rf(.8f, 1.1f); l.Add(new P { x = x, y = 940, vx = Rf(-40, 40), vy = -(940 - ty) / T, max = T, size = 4, col = col, kind = K_STREAK, aux = 1 });
            Sfx.Play(S.Launch, .5f, Rf(.9f, 1.15f));
        }
        /// <summary>Goldmuenzen-Regen von oben (verteilt ueber 'spread' Sekunden).</summary>
        public void CoinShower(float w, int n = 60, float spread = 1.5f)
        {
            for (int i = 0; i < n; i++) l.Add(new P { x = Rf(20, w - 20), y = Rf(-120, -20), vx = Rf(-50, 50), vy = Rf(60, 260), life = -Rf(0, spread), max = Rf(2.2f, 3.2f), size = Rf(10, 17), rot = Rf(0, 6), vr = Rf(5, 13) * (R.Next(2) * 2 - 1), grav = 820, drag = .35f, aux = Rf(-.5f, .5f), col = C.Gold, kind = K_COIN });
        }
        /// <summary>Muenzfontaene aus einem Punkt (z.B. Pokal, Gewinnlinie).</summary>
        public void CoinFountain(float x, float y, int n = 30, float speed = 900)
        {
            for (int i = 0; i < n; i++) { float a = -MathF.PI / 2 + Rf(-.5f, .5f), s = Rf(.55f, 1) * speed; l.Add(new P { x = x + Rf(-10, 10), y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, life = -Rf(0, .5f), max = Rf(1.8f, 2.6f), size = Rf(10, 16), rot = Rf(0, 6), vr = Rf(6, 14) * (R.Next(2) * 2 - 1), grav = 1100, drag = .25f, aux = Rf(-.5f, .5f), col = C.Gold, kind = K_COIN }); }
        }
        /// <summary>Kurze Glitzersterne im Rechteck.</summary>
        public void Glints(Box r, int n, Col col, float spread = 1)
        {
            for (int i = 0; i < n; i++) l.Add(new P { x = Rf(r.Left, r.Right), y = Rf(r.Top, r.Bottom), life = -Rf(0, spread), max = Rf(.45f, .8f), size = Rf(10, 26), rot = Rf(0, .8f), col = col, kind = K_GLINT });
        }
        /// <summary>Luftschlangen: sich drehende, wehende Baender.</summary>
        public void Streamers(float w, int n = 24, float spread = 1)
        {
            for (int i = 0; i < n; i++) l.Add(new P { x = Rf(0, w), y = Rf(-160, -40), vx = Rf(-40, 40), vy = Rf(90, 200), life = -Rf(0, spread), max = Rf(3.5f, 5f), size = Rf(60, 110), rot = Rf(0, 6), vr = Rf(2, 4), grav = 60, drag = .5f, aux = Rf(0, 6), col = i % 3 == 0 ? Foil[R.Next(Foil.Length)] : Party[R.Next(Party.Length)], kind = K_RIBBON });
        }
        void Firework(float x, float y, Col col)
        {
            int type = R.Next(6), n = type == 1 ? 48 : type == 3 ? 110 : type == 4 ? 70 : 85; var alt = Party[R.Next(Party.Length)]; Sfx.Play(S.FwPop, .55f, Rf(.85f, 1.2f));
            for (int i = 0; i < n; i++)
            {
                float a = type == 1 ? i * MathF.PI * 2 / n : Rf(0, MathF.PI * 2), s = type == 1 ? 380 : type == 3 ? 420 * MathF.Pow(Rf(0, 1), .4f) : Rf(.25f, 1) * 460;
                if (type == 4) { l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s * .8f, vy = MathF.Sin(a) * s * .8f, max = Rf(2.2f, 3f), size = Rf(2.5f, 4), grav = 70, drag = 1.25f, col = new Col(255, 190, 90), kind = K_STREAK, aux = 3 }); continue; }
                l.Add(new P { x = x, y = y, vx = MathF.Cos(a) * s, vy = MathF.Sin(a) * s, max = type == 2 ? Rf(1.4f, 2.2f) : Rf(.9f, 1.5f), size = Rf(3, 5.5f), grav = type == 2 ? 240 : 110, drag = type == 2 ? 1.6f : 1.9f, col = i % 3 == 0 ? alt : col, kind = K_STREAK, aux = type == 5 ? 2 : 0 });
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
                if (p.life >= p.max) { bool boom = p.kind == K_STREAK && p.aux == 1, crack = p.kind == K_STREAK && p.aux == 2; l.RemoveAt(i); if (boom) Firework(p.x, p.y, p.col); else if (crack) for (int k = 0; k < 3; k++) l.Add(new P { x = p.x + Rf(-14, 14), y = p.y + Rf(-14, 14), life = -Rf(0, .15f), max = Rf(.12f, .3f), size = Rf(6, 12), rot = Rf(0, .8f), col = Col.White, kind = K_GLINT }); continue; }
                if (p.kind == K_CONF || p.kind == K_PETAL)
                {
                    // flatterndes Papier: flach liegend bremst die Luft stark, hochkant kaum; die Schraeglage lenkt seitlich ab
                    float face = MathF.Abs(MathF.Cos(p.rot * 1.7f)), dr = p.drag * (.35f + 1.3f * face);
                    p.vx -= p.vx * dr * dt; p.vy -= p.vy * dr * dt; p.vy += p.grav * dt; p.vx += MathF.Sin(p.rot * 3.4f) * Math.Max(0, p.vy) * 1.6f * dt;
                }
                else { p.vx -= p.vx * p.drag * dt; p.vy -= p.vy * p.drag * dt; p.vy += p.grav * dt; }
                p.x += p.vx * dt; p.y += p.vy * dt; p.rot += p.vr * dt;
                if (p.kind == K_COIN && p.vy > 0 && p.y > App.VY1 - p.size * .5f)
                {
                    // Muenze prallt vom unteren Rand ab: Restitution, Reibung bremst Gleiten und Drehung
                    p.y = App.VY1 - p.size * .5f; if (p.vy > 220 && R.NextDouble() < .25) Impact.Play(Mat.Metal, p.vy / 3000, .6f);
                    if (p.vy > 90) { p.vy = -p.vy * .42f; p.vx *= .72f; p.vr *= .6f; }
                    else { p.vy = 0; p.grav = 0; p.drag = 1.1f; p.vr = 0; p.rot = MathF.Round(p.rot / MathF.PI) * MathF.PI; }   // liegt: ab jetzt kreiseln
                }
                else if (p.kind == K_COIN && p.grav == 0 && p.drag > 0)
                {
                    // Euler-Scheibe: Neigung (drag) nimmt ab, die Kreiselfrequenz steigt wie 1/Wurzel(Neigung); rollt dabei aus
                    p.drag *= MathF.Exp(-1.6f * dt); p.vr += (5 / MathF.Sqrt(p.drag + .03f)) * dt; p.vx *= 1 - Math.Min(1, 2.5f * dt);
                    p.aux = .5f * p.drag * MathF.Sin(p.vr); p.rot = MathF.Round(p.rot / MathF.PI) * MathF.PI + p.drag * .9f * MathF.Cos(p.vr);
                    if (p.drag < .02f) p.drag = 0;
                }
                if (p.kind == K_BUB) p.x += MathF.Sin(p.life * 7 + p.aux) * 22 * dt;
                if (p.kind == K_STREAK && p.aux == 3 && R.NextDouble() < dt * 30) l.Add(new P { x = p.x, y = p.y, vx = Rf(-8, 8), vy = Rf(10, 40), max = Rf(.6f, 1.1f), size = 2.2f, drag = 1.5f, grav = 60, col = new Col(255, 170, 70), kind = K_SPARK });
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
                    bool addK = p.kind == K_SPARK || p.kind == K_FIRE || p.kind == K_STREAK || p.kind == K_FLASH || p.kind == K_GLINT; if (addK != (pass == 1)) continue;
                    float t = p.life / p.max, a = t < .15f ? t / .15f : 1 - (t - .15f) / .85f; a = Math.Clamp(a, 0, 1);
                    switch (p.kind)
                    {
                        case K_SPARK:
                            c.DrawRadial(p.x, p.y, p.size * 2.6f, Add(Gfx.Fill(p.col.A(a * .5f)))); c.DrawCircle(p.x, p.y, p.size * .6f, Add(Gfx.Fill(p.col.A(a).Light(.4f)), 2.2f)); break;
                        case K_CONF:
                            {
                                c.Save(); c.Translate(p.x, p.y); c.RotateDegrees(p.rot * 57.3f); float sh = MathF.Cos(p.rot * 1.7f); c.Scale(1, MathF.Abs(sh) * .9f + .1f);
                                float lit = MathF.Abs(sh); var cc = (sh > 0 ? p.col : p.col.Dark(.72f)).Dark(.55f + .45f * lit);
                                var cb = new Box(-p.size / 2, -p.size / 4, p.size / 2, p.size / 4);
                                c.DrawRoundRect(cb, 1.5f, 1.5f, Gfx.Fill(cc.A(a)));
                                if (p.aux == 1 && lit > .8f) { var fl = Gfx.Fill(Col.White.A(a * (lit - .8f) * 4.5f)); fl.Additive = true; fl.Glow = 2.6f; c.DrawRoundRect(cb, 1.5f, 1.5f, fl); }
                                c.Restore(); break;
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
                        case K_COIN:
                            {
                                float cs = MathF.Cos(p.rot), sy = Math.Max(.07f, MathF.Abs(cs)), r = p.size, th = r * .2f * MathF.Sqrt(1 - cs * cs);
                                c.Save(); c.Translate(p.x, p.y); c.RotateRadians(p.aux);
                                if (th > .5f) c.DrawRoundRect(new Box(-r, -r * sy, r, r * sy + th), r * sy, r * sy, Gfx.Fill(new Col(150, 96, 14).A(a)));
                                c.Scale(1, sy);
                                var face = Gfx.Fill(Col.White); face.Shader = Grad.Radial(-r * .35f, -r * .45f, r * 1.7f, new Col(255, 246, 190).A(a), new Col(196, 128, 20).A(a)); c.DrawCircle(0, 0, r, face);
                                c.DrawCircle(0, 0, r * .74f, Gfx.Line(new Col(150, 92, 10).A(a * .8f), r * .09f));
                                c.DrawCircle(0, 0, r * .96f, Gfx.Line(new Col(255, 236, 160).A(a * .9f), r * .08f));
                                Gfx.Star(c, 0, r * .03f, r * .42f, new Col(170, 108, 16).A(a * .85f), 0); Gfx.Star(c, 0, -r * .02f, r * .4f, new Col(255, 222, 110).A(a), 0);
                                c.Restore();
                                float shine = MathF.Pow(Math.Max(0, cs), 10); if (shine > .05f) Metal.Glint(c, p.x - r * .3f, p.y - r * .3f * sy, r * 1.3f, Metal.GoldLight, shine * a, .4f);
                                break;
                            }
                        case K_GLINT: { float u = MathF.Sin(t * MathF.PI); Metal.Glint(c, p.x, p.y, p.size * u, p.col, u, p.rot); break; }
                        case K_RIBBON:
                            {
                                float seg = p.size / 9, px = p.x, py = p.y;
                                for (int k = 1; k <= 9; k++)
                                {
                                    float ph = p.life * 3 + p.aux + k * .75f, nx = p.x + MathF.Sin(ph) * 16 * (k / 9f + .3f), ny = p.y - k * seg, tw = MathF.Cos(p.life * p.vr + k * .55f);
                                    var rc = (tw > 0 ? p.col : p.col.Dark(.6f)).Dark(.6f + .4f * MathF.Abs(tw));
                                    c.DrawLine(px, py, nx, ny, Gfx.Line(rc.A(a), 1.2f + 4.8f * MathF.Abs(tw))); px = nx; py = ny;
                                }
                                break;
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
