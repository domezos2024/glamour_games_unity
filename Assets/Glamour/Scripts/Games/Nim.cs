using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class Nim : Scene
    {
        public override string Title => "Nim";
        public override Col Acc1 => C.Orange; public override Col Acc2 => C.Gold;
        public override string OppKey => "nim";
        /// <summary>"Computer denkt nach" zwischen Statuszeile und Staeben (unten stehen die Stapel-Beschriftungen).</summary>
        public override Pt ThinkPos => new Pt(800, 230);
        int[] piles = { 3, 5, 7 }; int cur = 1; int[] score = new int[2]; bool over; int hp = -1, hs = -1; readonly float[][] pop = new float[3][];
        class Fly { public float X, Y, Vx, Vy, Rot, Vr, T; public Col Col; }
        readonly List<Fly> fly = new List<Fly>(); string status = "";
        // Computer-Gegner: gen verhindert, dass alte Timer in eine neue Runde feuern
        int gen; bool tossing, cpuAiming;
        const float PX0 = 470, PDX = 330, BASE = 770, RW = 220, RHt = 52, GAP = 10;
        static float PX(int p) => PX0 + p * PDX;
        static Box Rod(int p, int k) => Gfx.Ctr(PX(p), BASE - RHt / 2 - k * (RHt + GAP), RW, RHt);
        public Nim() { for (int i = 0; i < 3; i++) pop[i] = Enumerable.Range(0, 7).Select(k => -k * .08f - i * .1f).ToArray(); }
        public override void Enter()
        {
            base.Enter();
            Opponents.AddSwitch(this, OppKey, 40, 600, 260, 70, NewMatch);
            Ui.Add(new Button(40, 700, 260, 62, "Neue Runde", C.Green, NextRound, 24)); Ui.Add(new Button(40, 780, 260, 62, "Punkte zurück", C.Purple, NewMatch, 22));
            Opponents.Pick(this, OppKey, o => NewMatch());
        }
        int starter = 1;
        void NextRound() { starter = 3 - starter; Reset(); }
        void NewMatch() { score = new int[2]; starter = 1; tossing = true; Reset(); CoinToss.Start(this, f => { tossing = false; starter = f + 1; Reset(); }); }
        void Reset()
        {
            gen++; CancelCpuThink(); piles = new[] { 3, 5, 7 }; cur = starter; over = false; fly.Clear(); status = $"{PName(cur - 1)} ist dran"; Modal = null; hp = hs = -1; cpuAiming = false;
            for (int i = 0; i < 3; i++) pop[i] = Enumerable.Range(0, 7).Select(k => -k * .08f - i * .1f).ToArray();
            CpuCheck();
        }
        bool CpuTurn => VsCpu && cur == 2 && !over && !tossing;
        bool Locked => CpuTurn || CpuThinking || cpuAiming;
        public override bool WantsHand => hp >= 0 && !Locked;
        public override void MouseMove(float x, float y)
        {
            if (Locked) return;                                // Markierung gehoert gerade dem Computer
            hp = hs = -1; if (over || Modal != null) return;
            for (int p = 0; p < 3; p++) for (int k = 0; k < piles[p]; k++) if (Gfx.Inflate(Rod(p, k), 6).Contains(x, y)) { hp = p; hs = k; }
        }
        public override void MouseUp(float x, float y) { if (Locked || hp < 0) return; Take(hp, hs); }

        // ---------------------------------------------------------------- Computer
        void CpuCheck()
        {
            if (!CpuTurn) return; hp = hs = -1; int g = gen;
            CpuThink(Opponents.ThinkTime(Opp), () => CpuFire(g));
        }
        void CpuFire(int g)
        {
            if (g != gen || !CpuTurn) return;
            if (Modal != null) { Tm.After(.3f, () => CpuFire(g)); return; }
            var (p, n) = NimAi.Move(piles, (int)Opp, Rng.Shared); if (p < 0) return;
            // erst markieren (wie die Maus-Vorschau), dann nehmen
            cpuAiming = true; hp = p; hs = piles[p] - n; Sfx.Play(S.Hover, .4f);
            Tm.After(.55f, () => { if (g != gen || !CpuTurn) return; cpuAiming = false; Take(p, piles[p] - n); if (!Locked) MouseMove(App.MX, App.MY); });
        }

        void Take(int p, int k)
        {
            if (over || p < 0 || k < 0 || k >= piles[p]) return;
            int n = piles[p] - k;
            for (int j = k; j < piles[p]; j++) { var r = Rod(p, j); fly.Add(new Fly { X = r.MidX, Y = r.MidY, Vx = (Rng.Shared.NextSingle() - .5f) * 500, Vy = -300 - Rng.Shared.NextSingle() * 300, Vr = (Rng.Shared.NextSingle() - .5f) * 8, Col = C.Gold }); Fx.Burst(r.MidX, r.MidY, 12, new[] { C.Gold, C.Orange, C.Yellow }, 260); }
            piles[p] -= n; Sfx.Play(S.Take); App.Shake(4 + n); hp = hs = -1;
            if (piles.All(v => v == 0))
            {
                over = true; score[cur - 1]++; status = $"{PName(cur - 1)} GEWINNT!"; Sfx.Play(S.Win); Celebrate(cur == 1 ? C.Cyan : C.Pink, 4, 1f, $"{PName(cur - 1)} gewinnt!"); App.Flash(C.Gold, .3f);
                int w = cur, g = gen; Tm.After(3.4f, () => { if (g != gen) return; Result($"{PName(w - 1)} gewinnt!", $"Wer den letzten Stab nimmt, gewinnt.  Stand {score[0]} : {score[1]}", w == 1 ? C.Cyan : C.Pink, ("Nochmal", C.Green, NextRound), ("Menü", C.Purple, () => App.Go(new Menu()))); });
            }
            else { cur = 3 - cur; status = $"{PName(cur - 1)} ist dran"; CpuCheck(); }
        }
        public override void Update(float dt)
        {
            for (int p = 0; p < 3; p++) for (int k = 0; k < 7; k++) pop[p][k] = Math.Min(1, pop[p][k] + dt * 3);
            for (int i = fly.Count - 1; i >= 0; i--) { var f = fly[i]; f.T += dt; f.Vy += 1800 * dt; f.X += f.Vx * dt; f.Y += f.Vy * dt; f.Rot += f.Vr * dt; if (f.T > 1.1f) fly.RemoveAt(i); }
        }
        public override void Draw(Canvas2D c)
        {
            W.PlayerBox(c, Gfx.R(40, 140, 260, 200), PName(0), score[0].ToString(), C.Cyan, !over && cur == 1, Time, "Siege");
            W.PlayerBox(c, Gfx.R(40, 370, 260, 200), PName(1), score[1].ToString(), C.Pink, !over && cur == 2, Time, "Siege");
            Gfx.Text(c, "Klicke einen Stab: er und alle darüber werden genommen.", 800, 120, 26, C.Dim, Al.C, false);
            for (int p = 0; p < 3; p++)
            {
                float x = PX(p); var plate = Gfx.Ctr(x, BASE + 30, RW + 60, 30);
                // warmes Licht von unten auf den Stapel
                Gfx.Light(c, x, BASE + 10, RW * .9f, C.Orange, .10f + .03f * MathF.Sin(Time * 2 + p), 1.4f);
                Gfx.Shadow(c, plate, 12, 10, .5f, 0, 8);
                Gfx.GlowFill(c, plate, 14, C.Orange, 14, .3f); Gfx.RectGrad(c, plate, 12, new Col(80, 50, 20), new Col(30, 16, 8)); Gfx.Stroke(c, plate, 12, C.Orange.A(.7f), 2);
                Gfx.Text(c, $"Stapel {p + 1}", x, BASE + 80, 26, C.Dim, Al.C, true); Gfx.Text(c, piles[p].ToString(), x, BASE + 30, 22, C.Gold, Al.C, true, 4);
                bool cpuSel = hp == p && cpuAiming;
                for (int k = 0; k < piles[p]; k++)
                {
                    float a = Ease.OutBack(pop[p][k]); if (pop[p][k] <= 0) continue; bool hl = hp == p && k >= hs; var r = Rod(p, k);
                    c.Save(); c.Translate(r.MidX, r.MidY); c.Scale(a, a); c.Translate(-r.MidX, -r.MidY);
                    // Markierung: Mensch rot-orange, Computer pink
                    var col = hl ? (cpuSel ? C.Pink.Mix(C.Orange, .25f) : C.Red.Mix(C.Orange, .4f)) : C.Gold;
                    Gfx.Shadow(c, r, 22, 8, .35f, 0, 6);
                    Gfx.Glow(c, r, 22, col, hl ? 16 : 8, hl ? .9f : .35f);
                    Gfx.Rod(c, r, 22, col, 1, hl ? .22f : .3f);
                    var st = Gfx.Line(col.Light(.6f), 2); st.Glow = hl ? 1.8f : 1.15f; c.DrawRoundRect(r, 22, 22, st);
                    for (int q = 0; q < 3; q++) c.DrawCircle(r.Left + 34 + q * 8, r.MidY, 2, Gfx.Fill(col.Dark(.4f)));
                    // wandernder Glanzpunkt
                    float gx = r.Left + 30 + (r.Width - 60) * (.5f + .5f * MathF.Sin(Time * 1.3f + k * .7f + p * 1.9f));
                    Gfx.Light(c, gx, r.Top + 12, 22, Col.White, hl ? .35f : .18f, 1.5f);
                    c.Restore();
                }
            }
            // Hinweis "Nehme N" erst nach allen Stapeln zeichnen, damit er nicht verdeckt wird
            if (hp >= 0 && hp < 3 && hs >= 0 && hs < piles[hp]) { var r0 = Rod(hp, hs); Gfx.Text(c, $"Nehme {piles[hp] - hs}", PX(hp) + RW / 2 + 70, r0.MidY, 26, (hp >= 0 && cpuAiming ? C.Pink : C.Red).Light(.4f), Al.L, true, 8); }
            foreach (var f in fly)
            {
                float a = 1 - Ease.Clamp(f.T / 1.1f); c.Save(); c.Translate(f.X, f.Y); c.RotateDegrees(f.Rot * 57.3f); var r = Gfx.Ctr(0, 0, RW, RHt);
                Gfx.GlowFill(c, r, 22, C.Orange, 12, .4f * a); Gfx.Rod(c, r, 22, f.Col.A(a), 1, .3f); c.Restore();
            }
            Gfx.Text(c, status, 800, 172, 36, over ? C.Gold : (cur == 1 ? C.Cyan : C.Pink), Al.C, true, 8);
        }
    }

    /// <summary>KI fuer Nim (Normalspiel: wer den letzten Stab nimmt, gewinnt). Unity-frei, testbar.</summary>
    public static class NimAi
    {
        public static int Sum(int[] piles) { int x = 0; foreach (var p in piles) x ^= p; return x; }
        /// <summary>Zug (Stapel, Anzahl). level: 1 Leicht, 2 Mittel, 3 Schwer.</summary>
        public static (int pile, int n) Move(int[] piles, int level, Random rnd)
        {
            var ne = Enumerable.Range(0, piles.Length).Where(i => piles[i] > 0).ToList(); if (ne.Count == 0) return (-1, 0);
            if (ne.Count == 1) return (ne[0], piles[ne[0]]);   // letzter Stapel: alles nehmen (auch Leicht)
            bool opt = level >= 3 || level <= 0 || (level == 2 && rnd.NextDouble() < .7) || (level == 1 && rnd.NextDouble() < .2);
            return opt ? Optimal(piles, rnd) : RandomMove(piles, rnd);
        }
        /// <summary>Optimal: Nim-Summe auf 0 bringen; in verlorener Stellung kleiner Zufallszug.</summary>
        public static (int pile, int n) Optimal(int[] piles, Random rnd)
        {
            int x = Sum(piles);
            if (x != 0)
            {
                var c = Enumerable.Range(0, piles.Length).Where(i => (piles[i] ^ x) < piles[i]).ToList(); int i0 = c[rnd.Next(c.Count)];
                return (i0, piles[i0] - (piles[i0] ^ x));
            }
            var ne = Enumerable.Range(0, piles.Length).Where(i => piles[i] > 0).ToList(); int p = ne[rnd.Next(ne.Count)];
            return (p, Math.Min(piles[p], 1 + (rnd.NextDouble() < .3 ? 1 : 0)));
        }
        public static (int pile, int n) RandomMove(int[] piles, Random rnd)
        {
            var ne = Enumerable.Range(0, piles.Length).Where(i => piles[i] > 0).ToList(); int p = ne[rnd.Next(ne.Count)];
            return (p, 1 + rnd.Next(piles[p]));
        }
    }
}
