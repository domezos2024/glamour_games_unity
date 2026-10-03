using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class MemoryGame : Scene
    {
        public override string Title => "Memory";
        public override Col Acc1 => C.Purple; public override Col Acc2 => C.Pink;
        public override string OppKey => "memory";
        static readonly string[] Sym = { "diamond", "car", "heart", "crown", "star", "clover", "butterfly", "paw", "rose", "wolf", "moon", "gamepad", "sun", "palms", "infinity", "notes", "coffee", "wings", "unicorn", "dragonfly" };
        class Card { public int Sym; public Spring Flip = new Spring(0) { K = 190, D = 20 }; public bool Matched; public float MatchT, Hov, CpuT = -1; public Spring Lift = new Spring(0); }
        Card[] cards; readonly List<int> up = new List<int>(); bool locked; int cur, first; int[] score = new int[2]; int matched;
        bool toss = true; Button bNew;
        int over = -1;
        // Computer: Gedaechtnis + Generationszaehler (alte geplante Zuege verfallen nach Neustart)
        MemoryBrain brain; int gen;
        const float CW = 104, CH = 128, GAP = 12, X0 = (1600 - (8 * CW + 7 * GAP)) / 2, Y0 = 128;
        static Box Rc(int i) => Gfx.R(X0 + (i % 8) * (CW + GAP), Y0 + (i / 8) * (CH + GAP), CW, CH);
        bool CpuTurn => VsCpu && cur == 1;
        public override void Enter()
        {
            base.Enter();
            bNew = Ui.Add(new Button(50, 780, 250, 62, "Neues Spiel", C.Purple, () => ToToss(), 24) { Visible = false });
            Opponents.AddSwitch(this, OppKey, 1315, 560, 260, 70, ToToss);
            Opponents.Pick(this, OppKey, o => ToToss());
        }
        void ToToss()
        {
            toss = true; Modal = null; cards = null; Tm.Clear(); gen++; CancelCpuThink(); over = -1;
            bNew.Visible = false;
            CoinToss.Start(this, f => { first = f; StartGame(); });
        }
        void StartGame()
        {
            toss = false; bNew.Visible = true; score = new int[2]; matched = 0; up.Clear(); locked = false; cur = first; gen++; CancelCpuThink();
            var pairs = Sym.Concat(Sym).OrderBy(_ => Rng.Shared.Next()).ToArray();
            cards = pairs.Select(s => new Card { Sym = Array.IndexOf(Sym, s) }).ToArray();
            for (int i = 0; i < cards.Length; i++) { cards[i].Lift.V = -400 - i * 20; cards[i].Lift.Target = 0; }
            brain = VsCpu ? MemoryBrain.For((int)Opp) : null;
            CpuMaybe(1.4f);   // erst austeilen lassen
        }
        public override bool WantsHand => !toss && over >= 0;
        public override void MouseMove(float x, float y)
        {
            over = -1; if (cards == null || toss || Modal != null || CpuTurn || CpuThinking) return;
            for (int i = 0; i < 40; i++) if (Rc(i).Contains(x, y)) over = i;
        }
        public override void MouseUp(float x, float y)
        {
            if (toss || locked || over < 0 || CpuTurn || CpuThinking) return; var c = cards[over];
            if (c.Matched || up.Contains(over)) return;
            Reveal(over);
        }
        /// <summary>Karte aufdecken - gemeinsamer Weg fuer Mensch und Computer.</summary>
        void Reveal(int i)
        {
            var c = cards[i]; c.Flip.Target = 1; up.Add(i); Sfx.Play(S.Flip);
            if (CpuTurn) { c.CpuT = 0; var r = Rc(i); Fx.Ring(r.MidX, r.MidY, C.Pink, 16, 170); }
            brain?.Observe(i, c.Sym, Rng.Shared);
            if (up.Count == 2) Check();
        }
        void Check()
        {
            locked = true; int a = up[0], b = up[1];
            if (cards[a].Sym == cards[b].Sym)
                Tm.After(.55f, () =>
                {
                    cards[a].Matched = cards[b].Matched = true; score[cur] += 8; matched += 2; up.Clear(); locked = false; Sfx.Play(S.Match);
                    brain?.Remove(a); brain?.Remove(b);
                    foreach (var i in new[] { a, b }) { var r = Rc(i); Fx.Burst(r.MidX, r.MidY, 34, null, 380); Fx.Ring(r.MidX, r.MidY, C.Gold, 18, 240); Fx.Shockwave(r.MidX, r.MidY, C.Gold, 90, .4f); }
                    var r0 = Rc(a); Pop("+8", r0.MidX, r0.Top, C.Gold, 46); App.Flash(C.Gold, .18f);
                    if (matched == 40) Tm.After(.7f, End); else CpuMaybe();
                });
            else
                Tm.After(1.0f, () =>
                {
                    Sfx.Play(S.NoMatch); cards[a].Flip.Target = 0; cards[b].Flip.Target = 0; up.Clear(); cur = 1 - cur; locked = false; Sfx.Play(S.Turn, .5f);
                    brain?.Decay(Rng.Shared); CpuMaybe(.3f);
                });
        }
        // ---------------------------------------------------------------- Computer-Zug
        void CpuMaybe(float delay = 0)
        {
            if (!CpuTurn || cards == null || matched == 40) return;
            int g = gen; over = -1;
            Tm.After(delay, () =>
            {
                if (g != gen || !CpuTurn) return;
                CpuThink(Opponents.ThinkTime(Opp), () =>
                {
                    if (g != gen || !CpuTurn || locked || up.Count > 0) return;
                    int a = brain.PickFirst(Avail(), Rng.Shared); if (a < 0) return;
                    Reveal(a);
                    // zweite Karte mit kleiner Pause, damit man zusehen kann
                    Tm.After(.85f + Rng.F() * .25f, () =>
                    {
                        if (g != gen || !CpuTurn || up.Count != 1) return;
                        int b = brain.PickSecond(a, cards[a].Sym, Avail(), Rng.Shared); if (b >= 0) Reveal(b);
                    });
                });
            });
        }
        bool[] Avail() { var av = new bool[40]; for (int i = 0; i < 40; i++) av[i] = !cards[i].Matched && !up.Contains(i); return av; }
        void End()
        {
            // gegen den Computer zaehlt fuer die Bestenliste nur das Ergebnis des Menschen
            int best = VsCpu ? score[0] : Math.Max(score[0], score[1]); int w = score[0] > score[1] ? 0 : score[1] > score[0] ? 1 : -1;
            Celebrate(w == 0 ? C.Cyan : w == 1 ? C.Pink : C.Gold, 5, 1.1f, w < 0 ? "Unentschieden!" : $"{PName(w)} gewinnt!"); Sfx.Play(S.Win); App.Shake(10);
            Action show = () => Result(w < 0 ? "UNENTSCHIEDEN!" : $"{PName(w)} gewinnt!", $"{PName(0)}: {score[0]} Pkt   |   {PName(1)}: {score[1]} Pkt", w == 0 ? C.Cyan : w == 1 ? C.Pink : C.Gold, ("Nochmal", C.Green, () => ToToss()), ("Menü", C.Purple, () => App.Go(new Menu())));
            int g = gen;
            Tm.After(3f, () => { if (g != gen) return; if (Save.IsHigh("hs_mem", best)) NameEntry("hs_mem", best, "NEUER HIGHSCORE!", show); else show(); });
        }
        public override void Update(float dt)
        {
            if (toss || cards == null) return;
            foreach (var c in cards) { c.Flip.Update(dt); c.Lift.Update(dt); if (c.Matched) c.MatchT += dt; if (c.CpuT >= 0) { c.CpuT += dt; if (c.CpuT > 1.2f) c.CpuT = -1; } }
            for (int i = 0; i < 40; i++) cards[i].Hov = Ease.Lerp(cards[i].Hov, i == over && !cards[i].Matched ? 1 : 0, Math.Min(1, dt * 14));
        }
        public override void Draw(Canvas2D c)
        {
            HighscoreList(c, "hs_mem", 1320, 150, 250, C.Purple);
            if (toss || cards == null) return;
            W.PlayerBox(c, Gfx.R(40, 140, 260, 210), PName(0), score[0].ToString(), C.Cyan, cur == 0, Time, "Punkte");
            W.PlayerBox(c, Gfx.R(40, 380, 260, 210), PName(1), score[1].ToString(), C.Pink, cur == 1, Time, "Punkte");
            Gfx.Text(c, $"Paare übrig: {20 - matched / 2}", 170, 640, 26, C.Dim, Al.C, false);
            Gfx.Text(c, $"{PName(cur)} ist dran", 170, 690, 26, cur == 0 ? C.Cyan : C.Pink, Al.C, true, 6);
            for (int i = 0; i < 40; i++) DrawCard(c, i);
        }
        void DrawCard(Canvas2D c, int i)
        {
            var cd = cards[i]; var r = Rc(i); float f = cd.Flip.V, sx = MathF.Abs(MathF.Cos(f * MathF.PI)), lift = cd.Hov * 8 + (f > .05f && f < .95f ? MathF.Sin(f * MathF.PI) * 16 : 0);
            float sc = 1 + cd.Hov * .04f + (cd.Matched ? .03f * MathF.Sin(cd.MatchT * 4) : 0);
            if (cd.Lift.V < -300) return;
            // beim Austeilen fliegen die Karten leicht gedreht ein
            float rot = cd.Lift.V * .06f + (f > .05f && f < .95f ? MathF.Sin(f * MathF.PI) * 4 : 0);
            c.Save(); c.Translate(r.MidX, r.MidY - lift + cd.Lift.V); c.RotateDegrees(rot); c.Scale(Math.Max(sx, .02f) * sc, sc);
            var rr = Gfx.Ctr(0, 0, CW, CH); bool face = f > .5f; var hue = Col.FromHsv(cd.Sym * 18, 70, 100);
            Gfx.Shadow(c, rr, 14, 8 + lift * .4f, .42f, 3, 8 + lift);
            if (!face)
            {
                Gfx.Glow(c, rr, 14, C.Purple, 8, .25f + cd.Hov * .5f);
                Gfx.RectGrad(c, rr, 14, new Col(80, 30, 150), new Col(28, 8, 70)); Gfx.Stroke(c, rr, 14, C.Purple.Light(.3f), 2.5f + cd.Hov * 1.5f);
                var inner = Gfx.Inflate(rr, -10); Gfx.Stroke(c, inner, 8, C.Gold.A(.4f), 1.5f);
                // Rautenmuster: Linien auf das Innenfeld zugeschnitten (Clip waere bei Drehung nur achsparallel)
                var p = Gfx.Line(C.Gold.A(.22f + cd.Hov * .12f), 1.2f); p.Glow = 1.25f;
                for (float k = -CH; k < CW + CH; k += 16) { Geo.LineIn(c, inner, -CW / 2 + k, -CH / 2, -CW / 2 + k + CH, CH / 2, p); Geo.LineIn(c, inner, -CW / 2 + k, CH / 2, -CW / 2 + k + CH, -CH / 2, p); }
                Gfx.Light(c, 0, 0, 34, C.Gold, .16f + cd.Hov * .2f, 1.4f);
                Gfx.Suit(c, 2, 0, 0, 18, C.Gold.A(.85f));
                Gfx.RectGrad(c, new Box(rr.Left + 3, rr.Top + 3, rr.Right - 3, rr.Top + CH * .4f), 12, Col.White.A(.09f), Col.White.A(0));
            }
            else
            {
                Gfx.Glow(c, rr, 14, cd.Matched ? C.Gold : hue, 12, cd.Matched ? .9f : .55f);
                Gfx.RectGrad(c, rr, 14, hue.Dark(.55f), hue.Dark(.16f)); Gfx.RectGrad(c, new Box(rr.Left + 3, rr.Top + 3, rr.Right - 3, rr.MidY), 12, Col.White.A(.12f), Col.White.A(0));
                var st = Gfx.Line(cd.Matched ? C.Gold : hue, cd.Matched ? 4 : 2.5f); st.Glow = cd.Matched ? 1.9f : 1.3f; c.DrawRoundRect(rr, 14, 14, st);
                Gfx.Light(c, 0, 0, 60, hue, .32f, 1.3f);
                if (cd.Matched) Gfx.Light(c, 0, 0, 80, C.Gold, .14f + .08f * MathF.Sin(cd.MatchT * 4), 1.8f);
                Gfx.Image(c, Assets.Img(Sym[cd.Sym]), Gfx.Ctr(0, 0, CW * .8f, CW * .8f));
            }
            // vom Computer gewaehlte Karte kurz pink markieren
            if (cd.CpuT >= 0) { float a = 1 - Ease.Clamp(cd.CpuT / 1.2f); Gfx.Glow(c, rr, 14, C.Pink, 14, .9f * a); }
            c.Restore();
        }
    }

    // ==== KI-BEGIN (Unity-frei, wird im Konsolentest mitkompiliert)
    /// <summary>Gedaechtnis und Strategie des Memory-Computers.</summary>
    public sealed class MemoryBrain
    {
        readonly Dictionary<int, int> known = new Dictionary<int, int>();
        /// <summary>Wahrscheinlichkeit, eine gesehene Karte zu behalten / pro Zugwechsel eine Karte zu vergessen.</summary>
        public readonly float Recall, Forget;
        public MemoryBrain(float recall, float forget) { Recall = recall; Forget = forget; }
        /// <summary>Stufe 1 = Leicht, 2 = Mittel, 3 = Schwer.</summary>
        public static MemoryBrain For(int level) => level switch { 1 => new MemoryBrain(.25f, .15f), 2 => new MemoryBrain(.65f, .05f), _ => new MemoryBrain(.95f, .005f) };
        public int Known => known.Count;
        public void Observe(int idx, int sym, Random rnd) { if (!known.ContainsKey(idx) && rnd.NextDouble() < Recall) known[idx] = sym; }
        public void Remove(int idx) => known.Remove(idx);
        /// <summary>Vergessen ueber Zeit (pro Spielerwechsel).</summary>
        public void Decay(Random rnd) { if (Forget <= 0) return; foreach (var k in known.Keys.ToList()) if (rnd.NextDouble() < Forget) known.Remove(k); }
        /// <summary>Erste Karte: bekanntes Paar, sonst eine unbekannte Karte.</summary>
        public int PickFirst(bool[] avail, Random rnd)
        {
            var seen = new Dictionary<int, int>();
            foreach (var kv in known.OrderBy(k => k.Key))
            {
                if (!avail[kv.Key]) continue;
                if (seen.TryGetValue(kv.Value, out var other)) return other;
                seen[kv.Value] = kv.Key;
            }
            return PickUnknown(avail, -1, rnd);
        }
        /// <summary>Zweite Karte: die bekannte passende, sonst eine unbekannte.</summary>
        public int PickSecond(int first, int sym, bool[] avail, Random rnd)
        {
            foreach (var kv in known) if (kv.Key != first && kv.Value == sym && avail[kv.Key]) return kv.Key;
            return PickUnknown(avail, first, rnd);
        }
        int PickUnknown(bool[] avail, int not, Random rnd)
        {
            var l = new List<int>(); for (int i = 0; i < avail.Length; i++) if (avail[i] && i != not && !known.ContainsKey(i)) l.Add(i);
            if (l.Count == 0) for (int i = 0; i < avail.Length; i++) if (avail[i] && i != not) l.Add(i);
            return l.Count == 0 ? -1 : l[rnd.Next(l.Count)];
        }
    }
    // ==== KI-END
}
