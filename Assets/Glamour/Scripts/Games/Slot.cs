using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GlamourGames
{
    // =====================================================================================================
    //  Reine Slot-Logik (Unity-frei, auch im Konsolentest nutzbar): Walzen, Gewinnberechnung, Risikoleiter, KI.
    // =====================================================================================================

    /// <summary>Walzenbaender, Gewinntabelle und Auswertung (1:1 aus dem Original).</summary>
    public static class SlotMath
    {
        public static readonly Dictionary<string, int[]> Pay = new Dictionary<string, int[]>
        {
            ["EXPLORER"] = new[] { 0, 0, 10, 100, 1000, 5000 }, ["PHARAOH"] = new[] { 0, 0, 5, 40, 400, 2000 }, ["GODDESS"] = new[] { 0, 0, 5, 30, 100, 750 }, ["SCARAB"] = new[] { 0, 0, 5, 30, 100, 750 },
            ["A"] = new[] { 0, 0, 0, 5, 40, 150 }, ["K"] = new[] { 0, 0, 0, 5, 40, 150 }, ["Q"] = new[] { 0, 0, 0, 5, 25, 100 }, ["J"] = new[] { 0, 0, 0, 5, 25, 100 }, ["T"] = new[] { 0, 0, 0, 5, 25, 100 }
        };
        public static readonly int[] Scatter = { 0, 0, 0, 2, 20, 200 };
        public static readonly int[][] Lines = { new[] { 1, 1, 1, 1, 1 }, new[] { 0, 0, 0, 0, 0 }, new[] { 2, 2, 2, 2, 2 }, new[] { 0, 1, 2, 1, 0 }, new[] { 2, 1, 0, 1, 2 }, new[] { 1, 2, 2, 2, 1 }, new[] { 1, 0, 0, 0, 1 }, new[] { 2, 2, 1, 0, 0 }, new[] { 0, 0, 1, 2, 2 }, new[] { 2, 1, 1, 1, 0 } };
        public static readonly string[] Ids = { "EXPLORER", "PHARAOH", "GODDESS", "SCARAB", "A", "K", "Q", "J", "T", "BOOK" };
        // 10 Symbole, 1 Buch pro 30er-Walze
        public static readonly int[][] Counts = {
            new[] { 1, 2, 2, 2, 4, 4, 4, 4, 6, 1 }, new[] { 1, 2, 2, 2, 4, 4, 4, 4, 6, 1 }, new[] { 1, 2, 2, 2, 4, 4, 4, 4, 6, 1 }, new[] { 1, 2, 2, 2, 4, 4, 4, 4, 6, 1 }, new[] { 1, 2, 2, 2, 4, 4, 4, 4, 6, 1 } };
        public static readonly string[] Regular = { "EXPLORER", "PHARAOH", "GODDESS", "SCARAB", "A", "K", "Q", "J", "T" };
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string> { ["EXPLORER"] = "Abenteurer", ["PHARAOH"] = "Pharao", ["GODDESS"] = "Göttin", ["SCARAB"] = "Skarabäus", ["A"] = "Ass", ["K"] = "König", ["Q"] = "Dame", ["J"] = "Bube", ["T"] = "Zehn", ["BOOK"] = "Buch" };
        public static readonly int[] LineBets = { 1, 2, 4, 5, 10, 20, 50, 100, 200, 500, 1000 };
        public static readonly string[][] Strips = Enumerable.Range(0, 5).Select(i => MakeStrip(Counts[i], (uint)(1234 + i * 7919))).ToArray();
        public static readonly long[] LadderBase = { 0, 15, 30, 60, 120, 240, 400, 800, 1600, 3200, 6400, 14000 };
        // Deutsches Zahlenformat ohne Abhaengigkeit von installierten Kulturen (IL2CPP/WebGL)
        static readonly NumberFormatInfo De = new NumberFormatInfo { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };
        public static string Eu(long c) => (c / 100.0).ToString("N2", De) + " €";
        public static string Short(long cents) { double e = cents / 100.0; return e >= 1000 ? (e / 1000).ToString("0.#", De) + "k €" : e.ToString(e < 10 ? "0.00" : "0", De) + " €"; }

        public static string[] MakeStrip(int[] counts, uint seed)
        {
            uint s = seed; double Rnd() { s = unchecked(s * 1664525u + 1013904223u); return s / 4294967296.0; }
            var items = new List<(string id, double key)>();
            for (int k = 0; k < Ids.Length; k++) for (int i = 0; i < counts[k]; i++) items.Add((Ids[k], (i + Rnd() * 0.8 + 0.1) / counts[k]));
            var o = items.OrderBy(x => x.key).Select(x => x.id).ToArray(); int L = o.Length;
            for (int pass = 0; pass < 50; pass++)
            {
                bool ok = true;
                for (int i = 0; i < L; i++) { int j = (i + 1) % L; if (o[i] == o[j]) { int k = (i + 2 + (int)Math.Floor(Rnd() * (L - 3))) % L; (o[j], o[k]) = (o[k], o[j]); ok = false; } }
                if (ok) break;
            }
            return o;
        }
        public static string Sym(int r, int i) { var s = Strips[r]; return s[((i % s.Length) + s.Length) % s.Length]; }
        /// <summary>Sichtbares 5x3-Feld fuer die Stopp-Positionen.</summary>
        public static string[][] Grid(int[] stops) { var g = new string[5][]; for (int r = 0; r < 5; r++) g[r] = new[] { Sym(r, stops[r]), Sym(r, stops[r] + 1), Sym(r, stops[r] + 2) }; return g; }
        public static int[] RandomStops(Random rnd) => Strips.Select(s => rnd.Next(s.Length)).ToArray();

        /// <summary>Liniengewinne (Buch = Joker, von links). wins (optional) erhaelt (Linie, Symbol, Anzahl, Gewinn).</summary>
        public static long LineWins(string[][] gr, int lines, long lb, List<(int li, string sym, int n, long win)> wins = null)
        {
            long total = 0; wins?.Clear();
            for (int li = 0; li < lines; li++)
            {
                string bs = null; for (int r = 0; r < 5; r++) { var x = gr[r][Lines[li][r]]; if (x != "BOOK") { bs = x; break; } }
                if (bs == null) continue;
                int n = 0; for (int r = 0; r < 5; r++) { var x = gr[r][Lines[li][r]]; if (x == bs || x == "BOOK") n++; else break; }
                int m = Pay[bs][n]; if (m > 0) { long w = m * lb; total += w; wins?.Add((li, bs, n, w)); }
            }
            return total;
        }
        public static int Books(string[][] gr) => gr.Sum(col => col.Count(x => x == "BOOK"));
        public static long ScatterWin(int books, long tb) => Scatter[Math.Min(books, 5)] * tb;
        /// <summary>Sondersymbol im Freispiel: Walzen mit dem Symbol und der Gewinn (ueber alle gespielten Linien).</summary>
        public static (List<int> reels, long win) Expand(string[][] gr, string fsSym, long lb, int lines)
        {
            var reels = Enumerable.Range(0, 5).Where(r => gr[r].Contains(fsSym)).ToList(); int m = Pay[fsSym][reels.Count];
            return (reels, m > 0 ? m * lb * lines : 0);
        }

        public static long LadderVal(int step, long totalBet)
        {
            if (step <= 0) return 0;
            step = Math.Clamp(step, 0, LadderBase.Length - 1);
            double factor = Math.Max(1.0, totalBet / 10.0);
            return (long)Math.Round(LadderBase[step] * factor);
        }
        public static int FindLadderStep(long amt, long totalBet)
        {
            if (amt <= 0) return 0;
            int best = 1; long bestDiff = long.MaxValue;
            for (int i = 1; i < LadderBase.Length; i++) { long diff = Math.Abs(LadderVal(i, totalBet) - amt); if (diff < bestDiff) { bestDiff = diff; best = i; } }
            return best;
        }

        /// <summary>
        /// Ein kompletter bezahlter Spin inkl. Freispielen ohne Animation (fuer Tests/Simulation), gleiche Regeln wie SpinCo.
        /// Liefert den Gesamtgewinn vor der Risikoleiter.
        /// </summary>
        public static long PlayRound(Random rnd, int lines, long lb)
        {
            long tb = lb * lines; var gr = Grid(RandomStops(rnd));
            int books = Books(gr); long win = LineWins(gr, lines, lb) + ScatterWin(books, tb);
            if (books < 3) return win;
            string fs = Regular[rnd.Next(Regular.Length)]; int free = 10; long sum = win;
            while (free > 0)
            {
                free--; gr = Grid(RandomStops(rnd)); books = Books(gr);
                long w = LineWins(gr, lines, lb) + ScatterWin(books, tb) + Expand(gr, fs, lb, lines).win;
                sum += w; if (books >= 3) free += 10;
            }
            return sum;
        }
    }

    public enum SlotLevel { Easy = 1, Medium = 2, Hard = 3 }
    public enum SlotMove { Take, Risk, Half, Red, Black }

    /// <summary>Was der Computer im Duell ueber die Risiko-Entscheidung weiss.</summary>
    public struct SlotGambleView
    {
        public long Mine, Opp, Amount, TotalBet, OppBet; public int Step, MyTurnsLeft, OppTurnsLeft; public bool CanLadder, CanDouble, CanHalf, Cards;
    }

    /// <summary>Computer-Entscheidungen im Slot-Duell (Einsatz, Linien, Risikoleiter).</summary>
    public static class SlotAi
    {
        static int Fit(long target, int lines, long mine)
        {
            int best = 0; for (int i = 0; i < SlotMath.LineBets.Length; i++) if (SlotMath.LineBets[i] * lines <= Math.Min(target, mine)) best = i;
            return best;
        }
        /// <summary>Einsatz-Stufe und Linienzahl fuer den naechsten Spin. myLeft = eigene Zuege inkl. diesem.</summary>
        public static (int lbIdx, int lines) ChooseBet(SlotLevel lvl, long mine, long opp, int myLeft, int oppLeft, Random rnd)
        {
            int lines; long target;
            switch (lvl)
            {
                case SlotLevel.Easy:
                    {
                        // zufaellig und riskant
                        lines = 1 + rnd.Next(10); int idx = Math.Max(rnd.Next(SlotMath.LineBets.Length), rnd.Next(SlotMath.LineBets.Length));
                        while (idx > 0 && SlotMath.LineBets[idx] * lines > mine) idx--;
                        return Afford(idx, lines, mine);
                    }
                case SlotLevel.Medium:
                    lines = 10; target = mine * 3 / 100; break;   // solide: ca. 3 % des Guthabens
                default:
                    {
                        lines = 10; long diff = mine - opp;
                        if (diff > 0) target = 10;                                               // vorne: minimale Schwankung
                        else if (myLeft <= 1) target = mine;                                    // letzter Zug im Rueckstand: alles
                        else { long deficit = -diff + 1; target = Math.Clamp(deficit * 25 / 10 / myLeft, mine * 2 / 100, mine * 35 / 100); }
                        break;
                    }
            }
            return Afford(Fit(target, lines, mine), lines, mine);
        }
        static (int, int) Afford(int idx, int lines, long mine)
        {
            if (SlotMath.LineBets[idx] * lines > mine) { idx = 0; lines = (int)Math.Max(1, Math.Min(10, mine / SlotMath.LineBets[0])); }
            return (idx, lines);
        }
        /// <summary>Entscheidung im Risiko-Modus (Leiter oder Karten).</summary>
        public static SlotMove Gamble(SlotLevel lvl, SlotGambleView v, Random rnd)
        {
            if (v.Cards) return v.CanDouble && rnd.NextDouble() < .6 ? (rnd.Next(2) == 0 ? SlotMove.Red : SlotMove.Black) : SlotMove.Take;
            if (!v.CanLadder) return SlotMove.Take;
            switch (lvl)
            {
                case SlotLevel.Easy:
                    if (rnd.NextDouble() < .62) return SlotMove.Risk;
                    return v.CanHalf && rnd.NextDouble() < .3 ? SlotMove.Half : SlotMove.Take;
                case SlotLevel.Medium:
                    return v.Step <= 2 && rnd.NextDouble() < .6 ? SlotMove.Risk : SlotMove.Take;
                default:
                    {
                        // Schwer: vorne sichern, hinten riskieren. Die Leiter ist ab Stufe 2 EV-positiv - wenn ein Absturz die
                        // Fuehrung (inkl. Puffer fuer die Restzuege des Gegners) nicht kostet, wird trotzdem hochgedrueckt.
                        long take = v.Mine + v.Amount;
                        long down = v.Mine + (v.Step <= 1 ? 0 : SlotMath.LadderVal(v.Step - 1, v.TotalBet));
                        long margin = Math.Max(v.OppBet, 10) * Math.Max(0, v.OppTurnsLeft) * 3;
                        if (take > v.Opp + margin) return down > v.Opp + margin ? SlotMove.Risk : SlotMove.Take;
                        if (v.MyTurnsLeft > 0 && v.OppTurnsLeft == 0 && take > v.Opp) return SlotMove.Take;
                        return SlotMove.Risk;
                    }
            }
        }
    }

#if !GLAMOUR_AITEST
    // =====================================================================================================
    //  Spielszene
    // =====================================================================================================
    public class SlotGame : Scene
    {
        public override string Title => "Buch der Pharaonen";
        public override Col Acc1 => C.Gold; public override Col Acc2 => C.Orange;
        public override string OppKey => "slot";
        static Dictionary<string, int[]> Pay => SlotMath.Pay;
        static int[][] Lines => SlotMath.Lines;
        static string[] Regular => SlotMath.Regular;
        static Dictionary<string, string> Names => SlotMath.Names;
        static int[] LineBets => SlotMath.LineBets;
        static string[][] Strips => SlotMath.Strips;
        static readonly Col[] LC = { C.Gold, C.Cyan, C.Pink, C.Green, C.Orange, C.Purple, C.Yellow, C.Magenta, C.Blue, C.Red };
        static string Eu(long c) => SlotMath.Eu(c);
        const float CW = 138, GP = 6, RX = 443, RY = 130;

        // ---- Guthaben/Einsatz: Solo (gespeichert) oder Duell (je Spieler, nicht gespeichert) ----
        long soloCredits; readonly long[] dCred = new long[2]; readonly int[] lbP = { 3, 3 }, lnP = { 10, 10 }; readonly long[] lastBetP = { 50, 50 };
        bool duel, duelLive, duelOver, turnLive, turnReady, cpuPlan, cpuCards; int turn, firstP, turnsDone, gen;
        const long DuelStart = 100000; const int DuelRounds = 10;
        int Pi => duel ? turn : 0;
        long Credits { get => duel ? dCred[turn] : soloCredits; set { if (duel) dCred[turn] = value; else soloCredits = value; } }
        int LbIdx { get => lbP[Pi]; set => lbP[Pi] = value; }
        int NLines { get => lnP[Pi]; set => lnP[Pi] = value; }
        void Store() { if (!duel) Save.Set("slot_cents", soloCredits); }
        static long LoadCents() => long.TryParse(Save.Str("slot_cents", "10000"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 10000;
        bool CpuTurn => duel && duelLive && !duelOver && turn == 1;
        /// <summary>Darf der Mensch gerade bedienen? (Solo immer; Duell nur im eigenen Zug.)</summary>
        bool HumanOk => !duel || (duelLive && turnReady && !duelOver && turn == 0);
        int Round => Math.Min(DuelRounds, turnsDone / 2 + 1);
        SlotLevel Lvl => Opp == Opponent.Easy ? SlotLevel.Easy : Opp == Opponent.Hard ? SlotLevel.Hard : SlotLevel.Medium;
        /// <summary>Verbleibende Zuege von Spieler p ab Zugnummer from (inklusive).</summary>
        int TurnsLeft(int p, int from) { int n = 0; for (int t = from; t < DuelRounds * 2; t++) if ((firstP + t) % 2 == p) n++; return n; }

        bool spinning, inFree; int freeSpins; string fsSym; long fsSum, lastWin;
        readonly float[] pos = new float[5], rs = new float[5], re = new float[5], rt = new float[5], rd = new float[5], last = new float[5], bump = new float[5]; readonly bool[] moving = new bool[5];
        string[][] grid = new string[5][]; string msg = ""; List<(int li, string sym, int n, long win)> wins = new List<(int, string, int, long)>(); int showLine = -2; float lineT, prevT; List<int> expReels = new List<int>(); float expT = 99; string expSym;
        (string title, string sub, string img)? overlay; float ovT;
        enum GambleMode { Cards, Ladder }
        GambleMode gMode = GambleMode.Ladder, gModeHuman = GambleMode.Ladder;
        int ladderStep;
        int[] stops = new int[5];
        bool fastStopping;

        class Gamble { public bool Active, Busy; public int Steps; public long Amount, Base; public List<(string rank, int suit)> Hist = new List<(string, int)>(); public string Rank = ""; public int Suit; public Spring Flip = new Spring(0) { K = 200, D = 22 }; }
        Gamble g = new Gamble();
        Button bAuto, bAutoSel, bSpin, bRefill, bBetM, bBetP, bLnM, bLnP, bRed, bBlack, bLadderRisk, bHalf, bTake, bTabCards, bTabLadder;
        long LineBet => LineBets[LbIdx]; long TotalBet => LineBet * NLines;
        long LadderVal(int step) => SlotMath.LadderVal(step, TotalBet);
        int FindLadderStep(long amt) => SlotMath.FindLadderStep(amt, TotalBet);

        public override void Enter()
        {
            base.Enter(); soloCredits = LoadCents();
            for (int r = 0; r < 5; r++) { pos[r] = last[r] = Rng.I(Strips[r].Length); bump[r] = 9; }
            SetGrid();
            bSpin = Ui.Add(new Button(732, 672, 136, 136, "SPIN", C.Gold, () => Spin(), 34) { Round = true });
            bBetM = Ui.Add(new Button(520, 725, 60, 54, "-", C.Purple, () => ChangeBet(-1), 34)); bBetP = Ui.Add(new Button(600, 725, 60, 54, "+", C.Purple, () => ChangeBet(1), 34));
            bLnM = Ui.Add(new Button(940, 725, 60, 54, "-", C.Purple, () => ChangeLines(-1), 34)); bLnP = Ui.Add(new Button(1020, 725, 60, 54, "+", C.Purple, () => ChangeLines(1), 34));
            bAutoSel = Ui.Add(new Button(1098, 725, 60, 54, "25", C.Purple, () => { if (autoOn || duel) return; autoIdx = (autoIdx + 1) % AutoOpts.Length; Sfx.Play(S.Chip); }, 26));
            bAuto = Ui.Add(new Button(1166, 725, 124, 54, "AUTO", C.Cyan, AutoToggle, 20));
            bRefill = Ui.Add(new Button(300, 730, 180, 56, "+100 € Spielgeld", C.Green, () => { if (duel) return; soloCredits += 10000; Store(); Sfx.Play(S.Coin); msg = "Spielgeld aufgefüllt"; }, 20) { Visible = false });

            bTabCards = Ui.Add(new Button(1205, 116, 170, 36, "♠ KARTEN", C.Purple, () => { if (!HumanOk) return; SetMode(GambleMode.Cards); gModeHuman = gMode; Sfx.Play(S.Chip); }, 20) { Visible = false });
            bTabLadder = Ui.Add(new Button(1390, 116, 170, 36, "🪜 LEITER", C.Gold, () => { if (!HumanOk) return; SetMode(GambleMode.Ladder); gModeHuman = gMode; Sfx.Play(S.Chip); }, 20) { Visible = false });
            bRed = Ui.Add(new Button(1205, 442, 170, 52, "ROT", C.Red, () => { if (HumanOk) Guess(true); }, 28) { Visible = false });
            bBlack = Ui.Add(new Button(1390, 442, 170, 52, "SCHWARZ", new Col(70, 70, 90), () => { if (HumanOk) Guess(false); }, 26) { Visible = false });
            bLadderRisk = Ui.Add(new Button(1205, 442, 355, 52, "⬆ RISIKO (1:1)", C.Orange, () => { if (HumanOk) LadderRisk(); }, 24) { Visible = false });
            bHalf = Ui.Add(new Button(1205, 506, 170, 48, "½ TEILEN", C.Cyan, () => { if (HumanOk) Half(); }, 22) { Visible = false });
            bTake = Ui.Add(new Button(1390, 506, 170, 48, "NEHMEN", C.Green, () => { if (HumanOk) Collect(false); }, 24) { Visible = false });
            // Gegnerwahl: "Solo" statt "2 Spieler" (AddSwitch reicht humanSub nicht durch -> Klick lokal ersetzt)
            var sw = Opponents.AddSwitch(this, OppKey, 30, 596, 255, 62, StartMode, "Solo");
            sw.Click = () => Opponents.Pick(this, OppKey, o => { Opp = o; StartMode(); }, "Solo", "allein spielen");
            StartMode();
            Opponents.Pick(this, OppKey, o => StartMode(), "Solo", "allein spielen");
        }
        /// <summary>Startet Solo bzw. ein neues Duell; alles Laufende wird verworfen (Generationszaehler).</summary>
        void StartMode()
        {
            gen++; Co.Clear(); overlay = null; autoOn = false; g.Busy = false; EndGamble(); g.Hist.Clear(); inFree = false; freeSpins = 0; fsSym = null; fsSum = 0;
            spinning = false; fastStopping = false; wins.Clear(); showLine = -2; expReels.Clear(); expT = 99; lastWin = 0; cpuPlan = false;
            for (int r = 0; r < 5; r++) if (moving[r]) { moving[r] = false; pos[r] = re[r]; bump[r] = 0; }
            SetGrid();
            duel = VsCpu; duelLive = duelOver = turnLive = turnReady = false; gMode = gModeHuman;
            if (!duel) { soloCredits = LoadCents(); msg = "Viel Glück! Leertaste = SPIN / STOPP"; return; }
            dCred[0] = dCred[1] = DuelStart; lbP[1] = 3; lnP[1] = 10; turnsDone = 0; turn = 0; lastBetP[0] = lastBetP[1] = 50;
            msg = $"Duell gegen den Computer ({Opponents.Label(Opp)}) - {DuelRounds} Runden";
            int g0 = gen;
            CoinToss.Start(this, first => { if (g0 != gen) return; firstP = first; turn = first; duelLive = true; StartTurn(); });
        }
        void StartTurn()
        {
            if (!duel || duelOver) return;
            turnReady = true; turnLive = false; cpuPlan = false;
            string n = PName(turn); var col = turn == 0 ? C.Cyan : C.Pink;
            msg = turn == 0 ? $"Runde {Round}/{DuelRounds}: {n} ist am Zug - SPIN!" : $"Runde {Round}/{DuelRounds}: {n} ist am Zug ...";
            if (turn == 0) SetMode(gModeHuman);
            Pop(n + " ist dran", 800, 470, col, 44); Sfx.Play(S.Turn, .5f);
        }
        void EndTurn()
        {
            turnReady = false; lastBetP[turn] = TotalBet; turnsDone++;
            if (turnsDone >= DuelRounds * 2 || dCred[0] <= 0 || dCred[1] <= 0) { EndDuel(); return; }
            turn = 1 - turn; int g0 = gen; Tm.After(.7f, () => { if (g0 == gen) StartTurn(); });
        }
        void EndDuel()
        {
            duelOver = true; turnReady = false; long a = dCred[0], b = dCred[1]; string n0 = PName(0), n1 = PName(1); int g0 = gen;
            msg = a > b ? $"{n0} gewinnt das Duell!" : b > a ? $"{n1} gewinnt das Duell!" : "Unentschieden!";
            if (a != b) Celebrate(a > b ? C.Gold : C.Red, 5, 1.1f, (a > b ? n0 : n1) + " gewinnt!"); else Sfx.Play(S.Big);
            if (b > a) Sfx.Play(S.Lose);
            var lines = new List<string> { $"nach {(turnsDone + 1) / 2} von {DuelRounds} Runden" + (a <= 0 || b <= 0 ? " (pleite)" : "") };
            Tm.After(a != b ? 3f : 1.2f, () =>
            {
                if (g0 != gen) return;
                Result(msg, $"{n0} {Eu(a)}  :  {Eu(b)} {n1}", a > b ? C.Gold : b > a ? C.Red : C.Cyan, lines, ("Neues Duell", C.Green, StartMode), ("Menü", C.Purple, () => App.Go(new Menu())));
            });
        }
        void SetMode(GambleMode m)
        {
            if (m == gMode) return; gMode = m;
            if (m == GambleMode.Ladder && g.Active) { ladderStep = FindLadderStep(g.Amount); g.Amount = LadderVal(ladderStep); }
        }

        // ---- Computer im Duell: gleiche Methoden wie die Buttons, ausgeloest ueber CpuThink ----
        void CpuTick()
        {
            if (!CpuTurn || !turnReady || cpuPlan || CpuThinking || Modal != null) return;
            if (overlay != null || spinning || g.Busy || Co.Busy) return;
            int g0 = gen;
            if (g.Active)
            {
                cpuPlan = true;
                CpuThink(Opponents.ThinkTime(Opp) * .8f, () => { cpuPlan = false; if (g0 != gen || !CpuTurn || !g.Active || g.Busy) return; CpuGamble(); });
                return;
            }
            if (turnLive) return;
            cpuPlan = true;
            CpuThink(Opponents.ThinkTime(Opp), () =>
            {
                if (g0 != gen || !CpuTurn) { cpuPlan = false; return; }
                var (lb, ln) = SlotAi.ChooseBet(Lvl, Credits, dCred[0], TurnsLeft(1, turnsDone), TurnsLeft(0, turnsDone + 1), Rng.Shared);
                if (lb != LbIdx || ln != NLines) { LbIdx = lb; NLines = ln; prevT = 1.6f; Sfx.Play(S.Chip); }
                msg = $"{PName(1)} setzt {Eu(TotalBet)} auf {NLines} Linie{(NLines == 1 ? "" : "n")}";
                CpuThink(.5f, () => { cpuPlan = false; if (g0 == gen && CpuTurn) Spin(true); });
            });
        }
        void CpuGamble()
        {
            SetMode(cpuCards ? GambleMode.Cards : GambleMode.Ladder);
            var v = new SlotGambleView
            {
                Mine = Credits, Opp = dCred[0], Amount = g.Amount, TotalBet = TotalBet, OppBet = lastBetP[0], Step = ladderStep, MyTurnsLeft = TurnsLeft(1, turnsDone + 1), OppTurnsLeft = TurnsLeft(0, turnsDone + 1),
                CanLadder = CanLadderStep, CanDouble = CanDouble, CanHalf = !g.Busy && g.Amount >= 2 && ladderStep > 1, Cards = cpuCards
            };
            switch (SlotAi.Gamble(Lvl, v, Rng.Shared))
            {
                case SlotMove.Risk: LadderRisk(); break;
                case SlotMove.Half: Half(); break;
                case SlotMove.Red: Guess(true); break;
                case SlotMove.Black: Guess(false); break;
                default: Collect(false); break;
            }
        }

        void SetGrid() { for (int r = 0; r < 5; r++) grid[r] = Enumerable.Range(0, 3).Select(k => Sym(r, (int)MathF.Floor(pos[r]) + k)).ToArray(); }
        static string Sym(int r, int i) => SlotMath.Sym(r, i);
        void ChangeBet(int d) { if (!HumanOk || spinning || inFree || g.Active) return; LbIdx = Math.Clamp(LbIdx + d, 0, LineBets.Length - 1); Sfx.Play(S.Chip); }
        void ChangeLines(int d) { if (!HumanOk || spinning || inFree || g.Active) return; NLines = Math.Clamp(NLines + d, 1, 10); prevT = 1.6f; Sfx.Play(S.Chip); }
        static readonly int[] AutoOpts = { 25, 50, 100, -1 }; int autoIdx, autoLeft; float autoWait; bool autoOn;
        string AutoLbl(int n) => n < 0 ? "∞" : n.ToString();
        void AutoToggle()
        {
            if (duel) return;
            if (autoOn) { autoOn = false; msg = "Auto-Spiel gestoppt"; Sfx.Play(S.Click); return; }
            autoOn = true; autoLeft = AutoOpts[autoIdx]; autoWait = 0; msg = $"Auto-Spiel: {AutoLbl(autoLeft)} Durchläufe"; Sfx.Play(S.Chip);
        }
        void AutoTick(float dt, bool act)
        {
            if (!autoOn) return;
            if (duel) { autoOn = false; return; }
            if (!act || g.Busy || spinning) { autoWait = .7f; return; }
            if (inFree) return;
            autoWait -= dt; if (autoWait > 0) return;
            if (g.Active) { Collect(true); autoWait = .5f; return; }
            if (autoLeft == 0 || TotalBet > Credits) { autoOn = false; msg = autoLeft == 0 ? "Auto-Spiel beendet" : "Auto-Spiel gestoppt: zu wenig Guthaben"; return; }
            if (autoLeft > 0) autoLeft--; autoWait = .5f; Spin();
        }
        /// <summary>SPIN/STOPP/NEHMEN. cpu = true: vom Computer ausgeloest (Menschen-Sperre gilt nicht).</summary>
        void Spin(bool cpu = false)
        {
            if (!cpu && !HumanOk) return;
            if (overlay != null) { CloseOv(); return; }
            if (spinning) { FastStop(); return; }
            if (Co.Busy) return;
            if (g.Active) { if (g.Busy) return; Collect(true); if (duel) return; }   // im Duell beendet NEHMEN den Zug
            Co.Start(SpinCo());
        }

        void FastStop()
        {
            if (!spinning || fastStopping) return;
            fastStopping = true;
            for (int r = 0; r < 5; r++)
            {
                if (!moving[r]) continue;
                int L = Strips[r].Length; float cur = pos[r]; float curMod = ((cur % L) + L) % L; float dist = curMod - stops[r];
                while (dist < 1.5f) dist += L;
                rs[r] = cur; re[r] = cur - dist; rt[r] = 0; rd[r] = 0.06f + r * 0.035f;
            }
            Sfx.Play(S.Stop, .7f, 1.2f);
            msg = "Schnellstopp!";
        }

        IEnumerator<object> SpinCo()
        {
            bool isFree = inFree && freeSpins > 0; long tb = TotalBet, lb = LineBet;
            if (!isFree)
            {
                if (tb > Credits) { msg = duel ? "Nicht genug Guthaben - Einsatz senken." : "Nicht genug Guthaben - Einsatz senken oder Spielgeld auffüllen."; yield break; }
                Credits -= tb; Store(); lastWin = 0; if (duel) turnLive = true;
            }
            else freeSpins--;
            spinning = true; fastStopping = false; showLine = -2; wins.Clear(); expReels.Clear(); expT = 99;
            msg = isFree ? $"Freispiel ... ({freeSpins} übrig)" : "Walzen drehen ...";
            Sfx.Play(S.Spin);

            // Stopps rein zufaellig und unabhaengig (Casino-RNG)
            stops = SlotMath.RandomStops(Rng.Shared);

            for (int r = 0; r < 5; r++)
            {
                int L = Strips[r].Length; float st = pos[r]; float minTravel = L * (2 + r * .6f); int e = stops[r] + L * (int)MathF.Floor((st - minTravel - stops[r]) / L);
                rs[r] = st; re[r] = e; rt[r] = 0; rd[r] = .9f + r * .3f; moving[r] = true;
            }
            yield return (Func<bool>)(() => !moving.Any(m => m));
            spinning = false; fastStopping = false;
            SetGrid(); var gr = grid;
            long lineTotal = SlotMath.LineWins(gr, NLines, lb, wins);
            int books = SlotMath.Books(gr); long scWin = SlotMath.ScatterWin(books, tb); long win = lineTotal + scWin;
            if (wins.Count > 0) { Sfx.Play(win >= tb * 10 ? S.Big : S.Match); yield return ShowWins(); }
            if (scWin > 0) { msg = $"{books}× Buch (Scatter) = {Eu(scWin)}"; Sfx.Play(S.Match); BookFx(gr); yield return .7f; }
            if (isFree && fsSym != null)
            {
                var (reels, ex) = SlotMath.Expand(gr, fsSym, lb, NLines);
                if (ex > 0) { msg = $"{Names[fsSym]} expandiert auf {reels.Count} Walzen!"; expReels = reels; expSym = fsSym; expT = 0; Sfx.Play(S.Big); App.Shake(10); App.Flash(C.Gold, .25f); yield return 1.7f; win += ex; msg = $"Sondersymbol-Gewinn: {Eu(ex)}"; yield return .5f; expReels.Clear(); expT = 99; }
            }
            lastWin = win;
            if (win >= tb * 20) { Celebrate(C.Gold, 5, 1.2f); for (int k = 0; k < 4; k++) { int kk = k; Tm.After(.3f * kk, () => Fx.Lightning(400 + kk * 270, -20, 400 + kk * 270, 420, C.Gold)); } }
            if (isFree)
            {
                fsSum += win;
                if (books >= 3) { freeSpins += 10; yield return Overlay("+10 FREISPIELE!", $"{books}× Buch - Freispiele verlängert", "BOOK", 2.2f); }
                if (freeSpins > 0) { msg = win > 0 ? $"Gewinn: {Eu(win)}" : "Kein Gewinn"; yield return 1.0f; Co.Start(SpinCo()); yield break; }
                long total = fsSum; inFree = false; fsSym = null; fsSum = 0;
                yield return Overlay("FREISPIELE BEENDET", $"Gesamtgewinn: {Eu(total)}", "BOOK", 3f); lastWin = total;
                if (total > 0) { msg = $"Freispiel-Gewinn: {Eu(total)} - Risiko oder Nehmen"; StartGamble(total); } else msg = "Freispiele ohne Gewinn.";
                yield break;
            }
            if (books >= 3)
            {
                fsSym = Regular[Rng.I(Regular.Length)]; inFree = true; freeSpins = 10; fsSum = win;
                yield return Overlay("10 FREISPIELE!", $"Sondersymbol: {Names[fsSym]}", fsSym, 3.2f); yield return .4f; Co.Start(SpinCo()); yield break;
            }
            if (win > 0) { msg = $"GEWINN: {Eu(win)} - Risiko oder Nehmen"; StartGamble(win); } else msg = "Leider kein Gewinn - nochmal!";
        }
        void BookFx(string[][] gr)
        {
            for (int r = 0; r < 5; r++) for (int k = 0; k < 3; k++) if (gr[r][k] == "BOOK") { var p = Ctr(r, k); Fx.Ring(p.X, p.Y, C.Gold, 24, 220); Fx.Spark(p.X, p.Y, C.Yellow, 8, 200); }
        }
        IEnumerator<object> ShowWins()
        {
            showLine = -1; yield return .9f; int n = Math.Min(wins.Count, 5);
            for (int i = 0; i < n; i++) { showLine = wins[i].li; lineT = 0; msg = $"Linie {wins[i].li + 1}: {wins[i].n}× {Names[wins[i].sym]} = {Eu(wins[i].win)}"; Sfx.Play(S.Chip); yield return .8f; }
            showLine = -1;
        }
        IEnumerator<object> Overlay(string t, string s, string img, float auto)
        {
            overlay = (t, s, img); ovT = 0; Sfx.Play(S.Big); Celebrate(C.Gold, 3, .8f); int g0 = gen;
            Tm.After(auto, () => { if (g0 == gen && overlay != null && overlay.Value.title == t) CloseOv(); });
            yield return (Func<bool>)(() => overlay == null);
        }
        void CloseOv() { overlay = null; }
        void StartGamble(long amt)
        {
            g.Active = true; g.Steps = 0; g.Amount = amt; g.Base = TotalBet; g.Busy = false; g.Flip.Snap(0);
            ladderStep = FindLadderStep(amt); g.Amount = LadderVal(ladderStep);
            // Leichter Computer probiert gern die Karten, sonst Leiter
            cpuCards = CpuTurn && Lvl == SlotLevel.Easy && Rng.F() < .5f;
            if (CpuTurn) gMode = cpuCards ? GambleMode.Cards : GambleMode.Ladder;
        }
        bool CanDouble => g.Active && !g.Busy && g.Amount > 0 && g.Steps < 5 && g.Amount * 2 <= g.Base * 1000;
        bool CanLadderStep => g.Active && !g.Busy && g.Amount > 0 && ladderStep < 11;
        void Guess(bool red)
        {
            if (!CanDouble) return; g.Busy = true; var suits = new[] { 1, 2, 0, 3 }; g.Suit = suits[Rng.I(4)]; g.Rank = CardArt.Ranks[Rng.I(13)]; g.Flip.Target = 1; Sfx.Play(S.Flip); int g0 = gen;
            Tm.After(.7f, () =>
            {
                if (g0 != gen) return;
                bool isRed = g.Suit == 1 || g.Suit == 2; g.Hist.Insert(0, (g.Rank, g.Suit)); if (g.Hist.Count > 8) g.Hist.RemoveAt(8);
                if (isRed == red)
                {
                    g.Amount *= 2; g.Steps++; ladderStep = FindLadderStep(g.Amount); lastWin = g.Amount; msg = $"{g.Rank} - RICHTIG! Neuer Betrag: {Eu(g.Amount)}"; Sfx.Play(S.Match); Fx.Burst(1380, 290, 40, new[] { C.Gold, C.Green }, 320); Fx.Shockwave(1380, 290, C.Gold, 160, .5f); g.Busy = false;
                    Tm.After(1.0f, () => { if (g0 == gen && g.Active && !g.Busy) g.Flip.Target = 0; });
                    if (!CanDouble) { msg += " - Limit erreicht, wird gutgeschrieben."; g.Busy = true; Tm.After(1.6f, () => { if (g0 != gen) return; g.Busy = false; Collect(false); }); }
                }
                else { msg = $"{g.Rank} - FALSCH! {Eu(g.Amount)} verloren."; g.Amount = 0; lastWin = 0; ladderStep = 0; Sfx.Play(S.Die); App.Shake(8); Tm.After(1.6f, () => { if (g0 != gen) return; g.Busy = false; EndGamble(); }); }
            });
        }

        void LadderRisk()
        {
            if (!CanLadderStep) return;
            g.Busy = true; Sfx.Play(S.Chip); int g0 = gen;
            Tm.After(0.35f, () =>
            {
                if (g0 != gen || !g.Active) return;
                bool up = Rng.I(2) == 0;
                if (up)
                {
                    ladderStep = Math.Min(11, ladderStep + 1); g.Amount = LadderVal(ladderStep); lastWin = g.Amount; Sfx.Play(S.Match);
                    Fx.Burst(1380, 178 + (11 - ladderStep) * 21 + 10, 26, new[] { C.Gold, C.Green }, 240);
                    msg = $"Leiter: {Eu(g.Amount)}! Hochdrücken oder Nehmen"; g.Busy = false;
                    if (ladderStep == 11)
                    {
                        Celebrate(C.Gold, 6, 1.4f); Sfx.Play(S.Big); App.Flash(C.Gold, .4f); msg = $"LEITER VOLL: {Eu(g.Amount)}!"; g.Busy = true;
                        Tm.After(2.0f, () => { if (g0 != gen) return; g.Busy = false; Collect(false); });
                    }
                }
                else
                {
                    if (ladderStep <= 1)
                    {
                        ladderStep = 0; g.Amount = 0; lastWin = 0; msg = "Abgestürzt auf 0,00 €!"; Sfx.Play(S.Die); App.Shake(8);
                        Tm.After(1.4f, () => { if (g0 != gen) return; g.Busy = false; EndGamble(); });
                    }
                    else
                    {
                        ladderStep = Math.Max(0, ladderStep - 1); g.Amount = LadderVal(ladderStep); lastWin = g.Amount;
                        if (ladderStep == 0) { msg = "Abgestürzt auf 0,00 €!"; Sfx.Play(S.Die); App.Shake(8); Tm.After(1.4f, () => { if (g0 != gen) return; g.Busy = false; EndGamble(); }); }
                        else { msg = $"Abgestürzt auf {Eu(g.Amount)}! Nochmal hochdrücken oder Nehmen"; Sfx.Play(S.NoMatch); g.Busy = false; }
                    }
                }
            });
        }

        void Half()
        {
            if (!g.Active || g.Busy || g.Amount < 2 || ladderStep <= 1) return;
            long h = g.Amount / 2; Credits += h; Store(); g.Amount -= h;
            ladderStep = FindLadderStep(g.Amount); g.Amount = LadderVal(ladderStep); lastWin = g.Amount;
            msg = $"{Eu(h)} gesichert - {Eu(g.Amount)} verbleiben im Risiko."; Sfx.Play(S.Coin);
        }
        void Collect(bool silent)
        {
            if (!g.Active || g.Busy) return;
            long a = g.Amount; Credits += a; Store();
            if (!silent && a > 0) { Sfx.Play(S.Coin); msg = $"{Eu(a)} gutgeschrieben."; Fx.Burst(400, 690, 30, new[] { C.Gold, C.Yellow }, 300); }
            EndGamble();
        }
        void EndGamble() { g.Active = false; g.Amount = 0; g.Busy = false; ladderStep = 0; g.Flip.Snap(0); }

        public override void KeyDown(Key k)
        {
            if (!HumanOk) return;   // im Duell waehrend des Computerzugs keine Eingaben
            if (overlay != null) { CloseOv(); return; }
            if (k == Key.Space || k == Key.Enter)
            {
                if (spinning) FastStop();
                else if (g.Active) { if (gMode == GambleMode.Ladder) LadderRisk(); else Guess(true); }
                else Spin();
            }
            else if (k == Key.Up) ChangeBet(1);
            else if (k == Key.Down) ChangeBet(-1);
            else if (k == Key.Right) ChangeLines(1);
            else if (k == Key.Left) ChangeLines(-1);
        }
        public override void MouseUp(float x, float y) { if (overlay != null && HumanOk) CloseOv(); }
        public override Pt ThinkPos => new Pt(800, 838);
        public override void Update(float dt)
        {
            ovT += dt; expT += dt; lineT += dt; prevT = Math.Max(0, prevT - dt); g.Flip.Update(dt);
            for (int r = 0; r < 5; r++)
            {
                last[r] = pos[r]; bump[r] += dt;
                if (moving[r])
                {
                    rt[r] += dt; float p = Ease.Clamp(rt[r] / rd[r]); pos[r] = rs[r] + (re[r] - rs[r]) * (1 - MathF.Pow(1 - p, 3));
                    if (p >= 1) { moving[r] = false; pos[r] = re[r]; bump[r] = 0; Sfx.Play(S.Stop, .5f, 1 + r * .05f); float x = RX + r * (CW + GP) + CW / 2, y = RY + 1.5f * (CW + GP); Fx.Spark(x, y + 60, C.Gold, 5, 150); }
                }
            }
            bool act = !spinning && !Co.Busy && overlay == null;
            bool human = HumanOk;
            if (spinning) { bSpin.Enabled = !fastStopping && human; bSpin.Text = fastStopping ? "..." : "STOPP"; }
            else { bSpin.Enabled = act && !g.Busy && human; bSpin.Text = inFree ? "FREI" : g.Active ? "NEHMEN" : "SPIN"; }
            bool can = act && !inFree && !g.Active && human;
            bAuto.Visible = bAutoSel.Visible = !duel;
            bAuto.Text = autoOn ? $"STOP ({AutoLbl(autoLeft)})" : "AUTO"; bAuto.Col = autoOn ? C.Red : C.Cyan; bAuto.Selected = autoOn; bAuto.Enabled = autoOn || can;
            bAutoSel.Text = autoOn ? AutoLbl(autoLeft) : AutoLbl(AutoOpts[autoIdx]); bAutoSel.Size = 22; bAutoSel.Enabled = !autoOn && can;
            AutoTick(dt, act);
            bBetM.Enabled = bBetP.Enabled = bLnM.Enabled = bLnP.Enabled = can;
            bRefill.Visible = !duel && Credits < TotalBet && can;

            bool gv = g.Active && overlay == null;
            bTabCards.Visible = bTabLadder.Visible = gv; bTabCards.Enabled = bTabLadder.Enabled = human;
            bTabCards.Col = gMode == GambleMode.Cards ? C.Gold : C.Purple; bTabCards.Selected = gMode == GambleMode.Cards;
            bTabLadder.Col = gMode == GambleMode.Ladder ? C.Gold : C.Purple; bTabLadder.Selected = gMode == GambleMode.Ladder;
            if (gMode == GambleMode.Cards) { bRed.Visible = bBlack.Visible = gv; bLadderRisk.Visible = false; bRed.Enabled = bBlack.Enabled = CanDouble && human; }
            else { bRed.Visible = bBlack.Visible = false; bLadderRisk.Visible = gv; bLadderRisk.Enabled = CanLadderStep && human; }
            bHalf.Visible = bTake.Visible = gv;
            bHalf.Enabled = !g.Busy && g.Amount >= 2 && ladderStep > 1 && human;
            bTake.Enabled = !g.Busy && g.Amount > 0 && human;

            // Duell: Zugende erkennen (Spin + Freispiele + Risiko abgeschlossen), Computer steuern
            if (duel && duelLive && !duelOver && turnReady && turnLive && !spinning && !Co.Busy && !g.Active && !g.Busy && overlay == null) { turnLive = false; EndTurn(); }
            CpuTick();
        }

        // ---- Zeichnen ----
        static Box Cell(int r, int k) => Gfx.R(RX + r * (CW + GP), RY + k * (CW + GP), CW, CW);
        static Pt Ctr(int r, int k) { var c = Cell(r, k); return new Pt(c.MidX, c.MidY); }
        static void DrawCell(Canvas2D c, Box r, string sym, float scale = 1, float dim = 0, bool hl = false, float pulse = 0)
        {
            c.Save(); c.Translate(r.MidX, r.MidY); c.Scale(scale, scale); c.Translate(-r.MidX, -r.MidY);
            if (hl) { Gfx.Glow(c, r, 14, C.Gold, 12, .7f + .3f * pulse); Gfx.Light(c, r.MidX, r.MidY, CW * .9f, C.Gold, .25f + .15f * pulse, 1.8f); }
            Gfx.RectGrad(c, r, 14, new Col(255, 243, 208), new Col(212, 172, 100));
            Gfx.RectGrad(c, new Box(r.Left + 3, r.Top + 3, r.Right - 3, r.Top + r.Height * .42f), 12, Col.White.A(.35f), Col.White.A(0));
            var st = Gfx.Line(hl ? C.Gold : new Col(120, 80, 20), hl ? 5 : 3); if (hl) st.Glow = 1.8f; c.DrawRoundRect(r, 14, 14, st);
            Gfx.Image(c, Assets.Img("slot_" + sym), Gfx.Inflate(r, -9)); if (dim > 0) Gfx.Rect(c, r, 14, Col.Black.A(dim)); c.Restore();
        }
        public override void Draw(Canvas2D c)
        {
            DrawPaytable(c);
            var frame = Gfx.R(RX - 22, RY - 22, 5 * CW + 4 * GP + 44, 3 * CW + 2 * GP + 44);
            Gfx.Shadow(c, frame, 26, 30, .7f, 0, 20);
            Gfx.Glow(c, frame, 26, C.Gold, 24, .45f + .1f * MathF.Sin(Time * 2));
            Gfx.RectGrad(c, frame, 26, new Col(110, 60, 18), new Col(36, 16, 4));
            var fr = Gfx.Line(C.Gold, 4); fr.Glow = 1.7f; c.DrawRoundRect(frame, 26, 26, fr); Gfx.Stroke(c, Gfx.Inflate(frame, -8), 20, C.Gold.A(.4f), 2);
            // Lauflichter im Rahmen
            for (int i = 0; i < 24; i++)
            {
                float t = i / 24f, per = 2 * (frame.Width + frame.Height), d = t * per, x, y;
                if (d < frame.Width) { x = frame.Left + d; y = frame.Top + 4; } else if (d < frame.Width + frame.Height) { x = frame.Right - 4; y = frame.Top + d - frame.Width; }
                else if (d < 2 * frame.Width + frame.Height) { x = frame.Right - (d - frame.Width - frame.Height); y = frame.Bottom - 4; } else { x = frame.Left + 4; y = frame.Bottom - (d - 2 * frame.Width - frame.Height); }
                float on = .5f + .5f * MathF.Sin(Time * 6 - i * .8f) + (spinning ? .3f : 0); c.DrawCircle(x, y, 3.2f, Gfx.Fill(C.Gold.Light(.4f).A(.4f + .6f * Math.Min(1, on)))); Gfx.Light(c, x, y, 12, C.Gold, .35f * on, 1.8f);
            }
            var HL = new HashSet<(int, int)>(); float pu = .5f + .5f * MathF.Sin(Time * 8);
            if (showLine == -1) foreach (var w in wins) for (int r = 0; r < w.n; r++) HL.Add((r, Lines[w.li][r])); else if (showLine >= 0) { var w = wins.First(x => x.li == showLine); for (int r = 0; r < w.n; r++) HL.Add((r, Lines[w.li][r])); }
            for (int r = 0; r < 5; r++)
            {
                var col = Gfx.R(RX + r * (CW + GP) - 2, RY - 4, CW + 4, 3 * CW + 2 * GP + 8); c.Save(); c.ClipRoundRect(col, 16);
                Gfx.Rect(c, col, 14, new Col(20, 8, 2)); float v = Math.Abs(pos[r] - last[r]) * (CW + GP) / Math.Max(.001f, .016f); float fl = MathF.Floor(pos[r]), fr2 = pos[r] - fl;
                float off = bump[r] < 1 ? MathF.Sin(bump[r] * 26) * 9 * MathF.Exp(-bump[r] * 9) : 0;
                c.Save(); c.Translate(0, off);
                for (int k = -1; k <= 3; k++)
                {
                    var cr = Cell(r, 0); float y = RY + (k - fr2) * (CW + GP); var rect = Gfx.R(cr.Left, y, CW, CW); string sym = Sym(r, (int)fl + k); bool hl = !moving[r] && !spinning && k >= 0 && k < 3 && HL.Contains((r, k));
                    bool dimIt = !moving[r] && !spinning && HL.Count > 0 && !hl && k >= 0 && k < 3;
                    // Bewegungsunschaerfe: versetzte, durchscheinende Kopien (wie Original per SaveLayer-Alpha)
                    // Zylinder-Projektion der Walze: Symbole wandern auf einer Trommel (oben/unten gestaucht)
                    float drumR = col.Height * .62f, th = (y + CW / 2f - col.MidY - off) / drumR;
                    if (Math.Abs(th) > 1.5f) continue;
                    c.Save(); c.Translate(rect.MidX, col.MidY - off + drumR * MathF.Sin(th)); c.Scale(1, MathF.Cos(th)); c.Translate(-rect.MidX, -(y + CW / 2f));
                    if (v > 1800) { for (int gI = 1; gI <= 3; gI++) { var gr2 = Gfx.R(cr.Left, y - gI * v * .006f, CW, CW); c.SaveLayer(90f / gI / 255f); DrawCell(c, gr2, sym); c.Restore(); } }
                    DrawCell(c, rect, sym, hl ? 1.05f + .04f * pu : 1, dimIt ? .5f : 0, hl, pu);
                    c.Restore();
                }
                c.Restore(); if (v > 1500) Gfx.Rect(c, col, 14, Col.Black.A(.15f));
                // Walzen-Woelbung: oben und unten abdunkeln, Glanzstreifen in der Mitte
                Gfx.RectGrad(c, new Box(col.Left, col.Top, col.Right, col.Top + 70), 14, Col.Black.A(.55f), Col.Black.A(0));
                Gfx.RectGrad(c, new Box(col.Left, col.Bottom - 70, col.Right, col.Bottom), 14, Col.Black.A(0), Col.Black.A(.55f));
                var gl = Gfx.Fill(Col.White.A(.05f)); gl.Additive = true; c.DrawRect(new Box(col.Left + 6, col.MidY - 50, col.Left + 22, col.MidY + 50), gl);
                c.Restore();
            }
            if (expT < 5) foreach (var r in expReels)
                {
                    float e = Ease.OutBack(expT / .5f), h = (3 * CW + 2 * GP) * e; var rect = Gfx.Ctr(RX + r * (CW + GP) + CW / 2, RY + 1.5f * (CW + GP), CW, h);
                    Gfx.Light(c, rect.MidX, rect.MidY, 260, C.Gold, .35f, 1.8f);
                    Gfx.Glow(c, rect, 14, C.Gold, 22, .9f); Gfx.RectGrad(c, rect, 14, new Col(255, 243, 208), new Col(212, 172, 100)); var st = Gfx.Line(C.Gold, 5); st.Glow = 2f; c.DrawRoundRect(rect, 14, 14, st);
                    Gfx.Image(c, Assets.Img("slot_" + expSym), Gfx.Ctr(rect.MidX, rect.MidY, CW - 14, Math.Min(h, CW * 2.4f)));
                }
            if ((showLine == -1 || showLine >= 0 || prevT > 0) && !spinning || showLine != -2)
            {
                var ls = showLine == -1 ? wins.Select(w => w.li).ToList() : showLine >= 0 ? new List<int> { showLine } : prevT > 0 ? Enumerable.Range(0, NLines).ToList() : new List<int>();
                float la = showLine >= 0 ? Ease.OutCubic(lineT / .25f) : 1;
                foreach (var li in ls)
                {
                    using var p = new Path2D(); for (int r = 0; r < 5; r++) { var pt = Ctr(r, Lines[li][r]); if (r == 0) p.MoveTo(pt); else p.LineTo(pt); }
                    // leuchtende Gewinnlinie: breiter HDR-Schein, Farbkern, heller Mittelstrich
                    var gl = Gfx.Line(LC[li].A(.8f * la), 16); gl.Blur = 9; gl.Glow = 2.2f; gl.Additive = true; c.DrawPath(p, gl);
                    var core = Gfx.Line(LC[li].A(la), 5); core.Glow = 1.6f; c.DrawPath(p, core);
                    c.DrawPath(p, Gfx.Line(Col.White.A(.7f * la), 1.6f));
                    var p0 = Ctr(0, Lines[li][0]); c.DrawCircle(p0.X - CW / 2 - 8, p0.Y, 11, Gfx.Fill(LC[li])); Gfx.Text(c, (li + 1).ToString(), p0.X - CW / 2 - 8, p0.Y, 14, Col.Black, Al.C);
                }
            }
            DrawHud(c); DrawGamble(c); DrawDuel(c);
            if (inFree)
            {
                var b = Gfx.R(RX - 22, 84, 5 * CW + 4 * GP + 44, 40); Gfx.Glow(c, b, 14, C.Green, 10, .4f); Gfx.RectGrad(c, b, 14, new Col(14, 30, 22, 245), new Col(6, 12, 12, 245)); Gfx.Stroke(c, b, 14, C.Green.A(.85f), 2);
                var seg = new (string l, string v, Col col)[] { ("FREISPIELE", freeSpins.ToString(), C.Green.Light(.35f)), ("Sondersymbol", Names[fsSym], C.Gold), ("Summe", Eu(fsSum), C.Gold.Light(.3f)) };
                float gap = 44, tw = 0; foreach (var sg in seg) tw += Gfx.TW(sg.l + "  ", 18, false) + Gfx.TW(sg.v, 24); tw += gap * 2; float x = b.MidX - tw / 2;
                foreach (var sg in seg) { float w1 = Gfx.TW(sg.l + "  ", 18, false); Gfx.Text(c, sg.l, x, b.MidY + 1, 18, new Col(215, 205, 235), Al.L, false); Gfx.TextShadow(c, sg.v, x + w1, b.MidY, 24, sg.col, Al.L); x += w1 + Gfx.TW(sg.v, 24) + gap; }
            }
            if (overlay != null) DrawOverlay(c);
        }
        void DrawOverlay(Canvas2D c)
        {
            var (t, s, img) = overlay.Value; float a = Ease.OutCubic(ovT / .3f); c.DrawRect(-2000, -2000, 5600, 4900, Gfx.Fill(Col.Black.A(.72f * a)));
            Gfx.Light(c, 800, 400, 460, C.Gold, .4f * a, 1.6f);
            c.Save(); c.Translate(800, 450); float sc = Ease.OutBack(ovT / .5f); c.Scale(sc, sc);
            c.Save(); c.RotateDegrees(ovT * 20);
            for (int i = 0; i < 12; i++) { using var ray = new Path2D(); float an = i * MathF.PI / 6, w = .09f; ray.MoveTo(0, -20); ray.LineTo(MathF.Cos(an - w) * 420, -20 + MathF.Sin(an - w) * 420); ray.LineTo(MathF.Cos(an + w) * 420, -20 + MathF.Sin(an + w) * 420); ray.Close(); var rp = Gfx.Fill(Col.White); rp.Shader = Grad.Radial(0, -20, 420, C.Gold.A(.22f * a), C.Gold.A(0)); rp.Additive = true; rp.Glow = 1.4f; c.DrawPath(ray, rp); }
            c.Restore();
            Gfx.Text(c, t, 0, -250, 84, C.Gold, Al.C, true, 24, true); c.Save(); c.RotateDegrees(MathF.Sin(ovT * 2) * 4); Gfx.Image(c, Assets.Img("slot_" + img), Gfx.Ctr(0, -20, 240, 240)); c.Restore();
            Gfx.Text(c, s, 0, 150, 40, Col.White, Al.C, true, 6); Gfx.Text(c, CpuTurn ? $"{PName(1)} spielt ..." : "Klicken oder Leertaste zum Fortfahren", 0, 215, 22, C.Dim, Al.C, false); c.Restore();
        }
        void DrawPaytable(Canvas2D c)
        {
            var r = Gfx.R(30, 108, 385, 470); W.Panel(c, r, C.Gold); Gfx.Text(c, "GEWINNTABELLE", r.MidX, r.Top + 26, 24, C.Gold, Al.C, true, 6, true);
            Gfx.Text(c, "3×", 250, r.Top + 62, 18, C.Dim); Gfx.Text(c, "4×", 320, r.Top + 62, 18, C.Dim); Gfx.Text(c, "5×", 390, r.Top + 62, 18, C.Dim);
            long lb = LineBet;
            for (int i = 0; i < Regular.Length; i++)
            {
                float y = r.Top + 96 + i * 38; var id = Regular[i]; var ic = Gfx.Ctr(66, y, 34, 34); Gfx.Rect(c, Gfx.Inflate(ic, 2), 6, new Col(255, 240, 200)); Gfx.Image(c, Assets.Img("slot_" + id), ic);
                if (inFree && fsSym == id) { Gfx.Glow(c, Gfx.Inflate(ic, 3), 8, C.Green, 8, .9f); Gfx.Light(c, ic.MidX, ic.MidY, 40, C.Green, .4f); }
                for (int k = 0; k < 3; k++) { long v = Pay[id][3 + k] * lb; Gfx.Text(c, SlotMath.Short(v), 250 + k * 70, y, 15, i < 2 ? C.Gold.Light(.3f) : Col.White, Al.C, false); }
            }
            float by = r.Top + 96 + 9 * 38 + 4; var bi = Gfx.Ctr(66, by, 34, 34); Gfx.Rect(c, Gfx.Inflate(bi, 2), 6, new Col(255, 240, 200)); Gfx.Image(c, Assets.Img("slot_BOOK"), bi);
            Gfx.Text(c, "Buch: Joker + Scatter", 236, by - 8, 15, C.Gold, Al.L, false); Gfx.Text(c, "3+ = 10 Freispiele", 236, by + 10, 15, C.Gold.Light(.3f), Al.L, false);
        }
        void DrawHud(Canvas2D c)
        {
            var p = Gfx.R(300, 600, 1000, 270); W.Panel(c, p, C.Gold, 26);
            Gfx.Text(c, msg, 800, 626, 24, C.Yellow.Light(.3f), Al.C, true, 4);
            void Lb(string t, float x, string v, Col col) { Gfx.Text(c, t, x, 662, 18, C.Dim, Al.C, false); Gfx.Text(c, v, x, 696, 30, col, Al.C, true, 5); }
            Lb("GUTHABEN", 390, Eu(Credits), duel ? (turn == 0 ? C.Cyan : C.Pink).Mix(C.Gold, .4f) : C.Gold); Lb("EINSATZ / LINIE", 590, Eu(LineBet), Col.White); Lb("LINIEN", 1010, NLines.ToString(), Col.White);
            Lb("GEWINN", 1200, Eu(lastWin), lastWin > 0 ? C.Green : C.Dim); Gfx.Text(c, $"Gesamteinsatz {Eu(TotalBet)}", 1200, 808, 20, C.Dim, Al.C, false);
            if (lastWin > 0) Gfx.Light(c, 1200, 696, 90, C.Green, .18f);
            if (!duel || !CpuThinking) Gfx.Text(c, duel ? "Duell: abwechselnd je 1 Spin  ·  Autoplay aus  ·  mehr Guthaben gewinnt" : "Einsatz und Linien mit  -  und  +  ändern", 800, 852, 16, C.Dim.A(.7f), Al.C, false);
        }
        /// <summary>Duell-Anzeige: Runde und beide Guthaben.</summary>
        void DrawDuel(Canvas2D c)
        {
            if (!duel) return;
            var rb = Gfx.R(1315, 596, 260, 62); W.Panel(c, rb, C.Gold, 18);
            Gfx.Text(c, "RUNDE", rb.MidX, rb.Top + 18, 15, C.Dim, Al.C, true);
            Gfx.Text(c, duelOver ? "ENDE" : duelLive ? $"{Round} / {DuelRounds}" : "Münzwurf", rb.MidX, rb.MidY + 9, 24, C.Gold, Al.C, true, 4);
            for (int i = 0; i < 2; i++)
            {
                var r = i == 0 ? Gfx.R(30, 668, 255, 200) : Gfx.R(1315, 668, 260, 200); var col = i == 0 ? C.Cyan : C.Pink;
                bool on = duelLive && !duelOver && turn == i; float pulse = on ? .5f + .5f * MathF.Sin(Time * 5) : 0; long diff = dCred[i] - dCred[1 - i];
                if (on) Gfx.Light(c, r.MidX, r.MidY, 200, col, .14f + .06f * pulse, 1.5f);
                Gfx.Shadow(c, r, 20, 14, .4f, 0, 8);
                if (on) Gfx.Glow(c, r, 20, col, 18, .45f + .35f * pulse);
                Gfx.RectGrad(c, r, 20, col.Dark(on ? .38f : .16f), new Col(10, 3, 24));
                Gfx.RectGrad(c, new Box(r.Left + 3, r.Top + 3, r.Right - 3, r.Top + r.Height * .4f), 18, Col.White.A(on ? .1f : .05f), Col.White.A(0));
                Gfx.Stroke(c, r, 20, col.A(on ? 1 : .4f), on ? 3.5f : 2);
                Gfx.Text(c, PName(i) + (i == 1 ? $" ({Opponents.Label(Opp)})" : ""), r.MidX, r.Top + 30, 21, on ? Col.White : C.Dim, Al.C, true, on ? 5 : 0);
                Gfx.Text(c, Eu(dCred[i]), r.MidX, r.Top + 86, 32, col.Light(on ? .5f : .2f), Al.C, true, on ? 10 : 0);
                long d0 = dCred[i] - DuelStart; Gfx.Text(c, (d0 >= 0 ? "+" : "-") + Eu(Math.Abs(d0)), r.MidX, r.Top + 124, 18, d0 >= 0 ? C.Green.Light(.3f) : C.Red.Light(.3f), Al.C, false);
                Gfx.Text(c, duelOver ? (diff > 0 ? "SIEGER" : diff < 0 ? "" : "REMIS") : on ? "AM ZUG" : diff > 0 ? "führt" : diff < 0 ? "liegt zurück" : "gleichauf", r.MidX, r.Bottom - 30, 20, on || (duelOver && diff > 0) ? col.Light(.4f) : C.Dim, Al.C, true, on ? 4 : 0);
            }
        }
        void DrawGamble(Canvas2D c)
        {
            var r = Gfx.R(1190, 108, 385, 470);
            W.Panel(c, r, g.Active ? (gMode == GambleMode.Ladder ? C.Orange : C.Red) : C.Dim);
            if (g.Active) Gfx.Glow(c, r, 22, gMode == GambleMode.Ladder ? C.Orange : C.Red, 16, .3f + .1f * MathF.Sin(Time * 4));
            if (!g.Active)
            {
                Gfx.Text(c, "RISIKO", r.MidX, r.MidY - 30, 36, C.Dim.A(.6f), Al.C, true, 0, true);
                Gfx.Text(c, "Nach einem Gewinn:", r.MidX, r.MidY + 15, 20, C.Dim.A(.6f), Al.C, false);
                Gfx.Text(c, "Karten oder Risikoleiter", r.MidX, r.MidY + 42, 20, C.Gold.A(.7f), Al.C, true);
                Gfx.Text(c, "um den Gewinn zu steigern!", r.MidX, r.MidY + 68, 18, C.Dim.A(.6f), Al.C, false);
                return;
            }
            if (gMode == GambleMode.Cards)
            {
                Gfx.Text(c, "KARTENRISIKO", r.MidX, r.Top + 54, 22, C.Red.Light(.3f), Al.C, true, 6, true);
                Gfx.Text(c, Eu(g.Amount), r.MidX, r.Top + 88, 36, C.Gold, Al.C, true, 6);
                Gfx.Text(c, $"Stufe {g.Steps}/5  -  nächste: {Eu(g.Amount * 2)}", r.MidX, r.Top + 118, 18, C.Dim, Al.C, false);
                Gfx.Light(c, r.MidX, 290, 120, C.Red, .15f + .08f * MathF.Sin(Time * 3));
                CardArt.Card(c, r.MidX, 290, 130, g.Rank, g.Suit, g.Flip.V, 0, 4);
                for (int i = 0; i < g.Hist.Count; i++)
                {
                    var h = g.Hist[i]; var cr = Gfx.Ctr(r.Left + 36 + i * 44, 400, 36, 36);
                    Gfx.Rect(c, cr, 8, new Col(250, 248, 255)); Gfx.Suit(c, h.suit, cr.MidX, cr.MidY, 9, CardArt.SuitCol(h.suit));
                }
            }
            else
            {
                Gfx.Text(c, "RISIKOLEITER", r.MidX, r.Top + 46, 18, C.Orange.Light(.3f), Al.C, true, 4, true);
                Gfx.Text(c, Eu(g.Amount), r.MidX, r.Top + 74, 30, C.Gold, Al.C, true, 5);
                float lLeft = r.MidX - 110, lRight = r.MidX + 110;
                var rail = Gfx.Line(C.Gold.A(.5f), 4); rail.Glow = 1.4f;
                c.DrawLine(lLeft - 4, 164, lLeft - 4, 432, rail); c.DrawLine(lRight + 4, 164, lRight + 4, 432, rail);
                int stepUp = Math.Min(11, ladderStep + 1), stepDown = Math.Max(0, ladderStep - 1); bool flashPhase = ((int)(Time * 8) % 2) == 0;
                for (int i = 11; i >= 0; i--)
                {
                    float ry = 178 + (11 - i) * 21; var rungRect = Gfx.R(lLeft, ry, 220, 19); long val = LadderVal(i);
                    bool isCurrent = i == ladderStep && !g.Busy, isBlinkUp = i == stepUp && flashPhase && CanLadderStep && !g.Busy, isBlinkDown = i == stepDown && !flashPhase && CanLadderStep && !g.Busy, isTop = i == 11;
                    if (isCurrent)
                    {
                        Gfx.Light(c, rungRect.MidX, rungRect.MidY, 130, C.Green, .3f, 1.7f);
                        Gfx.Glow(c, rungRect, 8, C.Green, 10, .8f); Gfx.RectGrad(c, rungRect, 6, C.Green.Light(.4f), C.Green.Dark(.3f)); Gfx.Stroke(c, rungRect, 6, Col.White, 2);
                        Gfx.Text(c, $"▶  {Eu(val)}  ◀", rungRect.MidX, rungRect.MidY, 15, Col.White, Al.C, true, 4);
                    }
                    else if (isBlinkUp)
                    {
                        Gfx.Glow(c, rungRect, 10, C.Gold, 12, .9f); Gfx.RectGrad(c, rungRect, 6, new Col(255, 230, 80), new Col(210, 150, 20)); Gfx.Stroke(c, rungRect, 6, Col.White, 2);
                        Gfx.Text(c, isTop ? $"★ {Eu(val)} ★" : Eu(val), rungRect.MidX, rungRect.MidY, 15, new Col(40, 20, 0), Al.C, true, 4);
                    }
                    else if (isBlinkDown)
                    {
                        Gfx.Glow(c, rungRect, 8, C.Orange, 10, .8f); Gfx.RectGrad(c, rungRect, 6, new Col(255, 140, 40), new Col(180, 60, 10)); Gfx.Stroke(c, rungRect, 6, Col.White, 2);
                        Gfx.Text(c, Eu(val), rungRect.MidX, rungRect.MidY, 15, Col.White, Al.C, true, 4);
                    }
                    else
                    {
                        Col bg = isTop ? new Col(60, 48, 12) : i == 0 ? new Col(40, 14, 14) : new Col(24, 20, 36);
                        Col border = isTop ? C.Gold.A(.5f) : i == 0 ? C.Red.A(.3f) : Col.White.A(.12f);
                        Col txtCol = isTop ? C.Gold : i == 0 ? C.Red.Light(.3f) : Col.White.A(.65f);
                        Gfx.Rect(c, rungRect, 6, bg); Gfx.Stroke(c, rungRect, 6, border, 1);
                        Gfx.Text(c, isTop ? $"★ {Eu(val)} ★" : Eu(val), rungRect.MidX, rungRect.MidY, 14, txtCol, Al.C, isTop);
                    }
                }
            }
        }
    }
#endif
}
