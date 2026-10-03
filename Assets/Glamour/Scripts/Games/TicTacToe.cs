using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class TicTacToe : Scene
    {
        public override string Title => "Tic Tac Toe";
        public override Col Acc1 => C.Cyan; public override Col Acc2 => C.Pink;
        public override string OppKey => "ttt";
        static readonly int[][] Lines = TttAi.Lines;
        int[] b = new int[9]; float[] pt = new float[9]; int cur = 1, starter = 2, gameIdx; int[] rw = new int[3]; int[] wins = new int[3]; int[] points = new int[3];
        int[] dots = { -1, -1, -1 }; int[] win; bool over, roundEnd; string status = ""; float winT; int hover = -1; bool lockIn;
        // Computer-Gegner: gen verhindert, dass alte Timer in ein neues Spiel feuern
        int gen; bool tossing; int cpuAim = -1; float cpuAimT;
        const float CS = 180, BX = 800 - CS * 1.5f, BY = 190;
        static Box Cell(int i) => Gfx.R(BX + (i % 3) * CS, BY + (i / 3) * CS, CS, CS);
        public override void Enter()
        {
            base.Enter();
            Opponents.AddSwitch(this, OppKey, 40, 680, 260, 70, NewMatch);
            Ui.Add(new Button(40, 780, 260, 62, "Punkte zurück", C.Purple, NewMatch, 22));
            Opponents.Pick(this, OppKey, o => StartMatch());
        }
        void NewMatch() { points = new int[3]; wins = new int[3]; StartMatch(); }
        void StartMatch() { tossing = true; NewRound(); CoinToss.Start(this, f => { tossing = false; starter = 3 - (f + 1); NewRound(); }); }
        void NewRound() { roundEnd = false; rw = new int[3]; dots = new[] { -1, -1, -1 }; gameIdx = 0; starter = 3 - starter; NewGame(); }
        void NewGame()
        {
            gen++; b = new int[9]; pt = new float[9]; win = null; over = false; lockIn = false; winT = 0; cpuAim = -1; hover = -1;
            cur = gameIdx % 2 == 0 ? starter : 3 - starter; status = $"{PName(cur - 1)} ist dran"; CpuCheck();
        }
        bool CpuTurn => VsCpu && cur == 2 && !over && !tossing;
        /// <summary>Mensch darf nicht klicken, solange der Computer am Zug ist oder nachdenkt.</summary>
        bool Locked => CpuTurn || CpuThinking || cpuAim >= 0;
        public override bool WantsHand => hover >= 0 && !Locked;
        public override void MouseMove(float x, float y) { hover = -1; if (over || Modal != null || Locked) return; for (int i = 0; i < 9; i++) if (Cell(i).Contains(x, y) && b[i] == 0) hover = i; }
        public override void MouseUp(float x, float y) { if (Locked) return; if (hover >= 0) Place(hover); }

        // ---------------------------------------------------------------- Computer
        void CpuCheck()
        {
            if (!CpuTurn) return; hover = -1; int g = gen;
            CpuThink(Opponents.ThinkTime(Opp), () => CpuFire(g));
        }
        void CpuFire(int g)
        {
            if (g != gen || !CpuTurn) return;
            if (Modal != null) { Tm.After(.3f, () => CpuFire(g)); return; }   // z.B. Gegnerwahl offen: warten
            int m = TttAi.Move(b, 2, (int)Opp, Rng.Shared); if (m < 0) return;
            cpuAim = m; cpuAimT = 0;                                           // kurz anzeigen, wohin der Computer setzt
            Tm.After(.32f, () => { if (g != gen || !CpuTurn) return; cpuAim = -1; Place(m); if (!Locked) MouseMove(App.MX, App.MY); });
        }

        void Place(int i)
        {
            if (over || b[i] != 0 || lockIn) return;
            b[i] = cur; hover = -1; Sfx.Play(cur == 1 ? S.PlaceX : S.PlaceO); var r = Cell(i); Fx.Burst(r.MidX, r.MidY, 14, new[] { cur == 1 ? C.Cyan : C.Pink }, 200);
            Fx.Ring(r.MidX, r.MidY, cur == 1 ? C.Cyan : C.Pink, 18, 160);
            win = Lines.FirstOrDefault(l => b[l[0]] != 0 && b[l[0]] == b[l[1]] && b[l[1]] == b[l[2]]);
            int g = gen;
            if (win != null)
            {
                over = true; wins[cur]++; status = $"{PName(cur - 1)} gewinnt dieses Spiel!"; Sfx.Play(S.Win); App.Flash(cur == 1 ? C.Cyan : C.Pink, .3f); App.Shake(8);
                foreach (var k in win) { var q = Cell(k); Fx.Burst(q.MidX, q.MidY, 40, new[] { cur == 1 ? C.Cyan : C.Pink, Col.White }, 420); Fx.Explosion(q.MidX, q.MidY, .5f); }
                Fx.Lightning(Cell(win[1]).MidX, -20, Cell(win[1]).MidX, Cell(win[1]).MidY, cur == 1 ? C.Cyan : C.Pink); Celebrate(cur == 1 ? C.Cyan : C.Pink, 2.5f, .5f, $"{PName(cur - 1)} gewinnt!"); Sfx.Play(S.Boom, .5f);
                int w = cur; Tm.After(1.7f, () => { if (g == gen) Advance(w); });
            }
            else if (Full() || EarlyDraw())
            {
                over = true; wins[0]++; status = "UNENTSCHIEDEN - zählt nicht für die Runde"; Sfx.Play(S.Lose); Tm.After(1.5f, () => { if (g == gen) Advance(0); });
            }
            else { cur = 3 - cur; status = $"{PName(cur - 1)} ist dran"; CpuCheck(); }
        }
        bool Full() => b.All(v => v != 0);
        bool EarlyDraw() => TttAi.Dead(b);
        void Advance(int w)
        {
            if (w > 0) rw[w]++; dots[gameIdx] = w; gameIdx++;
            if (rw[1] >= 2 || rw[2] >= 2 || gameIdx >= 3) FinishRound(); else NewGame();
        }
        void FinishRound()
        {
            int w = rw[1] > rw[2] ? 1 : rw[2] > rw[1] ? 2 : 0; roundEnd = true; int g = gen;
            if (w > 0)
            {
                points[w] += 8; Sfx.Play(S.Big); Celebrate(w == 1 ? C.Cyan : C.Pink, 5, 1.1f); App.Shake(10);
                Action show = () => Result($"{PName(w - 1)} gewinnt die RUNDE!", $"Ergebnis {rw[1]}:{rw[2]}  -  +8 Punkte", w == 1 ? C.Cyan : C.Pink, ("Nächste Runde", C.Green, () => { roundEnd = false; NewRound(); }), ("Menü", C.Purple, () => App.Go(new Menu())));
                // Highscore nur fuer Menschen (der Computer traegt sich nicht ein)
                bool human = !(VsCpu && w == 2);
                Tm.After(3f, () => { if (g != gen) return; if (human && Save.IsHigh("hs_ttt", points[w])) NameEntry("hs_ttt", points[w], "NEUER HIGHSCORE!", show); else show(); });
            }
            else Result("Runde unentschieden", $"Ergebnis {rw[1]}:{rw[2]}", C.Gold, ("Nächste Runde", C.Green, () => { roundEnd = false; NewRound(); }), ("Menü", C.Purple, () => App.Go(new Menu())));
        }
        public override void Update(float dt)
        {
            for (int i = 0; i < 9; i++) if (b[i] != 0) pt[i] = Math.Min(1, pt[i] + dt * 3.2f);
            if (win != null) winT += dt;
            if (cpuAim >= 0) cpuAimT += dt;
        }
        public override void Draw(Canvas2D c)
        {
            W.PlayerBox(c, Gfx.R(40, 130, 260, 200), PName(0), points[1].ToString(), C.Cyan, !over && cur == 1, Time, $"Siege {wins[1]}");
            W.PlayerBox(c, Gfx.R(40, 360, 260, 200), PName(1), points[2].ToString(), C.Pink, !over && cur == 2, Time, $"Siege {wins[2]}");
            Gfx.Text(c, $"Unentschieden: {wins[0]}", 170, 600, 24, C.Dim, Al.C, false); Gfx.Text(c, "Rundenpunkte", 170, 640, 22, C.Dim, Al.C, false);
            HighscoreList(c, "hs_ttt", 1320, 150, 250, C.Cyan);
            for (int i = 0; i < 3; i++)
            {
                float x = 800 + (i - 1) * 60, y = 150; var col = dots[i] == 1 ? C.Cyan : dots[i] == 2 ? C.Pink : dots[i] == 0 ? C.Gold : C.Dim.A(.4f);
                if (dots[i] >= 0) Gfx.Light(c, x, y, 34, col, .35f);
                c.DrawCircle(x, y, 16, Gfx.Fill(col.A(dots[i] >= 0 ? 1 : .25f))); c.DrawCircle(x, y, 16, Gfx.Line(col, 2.5f)); if (i == gameIdx && !roundEnd) Gfx.Glow(c, Gfx.Ctr(x, y, 32, 32), 16, Col.White, 6, .5f + .4f * MathF.Sin(Time * 5));
            }
            var br = Gfx.R(BX - 14, BY - 14, CS * 3 + 28, CS * 3 + 28); Gfx.Glow(c, br, 28, C.Cyan, 20, .25f); W.Panel(c, br, C.Cyan, 28);
            // leichter Lichtschein in der Brettmitte
            Gfx.Radial(c, 800, BY + CS * 1.5f, CS * 1.7f, C.Cyan, .07f);
            var gl = Gfx.Line(C.Cyan.A(.5f), 12); gl.Blur = 8; gl.Glow = 1.7f;
            var lp = Gfx.Line(C.Cyan.Light(.25f).A(.9f), 6); lp.Glow = 1.35f;
            for (int k = 1; k < 3; k++)
            {
                c.DrawLine(BX + k * CS, BY + 14, BX + k * CS, BY + 3 * CS - 14, gl); c.DrawLine(BX + k * CS, BY + 14, BX + k * CS, BY + 3 * CS - 14, lp);
                c.DrawLine(BX + 14, BY + k * CS, BX + 3 * CS - 14, BY + k * CS, gl); c.DrawLine(BX + 14, BY + k * CS, BX + 3 * CS - 14, BY + k * CS, lp);
            }
            for (int i = 0; i < 9; i++)
            {
                var r = Cell(i);
                if (i == hover && !Locked && b[i] == 0) { Gfx.Rect(c, Gfx.Inflate(r, -12), 16, (cur == 1 ? C.Cyan : C.Pink).A(.12f)); DrawMark(c, cur, r.MidX, r.MidY, 1, .25f); }
                if (i == cpuAim && b[i] == 0)
                {
                    // Zielanzeige des Computers: Feld leuchtet kurz auf, Zeichen blendet ein
                    float k = Ease.OutCubic(cpuAimT / .25f); var ir = Gfx.Inflate(r, -12);
                    Gfx.Rect(c, ir, 16, C.Pink.A(.16f * k)); Gfx.Glow(c, ir, 16, C.Pink, 10, .5f * k); DrawMark(c, 2, r.MidX, r.MidY, 1, .3f * k);
                }
                if (b[i] != 0) DrawMark(c, b[i], r.MidX, r.MidY, Ease.OutCubic(pt[i]), 1, win != null && win.Contains(i) ? .5f + .5f * MathF.Sin(winT * 10) : 0);
            }
            if (win != null)
            {
                var a = Cell(win[0]); var z = Cell(win[2]); float p = Ease.OutCubic(winT / .5f); var col = (b[win[0]] == 1 ? C.Cyan : C.Pink);
                var e = new Pt(a.MidX + (z.MidX - a.MidX) * p, a.MidY + (z.MidY - a.MidY) * p);
                var g = Gfx.Line(col, 26); g.Blur = 14; g.Glow = 2.2f; c.DrawLine(a.MidX, a.MidY, e.X, e.Y, g);
                var wc = Gfx.Line(Col.White, 7); wc.Glow = 1.6f; c.DrawLine(a.MidX, a.MidY, e.X, e.Y, wc);
                Gfx.Light(c, e.X, e.Y, 70, col, .5f, 2f);
            }
            Gfx.Text(c, status, 800, 838, 34, over && win != null ? (b[win[0]] == 1 ? C.Cyan : C.Pink) : Col.White, Al.C, true, 8);
        }
        static void DrawMark(Canvas2D c, int who, float cx, float cy, float p, float alpha = 1, float glow = 0)
        {
            var col = who == 1 ? C.Cyan : C.Pink; float s = 52;
            c.Save(); c.Translate(cx, cy); float sc = 1 + glow * .08f; c.Scale(sc, sc);
            // Lichthof unter dem Zeichen (additiv, HDR)
            if (alpha > .5f) Gfx.Light(c, 0, 0, s * 1.9f, col, (.16f + glow * .2f) * alpha * p);
            if (who == 1)
            {
                float p1 = Ease.Clamp(p * 2), p2 = Ease.Clamp(p * 2 - 1);
                for (int pass = 0; pass < 2; pass++)
                {
                    var pa = Pen(pass, col, alpha, glow);
                    c.DrawLine(-s, -s, -s + 2 * s * p1, -s + 2 * s * p1, pa);
                    if (p2 > 0) { var pb = Pen(pass, col, alpha, glow); c.DrawLine(s, -s, s - 2 * s * p2, -s + 2 * s * p2, pb); }
                }
            }
            else
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    var pa = Pen(pass, col, alpha, glow);
                    c.DrawArc(new Box(-s, -s, s, s), -90, 360 * Math.Max(.001f, Math.Min(.9999f, p)), false, pa);
                }
            }
            c.Restore();
        }
        /// <summary>Pass 0 = weicher Neon-Schein (HDR), Pass 1 = heller Kern.</summary>
        static Paint Pen(int pass, Col col, float alpha, float glow)
        {
            if (pass == 0) { var p = Gfx.Line(col.A(alpha * .9f), 24); p.Blur = 10; p.Glow = 1.8f + glow * .8f; return p; }
            var q = Gfx.Line(Col.White.Mix(col, .15f).A(alpha), 9); q.Glow = 1.3f + glow * .4f; return q;
        }
    }

    /// <summary>KI fuer Tic Tac Toe (Unity-frei, testbar). Brett: 0 leer, 1/2 Spieler.</summary>
    public static class TttAi
    {
        public static readonly int[][] Lines = { new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, new[] { 0, 4, 8 }, new[] { 2, 4, 6 } };
        const sbyte Unknown = sbyte.MinValue;
        static readonly sbyte[] memo = Enumerable.Repeat(Unknown, 19683 * 2).ToArray();
        static readonly object gate = new object();

        public static int Winner(int[] b) { foreach (var l in Lines) if (b[l[0]] != 0 && b[l[0]] == b[l[1]] && b[l[1]] == b[l[2]]) return b[l[0]]; return 0; }
        /// <summary>Keine Reihe kann mehr gewonnen werden (fruehes Unentschieden wie im Original).</summary>
        public static bool Dead(int[] b) => Lines.All(l => { bool x = false, o = false; foreach (var i in l) { if (b[i] == 1) x = true; if (b[i] == 2) o = true; } return x && o; });
        static int Key(int[] b) { int k = 0; for (int i = 0; i < 9; i++) k = k * 3 + b[i]; return k; }

        /// <summary>Negamax-Wert fuer p am Zug: +10-n = Sieg in n Halbzuegen (schneller Sieg besser), 0 = remis.</summary>
        static int Value(int[] b, int p)
        {
            if (Winner(b) != 0) return -10;                     // der Gegner hat gerade gewonnen
            if (Dead(b) || b.All(v => v != 0)) return 0;
            int key = Key(b) * 2 + p - 1; if (memo[key] != Unknown) return memo[key];
            int best = -100;
            for (int i = 0; i < 9; i++) if (b[i] == 0) { b[i] = p; int v = -Value(b, 3 - p); b[i] = 0; if (v > best) best = v; }
            best = best > 0 ? best - 1 : best < 0 ? best + 1 : 0;
            memo[key] = (sbyte)best; return best;
        }
        /// <summary>Bewertung jedes freien Felds aus Sicht von p (int.MinValue = belegt).</summary>
        public static int[] Scores(int[] board, int p)
        {
            var b = (int[])board.Clone(); var s = new int[9];
            lock (gate) for (int i = 0; i < 9; i++) { if (b[i] != 0) { s[i] = int.MinValue; continue; } b[i] = p; s[i] = -Value(b, 3 - p); b[i] = 0; }
            return s;
        }
        static int Finishing(int[] b, int p)
        {
            for (int i = 0; i < 9; i++) if (b[i] == 0) { b[i] = p; bool w = Winner(b) == p; b[i] = 0; if (w) return i; }
            return -1;
        }
        static int Pick(List<int> l, Random rnd) => l.Count == 0 ? -1 : l[rnd.Next(l.Count)];

        /// <summary>Zug fuer Spieler p. level: 1 Leicht, 2 Mittel, 3 Schwer (sonst Schwer).</summary>
        public static int Move(int[] board, int p, int level, Random rnd)
        {
            var b = (int[])board.Clone(); var free = Enumerable.Range(0, 9).Where(i => b[i] == 0).ToList(); if (free.Count == 0) return -1;
            int winM = Finishing(b, p), block = Finishing(b, 3 - p);
            if (level == 1)
            {
                // Leicht: gewinnt meistens, blockt nur manchmal, sonst zufaellig
                if (winM >= 0 && rnd.NextDouble() < .85) return winM;
                if (block >= 0 && rnd.NextDouble() < .4) return block;
                return Pick(free, rnd);
            }
            var s = Scores(b, p); int max = free.Max(i => s[i]);
            var best = free.Where(i => s[i] == max).ToList();
            if (level == 2)
            {
                // Mittel: gewinnt und blockt immer, sonst ~30 % ein schwaecherer Zug
                if (winM >= 0) return winM;
                if (block >= 0) return block;
                var worse = free.Where(i => s[i] < max).ToList();
                if (worse.Count > 0 && rnd.NextDouble() < .3) return Pick(worse, rnd);
                return Pick(best, rnd);
            }
            return Pick(best, rnd);   // Schwer: perfektes Minimax, schnellster Sieg
        }
    }
}
