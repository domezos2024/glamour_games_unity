using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class Battleship : Scene
    {
        public override string Title => "Schiffe Versenken";
        public override Col Acc1 => C.Blue; public override Col Acc2 => C.Cyan;
        public override string OppKey => "ships";
        enum Ph { Setup, Pass, Attack, Over }
        class Ship { public int Size; public string Name; public List<int> Cells = new List<int>(); public bool Horiz = true, Sunk; }
        static readonly (int, string)[] Def = { (4, "Schlachtschiff"), (3, "Kreuzer"), (3, "Kreuzer"), (2, "Zerstörer") };
        const int N = 8;
        Ph ph = Ph.Setup; int cur, sel = -1; bool horiz = true; List<Ship>[] fleet = new List<Ship>[2]; HashSet<int>[] shots = { new HashSet<int>(), new HashSet<int>() }; int[] score = new int[2];
        readonly List<Button> shipBtns = new List<Button>(); string status = ""; int hover = -1; int passTo; Action passAfter; bool busy; readonly Dictionary<int, float> mark = new Dictionary<int, float>(); readonly Dictionary<Ship, float> sink = new Dictionary<Ship, float>(); readonly Dictionary<Ship, bool> sunkFx = new Dictionary<Ship, bool>();
        const float GS = 76, GX = 150, GY = 190, MS = 40, MX0 = 1080, MY0 = 236;
        static Box Cell(int i, float gx = GX, float gy = GY, float s = GS) => Gfx.R(gx + (i % N) * s, gy + (i / N) * s, s, s);
        // Computer: Gegner-Button, Generationszaehler, Fadenkreuz-Ziel
        Button bOpp; int gen; int cpuAim = -1; float aimX = GX + N * GS / 2, aimY = GY + N * GS / 2;
        bool CpuTurn => VsCpu && cur == 1 && ph == Ph.Attack;
        public override Pt ThinkPos => new Pt(GX + N * GS / 2, 858);
        public override void Enter()
        {
            base.Enter();
            bOpp = Opponents.AddSwitch(this, OppKey, 1150, 22, 260, 70, NewMatch);
            NewGame();
            Opponents.Pick(this, OppKey, o => NewMatch());
        }
        int starter;
        void NewMatch() { score = new int[2]; starter = 0; NewGame(); CoinToss.Start(this, f => starter = f); }
        void NextRound() { starter = 1 - starter; NewGame(); }
        void NewGame()
        {
            gen++; CancelCpuThink(); cpuAim = -1; parade = null; resShown = false; fleet = new[] { Mk(), Mk() }; shots = new[] { new HashSet<int>(), new HashSet<int>() }; cur = 0; ph = Ph.Setup; sel = -1; horiz = true; mark.Clear(); sink.Clear(); sunkFx.Clear(); Fx.Clear(); Modal = null; busy = false;
            if (VsCpu) PlaceRandom(1);   // Computer-Flotte zufaellig und verdeckt
            BuildUi(); status = $"{PName(0)}: Schiffe platzieren";
        }
        static List<Ship> Mk() => Def.Select(d => new Ship { Size = d.Item1, Name = d.Item2 }).ToList();
        void BuildUi()
        {
            Ui.Clear(); Ui.Add(Back); Ui.Add(bOpp); shipBtns.Clear();
            if (ph == Ph.Setup)
            {
                var f = fleet[cur];
                for (int i = 0; i < f.Count; i++) { int k = i; var b = Ui.Add(new Button(1020, 190 + i * 84, 420, 68, $"{f[i].Name} ({f[i].Size})", f[i].Cells.Count > 0 ? C.Green : C.Cyan, () => { sel = k; horiz = f[k].Horiz; }, 26)); shipBtns.Add(b); }
                Ui.Add(new Button(1020, 550, 200, 62, "Drehen", C.Purple, () => horiz = !horiz, 24));
                Ui.Add(new Button(1240, 550, 200, 62, "Zufällig", C.Orange, Randomize, 24));
                Ui.Add(new Button(1020, 640, 420, 76, "Fertig", C.Green, Done, 32) { Enabled = fleet[cur].All(s => s.Cells.Count > 0) });
            }
            else if (ph == Ph.Pass) Ui.Add(new Button(600, 610, 400, 90, "Bereit", C.Green, () => { var a = passAfter; passAfter = null; cur = passTo; a?.Invoke(); }, 36));
            else if (ph == Ph.Attack) Ui.Add(new Button(1020, 760, 420, 62, "Aufgeben / Neues Spiel", C.Red, NewMatch, 22));
        }
        bool Occ(int p, int cell, Ship except = null) => fleet[p].Any(s => s != except && s.Cells.Contains(cell));
        static List<int> Footprint(int cell, int size, bool h) => BattleAI.Footprint(cell, size, h);
        bool CanPlace(int cell, Ship s) { var fp = Footprint(cell, s.Size, horiz); return fp != null && fp.All(x => !Occ(cur, x, s)); }
        void Place(int cell)
        {
            if (sel < 0) { var s = fleet[cur].FirstOrDefault(x => x.Cells.Contains(cell)); if (s != null) { sel = fleet[cur].IndexOf(s); horiz = s.Horiz; s.Cells.Clear(); Sfx.Play(S.Take); BuildUi(); } return; }
            var sh = fleet[cur][sel];
            if (!CanPlace(cell, sh)) { App.Toast(Footprint(cell, sh.Size, horiz) == null ? "Schiff passt nicht aufs Feld" : "Position belegt"); Sfx.Play(S.NoMatch, .5f); return; }
            sh.Cells = Footprint(cell, sh.Size, horiz); sh.Horiz = horiz; Sfx.Play(S.Drop); var r = Cell(cell); Fx.Ring(r.MidX, r.MidY, C.Cyan, 20, 200); Fx.Splash(r.MidX, r.MidY, .5f);
            sel = fleet[cur].FindIndex(x => x.Cells.Count == 0); BuildUi();
        }
        /// <summary>Gueltige Zufallsflotte fuer Platz p (Regeln wie im Original: keine Ueberlappung).</summary>
        void PlaceRandom(int p)
        {
            var fl = BattleAI.RandomFleet(fleet[p].Select(s => s.Size).ToArray(), Rng.Shared);
            for (int i = 0; i < fleet[p].Count; i++) { fleet[p][i].Cells = fl[i].cells; fleet[p][i].Horiz = fl[i].horiz; }
        }
        void Randomize() { PlaceRandom(cur); sel = -1; Sfx.Play(S.Dice, .7f); BuildUi(); }
        void Done()
        {
            if (VsCpu) { StartAttack(starter); return; }   // gegen den Computer kein Platztausch
            if (cur == 0) ToPass(1, () => { ph = Ph.Setup; sel = -1; status = $"{PName(1)}: Schiffe platzieren"; BuildUi(); });
            else ToPass(starter, () => { ph = Ph.Attack; status = $"{PName(starter)} - Feuer frei!"; BuildUi(); });
        }
        void StartAttack(int who) { cur = who; ph = Ph.Attack; hover = -1; status = $"{PName(who)} - Feuer frei!"; BuildUi(); Sfx.Play(S.Turn); CpuMaybe(); }
        void ToPass(int to, Action after) { passTo = to; passAfter = after; ph = Ph.Pass; hover = -1; BuildUi(); Sfx.Play(S.Turn); }
        public override bool WantsHand => hover >= 0;
        public override void MouseMove(float x, float y)
        {
            hover = -1; if (Modal != null || busy || CpuTurn || CpuThinking) return;
            if (ph == Ph.Setup || ph == Ph.Attack) for (int i = 0; i < 64; i++) if (Cell(i).Contains(x, y)) hover = i;
        }
        public override void KeyDown(Key k) { if (k == Key.R && ph == Ph.Setup) horiz = !horiz; }
        public override void Wheel(float d) { if (ph == Ph.Setup) horiz = !horiz; }
        public override void MouseUp(float x, float y)
        {
            if (parade != null && parade.T > 1.5f) { FireRes(); return; }
            if (hover < 0 || CpuTurn || CpuThinking) return;
            if (ph == Ph.Setup) Place(hover); else if (ph == Ph.Attack) Shoot(hover);
        }
        void Shoot(int cell)
        {
            if (busy || shots[cur].Contains(cell)) return;
            int p = cur, o = 1 - p; shots[p].Add(cell); mark[cell + p * 100] = 0; var r = Cell(cell);
            var hit = fleet[o].FirstOrDefault(s => s.Cells.Contains(cell));
            if (hit != null)
            {
                Sfx.Play(S.Hit); Sfx.Play(S.Boom, .7f, .9f + Rng.F() * .3f); Tm.After(.25f, () => Sfx.Play(S.Crackle, .4f)); App.Shake(11); App.Flash(C.Orange, .22f); Fx.Explosion(r.MidX, r.MidY, 1f); Fx.Burst(r.MidX, r.MidY, 24, new[] { C.Orange, C.Yellow, C.Red }, 380, 0, 200); Pop("BUMM!", r.MidX, r.MidY - 30, C.Yellow, 44);
                if (hit.Cells.All(shots[p].Contains))
                {
                    hit.Sunk = true; sink[hit] = 0; Sfx.Play(S.Sunk); Sfx.Play(S.Creak, .8f); Tm.After(.7f, () => Sfx.Play(S.Gurgle, .8f)); App.Flash(C.Orange, .35f); App.Shake(16);
                    for (int ci = 0; ci < hit.Cells.Count; ci++) { var q = Cell(hit.Cells[ci]); Tm.After(ci * .2f, () => { Fx.Explosion(q.MidX, q.MidY, 1.3f); Sfx.Play(S.Boom, .6f, .8f + Rng.F() * .4f); App.Shake(9); }); }
                    Tm.After(.9f, () => Fx.Lightning(r.MidX, -20, r.MidX, r.MidY, C.Orange));
                    Pop("VERSENKT!", 480, 140, C.Orange, 60);
                    if (fleet[o].All(s => s.Sunk)) { Win(p); return; }
                    status = "VERSENKT! Nochmal!";
                }
                else status = "TREFFER! Nochmal!";
                CpuMaybe();   // Treffer: der Computer schiesst weiter
            }
            else
            {
                Sfx.Play(S.Miss, .5f); Sfx.Play(S.Splash, .9f, .9f + Rng.F() * .3f); Fx.Splash(r.MidX, r.MidY, 1.1f); Tm.After(.28f, () => { Sfx.Play(S.Blub, .7f); Fx.Bubbles(r.MidX, r.MidY, 6, 14); Pop("BLUBB!", r.MidX, r.MidY - 26, C.Blue.Light(.55f), 34); }); status = "Wasser - Wechsel"; busy = true;
                int g = gen;
                Tm.After(1.5f, () =>
                {
                    if (g != gen) return; busy = false;
                    if (VsCpu) StartAttack(o);
                    else ToPass(o, () => { ph = Ph.Attack; status = $"{PName(o)} - Feuer frei!"; BuildUi(); });
                });
            }
        }
        // ---------------------------------------------------------------- Computer-Zug
        void CpuMaybe()
        {
            if (!CpuTurn || busy) return;
            int g = gen, t = BattleAI.Shot(CpuView(), fleet[0].Where(s => !s.Sunk).Select(s => s.Size).ToList(), (int)Opp, Rng.Shared);
            if (t < 0) return;
            cpuAim = t;
            CpuThink(Opponents.ThinkTime(Opp), () => { if (g != gen || !CpuTurn) return; cpuAim = -1; Shoot(t); });
        }
        /// <summary>Was der Computer ueber die Flotte des Menschen weiss (unbekannt/Wasser/Treffer/versenkt).</summary>
        int[] CpuView()
        {
            var st = new int[N * N];
            foreach (var i in shots[1]) { var s = fleet[0].FirstOrDefault(x => x.Cells.Contains(i)); st[i] = s == null ? BattleAI.Miss : s.Sunk ? BattleAI.Sunk : BattleAI.Hit; }
            return st;
        }
        DiceParade parade; bool resShown; Action showRes;
        public override void DebugWin() { Modal = null; parade = new DiceParade(this, 1, new[] { C.Cyan, C.Pink }, new[] { PName(0), PName(1) }, new[] { 200, 150 }, PKind.Sailor, 1.35f); }
        void FireRes() { if (resShown) return; resShown = true; showRes?.Invoke(); }
        void Win(int p)
        {
            ph = Ph.Over; score[p]++; Sfx.Play(S.Big); App.Shake(12); busy = true; cpuAim = -1; int g = gen;
            Tm.After(2.2f, () =>
            {
                if (ph != Ph.Over || g != gen) return;
                parade = new DiceParade(this, p, new[] { C.Cyan, C.Pink }, new[] { PName(0), PName(1) }, new[] { score[0], score[1] }, PKind.Sailor, 1.35f);
                showRes = () => { busy = false; Result($"{PName(p)} GEWINNT!", $"Alle Schiffe von {PName(1 - p)} versenkt", p == 0 ? C.Cyan : C.Pink, new List<string> { $"Stand: {score[0]} : {score[1]}" }, ("Nochmal", C.Green, NextRound), ("Menü", C.Purple, () => App.Go(new Menu()))); };
            });
        }
        public override void Update(float dt)
        {
            parade?.Update(dt); if (parade != null && parade.T > parade.Total) FireRes();
            foreach (var k in mark.Keys.ToList()) mark[k] += dt;
            foreach (var sh in sink.Keys.ToList())
            {
                float st = sink[sh] + dt; sink[sh] = st;
                if (st < 3.2f) { var r0 = Cell(sh.Cells[0]); var r1 = Cell(sh.Cells[sh.Cells.Count - 1]); float mx = (r0.MidX + r1.MidX) / 2, my = (r0.MidY + r1.MidY) / 2; if (Rng.F() < dt * 14) Fx.Bubbles(mx + (Rng.F() - .5f) * sh.Size * GS * (sh.Horiz ? .8f : .1f), my + (Rng.F() - .5f) * sh.Size * GS * (sh.Horiz ? .1f : .8f), 1, 10); if (Rng.F() < dt * 3) Fx.Ripple(mx, my, 60 + 30 * sh.Size); if (st < 1.8f && Rng.F() < dt * 5) Fx.Smoke(mx, my, 1, 16, 55, 1.6f); }
                else if (!sunkFx.ContainsKey(sh)) { sunkFx[sh] = true; var r0 = Cell(sh.Cells[0]); var r1 = Cell(sh.Cells[sh.Cells.Count - 1]); float mx = (r0.MidX + r1.MidX) / 2, my = (r0.MidY + r1.MidY) / 2; Fx.Splash(mx, my, 1.8f); Fx.Bubbles(mx, my, 14, 30); Sfx.Play(S.Splash, 1f, .7f); Sfx.Play(S.Blub, .8f, .7f); Pop("BLUBB... BLUBB...", mx, my - 40, C.Blue.Light(.6f), 34); }
            }
            if (ph == Ph.Attack)
            {
                int o2 = 1 - cur; bool any = false;
                foreach (var i in shots[cur]) { var sh = fleet[o2].FirstOrDefault(x => x.Cells.Contains(i)); if (sh == null) continue; if (sh.Sunk && (!sink.TryGetValue(sh, out var st2) || st2 > 2.2f)) continue; any = true; var r = Cell(i); if (Rng.F() < dt * 2.6f) Fx.Smoke(r.MidX, r.MidY - 8, 1, 12, 55, 1.6f); if (Rng.F() < dt * 9) Fx.Flames(r.MidX, r.MidY + 8, 1, 9); }
                if (any && Rng.F() < dt * .8f) Sfx.Play(S.Crackle, .18f);
            }
            if (Rng.F() < dt * 8 && ph == Ph.Attack) { var r = Cell(Rng.I(64)); Fx.Spark(r.MidX, r.MidY, C.Cyan.A(.5f), 1, 12); }
            // Fadenkreuz des Computers gleitet zum Ziel
            if (cpuAim >= 0) { var r = Cell(cpuAim); float k = Math.Min(1, dt * 5); aimX += (r.MidX - aimX) * k; aimY += (r.MidY - aimY) * k; }
        }
        public override void Draw(Canvas2D c) { DrawBoard(c); parade?.Draw(c); }
        void DrawBoard(Canvas2D c)
        {
            if (ph == Ph.Pass) { DrawPass(c); return; }
            Gfx.Text(c, status, 800, 108, 34, ph == Ph.Attack ? (CpuTurn ? C.Pink : C.Cyan) : C.Gold, Al.C, true, 10);
            if (ph == Ph.Setup) DrawSetup(c); else DrawAttack(c);
        }
        void DrawPass(Canvas2D c)
        {
            var r = Gfx.Ctr(800, 450, 900, 420); Gfx.Glow(c, r, 30, C.Cyan, 26, .5f); W.Panel(c, r, C.Cyan, 30);
            Gfx.Text(c, PName(passTo), 800, 330, 84, passTo == 0 ? C.Cyan : C.Pink, Al.C, true, 24, true);
            Gfx.Text(c, "Platz tauschen - der andere Spieler schaut weg!", 800, 430, 30, Col.White, Al.C, false);
            Gfx.Text(c, "Erst wenn du bereit bist, auf \"Bereit\" klicken.", 800, 480, 24, C.Dim, Al.C, false);
        }
        void Water(Canvas2D c, float gx, float gy, float s, bool labels)
        {
            var r = Gfx.R(gx - 8, gy - 8, N * s + 16, N * s + 16); Gfx.Shadow(c, r, 14, 16, .5f, 0, 12); Gfx.Glow(c, r, 14, C.Cyan, 16, .35f);
            Gfx.RectGrad(c, r, 14, new Col(0, 60, 100), new Col(0, 12, 34)); c.Save(); c.ClipRoundRect(r, 14);
            // wandernde Lichtflecken (Kaustik) und Wellenlinien
            for (int k = 0; k < 4; k++) { float lx = gx + N * s * (.5f + .42f * MathF.Sin(Time * .23f + k * 1.7f)), ly = gy + N * s * (.5f + .42f * MathF.Cos(Time * .19f + k * 2.3f)); Gfx.Light(c, lx, ly, s * 2.6f, C.Cyan, .05f, 1.2f); }
            for (int k = 0; k < 9; k++) { float y = gy + k * s * N / 8 - 8; using var wp = new Path2D(); wp.MoveTo(gx - 10, y); for (float x = gx - 10; x < gx + N * s + 10; x += 12) wp.LineTo(x, y + MathF.Sin(x * .03f + Time * 1.4f + k) * 4); var lp = Gfx.Line(C.Cyan.A(.07f), 2); lp.Glow = 1.3f; c.DrawPath(wp, lp); }
            c.Restore(); var bp = Gfx.Line(C.Cyan.A(.8f), 2.5f); bp.Glow = 1.6f; c.DrawRoundRect(r, 14, 14, bp);
            var gl = Gfx.Line(C.Cyan.A(.22f), 1.5f); gl.Glow = 1.2f; for (int k = 0; k <= N; k++) { c.DrawLine(gx + k * s, gy, gx + k * s, gy + N * s, gl); c.DrawLine(gx, gy + k * s, gx + N * s, gy + k * s, gl); }
            if (labels) for (int k = 0; k < N; k++) { Gfx.Text(c, ((char)('A' + k)).ToString(), gx + k * s + s / 2, gy - 22, 20, C.Cyan.Light(.4f)); Gfx.Text(c, (k + 1).ToString(), gx - 24, gy + k * s + s / 2, 20, C.Cyan.Light(.4f)); }
        }
        static Col Ca(int r, int g, int b, float a) => new Col(r, g, b, (int)(255 * a));
        static void Hull(Canvas2D c, float x, float y, int len, float s, bool horiz, float alpha, float red, float t)
        {
            float L = len * s, W = s;
            c.Save(); if (horiz) c.Translate(x, y); else { c.Translate(x + s, y); c.RotateDegrees(90); }
            var sh = Gfx.Fill(Col.Black.A(.4f * alpha)); sh.Blur = 5; c.DrawRoundRect(Gfx.R(4, W * .3f, L - 6, W * .6f), 8, 8, sh);
            using var hp = new Path2D(); hp.MoveTo(W * .1f, W * .18f); hp.LineTo(L - W * .55f, W * .12f); hp.CubicTo(L - W * .1f, W * .2f, L - W * .05f, W * .4f, L - W * .03f, W * .5f);
            hp.CubicTo(L - W * .05f, W * .6f, L - W * .1f, W * .8f, L - W * .55f, W * .88f); hp.LineTo(W * .1f, W * .82f); hp.Close();
            var p = Gfx.Fill(Col.White.A(alpha)); p.Shader = Grad.Linear(0, W * .1f, 0, W * .9f, new Col(170, 190, 215).Mix(C.Red, red * .55f).A(alpha), new Col(80, 96, 124).Mix(C.Red, red * .55f).A(alpha)); c.DrawPath(hp, p); c.DrawPath(hp, Gfx.Line(Ca(30, 40, 60, alpha), 2));
            var deck = Gfx.R(W * .22f, W * .3f, L - W * .95f, W * .4f); Gfx.Rect(c, deck, 5, Ca(120, 135, 160, alpha).Mix(C.Red.A(alpha), red * .55f));
            int towers = Math.Max(1, len - 1);
            for (int k = 0; k < towers; k++) { float tx = W * .35f + k * (L - W * 1.3f) / Math.Max(1, towers - 1 == 0 ? 1 : towers - 1); if (towers == 1) tx = L * .4f; var tr = Gfx.Ctr(tx, W * .5f, W * .3f, W * .3f); Gfx.RectGrad(c, tr, 4, Ca(220, 230, 245, alpha), Ca(100, 115, 140, alpha)); if (k % 2 == 0) c.DrawLine(tx, W * .5f, tx + W * .28f, W * .5f, Gfx.Line(Ca(40, 50, 70, alpha), 3.5f)); }
            // Positionslichter am Bug
            if (red < .5f) Gfx.Light(c, L - W * .12f, W * .5f, W * .22f, C.Cyan, .45f * alpha * (.7f + .3f * MathF.Sin(t * 3 + len)), 1.8f);
            c.Restore();
        }
        void DrawFire(Canvas2D c, float cx, float cy, float s, float t, int seed)
        {
            float f = .75f + .25f * MathF.Sin(t * 14 + seed);
            Gfx.Light(c, cx, cy, s * .9f, C.Orange, .7f * f, 1.7f); Gfx.Light(c, cx, cy - s * .08f, s * .5f, C.Yellow, .8f, 2f); Gfx.Ball(c, cx, cy, s * .12f, C.White);
            using var fl = new Path2D(); fl.MoveTo(cx - s * .2f, cy + s * .2f); fl.CubicTo(cx - s * .3f, cy - s * .1f, cx - s * .05f, cy - s * .2f, cx, cy - s * (.4f + .08f * MathF.Sin(t * 11 + seed))); fl.CubicTo(cx + s * .05f, cy - s * .2f, cx + s * .3f, cy - s * .1f, cx + s * .2f, cy + s * .2f); fl.Close();
            var pp = Gfx.Fill(C.Orange.A(.85f)); pp.Additive = true; pp.Glow = 1.8f; c.DrawPath(fl, pp);
        }
        void DrawSetup(Canvas2D c)
        {
            Water(c, GX, GY, GS, true);
            foreach (var s in fleet[cur]) if (s.Cells.Count > 0) { var r = Cell(s.Cells[0]); Hull(c, r.Left + 3, r.Top + 3, s.Size, GS - 6, s.Horiz, 1, 0, Time); }
            if (hover >= 0 && sel >= 0)
            {
                var sh = fleet[cur][sel]; bool ok = CanPlace(hover, sh); var fp = Footprint(hover, sh.Size, horiz) ?? new List<int> { hover };
                foreach (var i in fp) { var r = Cell(i); Gfx.Rect(c, Gfx.Inflate(r, -3), 8, (ok ? C.Green : C.Red).A(.35f)); Gfx.Glow(c, Gfx.Inflate(r, -3), 8, ok ? C.Green : C.Red, 6, .4f); }
                if (ok) { var r = Cell(hover); Hull(c, r.Left + 3, r.Top + 3, sh.Size, GS - 6, horiz, .6f, 0, Time); }
            }
            Gfx.Text(c, $"Flotte von {PName(cur)}", 1230, 158, 28, cur == 0 ? C.Cyan : C.Pink, Al.C, true, 8);
            for (int i = 0; i < shipBtns.Count; i++) { shipBtns[i].Selected = sel == i; shipBtns[i].Col = fleet[cur][i].Cells.Count > 0 ? C.Green : C.Cyan; }
            Gfx.Text(c, sel >= 0 ? $"Ausgewählt: {fleet[cur][sel].Name} - {(horiz ? "waagerecht" : "senkrecht")}" : "Wähle ein Schiff (oder klicke ein platziertes zum Verschieben)", 1230, 528, 20, C.Dim, Al.C, false);
        }
        void Crosshair(Canvas2D c, float x, float y, Col col, float a)
        {
            float cr = 22 + MathF.Sin(Time * 6) * 2; var cp = Gfx.Line(col.A(a), 3); cp.Glow = 1.8f;
            Gfx.Light(c, x, y, 46, col, .18f * a, 1.4f);
            c.DrawCircle(x, y, cr, cp); c.DrawLine(x - cr - 8, y, x + cr + 8, y, cp); c.DrawLine(x, y - cr - 8, x, y + cr + 8, cp);
        }
        void DrawAttack(Canvas2D c)
        {
            int p = cur, o = 1 - p;
            Water(c, GX, GY, GS, true);
            // gegen den Computer: beim Computerzug sieht der Mensch die eigene Flotte
            if (VsCpu && o == 0) foreach (var s in fleet[0]) if (!s.Sunk) { var r = Cell(s.Cells[0]); Hull(c, r.Left + 3, r.Top + 3, s.Size, GS - 6, s.Horiz, 1, 0, Time); }
            foreach (var s in fleet[o]) if (s.Sunk)
                {
                    var r = Cell(s.Cells[0]); float st = sink.TryGetValue(s, out var q) ? q : 9;
                    if (st >= 3.2f) { Hull(c, r.Left + 3, r.Top + 3, s.Size, GS - 6, s.Horiz, .55f, 1, Time); continue; }
                    float u = Ease.InOutCubic(st / 3.2f), L = s.Size * (GS - 6), cw = s.Horiz ? L : GS - 6, ch = s.Horiz ? GS - 6 : L, cx = r.Left + 3 + cw / 2, cy = r.Top + 3 + ch / 2;
                    c.Save(); c.Translate(cx + MathF.Sin(st * 9) * 2 * (1 - u), cy + 34 * u); c.RotateDegrees((s.Horiz ? 1 : -1) * 24 * u + MathF.Sin(st * 5) * 2); c.Scale(1 - .25f * u); c.Translate(-cx, -cy);
                    Hull(c, r.Left + 3, r.Top + 3, s.Size, GS - 6, s.Horiz, 1 - .5f * u, 1, Time); c.Restore();
                    var wr = Gfx.R(cx - cw / 2 - 6, cy - ch / 2 - 6, cw + 12, ch + 40); Gfx.Rect(c, wr, 14, new Col(0, 60, 110).A(.8f * u));
                    for (int k = 0; k < 3; k++) { float ph2 = (st * .8f + k * .33f) % 1; c.DrawOval(cx, cy + 10, cw * (.3f + .5f * ph2), ch * (.2f + .35f * ph2), Gfx.Line(Col.White.A(.5f * (1 - ph2)), 2.5f)); }
                }
            for (int i = 0; i < 64; i++)
            {
                var r = Cell(i);
                if (shots[p].Contains(i))
                {
                    float t = mark.TryGetValue(i + p * 100, out var mt) ? mt : 9; bool isHit = fleet[o].Any(s => s.Cells.Contains(i));
                    if (isHit)
                    {
                        var hs = fleet[o].First(s => s.Cells.Contains(i)); float fs = 1; if (hs.Sunk) fs = 1 - Ease.Clamp((sink.TryGetValue(hs, out var sq) ? sq : 9) / 2.2f);
                        if (fs > .03f) DrawFire(c, r.MidX, r.MidY, GS * fs, Time, i); else c.DrawCircle(r.MidX, r.MidY, 14, Gfx.Fill(Col.Black.A(.35f)));
                    }
                    else
                    {
                        c.DrawCircle(r.MidX, r.MidY, 7, Gfx.Fill(Col.White.A(.75f))); c.DrawCircle(r.MidX, r.MidY, 9 + 20 * Ease.OutCubic(t / 1f), Gfx.Line(Col.White.A(.6f * (1 - Ease.Clamp(t))), 3));
                        for (int k = 0; k < 2; k++) { float ph2 = (Time * .55f + k * .5f + i * .13f) % 1; c.DrawOval(r.MidX, r.MidY, 10 + 24 * ph2, (10 + 24 * ph2) * .42f, Gfx.Line(new Col(200, 235, 255).A(.4f * (1 - ph2)), 2)); }
                    }
                }
            }
            if (hover >= 0 && !shots[p].Contains(hover) && !busy && !CpuTurn)
            {
                var r = Cell(hover); Gfx.Rect(c, Gfx.Inflate(r, -2), 6, C.Red.A(.18f)); Crosshair(c, r.MidX, r.MidY, C.Red, 1);
            }
            if (cpuAim >= 0 && CpuTurn) Crosshair(c, aimX, aimY, C.Pink, Ease.Clamp(.4f + .6f * MathF.Abs(MathF.Sin(Time * 5))));
            if (VsCpu) MiniBoard(c, p);
            int left = fleet[o].Count(s => !s.Sunk); Gfx.Text(c, $"Feindliche Schiffe übrig: {left}", 1240, 640, 26, Col.White, Al.C, true, 4);
            Gfx.Text(c, $"Stand: {score[0]} : {score[1]}", 1240, 690, 26, C.Gold, Al.C, true, 6);
            Gfx.Text(c, $"Treffer von {PName(p)}: {shots[p].Count(i => fleet[o].Any(s => s.Cells.Contains(i)))}   Schüsse: {shots[p].Count}", 1240, 590, 24, C.Cyan.Light(.3f), Al.C, false);
        }
        /// <summary>Kleine Karte rechts (nur gegen den Computer): das jeweils andere Seegebiet.</summary>
        void MiniBoard(Canvas2D c, int owner)
        {
            int shooter = 1 - owner;
            Gfx.Text(c, owner == 0 ? $"Flotte von {PName(0)}" : $"Gewässer von {PName(1)}", MX0 + N * MS / 2, MY0 - 30, 22, owner == 0 ? C.Cyan : C.Pink, Al.C, true, 4);
            Water(c, MX0, MY0, MS, false);
            foreach (var s in fleet[owner]) if (owner == 0 || s.Sunk) { var r = Cell(s.Cells[0], MX0, MY0, MS); Hull(c, r.Left + 2, r.Top + 2, s.Size, MS - 4, s.Horiz, s.Sunk ? .55f : 1, s.Sunk ? 1 : 0, Time); }
            foreach (var i in shots[shooter])
            {
                var r = Cell(i, MX0, MY0, MS);
                if (fleet[owner].Any(s => s.Cells.Contains(i))) { Gfx.Light(c, r.MidX, r.MidY, MS * .6f, C.Orange, .6f + .2f * MathF.Sin(Time * 9 + i), 1.8f); Gfx.Ball(c, r.MidX, r.MidY, MS * .14f, C.Yellow); }
                else c.DrawCircle(r.MidX, r.MidY, 4, Gfx.Fill(Col.White.A(.7f)));
            }
        }
    }

    // ==== KI-BEGIN (Unity-frei, wird im Konsolentest mitkompiliert)
    /// <summary>Platzierung und Schuss-KI fuer Schiffe Versenken (8x8).</summary>
    public static class BattleAI
    {
        public const int N = 8, Unknown = 0, Miss = 1, Hit = 2, Sunk = 3;
        static readonly int[] DR = { -1, 1, 0, 0 }, DC = { 0, 0, -1, 1 };
        public static List<int> Footprint(int cell, int size, bool h)
        {
            int r = cell / N, c = cell % N; var l = new List<int>(size);
            for (int k = 0; k < size; k++) { int rr = h ? r : r + k, cc = h ? c + k : c; if (rr >= N || cc >= N) return null; l.Add(rr * N + cc); }
            return l;
        }
        /// <summary>Zufaellige gueltige Flotte (keine Ueberlappung; Beruehren erlaubt wie im Original).</summary>
        public static (List<int> cells, bool horiz)[] RandomFleet(int[] sizes, Random rnd)
        {
            var res = new (List<int>, bool)[sizes.Length];
            while (true)
            {
                var occ = new bool[N * N]; bool ok = true;
                for (int i = 0; i < sizes.Length && ok; i++)
                {
                    ok = false;
                    for (int t = 0; t < 500; t++)
                    {
                        bool h = rnd.Next(2) == 0; var fp = Footprint(rnd.Next(N * N), sizes[i], h);
                        if (fp == null || fp.Any(x => occ[x])) continue;
                        foreach (var x in fp) occ[x] = true; res[i] = (fp, h); ok = true; break;
                    }
                }
                if (ok) return res;
            }
        }
        /// <summary>
        /// Naechster Schuss. st: Zustand je Feld (Unknown/Miss/Hit/Sunk), remaining: Groessen der noch schwimmenden Schiffe.
        /// level 1 = zufaellig mit leichter Nachverfolgung, 2 = Jagen/Zielen mit Schachbrett-Paritaet, 3 = Wahrscheinlichkeitsdichte.
        /// </summary>
        public static int Shot(int[] st, IList<int> remaining, int level, Random rnd)
        {
            var unknown = new List<int>(); for (int i = 0; i < N * N; i++) if (st[i] == Unknown) unknown.Add(i);
            if (unknown.Count == 0) return -1;
            bool anyHit = false; for (int i = 0; i < N * N; i++) if (st[i] == Hit) { anyHit = true; break; }
            if (level <= 1)
            {
                if (anyHit && rnd.NextDouble() < .5)
                {
                    var nb = Neighbors(st); if (nb.Count > 0) return nb[rnd.Next(nb.Count)];
                }
                return unknown[rnd.Next(unknown.Count)];
            }
            if (level == 2)
            {
                if (anyHit)
                {
                    var line = LineEnds(st); if (line.Count > 0) return line[rnd.Next(line.Count)];
                    var nb = Neighbors(st); if (nb.Count > 0) return nb[rnd.Next(nb.Count)];
                }
                int m = remaining.Count > 0 ? remaining.Min() : 2; if (m < 2) m = 2;
                var par = unknown.Where(i => (i / N + i % N) % m == 0).ToList();
                if (par.Count == 0) par = unknown;
                return par[rnd.Next(par.Count)];
            }
            // Schwer: Dichte aller noch moeglichen Positionen der verbleibenden Schiffe
            var w = Density(st, remaining, anyHit);
            float best = 0; var cand = new List<int>();
            foreach (var i in unknown) { if (w[i] > best + 1e-4f) { best = w[i]; cand.Clear(); cand.Add(i); } else if (w[i] > best - 1e-4f && best > 0) cand.Add(i); }
            if (cand.Count == 0 && anyHit) { w = Density(st, remaining, false); foreach (var i in unknown) { if (w[i] > best + 1e-4f) { best = w[i]; cand.Clear(); cand.Add(i); } else if (w[i] > best - 1e-4f && best > 0) cand.Add(i); } }
            if (cand.Count == 0) return unknown[rnd.Next(unknown.Count)];
            return cand[rnd.Next(cand.Count)];
        }
        public static float[] Density(int[] st, IList<int> remaining, bool target)
        {
            var w = new float[N * N];
            foreach (var size in remaining)
                for (int cell = 0; cell < N * N; cell++)
                    for (int o = 0; o < 2; o++)
                    {
                        var fp = Footprint(cell, size, o == 0); if (fp == null) continue;
                        int hits = 0; bool bad = false;
                        foreach (var x in fp) { if (st[x] == Miss || st[x] == Sunk) { bad = true; break; } if (st[x] == Hit) hits++; }
                        if (bad || (target && hits == 0)) continue;
                        float add = target ? hits * hits * 4 : 1;
                        foreach (var x in fp) if (st[x] == Unknown) w[x] += add;
                    }
            return w;
        }
        static List<int> Neighbors(int[] st)
        {
            var l = new List<int>();
            for (int i = 0; i < N * N; i++)
            {
                if (st[i] != Hit) continue;
                for (int d = 0; d < 4; d++) { int r = i / N + DR[d], c = i % N + DC[d]; if (r < 0 || r >= N || c < 0 || c >= N) continue; int j = r * N + c; if (st[j] == Unknown && !l.Contains(j)) l.Add(j); }
            }
            return l;
        }
        /// <summary>Enden von Trefferlinien (mind. 2 Treffer in Reihe) verlaengern.</summary>
        static List<int> LineEnds(int[] st)
        {
            var l = new List<int>();
            for (int i = 0; i < N * N; i++)
            {
                if (st[i] != Hit) continue;
                for (int d = 0; d < 4; d++)
                {
                    int br = i / N - DR[d], bc = i % N - DC[d];   // Gegenrichtung muss auch Treffer sein
                    if (br < 0 || br >= N || bc < 0 || bc >= N || st[br * N + bc] != Hit) continue;
                    int r = i / N + DR[d], c = i % N + DC[d];
                    if (r < 0 || r >= N || c < 0 || c >= N) continue; int j = r * N + c;
                    if (st[j] == Unknown && !l.Contains(j)) l.Add(j);
                }
            }
            return l;
        }
    }
    // ==== KI-END
}
