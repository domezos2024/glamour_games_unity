using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class Kniffel : Scene
    {
        public override string Title => "Kniffel";
        public override Col Acc1 => C.Red; public override Col Acc2 => C.Orange;
        public override string OppKey => "kniffel";
        // "Computer denkt nach" zwischen Hinweistext und Würfel-Knopf
        public override Pt ThinkPos => new Pt(430, 612);
        static readonly (string key, string label, bool upper)[] Cats = {
            ("1", "Einsen", true), ("2", "Zweien", true), ("3", "Dreien", true), ("4", "Vieren", true), ("5", "Fünfen", true), ("6", "Sechsen", true), ("bonus", "Bonus (63: +35)", true),
            ("3k", "3er Pasch", false), ("4k", "4er Pasch", false), ("fh", "Full House", false), ("ss", "Kleine Straße", false), ("ls", "Große Straße", false), ("yz", "Kniffel", false), ("ch", "Chance", false) };
        // Reihenfolge der KI-Kategorien (KniffelAI) -> Schlüssel
        static readonly string[] CatKeys = { "1", "2", "3", "4", "5", "6", "3k", "4k", "fh", "ss", "ls", "yz", "ch" };
        class Die { public int V = 1; public bool Held; public float T = 1, Dur = 1, Spins, Rz, Bounce, Jit; public float[] Axis = { 1, 0, 0 }; public Spring Lift = new Spring(0) { K = 300, D = 22 }; public DiceTrack Track; public int HitI; public System.Numerics.Quaternion Rest = DicePhysics.RestRotation(1, -.42f); }
        static bool Physical => Die3D.RenderTray != null;
        // Tischszene: 112 Designeinheiten = 1 Wuerfelkante, Weltmitte = Mitte der Wuerfelreihe (430, 400)
        const float DieUnits = 112;
        static System.Numerics.Vector3 Slot3D(int i) => new System.Numerics.Vector3((DieRect(i).MidX - 430) / DieUnits, .5f, 0);
        static void ResetDie(Die d, int i) { d.Held = false; d.V = 1; d.T = 1; d.Track = null; d.Rest = DicePhysics.RestRotation(1, -.42f + (i - 2) * .05f); }
        readonly Die[] dice = Enumerable.Range(0, 5).Select(_ => new Die()).ToArray();
        Dictionary<string, int>[] card = { new Dictionary<string, int>(), new Dictionary<string, int>() }; int cur, rolls; bool over, rolling; Button roll, newBtn, oppBtn; DiceParade parade; bool resShown; int hoverRow = -1, hoverDie = -1;
        // Generationszähler: geplante Computeraktionen eines alten Spiels verfallen
        int gen; bool cpuBusy;
        const float TX = 850, TY = 78, RH = 50, LW = 290, CW = 210;
        static readonly float[] Tilt = DieMul();
        static float[] DieMul() => Die3D.Mul(Die3D.RX(.5f), Die3D.RY(-.42f));
        bool CpuTurn => VsCpu && cur == 1 && !over;

        public override void Enter()
        {
            base.Enter(); roll = Ui.Add(new Button(200, 660, 460, 96, "WÜRFELN", C.Red, HumanRoll, 42)); newBtn = Ui.Add(new Button(40, 780, 260, 62, "Neues Spiel", C.Purple, NewMatch, 24));
            oppBtn = Opponents.AddSwitch(this, OppKey, 320, 776, 260, 70, NewMatch);
            NewGame(); Opponents.Pick(this, OppKey, o => NewMatch());
        }
        public override void DebugWin() { Modal = null; parade = new DiceParade(this, 0, new[] { C.Cyan, C.Pink }, new[] { PName(0), PName(1) }, new[] { 200, 150 }); }
        void NewMatch() { NewGame(); CoinToss.Start(this, f => cur = f); }
        void NewGame() { gen++; CancelCpuThink(); cpuBusy = false; selRow = -1; card = new Dictionary<string, int>[] { new Dictionary<string, int>(), new Dictionary<string, int>() }; cur = 0; rolls = 0; over = false; Modal = null; parade = null; resShown = false; Fx.Clear(); for (int i = 0; i < 5; i++) ResetDie(dice[i], i); }
        int Calc(string k, int[] v) => KniffelAI.Score(Array.IndexOf(CatKeys, k), v);
        int selRow = -1;
        int Total(int p) => card[p].Values.Sum();
        int UpperSum(int p) => Cats.Where(c => c.upper && c.key != "bonus" && card[p].ContainsKey(c.key)).Sum(c => card[p][c.key]);
        int[] Vals() => dice.Select(d => d.V).ToArray();
        void HumanRoll() { if (CpuTurn || CpuThinking) return; Roll(); }
        void Roll()
        {
            if (selRow >= 0 && !over && !rolling) { int r0 = selRow; selRow = -1; Score(r0); return; }
            if (rolls >= 3 || over || rolling) return;
            selRow = -1; rolls++; rolling = true; Sfx.Play(S.Dice); var rnd = Rng.Shared;
            for (int i = 0; i < 5; i++)
            {
                var d = dice[i]; if (d.Held) continue;
                d.V = rnd.Next(1, 7); d.T = 0; d.Dur = .9f + i * .09f;
                if (Physical) { d.Track = DicePhysics.Throw(new Random(rnd.Next()), Slot3D(i), d.V); d.Dur = d.Track.Duration; d.HitI = 0; d.Rest = d.Track.Rot[d.Track.Rot.Length - 1]; } d.Spins = 2 + rnd.Next(3); d.Rz = rnd.Next(4) * MathF.PI / 2 + (rnd.NextSingle() - .5f) * .3f; d.Bounce = 90 + rnd.Next(60); d.Jit = (rnd.NextSingle() - .5f) * 30;
                var a = new[] { rnd.NextSingle() - .5f, rnd.NextSingle() - .5f, rnd.NextSingle() - .5f + .2f }; d.Axis = a;
            }
        }
        void ToggleHold(int i) { dice[i].Held = !dice[i].Held; Sfx.Play(S.Take, .6f); }
        public override bool WantsHand => hoverRow >= 0 || hoverDie >= 0;
        static Box DieRect(int i) => Gfx.Ctr(130 + i * 145, 400, 128, 128);
        static Box RowRect(int r) => Gfx.R(TX, TY + 48 + r * RH, LW + 2 * CW, RH);
        public override void MouseMove(float x, float y)
        {
            hoverRow = -1; hoverDie = -1; if (over || Modal != null || CpuTurn) return;
            for (int i = 0; i < 5; i++) if (Gfx.Inflate(DieRect(i), 10).Contains(x, y)) hoverDie = i;
            if (rolls > 0 && !rolling) for (int r = 0; r < Cats.Length; r++) if (Cats[r].key != "bonus" && !card[cur].ContainsKey(Cats[r].key) && RowRect(r).Contains(x, y)) hoverRow = r;
        }
        public override void MouseUp(float x, float y)
        {
            if (parade != null && parade.T > 2 && !resShown) { ShowResult(); return; }
            if (CpuTurn) return;
            if (hoverDie >= 0 && rolls > 0 && !rolling) ToggleHold(hoverDie);
            else if (hoverRow >= 0) { selRow = selRow == hoverRow ? -1 : hoverRow; Sfx.Play(S.Take, .6f); }
        }
        public override void KeyDown(Key k)
        {
            if (CpuTurn) return;
            if (k == Key.Space || k == Key.Enter) HumanRoll();
            else if (k >= Key.Number1 && k <= Key.Number5 && rolls > 0 && !rolling) ToggleHold(k - Key.Number1);
        }
        void Score(int r)
        {
            var key = Cats[r].key; int v = Calc(key, Vals()); card[cur][key] = v; Sfx.Play(v > 0 ? S.Match : S.NoMatch);
            var rr = RowRect(r); float cx = TX + LW + CW * cur + CW / 2; if (v > 0) { Fx.Burst(cx, rr.MidY, 24, null, 300); Pop("+" + v, cx, rr.MidY, C.Gold, 40); }
            if (key == "yz" && v == 50) { Celebrate(C.Gold, 6, 1.4f); for (int i = 0; i < 5; i++) { var dr = DieRect(i); int kk = i; Tm.After(kk * .18f, () => { Fx.Lightning(dr.MidX + 40, -20, dr.MidX, dr.MidY, C.Cyan); Fx.Explosion(dr.MidX, dr.MidY, .8f); Sfx.Play(S.Boom, .5f, 1.1f); }); } Pop("KNIFFEL!!!", 430, 330, C.Gold, 96); App.Shake(20); }
            else if (v > 0 && (key == "ls" || key == "ss" || key == "fh" || key == "4k")) { Fx.Lightning(cx, -20, cx, rr.MidY, C.Gold); Fx.Shockwave(cx, rr.MidY, C.Gold, 200, .6f); Sfx.Play(S.Sparkle); Fx.Petals(cx, rr.MidY, 18, new[] { C.Pink, C.Gold, C.Cyan }, 320); Pop(key == "ls" ? "GROSSE STRASSE!" : key == "ss" ? "KLEINE STRASSE!" : key == "fh" ? "FULL HOUSE!" : "VIERLING!", 430, 330, C.Gold, 56); App.Shake(8); }
            else if (v == 0) { Fx.Smoke(cx, rr.MidY, 6, 12, 40, 1.4f); Pop("0", cx, rr.MidY, C.Dim, 40); }
            if (UpperSum(cur) >= 63 && !card[cur].ContainsKey("bonus")) { card[cur]["bonus"] = 35; Pop("BONUS +35", 1200, 130, C.Green, 46); Sfx.Play(S.Win); }
            else if (Cats.Where(c => c.upper && c.key != "bonus").All(c => card[cur].ContainsKey(c.key)) && !card[cur].ContainsKey("bonus")) { card[cur]["bonus"] = 0; }
            if (card[0].Count(kv => kv.Key != "bonus") == 13 && card[1].Count(kv => kv.Key != "bonus") == 13) { End(); return; }
            cur = 1 - cur; rolls = 0; for (int i = 0; i < 5; i++) ResetDie(dice[i], i);
        }
        void End()
        {
            over = true; int a = Total(0), b = Total(1); int w = a > b ? 0 : b > a ? 1 : -1; Sfx.Play(S.Win);
            parade = new DiceParade(this, w, new[] { C.Cyan, C.Pink }, new[] { PName(0), PName(1) }, new[] { a, b }); resWin = w; resA = a; resB = b;
            int g = gen; Tm.After(13.2f, () => { if (g == gen) ShowResult(); });
        }
        int resWin, resA, resB;
        void ShowResult()
        {
            if (resShown) return; resShown = true; int w = resWin;
            Result(w < 0 ? "UNENTSCHIEDEN" : $"{PName(w)} gewinnt!", $"{resA}  :  {resB}", w == 0 ? C.Cyan : w == 1 ? C.Pink : C.Gold, ("Nochmal", C.Green, NewMatch), ("Menü", C.Purple, () => App.Go(new Menu())));
        }

        // ---------------------------------------------------------------- Computer-Gegner
        bool[] OpenCats(int p) { var o = new bool[KniffelAI.N]; for (int i = 0; i < o.Length; i++) o[i] = !card[p].ContainsKey(CatKeys[i]); return o; }
        static int RowOf(int cat) => cat < 6 ? cat : cat + 1;
        /// <summary>Schrittweiser Computerzug: würfeln -> Würfel halten -> würfeln ... -> Zeile markieren -> eintragen.</summary>
        void CpuDrive()
        {
            if (!CpuTurn || cpuBusy || Modal != null || parade != null || rolling || CpuThinking) return;
            cpuBusy = true; int g = gen; float think = Opponents.ThinkTime(Opp);
            if (rolls == 0) { CpuThink(think, () => { if (g != gen) return; cpuBusy = false; Roll(); }); return; }
            int[] v = Vals(); var open = OpenCats(1); int up = UpperSum(1);
            bool[] hold = rolls < 3 ? KniffelAI.ChooseHold(v, 3 - rolls, open, up, (int)Opp, Rng.Shared) : null;
            if (hold != null && !hold.All(h => h))
            {
                CpuThink(think, () =>
                {
                    if (g != gen) return;
                    var tog = Enumerable.Range(0, 5).Where(i => dice[i].Held != hold[i]).ToList();
                    for (int j = 0; j < tog.Count; j++) { int i = tog[j]; Tm.After(j * .24f, () => { if (g == gen) ToggleHold(i); }); }
                    Tm.After(tog.Count * .24f + .45f, () => { if (g != gen) return; cpuBusy = false; Roll(); });
                });
            }
            else
            {
                int row = RowOf(KniffelAI.ChooseCat(v, open, up, (int)Opp, Rng.Shared));
                CpuThink(think, () =>
                {
                    if (g != gen) return; selRow = row; Sfx.Play(S.Take, .6f);
                    Tm.After(.95f, () => { if (g != gen) return; cpuBusy = false; Roll(); });
                });
            }
        }

        public override void Update(float dt)
        {
            parade?.Update(dt); newBtn.Visible = oppBtn.Visible = parade == null; rolling = false;
            for (int i = 0; i < 5; i++)
            {
                var d = dice[i];
                if (d.T < d.Dur)
                {
                    d.T += dt; rolling = true;
                    // Aufprall-Geraeusche synchron zur simulierten Bahn
                    if (d.Track != null) while (d.HitI < d.Track.Hits.Count && d.Track.Hits[d.HitI].t <= d.T) { var h = d.Track.Hits[d.HitI++]; Sfx.Play(S.Stop, .12f + .4f * h.s, 1.15f + .3f * (1 - h.s)); }
                    if (d.T >= d.Dur) { d.T = d.Dur; if (d.Track == null) { var r = DieRect(i); Fx.Spark(r.MidX, r.Bottom, C.Orange, 10, 160); Fx.Smoke(r.MidX, r.Bottom - 10, 2, 8, 20, .8f); Sfx.Play(S.Stop, .35f, 1.2f); } }
                }
                d.Lift.Target = d.Held ? -26 : 0; d.Lift.Update(dt);
            }
            CpuDrive();
            roll.Visible = parade == null; roll.Enabled = (rolls < 3 || selRow >= 0) && !over && !rolling && !CpuTurn; roll.Col = selRow >= 0 ? C.Green : C.Red; roll.Text = selRow >= 0 ? $"EINTRAGEN: {Cats[selRow].label} (+{Calc(Cats[selRow].key, Vals())})" : rolls == 0 ? "WÜRFELN" : rolls < 3 ? $"NOCHMAL ({3 - rolls})" : "Kategorie wählen";
            // Schrift an Knopfbreite anpassen (lange Kategorienamen)
            float fs = selRow >= 0 ? 30 : 42; roll.Size = Math.Min(fs, fs * 420 / Math.Max(1, Gfx.TW(roll.Text, fs)));
        }
        public override void Draw(Canvas2D c)
        {
            W.PlayerBox(c, Gfx.R(40, 130, 260, 120), PName(0), Total(0).ToString(), C.Cyan, cur == 0 && !over, Time);
            W.PlayerBox(c, Gfx.R(320, 130, 260, 120), PName(1), Total(1).ToString(), C.Pink, cur == 1 && !over, Time);
            Gfx.Text(c, $"Runde {Math.Min(13, card[cur].Count(k => k.Key != "bonus") + 1)} / 13", 610, 190, 26, C.Dim, Al.L, false);
            for (int i = 0; i < 3; i++) { float x = 610 + Gfx.TW("Würfe übrig", 20, false) + 18 + i * 26; bool on = i < 3 - rolls; c.DrawCircle(x, 235, 10, Gfx.Fill(on ? C.Gold : C.Dim.A(.25f))); if (on) Gfx.Light(c, x, 235, 24, C.Gold, .45f, 1.8f); }
            Gfx.Text(c, "Würfe übrig", 610, 235, 20, C.Dim, Al.L, false);
            // Würfelbrett: Holzrahmen mit weichem Schatten, Filz mit radialem Verlauf und Lampenschein
            var tray = Gfx.R(40, 270, 780, 260); Gfx.Shadow(c, tray, 30, 20, .55f, 0, 14);
            Gfx.RectGrad(c, tray, 30, new Col(150, 88, 38), new Col(66, 34, 12)); Gfx.Stroke(c, tray, 30, new Col(190, 120, 60).A(.6f), 2);
            var felt = Gfx.Inflate(tray, -10);
            Gfx.RectRadial(c, felt, 22, felt.MidX, felt.MidY - 40, 470, new Col(22, 106, 68), new Col(5, 32, 22));
            Gfx.Stroke(c, felt, 22, Col.Black.A(.5f), 3); Gfx.Stroke(c, Gfx.Inflate(felt, -6), 18, C.Green.A(.28f), 2);
            Gfx.Light(c, felt.MidX, felt.MidY - 50, 380, new Col(255, 240, 200), .06f, 1.2f);
            Gfx.Text(c, CpuTurn ? "Der Computer ist am Zug ..." : selRow >= 0 ? "Zeile anklicken = abwählen, grüner Knopf = eintragen" : "Klicke auf Würfel oder Tasten 1-5, um sie zu halten  -  Leertaste würfelt", 430, 560, 22, CpuTurn ? C.Pink.Light(.4f) : C.Dim, Al.C, false);
            if (Physical)
            {
                var arr = new Die3D.TrayDie[5];
                for (int i = 0; i < 5; i++)
                {
                    var d = dice[i]; var r = DieRect(i);
                    if (d.Held) Gfx.Light(c, r.MidX, r.MidY + d.Lift.V, 115, C.Green, .32f, 1.7f);
                    System.Numerics.Vector3 p; System.Numerics.Quaternion q;
                    if (d.Track != null && d.T < d.Dur) d.Track.Sample(d.T, out p, out q); else { p = Slot3D(i); q = d.Rest; }
                    p.Y += -d.Lift.V / DieUnits;
                    arr[i] = new Die3D.TrayDie { Pos = p, Rot = q, Body = d.Held ? new Col(255, 250, 230) : new Col(246, 236, 214), Pip = new Col(30, 18, 30) };
                }
                if (Die3D.RenderTray(c, new Box(felt.Left - 40, felt.Top - 200, felt.Right + 40, felt.Bottom + 24), new Pt(430, 400), DieUnits, arr))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        var d = dice[i]; var r = DieRect(i);
                        if (d.Held) Gfx.Text(c, "GEHALTEN", r.MidX, r.Bottom + 34, 20, C.Green, Al.C, true, 6); else if (rolls > 0) Gfx.Text(c, "halten", r.MidX, r.Bottom + 34, 18, C.Dim.A(hoverDie == i ? 1 : .5f), Al.C, false);
                        if (i == hoverDie && rolls > 0 && !rolling) Gfx.Glow(c, Gfx.Inflate(r, -6), 20, C.Green, 10, .6f);
                    }
                    DrawTable(c); parade?.Draw(c);
                    return;
                }
            }
            for (int i = 0; i < 5; i++)
            {
                var d = dice[i]; var r = DieRect(i); float t = Ease.Clamp(d.T / d.Dur), e = Ease.OutCubic(t);
                var fc = Die3D.Mul(Die3D.RZ(d.Rz), Die3D.Face(d.V));
                var fin = Die3D.Mul(Tilt, fc);
                float[] m = d.T >= d.Dur ? fin : Die3D.Mul(Tilt, Die3D.Mul(Die3D.RAxis(d.Axis[0], d.Axis[1], d.Axis[2], d.Spins * MathF.PI * 2 * (1 - e)), fc));
                float by = d.T >= d.Dur ? 0 : MathF.Abs(MathF.Sin(t * MathF.PI * 3)) * d.Bounce * (1 - t) * (1 - t), jx = d.T >= d.Dur ? 0 : d.Jit * (1 - e);
                float sz = 112 * (1 + (d.Held ? .04f : 0));
                if (d.Held) { Gfx.Light(c, r.MidX, r.MidY + d.Lift.V, 115, C.Green, .32f, 1.7f); }
                Die3D.Draw(c, r.MidX + jx, r.MidY - by + d.Lift.V, sz, m, d.Held ? new Col(255, 250, 230) : new Col(246, 236, 214), new Col(30, 18, 30), false);
                if (d.Held) Gfx.Text(c, "GEHALTEN", r.MidX, r.Bottom + 34, 20, C.Green, Al.C, true, 6); else if (rolls > 0) Gfx.Text(c, "halten", r.MidX, r.Bottom + 34, 18, C.Dim.A(hoverDie == i ? 1 : .5f), Al.C, false);
                if (i == hoverDie && rolls > 0) Gfx.Glow(c, Gfx.Inflate(r, -6), 20, C.Green, 10, .6f);
            }
            DrawTable(c); parade?.Draw(c);
        }
        void DrawTable(Canvas2D c)
        {
            var frame = Gfx.R(TX - 12, TY - 10, LW + 2 * CW + 24, 48 + Cats.Length * RH + 60); W.Panel(c, frame, C.Red);
            for (int p = 0; p < 2; p++) { var hr = Gfx.R(TX + LW + p * CW + 6, TY, CW - 12, 40); var col = p == 0 ? C.Cyan : C.Pink; Gfx.Rect(c, hr, 12, col.A(cur == p && !over ? .35f : .12f)); if (cur == p && !over) Gfx.Glow(c, hr, 12, col, 8, .6f); Gfx.Text(c, PName(p), hr.MidX, hr.MidY, 24, col.Light(.4f)); }
            bool can = rolls > 0 && !over; int[] vals = Vals();
            for (int r = 0; r < Cats.Length; r++)
            {
                var rr = RowRect(r); var (key, label, upper) = Cats[r]; bool hv = hoverRow == r || selRow == r;
                if (r % 2 == 0) Gfx.Rect(c, Gfx.Inflate(rr, -2), 8, Col.White.A(.035f));
                if (hv) { Gfx.Rect(c, Gfx.Inflate(rr, -2), 8, C.Gold.A(selRow == r ? .32f : .16f)); if (selRow == r) Gfx.Glow(c, Gfx.Inflate(rr, -2), 8, C.Gold, 10, .55f + .25f * MathF.Sin(Time * 6)); Gfx.Stroke(c, Gfx.Inflate(rr, -2), 8, C.Gold, selRow == r ? 4 : 2); }
                if (r == 6 || r == 7) c.DrawLine(rr.Left + 10, rr.Top, rr.Right - 10, rr.Top, Gfx.Line(C.Red.A(.5f), 2));
                Gfx.Text(c, label, rr.Left + 16, rr.MidY, key == "bonus" ? 21 : 26, upper ? Col.White : C.Orange.Light(.5f), Al.L, false);
                if (key == "bonus") Gfx.Text(c, $"{UpperSum(cur)}/63", rr.Left + LW - 16, rr.MidY, 18, C.Dim, Al.R, false);
                for (int p = 0; p < 2; p++)
                {
                    float cx = TX + LW + p * CW + CW / 2;
                    if (card[p].TryGetValue(key, out var v)) Gfx.Text(c, v.ToString(), cx, rr.MidY, 30, v == 0 ? C.Dim : Col.White, Al.C, true, v > 0 ? 3 : 0);
                    else if (key == "bonus")
                    {
                        int us = UpperSum(p);
                        Gfx.Text(c, $"{us}/63", cx, rr.MidY, 20, us >= 63 ? C.Green : C.Dim.A(.75f), Al.C, false);
                    }
                    else if (can && p == cur) { int pv = Calc(key, vals); float pu = .5f + .5f * MathF.Sin(Time * 5 + r); Gfx.Text(c, pv.ToString(), cx, rr.MidY, 30, (pv > 0 ? C.Gold : C.Dim).A(hv ? 1 : .55f + .3f * pu), Al.C, true, hv ? 8 : 0); }
                }
            }
            float sy = TY + 48 + Cats.Length * RH + 30; var gl = Gfx.Line(C.Gold.A(.6f), 2); gl.Glow = 1.4f; c.DrawLine(TX, sy - 24, TX + LW + 2 * CW, sy - 24, gl);
            Gfx.Text(c, "SUMME", TX + 16, sy, 28, C.Gold, Al.L, true, 6); for (int p = 0; p < 2; p++) Gfx.Text(c, Total(p).ToString(), TX + LW + p * CW + CW / 2, sy, 34, p == 0 ? C.Cyan : C.Pink, Al.C, true, 8);
        }
    }

    // ==== KI-BEGIN (Unity-frei, wird im Konsolentest ~/aitest_teamC mitkompiliert) ====
    /// <summary>
    /// Kniffel-Computergegner ohne Engine-Abhängigkeit. Kategorien: 0..5 = Einsen..Sechsen, 6 3er Pasch, 7 4er Pasch,
    /// 8 Full House, 9 Kleine Straße, 10 Große Straße, 11 Kniffel, 12 Chance. Stufe: 1 Leicht, 2 Mittel, 3 Schwer.
    /// </summary>
    public static class KniffelAI
    {
        public const int N = 13, Three = 6, Four = 7, FH = 8, SS = 9, LS = 10, YZ = 11, CH = 12;
        // Erwartete Punkte einer noch offenen unteren Kategorie (= Opportunitätskosten beim Belegen)
        static readonly double[] FutLow = { 0, 0, 0, 0, 0, 0, 19.0, 11.0, 20.0, 27.0, 29.0, 13.0, 21.0 };
        // Modell für obere Kategorien: Anzahl der gewünschten Augen ~ Binomial(5, p)
        const double PUp = .50;
        static readonly double[] Q = Binom(5, PUp);
        static readonly double QMean = 5 * PUp;

        /// <summary>Punkte der Kategorie für fünf Würfel (identisch zur Original-Wertung, Full House streng 3+2).</summary>
        public static int Score(int cat, int[] v)
        {
            if (cat < 0 || cat >= N) return 0;
            var cnt = new int[7]; int sum = 0;
            foreach (var x in v) if (x >= 1 && x <= 6) { cnt[x]++; sum += x; }
            return ScoreCnt(cat, cnt, sum);
        }
        static int ScoreCnt(int cat, int[] cnt, int sum)
        {
            if (cat < 6) return cnt[cat + 1] * (cat + 1);
            int mx = 0, run = 0, best = 0; bool h3 = false, h2 = false;
            for (int f = 1; f <= 6; f++) { mx = Math.Max(mx, cnt[f]); if (cnt[f] == 3) h3 = true; if (cnt[f] == 2) h2 = true; run = cnt[f] > 0 ? run + 1 : 0; best = Math.Max(best, run); }
            switch (cat)
            {
                case Three: return mx >= 3 ? sum : 0;
                case Four: return mx >= 4 ? sum : 0;
                case FH: return h3 && h2 ? 25 : 0;
                case SS: return best >= 4 ? 30 : 0;
                case LS: return best >= 5 ? 40 : 0;
                case YZ: return mx == 5 ? 50 : 0;
                default: return sum;
            }
        }

        // ------------------------------------------------------------ Tabellen (einmalig, < 5 ms)
        static int[][] finCnt;          // 252 Endstände (Augen-Zählungen)
        static int[][] finSc;           // Punkte je Endstand und Kategorie
        static int[] keepOf, finOf;     // Code -> Index
        static int[][] keepCnt;         // 462 Halte-Mengen (0..5 Würfel)
        static int[][] trF; static double[][] trP;   // Halte-Menge -> Verteilung der Endstände
        static int[][] subK;            // Endstand -> alle unterscheidbaren Halte-Mengen
        static int Code(int[] cnt) { int c = 0, m = 1; for (int f = 1; f <= 6; f++) { c += cnt[f] * m; m *= 6; } return c; }
        static double[] Binom(int n, double p) { var r = new double[n + 1]; for (int k = 0; k <= n; k++) r[k] = Choose(n, k) * Math.Pow(p, k) * Math.Pow(1 - p, n - k); return r; }
        static double Choose(int n, int k) { double r = 1; for (int i = 1; i <= k; i++) r = r * (n - k + i) / i; return r; }
        static void Multisets(int n, int minF, int[] cnt, List<int[]> outL)
        {
            if (n == 0) { outL.Add((int[])cnt.Clone()); return; }
            for (int f = minF; f <= 6; f++) { cnt[f]++; Multisets(n - 1, f, cnt, outL); cnt[f]--; }
        }
        static readonly object initLock = new object();
        static void Init()
        {
            if (finCnt != null) return;
            lock (initLock)
            {
                if (finCnt != null) return;
                var fins = new List<int[]>(); Multisets(5, 1, new int[7], fins);
                var fo = new int[46656]; for (int i = 0; i < fo.Length; i++) fo[i] = -1;
                for (int i = 0; i < fins.Count; i++) fo[Code(fins[i])] = i;
                var keeps = new List<int[]>(); for (int n = 0; n <= 5; n++) Multisets(n, 1, new int[7], keeps);
                var ko = new int[46656]; for (int i = 0; i < ko.Length; i++) ko[i] = -1;
                for (int i = 0; i < keeps.Count; i++) ko[Code(keeps[i])] = i;
                // Wahrscheinlichkeiten für n neu geworfene Würfel (Multinomial)
                var outs = new List<(int[] cnt, double p)>[6];
                for (int n = 0; n <= 5; n++)
                {
                    var l = new List<int[]>(); Multisets(n, 1, new int[7], l); outs[n] = new List<(int[], double)>();
                    foreach (var o in l) { double p = Fact(n); for (int f = 1; f <= 6; f++) p /= Fact(o[f]); outs[n].Add((o, p / Math.Pow(6, n))); }
                }
                var tf = new int[keeps.Count][]; var tp = new double[keeps.Count][];
                for (int k = 0; k < keeps.Count; k++)
                {
                    int n = 5 - keeps[k].Sum(); var l = outs[n]; tf[k] = new int[l.Count]; tp[k] = new double[l.Count];
                    var t = new int[7];
                    for (int j = 0; j < l.Count; j++) { for (int f = 1; f <= 6; f++) t[f] = keeps[k][f] + l[j].cnt[f]; tf[k][j] = fo[Code(t)]; tp[k][j] = l[j].p; }
                }
                var sk = new int[fins.Count][]; var fs = new int[fins.Count][];
                for (int i = 0; i < fins.Count; i++)
                {
                    var d = ToDice(fins[i]); var set = new HashSet<int>(); var kc = new int[7];
                    for (int m = 0; m < 32; m++) { Array.Clear(kc, 0, 7); for (int b = 0; b < 5; b++) if ((m >> b & 1) != 0) kc[d[b]]++; set.Add(ko[Code(kc)]); }
                    sk[i] = set.ToArray(); fs[i] = new int[N]; int sum = d.Sum();
                    for (int c = 0; c < N; c++) fs[i][c] = ScoreCnt(c, fins[i], sum);
                }
                keepOf = ko; finOf = fo; keepCnt = keeps.ToArray(); trF = tf; trP = tp; subK = sk; finSc = fs; finCnt = fins.ToArray();
            }
        }
        static double Fact(int n) { double r = 1; for (int i = 2; i <= n; i++) r *= i; return r; }
        static int[] ToDice(int[] cnt) { var d = new int[5]; int j = 0; for (int f = 1; f <= 6; f++) for (int k = 0; k < cnt[f]; k++) d[j++] = f; return d; }
        static int[] Cnt(int[] dice) { var c = new int[7]; foreach (var x in dice) c[x]++; return c; }

        // ------------------------------------------------------------ Bewertung eines Endstands
        /// <summary>Wahrscheinlichkeit, mit den offenen oberen Kategorien (Bitmaske) noch "need" Punkte zu holen.</summary>
        static double PBonus(int need, int mask)
        {
            if (need <= 0) return 1; if (mask == 0) return 0;
            var dist = new double[need + 1]; dist[0] = 1;   // Index need = "erreicht"
            for (int c = 0; c < 6; c++)
            {
                if ((mask >> c & 1) == 0) continue; var nd = new double[need + 1];
                for (int s = 0; s <= need; s++) { if (dist[s] == 0) continue; for (int k = 0; k <= 5; k++) nd[Math.Min(need, s + k * (c + 1))] += dist[s] * Q[k]; }
                dist = nd;
            }
            return dist[need];
        }
        /// <summary>Wert "Kategorie c mit s Punkten belegen" = Punkte - Opportunitätskosten + Bonus-Erwartung.</summary>
        sealed class Eval
        {
            public readonly double[,] up = new double[6, 6]; public double now; public bool[] open; public bool bonusOpen;
            public Eval(bool[] open, int upperSum)
            {
                this.open = open; int mask = 0; for (int c = 0; c < 6; c++) if (open[c]) mask |= 1 << c;
                bonusOpen = upperSum < 63 && mask != 0;
                now = upperSum >= 63 ? 1 : PBonus(63 - upperSum, mask);
                for (int c = 0; c < 6; c++) if (open[c]) for (int k = 0; k <= 5; k++)
                            up[c, k] = upperSum >= 63 ? 1 : PBonus(63 - upperSum - k * (c + 1), mask & ~(1 << c));
            }
            public double Val(int c, int s)
            {
                if (c < 6) return s - (c + 1) * QMean + 35 * up[c, s / (c + 1)];
                return s - FutLow[c] + 35 * now;
            }
            public double Best(int[] sc, out int cat)
            {
                double b = double.NegativeInfinity; cat = -1;
                for (int c = 0; c < N; c++) if (open[c]) { double v = Val(c, sc[c]); if (v > b) { b = v; cat = c; } }
                return b;
            }
        }

        // Puffer (nur Hauptthread)
        static readonly double[] e0 = new double[252], e1 = new double[252], v1 = new double[462];

        /// <summary>
        /// Halte-Entscheidung. rollsLeft = verbleibende Würfe (1 oder 2). Ergebnis: Maske der zu haltenden Würfel;
        /// alle true = nicht mehr würfeln, sondern eintragen.
        /// </summary>
        public static bool[] ChooseHold(int[] dice, int rollsLeft, bool[] open, int upperSum, int level, Random rnd)
        {
            if (level <= 1) return rnd.NextDouble() < .45 ? EasyHold(dice, rnd) : MediumHold(dice, open);
            if (level == 2) return MediumHold(dice, open);
            int k = HardKeep(dice, rollsLeft, open, upperSum); return MaskFor(dice, keepCnt[k]);
        }
        /// <summary>Kategorie-Wahl nach dem letzten Wurf.</summary>
        public static int ChooseCat(int[] dice, bool[] open, int upperSum, int level, Random rnd)
        {
            var cnt = Cnt(dice); int sum = dice.Sum();
            if (level >= 3) { Init(); var ev = new Eval(open, upperSum); var sc = new int[N]; for (int c = 0; c < N; c++) sc[c] = ScoreCnt(c, cnt, sum); ev.Best(sc, out int cat); return cat; }
            var opens = Enumerable.Range(0, N).Where(c => open[c]).ToList();
            if (level <= 1 && rnd.NextDouble() < .3) return opens[rnd.Next(opens.Count)];
            // Mittel: höchste Sofortpunkte, Chance nur ungern; bei Nullen die "billigste" Kategorie
            int best = -1; double bv = double.NegativeInfinity;
            foreach (var c in opens)
            {
                int s = ScoreCnt(c, cnt, sum); double v = s - (c == CH ? 8 : 0) - (c < 6 ? (c + 1) * .4 : FutLow[c] * .12);
                if (s >= 3 * (c + 1) && c < 6) v += 4;
                if (v > bv) { bv = v; best = c; }
            }
            return best;
        }

        /// <summary>Schwer: Erwartungswert über alle Halte-Teilmengen, exakt über die verbleibenden Würfe.</summary>
        static int HardKeep(int[] dice, int rollsLeft, bool[] open, int upperSum)
        {
            Init(); var ev = new Eval(open, upperSum);
            for (int f = 0; f < 252; f++) e0[f] = ev.Best(finSc[f], out _);
            for (int k = 0; k < keepCnt.Length; k++) { double s = 0; var tf = trF[k]; var tp = trP[k]; for (int j = 0; j < tf.Length; j++) s += tp[j] * e0[tf[j]]; v1[k] = s; }
            int cur = finOf[Code(Cnt(dice))]; int all = keepOf[Code(Cnt(dice))];
            if (rollsLeft >= 2)
            {
                for (int f = 0; f < 252; f++) { double b = double.NegativeInfinity; foreach (var k in subK[f]) if (v1[k] > b) b = v1[k]; e1[f] = b; }
                int bk = all; double bv = double.NegativeInfinity;
                foreach (var k in subK[cur]) { double s = 0; var tf = trF[k]; var tp = trP[k]; for (int j = 0; j < tf.Length; j++) s += tp[j] * e1[tf[j]]; if (s > bv + 1e-9 || (Math.Abs(s - bv) <= 1e-9 && k == all)) { bv = s; bk = k; } }
                if (bk != all) return bk;
                // Alles halten mit zwei Würfen übrig = wie ein Wurf übrig entscheiden
            }
            {
                int bk = all; double bv = e0[cur];
                foreach (var k in subK[cur]) if (v1[k] > bv + 1e-9) { bv = v1[k]; bk = k; }
                return bk;
            }
        }
        static bool[] MaskFor(int[] dice, int[] keep)
        {
            var k = (int[])keep.Clone(); var m = new bool[5];
            for (int i = 0; i < 5; i++) if (k[dice[i]] > 0) { k[dice[i]]--; m[i] = true; }
            return m;
        }
        /// <summary>Mittel: meiste gleiche Augen halten bzw. Straßenansatz; fertige Kombinationen stehen lassen.</summary>
        static bool[] MediumHold(int[] dice, bool[] open)
        {
            var cnt = Cnt(dice); int sum = dice.Sum(); int mx = 0, face = 0;
            for (int f = 6; f >= 1; f--) if (cnt[f] > mx) { mx = cnt[f]; face = f; }
            var all = new bool[] { true, true, true, true, true };
            if (mx == 5 && open[YZ]) return all;
            if (open[LS] && ScoreCnt(LS, cnt, sum) > 0) return all;
            if (open[FH] && ScoreCnt(FH, cnt, sum) > 0 && mx == 3 && !(open[YZ] && face >= 5)) return all;
            // längster Lauf
            int run = 0, best = 0, end = 0; for (int f = 1; f <= 6; f++) { run = cnt[f] > 0 ? run + 1 : 0; if (run > best) { best = run; end = f; } }
            if ((open[SS] || open[LS]) && (best >= 4 || (best == 3 && mx <= 2)))
            {
                if (best >= 4 && !open[LS]) return all;
                var keep = new int[7]; for (int f = end - best + 1; f <= end; f++) keep[f] = 1; return MaskFor(dice, keep);
            }
            var kp = new int[7];
            if (mx >= 2) kp[face] = mx; else if (cnt[6] > 0) kp[6] = 1; else if (cnt[5] > 0) kp[5] = 1;
            return MaskFor(dice, kp);
        }
        /// <summary>Leicht: zufällige, naive Halte-Entscheidung (manchmal auch gar nicht weiterwürfeln).</summary>
        static bool[] EasyHold(int[] dice, Random rnd)
        {
            var m = new bool[5]; if (rnd.NextDouble() < .2) { for (int i = 0; i < 5; i++) m[i] = true; return m; }
            for (int i = 0; i < 5; i++) m[i] = rnd.NextDouble() < .4;
            return m;
        }
    }
    // ==== KI-END ====
}
