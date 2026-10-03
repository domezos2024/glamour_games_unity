using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class Blackjack : Scene
    {
        public override string Title => "Black Jack";
        public override Col Acc1 => C.Pink; public override Col Acc2 => C.Green;
        public override string OppKey => "bj";
        // "Computer denkt nach" über dem Panel von Spieler 2
        public override Pt ThinkPos => new Pt(PX[1], 612);
        class Card { public string Rank; public int Suit; public float X, Y, SX, SY, T, Lift; public Spring Flip = new Spring(0) { K = 170, D = 20 }; public bool Up; public float Rot; }
        class Hand { public List<Card> Cards = new List<Card>(); public string Status = "waiting", Result; public long Bet = 10, Credits = 1000; public bool Doubled; }
        readonly Hand[] pl = { new Hand(), new Hand() }; readonly List<Card> dealer = new List<Card>(); bool revealed; string phase = "betting"; int turn = -1; List<(string r, int s)> shoe = new List<(string, int)>(); string msg = "";
        Button bDeal, bHit, bStand, bDouble, bRefill, bNew, oppBtn; readonly Button[] bm = new Button[2], bp = new Button[2];
        const float CWd = 108, Off = 46; static readonly float[] PX = { 420, 1180 }; const float DY = 250, PY = 450; static readonly Pt Shoe = new Pt(1330, 190);
        // Guthaben-Schlüssel: der Computer hat ein eigenes Konto, damit er das Guthaben von Spieler 2 nicht verspielt
        readonly string[] keys = { "bj_c0", "bj_c1" };
        string KeyFor(int i) => i == 1 && VsCpu ? "bj_cpu" : "bj_c" + i;
        // Generationszähler gegen verspätete Computeraktionen nach Neustart
        int gen; bool cpuBusy, cpuBetDone = true;
        bool CpuTurn => VsCpu && turn == 1;

        public override void Enter()
        {
            base.Enter(); for (int i = 0; i < 2; i++) { int k = i; float cx = PX[i]; bm[i] = Ui.Add(new Button(cx + 60, 700, 70, 46, "-10", C.Purple, () => Bet(k, -10), 22)); bp[i] = Ui.Add(new Button(cx + 140, 700, 70, 46, "+10", C.Purple, () => Bet(k, 10), 22)); }
            bDeal = Ui.Add(new Button(650, 795, 300, 70, "AUSTEILEN", C.Green, HumanDeal, 32));
            bHit = Ui.Add(new Button(440, 795, 220, 70, "KARTE", C.Cyan, () => { if (!CpuTurn) Co.Start(Hit()); }, 30)); bStand = Ui.Add(new Button(690, 795, 220, 70, "HALTEN", C.Orange, () => { if (!CpuTurn) Stand(); }, 30)); bDouble = Ui.Add(new Button(940, 795, 220, 70, "VERDOPPELN", C.Pink, () => { if (!CpuTurn) Co.Start(Double()); }, 26));
            bRefill = Ui.Add(new Button(1180, 795, 250, 60, "Guthaben auffüllen", C.Gold, () => { foreach (var h in pl) if (h.Credits < 10) { h.Credits = 1000; } Persist(); Sfx.Play(S.Coin); }, 20));
            bNew = Ui.Add(new Button(40, 790, 230, 56, "Neues Spiel", C.Purple, AskNew, 22));
            oppBtn = Opponents.AddSwitch(this, OppKey, 1316, 14, 260, 70, NewMatch);
            LoadCredits(); msg = "Setzt eure Einsätze und drückt AUSTEILEN!";
            Opponents.Pick(this, OppKey, o => NewMatch());
        }
        void LoadCredits() { for (int i = 0; i < 2; i++) { keys[i] = KeyFor(i); pl[i].Credits = Save.Int(keys[i], 1000); } }
        /// <summary>Neues Match (auch nach Gegnerwechsel): laufende Runde abbrechen, Einsätze zurück, Guthaben des neuen Gegners laden.</summary>
        void NewMatch()
        {
            gen++; CancelCpuThink(); cpuBusy = false; Co.Clear();
            if (phase != "betting" && phase != "payout") foreach (var h in pl) h.Credits += h.Bet;
            Persist(); LoadCredits(); NewRound();
        }
        void AskNew()
        {
            Result("Neues Spiel?", "Guthaben und Kartenstapel werden zurückgesetzt", C.Pink, ("Ja, neu starten", C.Green, () =>
            {
                gen++; CancelCpuThink(); cpuBusy = false; Co.Clear(); Save.Set("bj_c0", 1000); Save.Set("bj_c1", 1000); Save.Set("bj_cpu", 1000);
                LoadCredits(); shoe.Clear(); foreach (var h in pl) h.Bet = 10; NewRound();
            }), ("Abbrechen", C.Dim, null));
        }
        void Persist() { for (int i = 0; i < 2; i++) Save.Set(keys[i], pl[i].Credits); }
        void Bet(int i, int d) { if (phase != "betting") return; pl[i].Bet = Math.Clamp(pl[i].Bet + d, 10, Math.Max(10, pl[i].Credits)); Sfx.Play(S.Chip); }
        static int Val(List<Card> cs) => BlackjackAI.Total(cs.Select(c => c.Rank), out _);
        static bool IsBJ(List<Card> cs) => cs.Count == 2 && Val(cs) == 21;
        void BuildShoe() { shoe.Clear(); for (int d = 0; d < 4; d++) for (int s = 0; s < 4; s++) foreach (var r in CardArt.Ranks) shoe.Add((r, s)); Rng.Shuffle(shoe); }
        IEnumerator<object> DealTo(List<Card> hand, bool up)
        {
            var (r, s) = shoe[shoe.Count - 1]; shoe.RemoveAt(shoe.Count - 1); var c = new Card { Rank = r, Suit = s, X = Shoe.X, Y = Shoe.Y, SX = Shoe.X, SY = Shoe.Y, Up = up, Rot = 20 }; hand.Add(c); c.Flip.Target = up ? 1 : 0; Sfx.Play(S.Deal); yield return .34f;
        }
        void HumanDeal() { if (VsCpu && !cpuBetDone) return; Co.Start(Deal()); }
        IEnumerator<object> Deal()
        {
            if (phase != "betting") yield break;
            foreach (var h in pl) if (h.Bet > h.Credits || h.Bet < 10) { msg = "Einsatz übersteigt Guthaben!"; yield break; }
            phase = "dealing"; msg = "Karten werden ausgeteilt ..."; revealed = false; BuildShoe(); dealer.Clear();
            foreach (var h in pl) { h.Cards.Clear(); h.Status = "playing"; h.Result = null; h.Doubled = false; h.Credits -= h.Bet; }
            Persist();
            yield return DealTo(pl[0].Cards, true); yield return DealTo(pl[1].Cards, true); yield return DealTo(dealer, true);
            yield return DealTo(pl[0].Cards, true); yield return DealTo(pl[1].Cards, true); yield return DealTo(dealer, false);
            for (int i = 0; i < 2; i++) { var h = pl[i]; if (IsBJ(h.Cards)) { h.Status = "blackjack"; Sfx.Play(S.Big); Fx.Burst(PX[i], PY, 40, null, 350); Pop("BLACKJACK!", PX[i], PY - 130, C.Gold, 46); } }
            Advance();
        }
        void Advance()
        {
            if (pl[0].Status == "playing") { phase = "p0"; turn = 0; msg = PName(0) + " ist am Zug"; }
            else if (pl[1].Status == "playing") { phase = "p1"; turn = 1; msg = PName(1) + " ist am Zug"; }
            else { phase = "dealer"; turn = -1; Co.Start(DealerCo()); }
            if (turn >= 0) Sfx.Play(S.Turn, .5f);
        }
        IEnumerator<object> Hit()
        {
            if (turn < 0 || phase == "busy") yield break; int i = turn; string ph = phase; phase = "busy"; yield return DealTo(pl[i].Cards, true); phase = ph;
            int v = Val(pl[i].Cards); if (v > 21) { pl[i].Status = "bust"; Sfx.Play(S.Lose); App.Shake(6); Pop("BUST!", PX[i], PY - 130, C.Red, 46); yield return .6f; Advance(); } else if (v == 21) { pl[i].Status = "stood"; yield return .3f; Advance(); }
        }
        void Stand() { if (turn < 0 || phase == "busy") return; pl[turn].Status = "stood"; Advance(); }
        IEnumerator<object> Double()
        {
            if (turn < 0 || phase == "busy") yield break; int i = turn; var h = pl[i]; if (h.Cards.Count != 2 || h.Credits < h.Bet) yield break;
            string ph = phase; phase = "busy"; h.Credits -= h.Bet; h.Bet *= 2; h.Doubled = true; Persist(); Sfx.Play(S.Chip); yield return DealTo(h.Cards, true); phase = ph;
            h.Status = Val(h.Cards) > 21 ? "bust" : "stood"; if (h.Status == "bust") { Sfx.Play(S.Lose); Pop("BUST!", PX[i], PY - 130, C.Red, 46); }
            yield return .6f; Advance();
        }
        IEnumerator<object> DealerCo()
        {
            msg = "Bank deckt auf ..."; yield return .5f; dealer[1].Flip.Target = 1; revealed = true; Sfx.Play(S.Flip); yield return .8f;
            if (!pl.All(p => p.Status == "bust")) while (Val(dealer) < 17) { msg = "Bank zieht ..."; yield return DealTo(dealer, true); yield return .35f; }
            Resolve();
        }
        void Resolve()
        {
            int d = Val(dealer); bool dBust = d > 21, dBJ = IsBJ(dealer); bool anyWin = false;
            for (int i = 0; i < 2; i++)
            {
                var p = pl[i]; string r; int v = Val(p.Cards);
                if (p.Status == "bust") r = "lose"; else if (p.Status == "blackjack") r = dBJ ? "push" : "blackjack"; else if (dBJ) r = "lose"; else if (dBust || v > d) r = "win"; else if (v < d) r = "lose"; else r = "push";
                p.Result = r; if (r == "win") p.Credits += p.Bet * 2; else if (r == "blackjack") p.Credits += (long)Math.Round(p.Bet * 2.5); else if (r == "push") p.Credits += p.Bet;
                if (r == "win" || r == "blackjack") { anyWin = true; Fx.Burst(PX[i], PY, 60, null, 420); Gfx_Flare(i); }
            }
            Persist(); phase = "payout"; turn = -1; msg = "Runde beendet - neue Runde starten!";
            if (anyWin) { Sfx.Play(S.Win); App.Flash(C.Gold, .25f); Celebrate(C.Gold, 3, .7f); } else Sfx.Play(S.Lose);
        }
        // Zusätzlicher Lichtring beim Gewinn (Grafik-Aufwertung)
        void Gfx_Flare(int i) { Fx.Shockwave(PX[i], PY, C.Gold, 220, .7f); Fx.Ring(PX[i], PY, C.Gold, 24, 260); }
        void NewRound() { phase = "betting"; turn = -1; foreach (var h in pl) { h.Cards.Clear(); h.Status = "waiting"; h.Result = null; h.Bet = Math.Clamp(h.Bet, 10, Math.Max(10, h.Credits)); } dealer.Clear(); revealed = false; msg = "Setzt eure Einsätze und drückt AUSTEILEN!"; CpuBet(); }
        public override void KeyDown(Key k)
        {
            if (phase == "betting" && (k == Key.Space || k == Key.Enter)) HumanDeal(); else if (phase == "payout" && (k == Key.Space || k == Key.Enter)) NewRound();
            else if (CpuTurn) return;
            else if (k == Key.H) Co.Start(Hit()); else if (k == Key.S) Stand(); else if (k == Key.D) Co.Start(Double());
        }

        // ---------------------------------------------------------------- Computer-Gegner (Spieler 2)
        /// <summary>Computer setzt in der Setzphase schrittweise über dieselben +/- Wege wie ein Mensch.</summary>
        void CpuBet()
        {
            if (!VsCpu || phase != "betting") { cpuBetDone = true; return; }
            cpuBetDone = false; int g = gen;
            CpuThink(Opponents.ThinkTime(Opp), () =>
            {
                if (g != gen || phase != "betting") return;
                long target = BlackjackAI.Bet((int)Opp, pl[1].Credits, Rng.Shared), diff = target - pl[1].Bet;
                int steps = (int)Math.Min(5, Math.Abs(diff) / 10);
                if (steps == 0) { cpuBetDone = true; return; }
                long per = diff / 10 / steps * 10;
                for (int k = 0; k < steps; k++)
                {
                    bool last = k == steps - 1;
                    Tm.After(k * .16f, () => { if (g != gen || phase != "betting") return; Bet(1, (int)(last ? target - pl[1].Bet : per)); });
                }
                Tm.After(steps * .16f + .1f, () => { if (g == gen) cpuBetDone = true; });
            });
        }
        /// <summary>Computer ist am Zug: nach Bedenkzeit Karte / Halten / Verdoppeln über die normalen Aktionen.</summary>
        void CpuDrive()
        {
            if (!VsCpu || phase != "p1" || turn != 1 || cpuBusy || Modal != null || CpuThinking) return;
            cpuBusy = true; int g = gen;
            CpuThink(Opponents.ThinkTime(Opp), () =>
            {
                if (g != gen) return; cpuBusy = false;
                if (phase != "p1" || turn != 1) return;
                var h = pl[1]; int v = BlackjackAI.Total(h.Cards.Select(c => c.Rank), out bool soft); int up = BlackjackAI.CardVal(dealer[0].Rank);
                bool canD = h.Cards.Count == 2 && h.Credits >= h.Bet;
                int a = BlackjackAI.Decide((int)Opp, v, soft, up, canD, Rng.Shared);
                Pop(a == BlackjackAI.Hit ? "KARTE" : a == BlackjackAI.Double ? "VERDOPPELN" : "HALTEN", PX[1], PY - 190, C.Pink.Light(.3f), 32);
                if (a == BlackjackAI.Hit) Co.Start(Hit()); else if (a == BlackjackAI.Double) Co.Start(Double()); else Stand();
            });
        }

        public override void Update(float dt)
        {
            bool bet = phase == "betting", pt = phase == "p0" || phase == "p1";
            bDeal.Visible = bet || phase == "payout"; bDeal.Text = bet ? "AUSTEILEN" : "NEUE RUNDE"; bDeal.Click = bet ? (Action)HumanDeal : NewRound; bDeal.Col = bet ? C.Green : C.Cyan;
            bDeal.Enabled = !(bet && VsCpu && !cpuBetDone);
            bHit.Visible = bStand.Visible = bDouble.Visible = pt; if (pt) { bDouble.Enabled = pl[turn].Cards.Count == 2 && pl[turn].Credits >= pl[turn].Bet && !CpuTurn; bHit.Text = $"KARTE (H)"; bHit.Enabled = bStand.Enabled = !CpuTurn; }
            for (int i = 0; i < 2; i++) { bm[i].Visible = bp[i].Visible = bet && !(i == 1 && VsCpu); }
            bRefill.Visible = (phase == "betting" || phase == "payout") && pl.Any(p => p.Credits < 10);
            Place(dealer, 800, DY, dt); Place(pl[0].Cards, PX[0], PY, dt); Place(pl[1].Cards, PX[1], PY, dt);
            CpuDrive();
        }
        void Place(List<Card> hand, float cx, float cy, float dt)
        {
            int n = hand.Count; float off = Math.Min(Off, 320f / Math.Max(1, n));
            for (int i = 0; i < n; i++)
            {
                var c = hand[i]; float tx = cx + (i - (n - 1) / 2f) * off, ty = cy; c.Flip.Update(dt);
                // Flugbogen als "Anheben" -> Schatten löst sich sichtbar vom Filz
                if (c.T < 1) { c.T += dt / .4f; float e = Ease.OutCubic(c.T); c.X = c.SX + (tx - c.SX) * e; c.Y = c.SY + (ty - c.SY) * e; c.Lift = MathF.Sin(e * MathF.PI) * 60; c.Rot = Ease.Lerp(20, 0, e); }
                else { c.Lift = 0; c.X += (tx - c.X) * Math.Min(1, dt * 12); c.Y += (ty - c.Y) * Math.Min(1, dt * 12); }
            }
        }
        public override void Draw(Canvas2D c)
        {
            // Tisch: weicher Schatten, Holzrand, Filz mit radialem Verlauf, Lampenlicht und leuchtende Goldkanten
            var table = Gfx.R(120, 96, 1360, 570); Gfx.Shadow(c, table, 285, 30, .6f, 0, 22); Gfx.Glow(c, table, 285, C.Gold, 20, .35f);
            Gfx.RectGrad(c, table, 285, new Col(124, 72, 26), new Col(46, 22, 6)); var felt = Gfx.Inflate(table, -16);
            Gfx.RectRadial(c, felt, 270, 800, 380, 700, new Col(22, 126, 74), new Col(5, 46, 30));
            Gfx.Light(c, 800, 340, 560, new Col(255, 244, 205), .07f, 1.2f);
            var rim = Gfx.Line(C.Gold.A(.6f), 3); rim.Glow = 1.5f; c.DrawRoundRect(felt, 270, 270, rim); Gfx.Stroke(c, Gfx.Inflate(felt, -18), 252, C.Gold.A(.22f), 2);
            Gfx.Text(c, "BLACKJACK PAYS 3 : 2", 800, 440, 40, C.Gold.A(.5f), Al.C, true, 0, true); Gfx.Text(c, "Bank zieht bis 17 und bleibt bei 17", 800, 480, 20, Col.White.A(.35f), Al.C, false);
            Gfx.Text(c, msg, 800, 100, 24, C.Yellow.Light(.3f), Al.C, true, 6);
            Gfx.Shadow(c, Gfx.Ctr(Shoe.X, Shoe.Y, 120, 160), 12, 10, .5f, 4, 8); for (int k = 0; k < 4; k++) CardArt.Card(c, Shoe.X + k * 2, Shoe.Y - k * 2, 110, "", 0, 0, 0);
            Gfx.Text(c, $"{shoe.Count} Karten", Shoe.X, Shoe.Y + 110, 18, C.Dim, Al.C, false);
            Gfx.Text(c, "BANK", 800, DY - 100, 22, C.Dim, Al.C, true, 4); DrawHand(c, dealer, -1);
            for (int i = 0; i < 2; i++) DrawPlayer(c, i);
        }
        void DrawHand(Canvas2D c, List<Card> hand, int pi)
        {
            foreach (var cd in hand) CardArt.Card(c, cd.X, cd.Y, CWd, cd.Rank, cd.Suit, cd.Flip.V, cd.Rot, cd.Lift, false);
            if (hand.Count > 0)
            {
                bool dl = pi < 0; var vis = dl && !revealed ? hand.Take(1).ToList() : hand; int v = Val(vis); float cx = hand.Average(x => x.X), y = (dl ? DY : PY) + 118;
                if (hand.All(x => x.T >= 1))
                {
                    var r = Gfx.Ctr(cx, y, 84, 40); var col = v > 21 ? C.Red : v == 21 ? C.Gold : C.Cyan;
                    if (v >= 21) Gfx.Glow(c, r, 20, col, 10, .7f);
                    Gfx.Rect(c, r, 20, Col.Black.A(.55f)); Gfx.Stroke(c, r, 20, col, 2); Gfx.Text(c, v.ToString() + (dl && !revealed ? "+" : ""), r.MidX, r.MidY, 26, v > 21 ? C.Red : v == 21 ? C.Gold : Col.White);
                }
            }
        }
        void DrawPlayer(Canvas2D c, int i)
        {
            var p = pl[i]; float cx = PX[i]; bool act = turn == i; var col = i == 0 ? C.Cyan : C.Pink;
            if (act) Gfx.Light(c, cx, PY, 260, col, .12f, 1.3f);
            DrawHand(c, p.Cards, i);
            var box = Gfx.Ctr(cx, 708, 440, 112); if (act) Gfx.Glow(c, box, 20, col, 16, .5f + .3f * MathF.Sin(Time * 5)); W.Panel(c, box, col);
            Gfx.Text(c, PName(i) + (act ? "  -  am Zug" : ""), cx - 200, 676, 24, act ? col.Light(.4f) : Col.White, Al.L, true, act ? 6 : 0);
            Gfx.Text(c, $"Guthaben: {p.Credits}", cx - 200, 712, 22, C.Gold, Al.L, false); Gfx.Text(c, $"Einsatz: {p.Bet}", cx - 200, 744, 22, Col.White, Al.L, true);
            if (i == 1 && VsCpu && phase == "betting") Gfx.Text(c, cpuBetDone ? "Einsatz steht" : "setzt ...", cx + 135, 723, 20, C.Pink.Light(.4f), Al.C, false);
            if (p.Result != null)
            {
                var (t, cc) = p.Result switch { "win" => ("GEWONNEN!", C.Green), "lose" => ("VERLOREN", C.Red), "push" => ("PUSH", C.Gold), _ => ("BLACKJACK 3:2", C.Gold) };
                var rr = Gfx.Ctr(cx, PY - 140, 300, 50); bool good = p.Result != "lose";
                if (good) { Gfx.Light(c, rr.MidX, rr.MidY, 220, cc, .18f, 1.6f); Gfx.Glow(c, rr, 25, cc, 14, .6f + .2f * MathF.Sin(Time * 4)); }
                Gfx.Shadow(c, rr, 25, 10, .45f, 0, 6);
                Gfx.Rect(c, rr, 25, cc.Dark(.3f).A(.9f)); Gfx.Stroke(c, rr, 25, cc, 3); Gfx.Text(c, t, rr.MidX, rr.MidY, 28, cc.Light(.4f), Al.C, true, 8);
            }
        }
    }

    // ==== KI-BEGIN (Unity-frei, wird im Konsolentest ~/aitest_teamC mitkompiliert) ====
    /// <summary>Black-Jack-Computergegner (Spieler 2). Regeln wie im Spiel: 4 Decks, Bank steht auf 17, kein Teilen, Verdoppeln auf 2 Karten, keine Bank-Vorschau.</summary>
    public static class BlackjackAI
    {
        public const int Hit = 0, Stand = 1, Double = 2;
        public static int CardVal(string r) => r == "A" ? 11 : r == "J" || r == "Q" || r == "K" ? 10 : int.Parse(r);
        /// <summary>Handwert wie im Original (Asse 11, bei Bedarf 1); soft = ein Ass zählt noch 11.</summary>
        public static int Total(IEnumerable<string> ranks, out bool soft)
        {
            int t = 0, a = 0; foreach (var r in ranks) { int v = CardVal(r); if (v == 11) a++; t += v; }
            while (t > 21 && a > 0) { t -= 10; a--; }
            soft = a > 0; return t;
        }
        /// <summary>Vollständige Basisstrategie (Hard/Soft) für diese Regeln. up = Bankkarte 2..11 (Ass = 11).</summary>
        public static int Basic(int total, bool soft, int up, bool canDouble)
        {
            int D = canDouble ? Double : Hit;
            if (!soft)
            {
                if (total >= 17) return Stand;
                if (total >= 13) return up <= 6 ? Stand : Hit;
                if (total == 12) return up >= 4 && up <= 6 ? Stand : Hit;
                if (total == 11) return up <= 9 ? D : Hit;          // ohne Bank-Vorschau: gegen 10/Ass nur ziehen
                if (total == 10) return up <= 9 ? D : Hit;
                if (total == 9) return up >= 3 && up <= 6 ? D : Hit;
                return Hit;
            }
            if (total >= 19) return Stand;
            if (total == 18) { if (up >= 3 && up <= 6 && canDouble) return Double; return up <= 8 ? Stand : Hit; }
            if (total == 17) return up >= 3 && up <= 6 ? D : Hit;
            if (total >= 15) return up >= 4 && up <= 6 ? D : Hit;
            if (total >= 13) return up >= 5 && up <= 6 ? D : Hit;
            return Hit;
        }
        /// <summary>Vereinfachte Basisstrategie (Mittel): keine Soft-Verdopplungen, 12 immer ziehen.</summary>
        public static int Simple(int total, bool soft, int up, bool canDouble)
        {
            if (soft) return total >= 19 || (total == 18 && up <= 8) ? Stand : Hit;
            if (total >= 17) return Stand;
            if (total >= 13) return up <= 6 ? Stand : Hit;
            if ((total == 11 || total == 10) && up <= 9 && canDouble) return Double;
            return Hit;
        }
        public static int Decide(int level, int total, bool soft, int up, bool canDouble, Random rnd)
        {
            if (total >= 21) return Stand;
            if (level >= 3) return Basic(total, soft, up, canDouble);
            if (level == 2) return Simple(total, soft, up, canDouble);
            // Leicht: zieht bis 15/16, macht gelegentlich Fehler und verdoppelt manchmal auf 11
            if (total == 11 && canDouble && rnd.NextDouble() < .3) return Double;
            int a = total < (rnd.NextDouble() < .5 ? 15 : 16) ? Hit : Stand;
            if (total <= 18 && rnd.NextDouble() < .15) a = a == Hit ? Stand : Hit;
            return a;
        }
        /// <summary>Einsatz: Leicht zufällig, Mittel fest 50, Schwer ~5 % des Guthabens.</summary>
        public static long Bet(int level, long credits, Random rnd)
        {
            long max = Math.Max(10, credits), b;
            if (level <= 1) b = 10 * (1 + rnd.Next(20));
            else if (level == 2) b = 50;
            else b = Math.Min(300, credits / 20 / 10 * 10);
            return Math.Clamp(b, 10, max);
        }
    }
    // ==== KI-END ====
}
