using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public enum PKind { Dice, Disc, Sailor }
    public class DiceParade
    {
        class A { public int team, val, mood; public float x, y, sx, tx, delay, ph, face = 1, hop, hopV, throwT = -1, tgtX, tgtY, lastSin; public A partner; public bool moving, crown; public int k; }
        const int N = 7; const float TShake = 3.9f, TThrow = 5.8f, TWin = 8.8f;
        readonly List<A> l = new(); readonly int winner; readonly Col[] cols; readonly string[] names; readonly int[] scores; readonly Scene sc; readonly Random R = new();
        float t, nextThrow; bool shook, won; readonly PKind kind; readonly float spd;
        public float T => t;
        public DiceParade(Scene sc, int winner, Col[] cols, string[] names, int[] scores, PKind kind = PKind.Dice, float speed = 1f)
        {
            this.kind = kind; spd = speed; this.sc = sc; this.winner = winner; this.cols = cols; this.names = names; this.scores = scores;
            for (int k = 0; k < N; k++)
            {
                float mx = 250 + k * 183, y = k % 2 == 0 ? 700 : 812;
                var a = new A { team = 0, k = k, sx = -110 - k * 40, tx = mx - 58, x = -110 - k * 40, y = y, delay = k * .17f, face = 1, val = R.Next(1, 7) };
                var b = new A { team = 1, k = k, sx = 1710 + k * 40, tx = mx + 58, x = 1710 + k * 40, y = y, delay = k * .17f + .08f, face = -1, val = R.Next(1, 7) };
                a.partner = b; b.partner = a; l.Add(a); l.Add(b);
            }
            Log.I($"parade start winner={winner}");
            Sfx.Play(S.Whoosh, .6f);
        }
        A Pick(int team) { var c = l.Where(a => a.team == team).ToList(); return c[R.Next(c.Count)]; }
        public float Total => TWin + 2.2f;
        public void Update(float dt)
        {
            t += dt * spd;
            foreach (var a in l)
            {
                float p = Ease.Clamp((t - a.delay) / 2.5f); a.x = Ease.Lerp(a.sx, a.tx, Ease.InOutCubic(p)); a.moving = p > 0 && p < 1;
                if (a.moving) { a.ph += dt * 13; float s = MathF.Sin(a.ph); if (a.lastSin * s < 0 && a.k % 3 == a.team) Sfx.Play(S.Step, .1f, .9f + a.k * .04f); a.lastSin = s; }
                if (a.throwT >= 0) { a.throwT += dt / .45f; if (a.throwT > 1) a.throwT = -1; }
                a.hopV -= 2200 * dt; a.hop += a.hopV * dt; if (a.hop < 0) { a.hop = 0; a.hopV = 0; }
                a.mood = t < TShake ? 0 : t < TWin ? 1 : winner < 0 || a.team == winner ? 2 : -1;
                a.crown = t >= TWin && winner >= 0 && a.team == winner;
            }
            if (!shook && t >= TShake)
            {
                shook = true; Sfx.Play(S.Shake, .7f);
                foreach (var a in l.Where(a => a.team == 0)) { var m = a; sc.Tm.After(a.k * .12f, () => { float hx = (m.x + m.partner.x) / 2, hy = m.y - 62 * Sz(m); sc.Fx.Burst(hx, hy, 8, new[] { C.Gold, C.Yellow, Col.White }, 120, 2, 100); sc.Fx.Ring(hx, hy, C.Gold, 10, 120); Sfx.Play(S.Shake, .35f, .9f + m.k * .05f); }); }
            }
            if (t >= TThrow && t < TWin)
            {
                nextThrow -= dt;
                while (nextThrow <= 0)
                {
                    nextThrow += .16f; var a = l[R.Next(l.Count)]; var tg = Pick(1 - a.team); if (a.throwT >= 0) continue;
                    a.throwT = 0; a.tgtX = tg.x; a.tgtY = tg.y - 60 * Sz(tg); var ta = a; var tt = tg;
                    sc.Tm.After(.2f, () => { sc.Fx.Throw(ta.x + ta.face * 30, ta.y - 90 * Sz(ta), tt.x, tt.y - 60 * Sz(tt), 12); Sfx.Play(S.Pop, .35f, .8f + (float)R.NextDouble() * .5f); });
                    sc.Tm.After(.95f, () => { tt.hopV = 260; Sfx.Play(S.Boing, .15f, .8f + (float)R.NextDouble() * .6f); });
                }
            }
            if (!won && t >= TWin)
            {
                won = true; Sfx.Play(S.Gong, .7f); var col = winner < 0 ? C.Gold : cols[winner]; sc.Celebrate(col, 6, 1.3f);
                foreach (var a in l) if (winner < 0 || a.team == winner) { var m = a; sc.Tm.After((float)R.NextDouble() * .6f, () => { m.hopV = 520; }); }
            }
            if (t >= TWin)
                foreach (var a in l) if ((winner < 0 || a.team == winner) && a.hop == 0 && R.NextDouble() < dt * 1.6) { a.hopV = 480 + (float)R.NextDouble() * 200; if (R.NextDouble() < .3) { sc.Fx.Burst(a.x, a.y - 100, 6, new[] { C.Gold, C.Yellow }, 150, 2, 200); Sfx.Play(S.Boing, .12f, 1.2f); } }
        }
        static float Sz(A a) => a.y < 750 ? .86f : 1f;
        public void Draw(Canvas2D c)
        {
            float veil = Ease.Clamp(t / .8f) * .6f; c.DrawRect(App.VX0 - 60, App.VY0 - 60, App.VX1 - App.VX0 + 120, App.VY1 - App.VY0 + 120, Gfx.Fill(Col.Black.A(veil)));
            float sa = Ease.Clamp(t / 1f);
            var stage = Gfx.R(App.VX0 - 60, 610, App.VX1 - App.VX0 + 120, 400); Gfx.RectGrad(c, stage, 0, new Col(20, 90, 70, (byte)(90 * sa)), new Col(6, 30, 30, (byte)(190 * sa)));
            c.DrawLine(App.VX0 - 60, 610, App.VX1 + 60, 610, Gfx.Line(C.Cyan.A(.5f * sa), 3));
            if (t >= TWin && winner >= 0) { float wx = winner == 0 ? 400 : 1200; Gfx.Radial(c, wx, 700, 620, cols[winner], .3f * Ease.Clamp((t - TWin) / .8f)); }
            DrawPlates(c);
            foreach (var a in l.OrderBy(a => a.y).ThenBy(a => a.hop)) DrawDie(c, a);
            DrawBanner(c);
        }
        void DrawPlates(Canvas2D c)
        {
            float pa = Ease.Clamp((t - 1.5f) / .6f); if (pa <= 0) return;
            for (int tm = 0; tm < 2; tm++)
            {
                bool w = t >= TWin && (winner < 0 || tm == winner), lose = t >= TWin && !w; float cx = tm == 0 ? 400 : 1200, cy = 552 + (w ? -6 * MathF.Sin(t * 6) : 0), sc2 = w ? 1.18f : lose ? .85f : 1f;
                c.Save(); c.Translate(cx, cy); c.Scale(sc2 * (.6f + .4f * Ease.OutBack(pa))); c.Translate(-cx, -cy);
                var r = Gfx.Ctr(cx, cy, 380, 70); if (w) Gfx.Glow(c, r, 20, C.Gold, 22, .9f); else Gfx.Glow(c, r, 20, cols[tm], 12, .35f);
                Gfx.RectGrad(c, r, 20, cols[tm].Dark(w ? .55f : .3f), new Col(10, 3, 24)); Gfx.Stroke(c, r, 20, (w ? C.Gold : cols[tm]).A(.95f), w ? 4 : 2.5f);
                Gfx.Text(c, $"{names[tm]}", cx - 60, cy, 30, Col.White, Al.C, true, w ? 8 : 0); Gfx.Text(c, scores[tm].ToString(), cx + 120, cy, 44, cols[tm].Light(.5f), Al.C, true, w ? 12 : 4);
                if (w) Trophy(c, cx - 170, cy - 6, 1f + .06f * MathF.Sin(t * 8));
                c.Restore();
            }
        }
        static void Trophy(Canvas2D c, float x, float y, float s)
        {
            c.Save(); c.Translate(x, y); c.Scale(s); using var cup = new Path2D(); cup.MoveTo(-16, -22); cup.LineTo(16, -22); cup.CubicTo(16, 0, 8, 8, 0, 8); cup.CubicTo(-8, 8, -16, 0, -16, -22); cup.Close();
            c.DrawPath(cup, Gfx.Fill(C.Gold)); c.DrawArc(new Box(-27, -20, -9, 0), 90, 180, false, Gfx.Line(C.Gold, 4)); c.DrawArc(new Box(9, -20, 27, 0), -90, 180, false, Gfx.Line(C.Gold, 4));
            c.DrawRect(-3, 8, 6, 10, Gfx.Fill(C.Gold.Dark(.8f))); Gfx.Rect(c, new Box(-12, 18, 12, 25), 3, C.Gold.Dark(.7f)); Gfx.Star(c, 0, -9, 8, Col.White.A(.9f)); c.Restore();
        }
        void DrawBanner(Canvas2D c)
        {
            if (t < TWin) { float ta = Ease.Clamp((t - 2.2f) / .6f) * (1 - Ease.Clamp((t - TShake) / .6f)); if (ta > 0) Gfx.Text(c, "Zwei Teams treffen aufeinander...", 800, 160, 40, Col.White.A(ta), Al.C, true, 8); else if (t >= TShake) { float a2 = Ease.Clamp((t - TShake - .3f) / .6f) * (1 - Ease.Clamp((t - TWin + .5f) / .5f)); if (a2 > 0) Gfx.Text(c, t < TThrow ? "Faire Hände!" : "KONFETTI-SCHLACHT!", 800, 160, 56, C.Gold.A(a2), Al.C, true, 14, true); } return; }
            float u = Ease.OutElastic((t - TWin) / 1.1f), pulse = 1 + .04f * MathF.Sin(t * 7);
            string s = winner < 0 ? "UNENTSCHIEDEN!" : $"{names[winner].ToUpper()} GEWINNT!"; var col = winner < 0 ? C.Gold : cols[winner];
            c.Save(); c.Translate(800, 200); c.Scale(u * pulse); Gfx.BannerText(c, s, 96, col, 1);
            if (winner >= 0) Gfx.Text(c, $"{scores[winner]} : {scores[1 - winner]}", 0, 84, 50, C.Gold, Al.C, true, 14); c.Restore();
        }
        void DrawDie(Canvas2D c, A a)
        {
            float s = Sz(a), W = 70 * s, legL = 34 * s, col = 0; var tc = cols[a.team];
            float bob = a.moving ? MathF.Abs(MathF.Sin(a.ph)) * 7 * s : 0, idle = a.mood == 0 && !a.moving ? MathF.Sin(t * 3 + a.k) * 1.5f : 0;
            float lift = bob + a.hop, feetY = a.y - a.hop; float cy = a.y - legL - W / 2 - lift + idle, cx = a.x;
            float shad = Math.Max(.3f, 1 - a.hop / 200f);
            c.DrawOval(cx, a.y + 4, W * .6f * shad, 8 * s * shad, Gfx.Fill(Col.Black.A(.4f * shad)));
            bool win = a.mood == 2 && winner >= 0; if (win) Gfx.Radial(c, cx, cy, W * 1.3f, C.Gold, .35f + .15f * MathF.Sin(t * 8 + a.k));
            var dark = tc.Dark(.45f); var navy = new Col(24, 44, 110); var limb = kind == PKind.Sailor ? navy : dark;
            float hipY = cy + W / 2 - 4;
            for (int side = -1; side <= 1; side += 2)
            {
                float hx = cx + side * W * .22f, ph = a.ph + (side > 0 ? MathF.PI : 0), fx = hx + (a.moving ? MathF.Sin(ph) * 14 * s * a.face : 0), fy = a.y - a.hop - (a.moving ? Math.Max(0, MathF.Cos(ph)) * 8 * s : 0);
                if (a.hop > 4) { fx = hx + side * 6 * s; fy = a.y - a.hop + 4 * s; if (a.hop > 20) fy -= 6 * s; }
                c.DrawLine(hx, hipY, fx, fy - 5 * s, Gfx.Line(limb, (kind == PKind.Sailor ? 10 : 7) * s)); if (kind == PKind.Sailor) c.DrawOval(fx, fy - 8 * s, 8 * s, 5 * s, Gfx.Fill(navy)); Shoe(c, fx, fy, s, a.face, kind == PKind.Sailor ? new Col(25, 25, 32) : tc);
            }
            float shY = cy - W * .06f;
            for (int side = -1; side <= 1; side += 2)
            {
                bool front = side == a.face; float sx = cx + side * W * .5f; Pt hand = Hand(a, front, sx, shY, s, side);
                Arm(c, sx, shY, hand.X, hand.Y, 19 * s, limb, s);
            }
            float wob = a.mood == 2 ? MathF.Sin(t * 9 + a.k) * 7 : a.moving ? MathF.Sin(a.ph) * 3f * a.face : 0;
            c.Save(); c.Translate(cx, cy); c.RotateDegrees(wob); float sq = a.hop > 0 ? 1 + Math.Min(.1f, a.hopV * .0002f) : 1; c.Scale(1 / sq, sq);
            if (kind == PKind.Dice)
            {
                float d3 = 9 * s, hw = W / 2;
                using (var side = new Path2D()) { side.MoveTo(hw, -hw + 6 * s); side.LineTo(hw + d3, -hw - d3 + 6 * s); side.LineTo(hw + d3, hw - d3 - 4 * s); side.LineTo(hw, hw - 6 * s); side.Close(); c.DrawPath(side, Gfx.Fill(new Col(196, 184, 160))); c.DrawPath(side, Gfx.Line(tc.Dark(.5f), 2.5f * s)); }
                using (var top = new Path2D()) { top.MoveTo(-hw + 6 * s, -hw); top.LineTo(-hw + 6 * s + d3, -hw - d3); top.LineTo(hw + d3 - 6 * s, -hw - d3); top.LineTo(hw - 6 * s, -hw); top.Close(); c.DrawPath(top, Gfx.Fill(new Col(255, 255, 250))); c.DrawPath(top, Gfx.Line(tc.Dark(.5f), 2.5f * s)); }
                var body = Gfx.Ctr(0, 0, W, W); Gfx.RectGrad(c, body, 12 * s, new Col(255, 250, 235), new Col(226, 214, 190)); Gfx.Stroke(c, body, 12 * s, tc, 4.5f * s);
                Gfx.RectGrad(c, new Box(-W / 2 + 4, -W / 2 + 3, W / 2 - 4, -W / 2 + 15 * s), 8 * s, Col.White.A(.6f), Col.White.A(0));
                Pips(c, a.val, s, tc.Dark(.5f));
            }
            else if (kind == PKind.Disc)
            {
                float r = W / 2, th = 9 * s;
                c.DrawLine(0, 0, th, th * .35f, Gfx.Line(tc.Dark(.55f), r * 2)); c.DrawLine(0, 0, th, th * .35f, Gfx.Line(tc.Dark(.8f), r * 2 - 5 * s));
                Gfx.RectGrad(c, new Box(-r, -r, r, r), r, tc.Light(.18f), tc.Dark(.72f)); c.DrawCircle(0, 0, r, Gfx.Line(tc.Dark(.5f), 3 * s));
                c.DrawCircle(0, 0, r * .8f, Gfx.Line(tc.Dark(.45f).A(.8f), 2.5f * s)); c.DrawCircle(0, 0, r * .8f + 2 * s, Gfx.Line(tc.Light(.55f).A(.5f), 1.6f * s));
                c.DrawArc(new Box(-r * .9f, -r * .9f, r * .9f, r * .9f), 200, 70, false, Gfx.Line(Col.White.A(.55f), 3 * s));
            }
            else Sailor(c, a, s, W, tc);
            Face(c, a, s, tc); Bubble(c, a, s, W);
            c.Restore();
            if (a.crown && kind != PKind.Sailor) Crown(c, cx, cy - W / 2 - 4 * s + MathF.Sin(t * 9 + a.k) * 2, s);
        }
        void Sailor(Canvas2D c, A a, float s, float W, Col tc)
        {
            var skin = new Col(255, 214, 178); var navy = new Col(24, 44, 110); bool win = a.crown;
            var torso = Gfx.Ctr(0, W * .24f, W * .9f, W * .58f); Gfx.RectGrad(c, torso, 12 * s, navy.Light(.16f), navy.Dark(.7f)); Gfx.Stroke(c, torso, 12 * s, navy.Dark(.45f), 2.5f * s);
            float top = torso.Top;
            using (var v = new Path2D()) { v.MoveTo(-W * .17f, top); v.LineTo(0, top + W * .34f); v.LineTo(W * .17f, top); v.Close(); c.DrawPath(v, Gfx.Fill(Col.White)); }
            using (var col = new Path2D())
            {
                col.MoveTo(-W * .44f, top + 3 * s); col.LineTo(-W * .13f, top - 2 * s); col.LineTo(0, top + W * .36f); col.LineTo(W * .13f, top - 2 * s); col.LineTo(W * .44f, top + 3 * s); col.LineTo(W * .38f, top + W * .25f); col.LineTo(0, top + W * .46f); col.LineTo(-W * .38f, top + W * .25f); col.Close();
                c.DrawPath(col, Gfx.Fill(navy.Light(.3f))); c.Save();
                for (int k = 0; k < 3; k++) { float o = k * 4.5f * s; c.DrawLine(-W * .40f, top + W * .17f - o, 0, top + W * .40f - o, Gfx.Line(Col.White, 1.8f * s)); c.DrawLine(W * .40f, top + W * .17f - o, 0, top + W * .40f - o, Gfx.Line(Col.White, 1.8f * s)); }
                c.Restore(); c.DrawPath(col, Gfx.Line(navy.Dark(.5f), 2 * s));
            }
            using (var kn = new Path2D()) { kn.MoveTo(-6 * s, top + W * .3f); kn.LineTo(6 * s, top + W * .3f); kn.LineTo(0, top + W * .44f); kn.Close(); c.DrawPath(kn, Gfx.Fill(tc)); }
            if (win) { c.DrawCircle(W * .27f, top + W * .34f, 7 * s, Gfx.Fill(C.Gold)); Gfx.Star(c, W * .27f, top + W * .34f, 5 * s, Col.White); }
            float hy = -W * .2f, hr = W * .3f; Gfx.Ball(c, 0, hy, hr, skin); c.DrawCircle(0, hy, hr, Gfx.Line(new Col(150, 100, 70), 2.5f * s));
            c.DrawArc(new Box(-hr * 1.02f, hy - hr * 1.02f, hr * 1.02f, hy + hr * .2f), 200, 140, false, Gfx.Line(new Col(120, 80, 40), 4 * s));
            float lift = win ? -W * .32f - MathF.Abs(MathF.Sin(t * 8 + a.k)) * W * .3f : 0;
            c.Save(); c.Translate(0, lift); c.RotateDegrees(win ? MathF.Sin(t * 9 + a.k) * 14 : -6);
            using (var hat = new Path2D()) { hat.MoveTo(-hr * .95f, hy - hr * .42f); hat.LineTo(-hr * .78f, hy - hr * 1.2f); hat.QuadTo(0, hy - hr * 1.38f, hr * .78f, hy - hr * 1.2f); hat.LineTo(hr * .95f, hy - hr * .42f); hat.Close(); c.DrawPath(hat, Gfx.Fill(Col.White)); c.DrawPath(hat, Gfx.Line(new Col(140, 150, 180), 2 * s)); }
            c.DrawOval(0, hy - hr * .42f, hr * 1.05f, hr * .26f, Gfx.Fill(new Col(245, 245, 250))); c.DrawOval(0, hy - hr * .42f, hr * 1.05f, hr * .26f, Gfx.Line(new Col(140, 150, 180), 2 * s));
            c.DrawLine(-hr * .9f, hy - hr * .6f, hr * .9f, hy - hr * .6f, Gfx.Line(navy, 3 * s)); c.Restore();
        }
        void Bubble(Canvas2D c, A a, float s, float W)
        {
            string txt = null;
            if (t >= TShake + .4f && t < TThrow && a.k % 3 == a.team) txt = a.team == 0 ? "Gut gespielt!" : "Danke, gleichfalls!";
            else if (t >= TWin + .6f && t < TWin + 3.4f && a.mood == 2 && a.k % 2 == 0) txt = a.k % 4 == 0 ? "Juhuu!" : "Sieg!";
            else if (t >= TWin + .6f && t < TWin + 3.4f && a.mood == -1 && a.k % 3 == 0) txt = "Glückwunsch!";
            if (txt == null) return;
            c.Save(); c.Scale(1, 1); float bw = Gfx.TW(txt, 22) + 26, bh = 40, by = -W * .5f - 60 * s;
            var r = Gfx.Ctr(0, by, bw, bh); Gfx.Rect(c, r, 14, Col.White.A(.96f)); using var tail = new Path2D(); tail.MoveTo(-8, by + bh / 2 - 2); tail.LineTo(0, by + bh / 2 + 12); tail.LineTo(8, by + bh / 2 - 2); tail.Close(); c.DrawPath(tail, Gfx.Fill(Col.White.A(.96f)));
            Gfx.Text(c, txt, 0, by, 22, new Col(40, 20, 70)); c.Restore();
        }
        static void Pips(Canvas2D c, int v, float s, Col col)
        {
            int[][] g = { null, new[] { 4 }, new[] { 0, 8 }, new[] { 0, 4, 8 }, new[] { 0, 2, 6, 8 }, new[] { 0, 2, 4, 6, 8 }, new[] { 0, 2, 3, 5, 6, 8 } };
            foreach (var i in g[v]) c.DrawCircle(((i % 3) - 1) * 16 * s, 10 * s + ((i / 3) - 1) * 12 * s, 4.2f * s, Gfx.Fill(col));
        }
        void Face(Canvas2D c, A a, float s, Col tc)
        {
            float ey = -19 * s, look = a.face * 2.2f * s; bool blink = a.mood >= 0 && a.mood != 2 && (t * .9f + a.k * .37f + a.team * .5f) % 3.1f < .12f;
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = side * 11 * s;
                c.DrawCircle(ex * 1.55f, ey + 13 * s, 5 * s, Gfx.Fill(new Col(255, 110, 130, 100)));
                if (a.mood == 2) { c.DrawArc(new Box(ex - 6 * s, ey - 6 * s, ex + 6 * s, ey + 6 * s), 200, 140, false, Gfx.Line(Col.Black, 3 * s)); c.DrawLine(ex - 6 * s, ey - 13 * s, ex + 6 * s, ey - 15 * s - MathF.Abs(MathF.Sin(t * 9 + a.k)) * 2 * s, Gfx.Line(Col.Black, 2.2f * s)); continue; }
                if (blink) { c.DrawLine(ex - 6 * s, ey, ex + 6 * s, ey, Gfx.Line(Col.Black, 3 * s)); continue; }
                c.DrawCircle(ex, ey, 7.5f * s, Gfx.Fill(Col.White)); c.DrawCircle(ex, ey, 7.5f * s, Gfx.Line(Col.Black.A(.6f), 1.2f));
                float py = ey + (a.mood < 0 ? 2.5f * s : 0); c.DrawCircle(ex + look, py, 3.9f * s, Gfx.Fill(Col.Black)); c.DrawCircle(ex + look - 1.3f * s, py - 1.4f * s, 1.3f * s, Gfx.Fill(Col.White));
                if (a.mood < 0) c.DrawLine(ex - side * 7 * s, ey - 13 * s, ex + side * 6 * s, ey - 8 * s + side * 4 * s, Gfx.Line(Col.Black, 2.5f * s));
                else c.DrawLine(ex - 6 * s, ey - 11 * s, ex + 6 * s, ey - 12.5f * s + (a.mood == 1 ? -1.5f * s : 0), Gfx.Line(Col.Black.A(.75f), 2.2f * s));
            }
            if (a.mood == 2) { float op = .5f + .5f * MathF.Abs(MathF.Sin(t * 9 + a.k)); var mo = new Box(-8 * s, -9 * s, 8 * s, (-9 + 14 * op) * s); c.DrawOval(mo, Gfx.Fill(new Col(90, 10, 30))); if (op > .6f) c.DrawOval(new Box(-4.5f * s, mo.Bottom - 5 * s, 4.5f * s, mo.Bottom), Gfx.Fill(new Col(255, 110, 130))); c.DrawArc(new Box(-8 * s, -14 * s, 8 * s, 4 * s), 20, 140, false, Gfx.Line(Col.Black, 2.5f * s)); }
            else if (a.mood == 1 && t >= TThrow) { c.DrawOval(new Box(-7 * s, -8 * s, 7 * s, 4 * s + MathF.Sin(t * 14 + a.k) * 2 * s), Gfx.Fill(new Col(90, 10, 30))); }
            else if (a.mood == 1) c.DrawArc(new Box(-8 * s, -13 * s, 8 * s, 1 * s), 20, 140, false, Gfx.Line(Col.Black, 2.5f * s));
            else if (a.mood < 0) { c.DrawArc(new Box(-7 * s, -8 * s, 7 * s, 6 * s), 200, 140, false, Gfx.Line(Col.Black, 2.5f * s)); float ty = ey + 8 * s + (t * 30 + a.k * 7) % 22 * s; c.DrawOval(-11 * s, ty, 2.4f * s, 3.4f * s, Gfx.Fill(new Col(120, 200, 255, 220))); }
            else c.DrawArc(new Box(-6 * s, -12 * s, 6 * s, -2 * s), 20, 140, false, Gfx.Line(Col.Black.A(.8f), 2.2f * s));
        }
        static void Shoe(Canvas2D c, float x, float y, float s, float face, Col up)
        {
            c.DrawOval(x + face * 5 * s, y - 4 * s, 12 * s, 6.5f * s, Gfx.Fill(up)); c.DrawRoundRect(Gfx.Ctr(x + face * 5 * s, y + 1.5f * s, 25 * s, 4.5f * s), 2, 2, Gfx.Fill(Col.White)); c.DrawLine(x + face * 7 * s, y - 8 * s, x + face * 12 * s, y - 5 * s, Gfx.Line(Col.White, 1.6f * s));
        }
        static void Crown(Canvas2D c, float x, float y, float s)
        {
            c.Save(); c.Translate(x, y); c.Scale(s); using var p = new Path2D(); p.MoveTo(-20, 0); p.LineTo(-24, -24); p.LineTo(-11, -12); p.LineTo(0, -30); p.LineTo(11, -12); p.LineTo(24, -24); p.LineTo(20, 0); p.Close();
            Gfx.Glow(c, new Box(-20, -26, 20, 0), 6, C.Gold, 8, .8f); c.DrawPath(p, Gfx.Fill(C.Gold)); c.DrawPath(p, Gfx.Line(C.Gold.Dark(.6f), 1.8f)); c.DrawCircle(0, -10, 3.5f, Gfx.Fill(C.Red)); c.DrawCircle(-12, -6, 2.5f, Gfx.Fill(C.Cyan)); c.DrawCircle(12, -6, 2.5f, Gfx.Fill(C.Green)); c.Restore();
        }
        Pt Hand(A a, bool front, float sx, float sy, float s, int side)
        {
            float f = a.face;
            if (a.moving) return new(sx + side * 6 * s + MathF.Sin(a.ph + (front ? 0 : MathF.PI)) * 16 * s * f, sy + 30 * s);
            if (t >= TShake && t < TThrow) { if (front) { float hx = (a.x + a.partner.x) / 2; return new(hx, sy + 6 * s + MathF.Sin(t * 11) * 6 * s); } return new(sx + side * 8 * s, sy + 28 * s); }
            if (t >= TThrow && t < TWin)
            {
                if (front && a.throwT >= 0) { float u = a.throwT, ang = Ease.Lerp(-2.6f, .4f, Ease.OutCubic(u)); return new(sx + f * MathF.Cos(ang) * 38 * s * (u < .25f ? -1 : 1) * (u < .25f ? .6f : 1) + f * 8 * s, sy - MathF.Sin(-ang) * 40 * s); }
                return new(sx + side * 8 * s, sy + 28 * s + MathF.Sin(t * 5 + a.k) * 2);
            }
            if (t >= TWin)
            {
                if (a.mood == 2) return new(sx + side * 16 * s, sy - 44 * s - MathF.Abs(MathF.Sin(t * 10 + a.k)) * 14 * s);
                float cl = MathF.Abs(MathF.Sin(t * 13 + a.k)); return new(a.x + side * (5 + 11 * cl) * s, sy + 14 * s);
            }
            return new(sx + side * 8 * s, sy + 28 * s);
        }
        static void Arm(Canvas2D c, float sx, float sy, float hx, float hy, float len, Col col, float s)
        {
            float dx = hx - sx, dy = hy - sy, d = MathF.Sqrt(dx * dx + dy * dy); float reach = len * 2 * .98f; if (d > reach) { hx = sx + dx / d * reach; hy = sy + dy / d * reach; dx = hx - sx; dy = hy - sy; d = reach; }
            float h = MathF.Sqrt(Math.Max(0, len * len - d * d / 4)), mx = (sx + hx) / 2, my = (sy + hy) / 2, nx = d > .01f ? -dy / d : 0, ny = d > .01f ? dx / d : 1; if (ny < 0) { nx = -nx; ny = -ny; }
            float ex = mx + nx * h * .7f, ey = my + ny * h * .7f; using var p = new Path2D(); p.MoveTo(sx, sy); p.LineTo(ex, ey); p.LineTo(hx, hy);
            c.DrawPath(p, Gfx.Line(col, 6.5f * s)); c.DrawCircle(hx, hy, 7 * s, Gfx.Fill(Col.White)); c.DrawCircle(hx, hy, 7 * s, Gfx.Line(col, 1.6f));
        }
    }
}
