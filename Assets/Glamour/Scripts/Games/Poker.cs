using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GlamourGames
{
    // =====================================================================================================
    //  Reine Poker-Logik (Unity-frei, auch im Konsolentest nutzbar): Karten, Handbewertung, Equity, KI.
    // =====================================================================================================

    /// <summary>Spielkarte: R = 2..14 (Ass = 14), S = 0 Pik, 1 Herz, 2 Karo, 3 Kreuz.</summary>
    public struct PokerCard
    {
        public int R, S;
        public PokerCard(int r, int s) { R = r; S = s; }
        /// <summary>Kartennummer 0..51 fuer die schnelle Auswertung.</summary>
        public int Code => (R - 2) * 4 + S;
        public static PokerCard FromCode(int c) => new PokerCard(c / 4 + 2, c & 3);
    }

    /// <summary>Handbewertung. Eval = Original (1:1), Fast = gleiche Kodierung ohne Speicheranforderung (fuer Simulationen).</summary>
    public static class PokerEval
    {
        public static readonly string[] Cat = { "Höchste Karte", "Paar", "Zwei Paare", "Drilling", "Straße", "Flush", "Full House", "Vierling", "Straight Flush" };
        public static string RS(int r) => r switch { 11 => "J", 12 => "Q", 13 => "K", 14 => "A", _ => r.ToString() };
        public static string CT(PokerCard c) => RS(c.R) + "♠♥♦♣"[c.S];
        static int StraightHigh(bool[] has) { for (int h = 14; h >= 5; h--) { bool ok = true; for (int k = 0; k < 5; k++) { int r = h - k == 1 ? 14 : h - k; if (!has[r]) { ok = false; break; } } if (ok) return h; } return 0; }

        /// <summary>Original-Bewertung: Punktzahl (Basis 15, 6 Stellen), Kategorie 0..8 und Name.</summary>
        public static (long score, int cat, string name) Eval(IEnumerable<PokerCard> cards)
        {
            var rc = new int[15]; var sc = new List<int>[4] { new List<int>(), new List<int>(), new List<int>(), new List<int>() }; var has = new bool[15];
            foreach (var c in cards) { rc[c.R]++; sc[c.S].Add(c.R); has[c.R] = true; }
            int[] res = null; int fs = Array.FindIndex(sc, a => a.Count >= 5);
            if (fs >= 0) { var fh = new bool[15]; sc[fs].ForEach(r => fh[r] = true); int sf = StraightHigh(fh); if (sf > 0) res = new[] { 8, sf }; }
            var quads = new List<int>(); var trips = new List<int>(); var pairs = new List<int>();
            for (int r = 14; r >= 2; r--) { if (rc[r] == 4) quads.Add(r); else if (rc[r] == 3) trips.Add(r); else if (rc[r] == 2) pairs.Add(r); }
            int[] Kick(int[] ex, int n) { var o = new List<int>(); for (int r = 14; r >= 2 && o.Count < n; r--) if (rc[r] > 0 && !ex.Contains(r)) o.Add(r); return o.ToArray(); }
            if (res == null && quads.Count > 0) res = new[] { 7, quads[0] }.Concat(Kick(new[] { quads[0] }, 1)).ToArray();
            if (res == null && trips.Count > 0 && (trips.Count > 1 || pairs.Count > 0)) { int pp = Math.Max(trips.Count > 1 ? trips[1] : 0, pairs.Count > 0 ? pairs[0] : 0); res = new[] { 6, trips[0], pp }; }
            if (res == null && fs >= 0) res = new[] { 5 }.Concat(sc[fs].OrderByDescending(x => x).Take(5)).ToArray();
            if (res == null) { int st = StraightHigh(has); if (st > 0) res = new[] { 4, st }; }
            if (res == null && trips.Count > 0) res = new[] { 3, trips[0] }.Concat(Kick(new[] { trips[0] }, 2)).ToArray();
            if (res == null && pairs.Count >= 2) res = new[] { 2, pairs[0], pairs[1] }.Concat(Kick(new[] { pairs[0], pairs[1] }, 1)).ToArray();
            if (res == null && pairs.Count == 1) res = new[] { 1, pairs[0] }.Concat(Kick(new[] { pairs[0] }, 3)).ToArray();
            res ??= new[] { 0 }.Concat(Kick(new int[0], 5)).ToArray();
            long score = 0; for (int i = 0; i < 6; i++) score = score * 15 + (i < res.Length ? res[i] : 0);
            string name = Cat[res[0]];
            if (res[0] == 8 && res[1] == 14) name = "Royal Flush"; else if (res[0] == 1) name = "Paar " + RS(res[1]); else if (res[0] == 2) name = $"Zwei Paare {RS(res[1])}/{RS(res[2])}"; else if (res[0] == 3) name = "Drilling " + RS(res[1]);
            else if (res[0] == 7) name = "Vierling " + RS(res[1]); else if (res[0] == 6) name = $"Full House {RS(res[1])}/{RS(res[2])}"; else if (res[0] == 4) name = "Straße bis " + RS(res[1]); else if (res[0] == 0) name = "Höchste Karte " + RS(res[1]);
            return (score, res[0], name);
        }

        // ---- schnelle Variante (Bitmasken, identische Punktzahl) ----
        /// <summary>Hoechste Strasse in einer Rangmaske (Bit r = Rang r, Ass zaehlt auch als 1). 0 = keine.</summary>
        public static int SH(int m) { if ((m & (1 << 14)) != 0) m |= 2; for (int h = 14; h >= 5; h--) if (((m >> (h - 4)) & 31) == 31) return h; return 0; }
        static long Pack(int a, int b = 0, int c = 0, int d = 0, int e = 0, int f = 0) => (((((long)a * 15 + b) * 15 + c) * 15 + d) * 15 + e) * 15 + f;
        static int Hi(ref int m) { for (int r = 14; r >= 2; r--) if ((m & (1 << r)) != 0) { m &= ~(1 << r); return r; } return 0; }
        public const long CatDiv = 759375; // 15^5
        public static int CatOf(long score) => (int)(score / CatDiv);

        /// <summary>Punktzahl wie Eval fuer n Karten (Codes 0..51).</summary>
        public static long Fast(int[] cs, int n)
        {
            Span<int> rc = stackalloc int[15]; Span<int> sm = stackalloc int[4]; Span<int> sn = stackalloc int[4]; int all = 0;
            for (int i = 0; i < n; i++) { int r = cs[i] / 4 + 2, s = cs[i] & 3; rc[r]++; sm[s] |= 1 << r; sn[s]++; all |= 1 << r; }
            int fs = -1; for (int s = 0; s < 4; s++) if (sn[s] >= 5) { fs = s; break; }
            if (fs >= 0) { int sf = SH(sm[fs]); if (sf > 0) return Pack(8, sf); }
            int q = 0, t1 = 0, t2 = 0, p1 = 0, p2 = 0, np = 0;
            for (int r = 14; r >= 2; r--)
            {
                int c = rc[r];
                if (c == 4) { if (q == 0) q = r; }
                else if (c == 3) { if (t1 == 0) t1 = r; else if (t2 == 0) t2 = r; }
                else if (c == 2) { np++; if (p1 == 0) p1 = r; else if (p2 == 0) p2 = r; }
            }
            if (q > 0) { int m = all & ~(1 << q); return Pack(7, q, Hi(ref m)); }
            if (t1 > 0 && (t2 > 0 || np > 0)) return Pack(6, t1, Math.Max(t2, p1));
            if (fs >= 0) { int m = sm[fs]; int a = Hi(ref m), b = Hi(ref m), c = Hi(ref m), d = Hi(ref m), e = Hi(ref m); return Pack(5, a, b, c, d, e); }
            int st = SH(all); if (st > 0) return Pack(4, st);
            if (t1 > 0) { int m = all & ~(1 << t1); int a = Hi(ref m), b = Hi(ref m); return Pack(3, t1, a, b); }
            if (np >= 2) { int m = all & ~(1 << p1) & ~(1 << p2); return Pack(2, p1, p2, Hi(ref m)); }
            if (np == 1) { int m = all & ~(1 << p1); int a = Hi(ref m), b = Hi(ref m), c = Hi(ref m); return Pack(1, p1, a, b, c); }
            { int m = all; int a = Hi(ref m), b = Hi(ref m), c = Hi(ref m), d = Hi(ref m), e = Hi(ref m); return Pack(0, a, b, c, d, e); }
        }
        public static long Fast(IList<PokerCard> a, IList<PokerCard> b = null)
        {
            int n = a.Count + (b?.Count ?? 0); var cs = new int[n]; int k = 0;
            foreach (var c in a) cs[k++] = c.Code; if (b != null) foreach (var c in b) cs[k++] = c.Code;
            return Fast(cs, n);
        }
    }

    /// <summary>Spielstaerke der Computer: Original = KI des Originals (2 Menschen + Computer), sonst Leicht/Mittel/Schwer.</summary>
    public enum PokerLevel { Original, Easy, Medium, Hard }

    /// <summary>Alles, was ein Computer-Spieler fuer seine Entscheidung sieht.</summary>
    public sealed class PokerSpot
    {
        public PokerCard[] Hole; public List<PokerCard> Board = new List<PokerCard>();
        /// <summary>Aktive Gegner (nicht gefoldet, nicht ausgeschieden).</summary>
        public int NOpp = 1;
        public int ToCall, MinTo, MaxTo, CurBet, MyBet, MyChips, Pot, BB = 20;
        /// <summary>Groesster Stapel (Chips + Einsatz) eines aktiven Gegners.</summary>
        public int EffStack;
        /// <summary>Gegner, die in dieser Setzrunde nach mir handeln (0 = letzte Position).</summary>
        public int Behind;
        public bool CanRaise, OppRaised;
        public int Street => Board.Count == 0 ? 0 : Board.Count == 3 ? 1 : Board.Count == 4 ? 2 : 3;
    }

    /// <summary>Entscheidungen der Computer-Spieler (rein rechnerisch, ohne Unity).</summary>
    public static class PokerAi
    {
        // ---- Starthand-Bewertung (Chen-Formel) ----
        static double[] chen;
        /// <summary>Chen-Punkte einer Starthand (-1 .. 20).</summary>
        public static double Chen(PokerCard a, PokerCard b) => Chen(a.Code, b.Code);
        public static double Chen(int a, int b)
        {
            if (chen == null)
            {
                var t = new double[52 * 52];
                for (int i = 0; i < 52; i++) for (int j = 0; j < 52; j++) t[i * 52 + j] = ChenCalc(i / 4 + 2, i & 3, j / 4 + 2, j & 3);
                chen = t;
            }
            return chen[a * 52 + b];
        }
        static double ChenCalc(int r1, int s1, int r2, int s2)
        {
            int hi = Math.Max(r1, r2), lo = Math.Min(r1, r2);
            double v = hi switch { 14 => 10, 13 => 8, 12 => 7, 11 => 6, _ => hi / 2.0 };
            if (hi == lo) return Math.Max(5, v * 2);
            if (s1 == s2) v += 2;
            int gap = hi - lo - 1; v -= gap switch { 0 => 0, 1 => 1, 2 => 2, 3 => 4, _ => 5 };
            if (gap <= 1 && hi < 12) v += 1;
            return Math.Ceiling(v);
        }

        // ---- Monte-Carlo-Gewinnwahrscheinlichkeit ----
        static ulong Xs(ref ulong s) { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return s; }
        /// <summary>
        /// Gewinnwahrscheinlichkeit (Split anteilig) gegen nOpp Zufallshaende. budgetMs &gt; 0 begrenzt die Rechenzeit.
        /// tight 0..1: Wahrscheinlichkeit, schwache Gegnerhaende (Chen &lt; 6) zu verwerfen (Gegner hat Staerke gezeigt).
        /// </summary>
        public static double Equity(IList<PokerCard> hole, IList<PokerCard> board, int nOpp, int iters, Random rnd, double budgetMs = 0, double tight = 0)
        {
            nOpp = Math.Max(1, nOpp);
            var used = new bool[52]; foreach (var c in hole) used[c.Code] = true; foreach (var c in board) used[c.Code] = true;
            var rest = new int[52 - hole.Count - board.Count]; int k = 0; for (int i = 0; i < 52; i++) if (!used[i]) rest[k++] = i;
            int bc = board.Count, needB = 5 - bc, need = needB + nOpp * 2, n = rest.Length, tail = n - need;
            var my = new int[7]; var op = new int[7]; my[0] = hole[0].Code; my[1] = hole[1].Code;
            for (int i = 0; i < bc; i++) my[2 + i] = op[2 + i] = board[i].Code;
            ulong st = ((ulong)(uint)rnd.Next() << 32) | (uint)rnd.Next() | 1UL;
            var sw = budgetMs > 0 ? Stopwatch.StartNew() : null;
            double win = 0; int done = 0;
            for (int it = 0; it < iters; it++)
            {
                for (int i = 0; i < need; i++) { int j = i + (int)(Xs(ref st) % (ulong)(n - i)); int tmp = rest[i]; rest[i] = rest[j]; rest[j] = tmp; }
                for (int i = 0; i < needB; i++) my[2 + bc + i] = op[2 + bc + i] = rest[i];
                long ms = PokerEval.Fast(my, 7); int off = needB, ties = 0; bool lose = false;
                for (int o = 0; o < nOpp; o++)
                {
                    if (tight > 0 && tail >= 2)
                        for (int tr = 0; tr < 3 && Chen(rest[off], rest[off + 1]) < 6 && (Xs(ref st) % 1000) < tight * 1000; tr++)
                        {
                            // schwache Hand verwerfen: zwei neue Karten aus dem unbenutzten Rest ziehen
                            int j1 = need + (int)(Xs(ref st) % (ulong)tail); int t1 = rest[off]; rest[off] = rest[j1]; rest[j1] = t1;
                            int j2 = need + (int)(Xs(ref st) % (ulong)tail); if (j2 == j1) continue; int t2 = rest[off + 1]; rest[off + 1] = rest[j2]; rest[j2] = t2;
                        }
                    op[0] = rest[off]; op[1] = rest[off + 1]; off += 2;
                    long os = PokerEval.Fast(op, 7);
                    if (os > ms) { lose = true; break; }
                    if (os == ms) ties++;
                }
                if (!lose) win += ties > 0 ? 1.0 / (ties + 1) : 1;
                done++;
                if (sw != null && (it & 31) == 31 && sw.Elapsed.TotalMilliseconds > budgetMs) break;
            }
            return done == 0 ? 0 : win / done;
        }

        // ---- Hilfen fuer die Heuristiken ----
        /// <summary>Flush-Draw, Open-Ender, Gutshot (nur Flop/Turn, nur wenn die eigenen Karten beteiligt sind).</summary>
        public static (bool fd, bool oesd, bool gut) Draws(IList<PokerCard> hole, IList<PokerCard> board)
        {
            if (board.Count < 3 || board.Count >= 5) return (false, false, false);
            var sn = new int[4]; foreach (var c in hole) sn[c.S]++; foreach (var c in board) sn[c.S]++;
            bool fd = false; for (int s = 0; s < 4; s++) if (sn[s] == 4 && hole.Any(c => c.S == s)) fd = true;
            int all = 0, bm = 0; foreach (var c in hole) all |= 1 << c.R; foreach (var c in board) { all |= 1 << c.R; bm |= 1 << c.R; }
            if (PokerEval.SH(all) > 0) return (fd, false, false);
            int outs = 0; for (int r = 2; r <= 14; r++) if ((all & (1 << r)) == 0 && PokerEval.SH(all | (1 << r)) > 0 && PokerEval.SH(bm | (1 << r)) == 0) outs++;
            return (fd, outs >= 2, outs == 1);
        }

        /// <summary>Grobe Staerke der gemachten Hand (0..1), beruecksichtigt Paare/Drillinge, die nur auf dem Board liegen.</summary>
        public static double MadeStrength(IList<PokerCard> hole, IList<PokerCard> board)
        {
            long sc = PokerEval.Fast(hole, board); int cat = PokerEval.CatOf(sc);
            int bcat = board.Count >= 2 ? PokerEval.CatOf(PokerEval.Fast(board)) : 0;
            var br = board.Select(c => c.R).Distinct().OrderByDescending(r => r).ToList(); int top = br.Count > 0 ? br[0] : 0, second = br.Count > 1 ? br[1] : 0;
            int hh = Math.Max(hole[0].R, hole[1].R);
            switch (cat)
            {
                case 0: return .1 + (hh == 14 ? .06 : hh >= 12 ? .03 : 0);
                case 1:
                    {
                        int p = (int)(sc / 50625 % 15); // zweite Stelle = Paarrang
                        if (bcat >= 1) return .16;
                        if (p > top) return .66;
                        if (p == top) { int kick = hole[0].R == p ? hole[1].R : hole[0].R; return .5 + kick / 14.0 * .1; }
                        return p >= second ? .4 : .3;
                    }
                case 2: return bcat >= 2 ? .3 : bcat == 1 ? .55 : .72;
                case 3: return bcat == 3 ? .35 : .8;
                case 4: return bcat == 4 ? .4 : .86;
                case 5: return bcat == 5 ? .45 : .9;
                case 6: return bcat == 6 ? .5 : .95;
                default: return .98;
            }
        }

        // ---- Entscheidungen ----
        /// <summary>Liefert ("fold"|"call"|"raise", Ziel-Einsatz) - Format wie die menschlichen Aktionen.</summary>
        public static (string act, int to) Decide(PokerLevel lvl, PokerSpot s, Random rnd)
        {
            (string act, int to) d = lvl switch
            {
                PokerLevel.Easy => Easy(s, rnd),
                PokerLevel.Medium => Medium(s, rnd),
                PokerLevel.Hard => Hard(s, rnd),
                _ => Original(s, rnd)
            };
            return Legalize(s, d.act, d.to);
        }
        static (string, int) Legalize(PokerSpot s, string act, int to)
        {
            if (act == "raise" && !s.CanRaise) act = "call";
            if (act == "raise") { int st = s.BB >= 20 ? 10 : 1; to = Math.Max(s.MinTo, Math.Min(s.MaxTo, (int)Math.Round(to / (double)st) * st)); if (to <= s.CurBet) act = "call"; }
            if (act == "fold" && s.ToCall == 0) act = "call";
            return (act, act == "raise" ? to : 0);
        }
        static int BetTo(PokerSpot s, double f) => s.CurBet + Math.Max(s.BB, (int)Math.Round((s.Pot + s.ToCall) * f));

        /// <summary>KI des Originals (unveraendert, nur schnellere Equity).</summary>
        static (string, int) Original(PokerSpot s, Random rnd)
        {
            int nOpp = Math.Max(1, s.NOpp); double eq = Equity(s.Hole, s.Board, nOpp, s.Board.Count > 0 ? 350 : 250, rnd); int pot = s.Pot;
            double potOdds = s.ToCall / (double)(pot + s.ToCall == 0 ? 1 : pot + s.ToCall), r = rnd.NextDouble();
            int Bs(double f) => s.CurBet + Math.Max(s.BB, (int)Math.Round((pot + s.ToCall) * f)); double fair = 1.0 / (nOpp + 1), edge = eq - fair; string act = "call"; int to = 0;
            if (s.ToCall == 0)
            {
                if (edge > .28 && s.CanRaise) { act = "raise"; to = r < .25 ? (eq > .85 ? s.MaxTo : Bs(.75)) : Bs(.5 + rnd.NextDouble() * .4); }
                else if (edge > .1 && s.CanRaise && r < .55) { act = "raise"; to = Bs(.4 + rnd.NextDouble() * .3); }
                else if (s.CanRaise && r < .09) { act = "raise"; to = Bs(.5); }
            }
            else
            {
                if (edge > .32 && s.CanRaise && r < .7) { act = "raise"; to = eq > .85 && r < .3 ? s.MaxTo : Bs(.7 + rnd.NextDouble() * .5); }
                else if (eq >= potOdds + .04 || (edge > .05 && s.ToCall <= s.BB * 2)) act = "call";
                else if (s.CanRaise && r < .04) { act = "raise"; to = Bs(.8); }
                else if (eq >= potOdds - .05 && r < .3) act = "call"; else act = "fold";
            }
            return (act, to);
        }

        /// <summary>Leicht: locker-passiv - callt zu viel, foldet zufaellig, erhoeht kaum.</summary>
        static (string, int) Easy(PokerSpot s, Random rnd)
        {
            double r = rnd.NextDouble();
            double est = s.Street == 0 ? Chen(s.Hole[0], s.Hole[1]) / 20.0 + .2 : MadeStrength(s.Hole, s.Board);
            if (s.ToCall == 0) return (est >= .7 && r < .3) || r < .05 ? ("raise", s.MinTo) : ("call", 0);
            if (s.ToCall > s.MyChips * .5 && est < .5 && r < .6) return ("fold", 0);
            if (est < .3 && rnd.NextDouble() < .25) return ("fold", 0);
            if (rnd.NextDouble() < .1) return ("fold", 0);
            if (est >= .85 && rnd.NextDouble() < .25) return ("raise", s.MinTo);
            return ("call", 0);
        }

        /// <summary>Mittel: Starthand-Tabelle (Chen), danach Handrang + Draws gegen die Pot-Odds.</summary>
        static (string, int) Medium(PokerSpot s, Random rnd)
        {
            double r = rnd.NextDouble(), call = s.ToCall, potOdds = call / Math.Max(1.0, s.Pot + call); int stack = s.MyChips + s.MyBet;
            if (s.Street == 0)
            {
                double ch = Chen(s.Hole[0], s.Hole[1]);
                if (stack <= 10 * s.BB) return ch >= 8 ? ("raise", s.MaxTo) : call == 0 ? ("call", 0) : ("fold", 0);
                if (ch >= 10) return ("raise", BetTo(s, .8 + .4 * r));
                if (ch >= 8) return call <= 3 * s.BB ? (r < .5 ? ("raise", BetTo(s, .7)) : ("call", 0)) : call <= s.MyChips * .2 ? ("call", 0) : ("fold", 0);
                if (ch >= 6) return call <= 2 * s.BB || call <= s.MyChips * .06 ? ("call", 0) : ("fold", 0);
                if (ch >= 4 && call <= s.BB) return ("call", 0);
                return ("fold", 0);
            }
            double est = MadeStrength(s.Hole, s.Board); var (fd, oesd, gut) = Draws(s.Hole, s.Board); double dk = s.Street == 1 ? 1 : .5;
            if (fd) est += .18 * dk; if (oesd) est += .15 * dk; else if (gut) est += .07 * dk;
            est = Math.Min(.99, est) * (1 - .1 * (Math.Max(1, s.NOpp) - 1));
            if (call == 0) { if (est >= .7) return ("raise", BetTo(s, .6)); if (est >= .5 && r < .5) return ("raise", BetTo(s, .4)); return ("call", 0); }
            if (est >= .8) return r < .6 ? ("raise", BetTo(s, .75)) : ("call", 0);
            if (est >= potOdds + .05) return ("call", 0);
            return ("fold", 0);
        }

        /// <summary>
        /// Schwer: Monte-Carlo-Equity (bis 1500 Simulationen, max. 30 ms) gegen die aktiven Gegner (Range enger, wenn sie
        /// erhoeht haben) + Pot-Odds, Position, Stack-Tiefe (SPR), Einsaetze als Bruchteil des Pots, Semi-Bluffs/Bluffs, Push/Fold.
        /// </summary>
        static (string, int) Hard(PokerSpot s, Random rnd)
        {
            int nOpp = Math.Max(1, s.NOpp), street = s.Street;
            double tight = s.OppRaised ? .55 : s.ToCall > s.BB && street > 0 ? .35 : 0;
            double eq = Equity(s.Hole, s.Board, nOpp, 1500, rnd, 30, tight);
            double pot = s.Pot, call = s.ToCall, potOdds = call / Math.Max(1.0, pot + call), fair = 1.0 / (nOpp + 1), r = rnd.NextDouble();
            var (fd, oesd, _) = Draws(s.Hole, s.Board); bool draw = fd || oesd;
            int stack = s.MyChips + s.MyBet, eff = Math.Min(stack, Math.Max(s.EffStack, s.BB)); double spr = (eff - s.MyBet) / Math.Max(1.0, pot), bbs = eff / (double)s.BB;
            bool inPos = s.Behind == 0;
            double strong = street == 0 ? fair + .12 : fair + .25, value = street == 0 ? fair + .05 : fair + .12;
            // Einsatz als Pot-Anteil; wer mehr als gut die Haelfte seines Rests setzen wuerde, geht gleich All-In
            int Bet(double f) { int to = BetTo(s, f); return to - s.MyBet >= s.MyChips * .55 ? s.MaxTo : to; }
            (string, int) Shove() => s.CanRaise ? ("raise", s.MaxTo) : ("call", 0);

            // kurzer Stack vor dem Flop: Push or Fold
            if (street == 0 && bbs <= 12)
            {
                if (call > s.BB) return eq >= potOdds + .04 ? Shove() : ("fold", 0);
                return eq >= fair + (nOpp == 1 ? 0 : .06) ? Shove() : call == 0 ? ("call", 0) : ("fold", 0);
            }
            // wenig Spielraum (SPR < 1,5) mit guter Hand: alles rein
            if (spr < 1.5 && eq >= value) return Shove();

            if (call == 0)
            {
                if (eq >= strong) return r < .12 && street < 3 && !inPos ? ("call", 0) : ("raise", Bet(.55 + rnd.NextDouble() * .3)); // selten Falle stellen
                if (eq >= value) return r < .7 ? ("raise", Bet(.35 + rnd.NextDouble() * .25)) : ("call", 0);
                if (draw && r < .4) return ("raise", Bet(.5));                                  // Semi-Bluff
                if (nOpp == 1 && r < (inPos ? .18 : .08)) return ("raise", Bet(.45));           // Bluff
                return ("call", 0);
            }
            double need = potOdds + (draw ? -.04 : .03);                                       // Draws: implizite Odds
            if (eq >= strong + .05) return r < .85 ? ("raise", Bet(.7 + .3 * rnd.NextDouble())) : ("call", 0);
            if (eq >= value + .05 && call <= pot * .5 && r < .3) return ("raise", Bet(.6));
            if (eq >= need) return ("call", 0);
            if (draw && r < .12) return ("raise", Bet(.75));
            if (nOpp == 1 && r < .03) return ("raise", Bet(.8));
            return ("fold", 0);
        }
    }

#if !GLAMOUR_AITEST
    // =====================================================================================================
    //  Spielszene
    // =====================================================================================================
    public class Poker : Scene
    {
        public override string Title => "Texas Hold'em";
        public override Col Acc1 => C.Magenta; public override Col Acc2 => C.Cyan;
        public override string OppKey => "poker";
        static readonly (int sb, int bb)[] Blinds = { (10, 20), (20, 40), (30, 60), (50, 100), (75, 150), (100, 200), (150, 300), (200, 400), (300, 600), (500, 1000) };
        const int PerLevel = 8, Start = 1000;
        class P { public string Name; public bool Human; public int Chips, Bet, Total, Raises; public List<PokerCard> Hole = new List<PokerCard>(); public bool Folded, AllIn, Out, Acted, CanRaise; public string LastAct = "", HandName = ""; public Spring[] Flip = { new Spring(0) { K = 170, D = 20 }, new Spring(0) { K = 170, D = 20 } }; }
        P[] pl; List<PokerCard> deck = new List<PokerCard>(), board = new List<PokerCard>(); int button, handNo, curBet, lastRaise, toAct = -1, sbI, bbI, sb = 10, bb = 20; string street = ""; bool inHand, gameOver, showAll; int revealed = -1; List<int> winners = new List<int>(); float handT;
        (string act, int to)? pending; (string t, string s)? overlay; bool overlayDone; readonly List<string> log = new List<string>(); string msg = ""; Button bNext, bNew, bFold, bCall, bRaise, bMin, bHalf, bPot, bAll, bShow;
        readonly Spring[] bflip = Enumerable.Range(0, 5).Select(_ => new Spring(0) { K = 170, D = 20 }).ToArray(); readonly float[] bt = new float[5];
        int sliderMin, sliderMax, sliderVal; bool drag; static readonly Box Track = Gfx.R(850, 748, 300, 14);
        static readonly Pt[] Seat = { new Pt(340, 545), new Pt(1060, 545), new Pt(700, 215) };
        // Generationszaehler: alte geplante Computeraktionen feuern nicht ins neue Spiel
        int gen; bool awaitHuman, hotseat; readonly HashSet<int> hlCards = new HashSet<int>();
        PokerLevel Lvl => Opp switch { Opponent.Easy => PokerLevel.Easy, Opponent.Medium => PokerLevel.Medium, Opponent.Hard => PokerLevel.Hard, _ => PokerLevel.Original };

        public override void Enter()
        {
            base.Enter();
            bNew = Ui.Add(new Button(40, 790, 230, 56, "Neues Spiel", C.Purple, () => NewGame(), 22)); bNext = Ui.Add(new Button(560, 785, 280, 72, "Nächste Hand", C.Green, () => { if (!inHand && !Co.Busy && !gameOver) Co.Start(HandCo()); }, 30));
            bFold = Ui.Add(new Button(330, 792, 190, 66, "FOLD", C.Red, () => Act("fold", 0), 28)); bCall = Ui.Add(new Button(540, 792, 250, 66, "CHECK", C.Cyan, () => Act("call", 0), 28)); bRaise = Ui.Add(new Button(810, 792, 300, 66, "RAISE", C.Gold, () => Act("raise", sliderVal), 26));
            bMin = Ui.Add(new Button(330, 738, 90, 40, "Min", C.Purple, () => Quick("min"), 18)); bHalf = Ui.Add(new Button(430, 738, 100, 40, "½ Pot", C.Purple, () => Quick("half"), 18)); bPot = Ui.Add(new Button(540, 738, 90, 40, "Pot", C.Purple, () => Quick("pot"), 18)); bAll = Ui.Add(new Button(640, 738, 110, 40, "All-In", C.Orange, () => Quick("all"), 18));
            bShow = Ui.Add(new Button(600, 600, 400, 80, "Karten zeigen", C.Green, () => overlayDone = true, 32) { Visible = false });
            Opponents.AddSwitch(this, OppKey, 1310, 790, 270, 70, NewGame);
            Opp = Opponents.Load(OppKey); NewGame();
            Opponents.Pick(this, OppKey, o => NewGame());
        }
        void Act(string a, int to) { if (HumanTurn) pending = (a, to); }
        void NewGame()
        {
            gen++; Co.Clear(); pending = null; overlay = null; awaitHuman = false; hlCards.Clear();
            pl = VsCpu ? new[] { Mk(PName(0), true), Mk(PName(1) + " 1", false), Mk(PName(1) + " 2", false) } : new[] { Mk(PName(0), true), Mk(PName(1), true), Mk("Computer", false) };
            hotseat = pl.Count(p => p.Human) > 1;
            button = Rng.I(3); handNo = 0; board.Clear(); inHand = false; gameOver = false; revealed = -1; winners.Clear(); showAll = false; toAct = -1; log.Clear(); if (Modal != null && Modal.Title != "Gegner wählen") Modal = null;
            Lg($"Neues Spiel - jeder startet mit {Start} Chips."); if (VsCpu) Lg($"Computer-Stärke: {Opponents.Label(Opp)}"); msg = "Neues Spiel! \"Nächste Hand\" drücken.";
        }
        static P Mk(string n, bool h) => new P { Name = n, Human = h, Chips = Start };
        void Lg(string t) { log.Insert(0, t); if (log.Count > 40) log.RemoveAt(40); }
        static string RS(int r) => PokerEval.RS(r);
        static string CT(PokerCard c) => PokerEval.CT(c);
        int Next(int i, Func<P, bool> f) { for (int k = 1; k <= 3; k++) { int j = (i + k) % 3; if (f(pl[j])) return j; } return -1; }
        int Pot => pl.Sum(p => p.Total); List<P> ActiveIn => pl.Where(p => !p.Out && !p.Folded).ToList();
        List<PokerCard> NewDeck() { var d = new List<PokerCard>(); for (int s = 0; s < 4; s++) for (int r = 2; r <= 14; r++) d.Add(new PokerCard(r, s)); Rng.Shuffle(d); return d; }
        PokerCard Draw() { var c = deck[deck.Count - 1]; deck.RemoveAt(deck.Count - 1); return c; }
        static (long score, int cat, string name) Eval(IEnumerable<PokerCard> cards) => PokerEval.Eval(cards);

        // ---- Regeln ----
        (int toCall, int minTo, int maxTo, bool canRaise) Legal(int i)
        {
            var p = pl[i]; int toCall = Math.Max(0, curBet - p.Bet), maxTo = p.Bet + p.Chips, minTo = curBet == 0 ? Math.Min(maxTo, bb) : Math.Min(maxTo, curBet + lastRaise);
            int others = pl.Where((q, j) => j != i && !q.Out && !q.Folded && !q.AllIn).Count(); return (Math.Min(toCall, p.Chips), minTo, maxTo, p.CanRaise && maxTo > curBet && others > 0);
        }
        void Post(int i, int amt, string label) { var p = pl[i]; int a = Math.Min(amt, p.Chips); p.Chips -= a; p.Bet += a; p.Total += a; if (p.Chips == 0) p.AllIn = true; p.LastAct = $"{label} {a}"; }
        bool RoundDone()
        {
            var live = ActiveIn; if (live.Count <= 1) return true;
            foreach (var p in live.Where(p => !p.AllIn)) if (!p.Acted || p.Bet < curBet) return false; return true;
        }
        void Apply(int i, string act, int to)
        {
            var p = pl[i]; var L = Legal(i);
            if (act == "fold") { p.Folded = true; p.LastAct = "Fold"; Lg($"{p.Name}: Fold"); Sfx.Play(S.Take, .5f); }
            else if (act == "call") { int a = L.toCall; p.Chips -= a; p.Bet += a; p.Total += a; if (p.Chips == 0) p.AllIn = true; p.LastAct = a == 0 ? "Check" : p.AllIn ? "All-In " + p.Bet : "Call " + a; Lg($"{p.Name}: {p.LastAct}"); Sfx.Play(a == 0 ? S.Tick : S.Chip); }
            else
            {
                to = Math.Max(L.minTo, Math.Min(L.maxTo, to)); int add = to - p.Bet; p.Chips -= add; p.Bet = to; p.Total += add; if (p.Chips == 0) p.AllIn = true; p.Raises++;
                int raiseSize = to - curBet; bool full = raiseSize >= lastRaise || (curBet == 0 && to >= bb); bool wasBet = curBet == 0;
                if (to > curBet) { for (int j = 0; j < 3; j++) { var q = pl[j]; if (j != i && !q.Folded && !q.AllIn && !q.Out) { if (!full && q.Acted) q.CanRaise = false; else if (full) q.CanRaise = true; q.Acted = false; } } if (full) lastRaise = raiseSize; curBet = to; }
                p.LastAct = (p.AllIn ? "All-In " : wasBet ? "Bet " : "Raise auf ") + to; Lg($"{p.Name}: {p.LastAct}"); Sfx.Play(S.Chip); Sfx.Play(S.Chip, .7f, 1.2f);
                if (p.AllIn) { var s = Seat[i]; Fx.Shockwave(s.X, s.Y, C.Orange, 220, .5f); App.Shake(4); }
            }
            p.Acted = true; toAct = Next(i, q => !q.Out && !q.Folded && !q.AllIn); if (toAct < 0) toAct = i;
        }
        /// <summary>Entscheidung eines Computer-Platzes (reine Logik in PokerAi).</summary>
        (string act, int to) CpuDecide(int i)
        {
            var p = pl[i]; var L = Legal(i);
            int pos(int j) => (j - button - 1 + 6) % 3; // 0 = handelt zuerst (nach dem Flop), Button zuletzt
            var opps = pl.Where((q, j) => j != i && !q.Out && !q.Folded).ToList();
            var s = new PokerSpot
            {
                Hole = p.Hole.ToArray(), Board = new List<PokerCard>(board), NOpp = Math.Max(1, ActiveIn.Count - 1), ToCall = L.toCall, MinTo = L.minTo, MaxTo = L.maxTo, CanRaise = L.canRaise,
                CurBet = curBet, MyBet = p.Bet, MyChips = p.Chips, Pot = Pot, BB = bb, EffStack = opps.Count > 0 ? opps.Max(q => q.Chips + q.Bet) : 0,
                Behind = Enumerable.Range(0, 3).Count(j => j != i && !pl[j].Out && !pl[j].Folded && !pl[j].AllIn && pos(j) > pos(i)), OppRaised = opps.Any(q => q.Raises > 0)
            };
            return PokerAi.Decide(Lvl, s, Rng.Shared);
        }

        // ---- Ablauf ----
        IEnumerator<object> HandCo()
        {
            if (inHand) yield break; foreach (var p in pl) if (p.Chips <= 0) p.Out = true; var alive = pl.Where(p => p.Chips > 0).ToList(); if (alive.Count < 2 || (!hotseat && pl[0].Out)) { GameOver(); yield break; }
            handNo++; int lvl = Math.Min(Blinds.Length - 1, (handNo - 1) / PerLevel); (sb, bb) = Blinds[lvl]; if ((handNo - 1) % PerLevel == 0 && handNo > 1) Lg($"Blinds steigen auf {sb}/{bb}.");
            foreach (var p in pl) { p.Hole.Clear(); p.Bet = p.Total = p.Raises = 0; p.Folded = p.Out; p.AllIn = p.Acted = false; p.CanRaise = true; p.LastAct = p.HandName = ""; p.Flip[0].Snap(0); p.Flip[1].Snap(0); }
            button = Next(button, p => !p.Out); bool heads = alive.Count == 2; sbI = heads ? button : Next(button, p => !p.Out); bbI = Next(sbI, p => !p.Out);
            deck = NewDeck(); board.Clear(); for (int k = 0; k < 5; k++) { bflip[k].Snap(0); bt[k] = 0; } winners.Clear(); hlCards.Clear(); revealed = hotseat ? -1 : 0; showAll = false; inHand = true; street = "preflop"; handT = 0;
            Post(sbI, sb, "SB"); Post(bbI, bb, "BB"); curBet = bb; lastRaise = bb; for (int k = 0; k < 2; k++) foreach (var p in pl) if (!p.Out) p.Hole.Add(Draw());
            Lg($"- Hand {handNo} - Blinds {sb}/{bb} - Dealer: {pl[button].Name}"); Sfx.Play(S.Deal); Sfx.Play(S.Chip); toAct = Next(bbI, p => !p.Out && !p.Folded && !p.AllIn); msg = $"Hand {handNo} - Blinds {sb}/{bb}"; yield return .9f;
            bool skip = false;
            while (true)
            {
                if (ActiveIn.Count <= 1) { EndFold(); yield break; }
                if (skip || RoundDone())
                {
                    skip = false; foreach (var p in pl) { p.Bet = 0; p.Acted = false; p.CanRaise = true; if (!p.Folded && !p.AllIn) p.LastAct = ""; } curBet = 0; lastRaise = bb;
                    if (street == "river") { Showdown(); yield break; }
                    if (street == "preflop") { draw(3); street = "flop"; } else if (street == "flop") { draw(1); street = "turn"; } else { draw(1); street = "river"; }
                    Lg($"{street.ToUpper()}: {string.Join(" ", board.Select(CT))}"); Sfx.Play(S.Flip);
                    if (ActiveIn.Count(p => !p.AllIn) <= 1) { showAll = true; yield return 1.3f; skip = true; continue; }
                    toAct = Next(button, p => !p.Out && !p.Folded && !p.AllIn); yield return .5f; continue;
                }
                if (toAct < 0 || pl[toAct].Folded || pl[toAct].AllIn || pl[toAct].Out) toAct = Next(toAct < 0 ? button : toAct, p => !p.Out && !p.Folded && !p.AllIn);
                var cur = pl[toAct];
                if (cur.Human)
                {
                    var humans = pl.Where(q => q.Human && !q.Out && !q.Folded).ToList();
                    if (hotseat && (humans.Count > 1 || revealed != toAct)) { revealed = -1; overlay = (cur.Name + " ist am Zug", "Platz tauschen - dann Karten zeigen"); overlayDone = false; msg = ""; yield return (Func<bool>)(() => overlayDone); overlay = null; }
                    revealed = toAct;
                    SetupRaise(); pending = null; msg = $"{cur.Name} ist dran - zu zahlen: {Legal(toAct).toCall}"; Sfx.Play(S.Turn, .5f);
                    awaitHuman = true; yield return (Func<bool>)(() => pending != null); awaitHuman = false;
                }
                else
                {
                    // Computer: gleicher Weg wie der Mensch (pending -> Apply), Entscheidung nach der Bedenkzeit
                    if (hotseat) revealed = -1; msg = $"{cur.Name} überlegt ..."; pending = null;
                    int g0 = gen, seat = toAct;
                    CpuThink(Opponents.ThinkTime(Opp), () => { if (g0 != gen || !inHand || toAct != seat || pending != null) return; pending = CpuDecide(seat); });
                    yield return (Func<bool>)(() => pending != null);
                }
                var (a, to) = pending.Value; pending = null; Apply(toAct, a, to);
                yield return .35f;
            }
            void draw(int n) { deck.RemoveAt(deck.Count - 1); for (int k = 0; k < n; k++) board.Add(Draw()); }
        }
        void SetupRaise() { var L = Legal(toAct); sliderMin = L.minTo; sliderMax = L.maxTo; sliderVal = L.minTo; }
        void Quick(string k)
        {
            if (!HumanTurn) return; var L = Legal(toAct); int pot = Pot + L.toCall, to = k switch { "min" => L.minTo, "all" => L.maxTo, "half" => curBet + (int)Math.Round(pot / 2.0), _ => curBet + pot };
            sliderVal = Math.Clamp(to, L.minTo, L.maxTo); Sfx.Play(S.Chip);
        }
        void EndFold()
        {
            var w = ActiveIn[0]; int pot = Pot; w.Chips += pot; winners = new List<int> { Array.IndexOf(pl, w) }; Lg($"{w.Name} gewinnt {pot} (alle anderen gefoldet)."); msg = $"{w.Name} gewinnt {pot}!"; Finish();
        }
        void Showdown()
        {
            showAll = true; street = "showdown"; var live = ActiveIn; foreach (var p in live) p.HandName = Eval(p.Hole.Concat(board)).name;
            var levels = pl.Select(p => p.Total).Where(c => c > 0).Distinct().OrderBy(x => x).ToList(); int prev = 0; var pots = new List<(int amt, List<int> elig)>();
            foreach (var lv in levels)
            {
                int amt = 0; var elig = new List<int>(); for (int j = 0; j < 3; j++) { var p = pl[j]; int c = Math.Min(p.Total, lv) - Math.Min(p.Total, prev); if (c > 0) amt += c; if (!p.Folded && !p.Out && p.Total >= lv) elig.Add(j); }
                if (amt > 0) { if (elig.Count > 0) pots.Add((amt, elig)); else if (pots.Count > 0) pots[pots.Count - 1] = (pots[pots.Count - 1].amt + amt, pots[pots.Count - 1].elig); }
                prev = lv;
            }
            var scores = new Dictionary<int, long>(); foreach (var p in live) scores[Array.IndexOf(pl, p)] = Eval(p.Hole.Concat(board)).score; var msgs = new List<string>(); var ws = new HashSet<int>();
            for (int k = 0; k < pots.Count; k++)
            {
                var pt = pots[k]; long best = pt.elig.Max(j => scores.TryGetValue(j, out var v) ? v : -1); var w = pt.elig.Where(j => (scores.TryGetValue(j, out var v) ? v : -1) == best).ToList(); int share = pt.amt / w.Count, rest = pt.amt - share * w.Count;
                foreach (var j in w) { pl[j].Chips += share; ws.Add(j); } if (rest > 0) pl[new[] { 1, 2, 3 }.Select(d => (button + d) % 3).First(j => w.Contains(j))].Chips += rest;
                string label = pots.Count > 1 ? (k == 0 ? "Hauptpot" : $"Side-Pot {k}") : "Pot"; msgs.Add($"{label} {pt.amt}: {string.Join(" & ", w.Select(j => $"{pl[j].Name} ({pl[j].HandName})"))}");
            }
            winners = ws.ToList(); foreach (var j in winners) MarkBest(pl[j]);
            foreach (var p in live) Lg($"{p.Name}: {string.Join(" ", p.Hole.Select(CT))} - {p.HandName}"); msgs.ForEach(Lg); msg = string.Join(" - ", msgs); Finish();
        }
        /// <summary>Merkt sich die 5 Karten der Gewinnerhand (werden golden hervorgehoben).</summary>
        void MarkBest(P p)
        {
            var all = p.Hole.Concat(board).ToList(); if (all.Count < 5) return; long best = Eval(all).score;
            for (int a = 0; a < all.Count; a++) for (int b = a + 1; b < all.Count; b++)
                {
                    var five = all.Where((_, k) => k != a && k != b).ToList(); if (all.Count == 6) five = all.Where((_, k) => k != a).ToList();
                    if (five.Count == 5 && Eval(five).score == best) { foreach (var cd in five) hlCards.Add(cd.Code); return; }
                }
        }
        void Finish()
        {
            inHand = false; toAct = -1; awaitHuman = false; foreach (var p in pl) { p.Total = 0; p.Bet = 0; }
            if (winners.Any(j => pl[j].Human)) { Sfx.Play(S.Win); Celebrate(C.Gold, 3, .7f); } else Sfx.Play(S.Lose);
            foreach (var j in winners) { var s = Seat[j]; Fx.Burst(s.X, s.Y, 40, new[] { C.Gold, C.Yellow, C.White }, 380); Fx.Shockwave(s.X, s.Y, C.Gold, 260, .6f); Pop("GEWINNER", s.X, s.Y - 100, C.Gold, 40); }
            foreach (var p in pl) if (p.Chips <= 0 && !p.Out) { p.Out = true; Lg($"{p.Name} ist ausgeschieden."); }
            int g0 = gen;
            if (pl.Count(p => !p.Out) < 2 || (!hotseat && pl[0].Out)) Tm.After(1.4f, () => { if (g0 == gen) GameOver(); });
        }
        void GameOver()
        {
            if (gameOver) return; gameOver = true; int g0 = gen;
            var alive = pl.Where(p => !p.Out).ToList();
            if (alive.Count != 1)
            {
                // Solo gegen zwei Computer: Mensch ist raus
                var h = pl[0]; Sfx.Play(S.Lose); App.Shake(8); Lg($"{h.Name} ist ausgeschieden.");
                Tm.After(1.2f, () => { if (g0 == gen) Result($"{h.Name} ist ausgeschieden", $"nach {handNo} Händen", C.Red, ("Neues Spiel", C.Green, NewGame), ("Menü", C.Purple, () => App.Go(new Menu()))); });
                return;
            }
            var w = alive[0]; Sfx.Play(S.Big); Celebrate(w.Human ? C.Gold : C.Red, 5, 1.2f, $"{w.Name} gewinnt!"); Lg($"TURNIERSIEG: {w.Name}");
            Tm.After(3f, () => { if (g0 == gen) Result($"{w.Name} gewinnt das Turnier!", $"nach {handNo} Händen", w.Human ? C.Gold : C.Red, ("Neues Spiel", C.Green, NewGame), ("Menü", C.Purple, () => App.Go(new Menu()))); });
        }

        // ---- Eingabe ----
        bool HumanTurn => inHand && awaitHuman && toAct >= 0 && pl[toAct].Human && pending == null && overlay == null && revealed == toAct;
        public override void MouseDown(float x, float y) { if (HumanTurn && Gfx.Inflate(Track, 52).Contains(x, y)) { drag = true; SetSlider(x); } }
        public override void MouseMove(float x, float y) { if (drag && HumanTurn) SetSlider(x); }
        public override void MouseUp(float x, float y) { drag = false; }
        void SetSlider(float x)
        {
            float t = Ease.Clamp((x - Track.Left) / Track.Width); int v = (int)Math.Round(sliderMin + (sliderMax - sliderMin) * t); int st = bb >= 20 ? 10 : 1; if (v > sliderMin && v < sliderMax) v = (int)Math.Round(v / (double)st) * st; sliderVal = Math.Clamp(v, sliderMin, sliderMax);
        }
        public override void KeyDown(Key k)
        {
            if (overlay != null) { if (k == Key.Space || k == Key.Enter) overlayDone = true; return; }
            if (!HumanTurn) { if (k == Key.Space && bNext.Visible) Co.Start(HandCo()); return; }
            if (k == Key.F) pending = ("fold", 0); else if (k == Key.C || k == Key.Space) pending = ("call", 0);
        }
        public override Pt ThinkPos => pl != null && toAct >= 0 && !pl[toAct].Human ? (toAct == 2 ? new Pt(1080, 160) : new Pt(Seat[toAct].X, 724)) : base.ThinkPos;
        public override void Update(float dt)
        {
            handT += dt; bool ht = HumanTurn; var L = ht ? Legal(toAct) : default;
            bNext.Visible = !inHand && !gameOver && !Co.Busy && overlay == null; bShow.Visible = overlay != null;
            foreach (var b in new[] { bFold, bCall, bRaise, bMin, bHalf, bPot, bAll }) b.Visible = ht;
            if (ht)
            {
                var p = pl[toAct]; bCall.Text = L.toCall == 0 ? "CHECK" : L.toCall >= p.Chips ? $"ALL-IN {p.Chips}" : $"CALL {L.toCall}"; bool ok = L.canRaise && L.maxTo > L.toCall + p.Bet;
                bRaise.Visible = bMin.Visible = bHalf.Visible = bPot.Visible = bAll.Visible = ok; bRaise.Text = curBet == 0 ? $"BET {sliderVal}" : $"RAISE AUF {sliderVal}";
            }
            for (int i = 0; i < 3; i++) for (int k = 0; k < 2; k++) { bool show = pl[i].Hole.Count > k && ((showAll && !pl[i].Folded) || (pl[i].Human && revealed == i)); pl[i].Flip[k].Target = show ? 1 : 0; pl[i].Flip[k].Update(dt); }
            for (int k = 0; k < 5; k++) { bflip[k].Target = k < board.Count ? 1 : 0; bflip[k].Update(dt); if (k < board.Count) bt[k] = Math.Min(1, bt[k] + dt * 3.5f); }
        }

        // ---- Zeichnen ----
        public override void Draw(Canvas2D c)
        {
            var table = Gfx.R(120, 100, 1160, 590);
            Gfx.Shadow(c, table, 290, 34, .7f, 0, 24);
            Gfx.Glow(c, table, 290, C.Magenta, 24, .32f + .08f * MathF.Sin(Time * 1.3f));
            Gfx.RectGrad(c, table, 290, new Col(110, 46, 104), new Col(40, 14, 42));
            var rimHi = Gfx.Line(Col.White.A(.12f), 2); c.DrawRoundRect(Gfx.Inflate(table, -3), 287, 287, rimHi);
            var felt = Gfx.Inflate(table, -16);
            Gfx.RectRadial(c, felt, 275, 700, 395, 640, new Col(66, 22, 120), new Col(18, 5, 44));
            // Lichtkegel der Tischlampe und weiche Randabdunklung
            Gfx.Light(c, 700, 380, 520, new Col(170, 90, 255), .10f, 1.15f);
            var inner = Gfx.Line(Col.Black.A(.55f), 18); inner.Blur = 14; c.DrawRoundRect(Gfx.Inflate(felt, -4), 271, 271, inner);
            var rim = Gfx.Line(C.Magenta.A(.75f), 3); rim.Glow = 1.9f; c.DrawRoundRect(felt, 275, 275, rim);
            Gfx.Stroke(c, Gfx.Inflate(felt, -18), 257, C.Magenta.A(.2f), 2);
            Gfx.Text(c, "GLAMOUR HOLD'EM", 700, 610, 30, C.Magenta.A(.09f), Al.C, true, 0, true);
            Gfx.Text(c, msg, 700, 100, 22, C.Yellow.Light(.3f), Al.C, true, 6);
            int pot = Pot;
            if (pot > 0) { Gfx.Light(c, 700, PotY, 150, C.Gold, .16f + .05f * MathF.Sin(Time * 3), 1.5f); float tw = Gfx.TW($"POT  {pot}", 34); for (int k = 0; k < Math.Min(6, 1 + pot / 150); k++) Chip(c, 700 - tw / 2 - 34, PotY + 12 - k * 5, k % 2 == 0 ? C.Gold : C.Magenta, k == 0); }
            Gfx.Text(c, $"POT  {pot}", 700, PotY, 34, C.Gold, Al.C, true, 10); Gfx.Text(c, $"Hand {handNo}  -  Blinds {sb}/{bb}", 700, PotY + 36, 18, C.Dim, Al.C, false);
            for (int k = 0; k < 5; k++)
            {
                float x = 700 + (k - 2) * 108, y = 400; if (k >= board.Count) { Gfx.Stroke(c, Gfx.Ctr(x, y, 92, 129), 10, Col.White.A(.15f), 2); continue; }
                bool hl = winners.Count > 0 && Hl(board[k]); if (hl) Gfx.Light(c, x, y, 110, C.Gold, .35f + .1f * MathF.Sin(Time * 6), 1.8f);
                float e = Ease.OutBack(bt[k]); c.Save(); c.Translate(x, y); c.Scale(e, e); CardArt.Card(c, 0, 0, 92, RS(board[k].R), board[k].S, bflip[k].V, 0, hl ? 6 : 0, hl); c.Restore();
            }
            for (int i = 0; i < 3; i++) DrawSeat(c, i);
            DrawLog(c);
            if (HumanTurn) DrawActionBar(c);
            if (overlay != null) DrawOverlay(c);
        }
        // Pot unter dem Board (im Original verdeckte ihn der obere Sitz)
        const float PotY = 502;
        bool Hl(PokerCard cd) => hlCards.Contains(cd.Code);
        static List<string> Wrap(string t, float w, float size)
        {
            var res = new List<string>(); var cur = "";
            foreach (var wd in t.Split(' ')) { var tr = cur.Length == 0 ? wd : cur + " " + wd; if (cur.Length > 0 && Gfx.TW(tr, size, false) > w) { res.Add(cur); cur = wd; } else cur = tr; }
            if (cur.Length > 0) res.Add(cur); return res;
        }
        void DrawSeat(Canvas2D c, int i)
        {
            var p = pl[i]; var s = Seat[i]; bool turn = inHand && toAct == i && !p.Folded; bool win = winners.Contains(i); var col = i == 0 ? C.Cyan : i == 1 ? C.Pink : C.Green; float dim = p.Out ? .3f : p.Folded ? .5f : 1;
            var box = Gfx.Ctr(s.X, s.Y + (i == 2 ? 10 : 24), 330, 200);
            if (win) Gfx.Light(c, s.X, box.MidY, 280, C.Gold, .22f + .08f * MathF.Sin(Time * 5), 1.6f);
            else if (turn) Gfx.Light(c, s.X, box.MidY, 240, col, .12f + .05f * MathF.Sin(Time * 6), 1.4f);
            if (dim < 1) c.SaveLayer(dim); else c.Save();
            if (turn || win) Gfx.Glow(c, box, 22, win ? C.Gold : col, 18, .55f + .3f * MathF.Sin(Time * 6)); W.Panel(c, box, win ? C.Gold : col);
            Gfx.RectGrad(c, new Box(box.Left + 3, box.Top + 3, box.Right - 3, box.Top + 46), 20, col.A(.16f), col.A(0));
            Gfx.Text(c, p.Name + (p.Human ? "" : " (KI)") + (inHand && i == sbI ? "  SB" : "") + (inHand && i == bbI ? "  BB" : ""), s.X, box.Top + 26, 22, turn ? col.Light(.5f) : Col.White, Al.C, true, turn ? 6 : 0);
            for (int k = 0; k < 2; k++)
            {
                float x = s.X + (k - .5f) * 92, y = box.Top + 100; if (p.Hole.Count <= k) { Gfx.Stroke(c, Gfx.Ctr(x, y, 84, 118), 8, Col.White.A(.12f), 2); continue; }
                bool hl = win && showAll && Hl(p.Hole[k]);
                float a = Ease.OutBack((handT - k * .15f - i * .08f) / .4f); c.Save(); c.Translate(x, y); c.Scale(a, a); CardArt.Card(c, 0, 0, 84, RS(p.Hole[k].R), p.Hole[k].S, p.Flip[k].V, k == 0 ? -5 : 5, hl ? 5 : 0, hl); c.Restore();
            }
            Gfx.Text(c, p.Out ? "ausgeschieden" : $"{p.Chips} Chips", s.X, box.Bottom - 26, 22, C.Gold, Al.C, true, 4);
            string st = p.HandName.Length > 0 && showAll ? p.HandName : p.AllIn ? "ALL-IN" : p.LastAct; if (st.Length > 0) Gfx.Text(c, st, s.X, box.Bottom + 20, 20, p.Folded ? C.Dim : p.AllIn ? C.Orange.Light(.3f) : C.Yellow.Light(.3f), Al.C, false);
            c.Restore();
            if (i == button && !p.Out)
            {
                var d = new Pt(box.Left + 20, box.Top - 8); var sh = Gfx.Fill(Col.Black.A(.5f)); sh.Blur = 5; c.DrawCircle(d.X + 2, d.Y + 5, 18, sh);
                c.DrawCircle(d.X, d.Y, 18, Gfx.Fill(Col.White)); var ring = Gfx.Line(C.Gold, 3); ring.Glow = 1.6f; c.DrawCircle(d.X, d.Y, 18, ring); Gfx.Text(c, "D", d.X, d.Y, 22, Col.Black, Al.C);
            }
            if (p.Bet > 0)
            {
                float bx = 700 + (s.X - 700) * .55f, by = i == 2 ? 330 : 500 - 10; if (i == 2) { bx = 700; by = 330; }
                if (i == 2) by = 335; for (int k = 0; k < Math.Min(5, 1 + p.Bet / 100); k++) Chip(c, bx + (i == 2 ? 190 : 0), by - k * 5 + (i == 2 ? -50 : 0), col, k == 0);
                Gfx.Text(c, p.Bet.ToString(), bx + (i == 2 ? 190 : 0), by + 22 + (i == 2 ? -50 : 0), 20, Col.White, Al.C, true, 4);
            }
        }
        static void Chip(Canvas2D c, float x, float y, Col col, bool shadow = true)
        {
            if (shadow) { var sh = Gfx.Fill(Col.Black.A(.5f)); sh.Blur = 4; c.DrawOval(x + 2, y + 7, 24, 9, sh); }
            c.DrawOval(x, y + 3, 22, 9, Gfx.Fill(col.Dark(.4f))); c.DrawOval(x, y, 22, 9, Gfx.Fill(col));
            var e = Gfx.Line(Col.White.A(.8f), 1.5f); e.Glow = 1.3f; c.DrawOval(x, y, 22, 9, e); c.DrawOval(x, y, 12, 5, Gfx.Line(Col.White.A(.6f), 1.2f));
        }
        void DrawLog(Canvas2D c)
        {
            var r = Gfx.R(1310, 100, 270, 590); W.Panel(c, r, C.Magenta); Gfx.Text(c, "VERLAUF", r.MidX, r.Top + 26, 22, C.Magenta.Light(.4f), Al.C, true, 4);
            c.Save(); c.ClipRect(Gfx.R(r.Left + 8, r.Top + 46, r.Width - 16, r.Height - 56));
            float ly = r.Top + 66; for (int i = 0; i < log.Count && ly < r.Bottom - 20; i++)
                foreach (var ln in Wrap(log[i], r.Width - 28, 16)) { Gfx.Text(c, ln, r.Left + 14, ly, 16, i == 0 ? Col.White : C.Dim.A(Math.Max(.4f, 1 - i * .04f)), Al.L, false); ly += 26; }
            c.Restore();
        }
        void DrawActionBar(Canvas2D c)
        {
            var p = pl[toAct]; string hint = board.Count == 0 ? "Starthand " + string.Join(" ", p.Hole.Select(CT)) : Eval(p.Hole.Concat(board)).name;
            var bar = Gfx.R(310, 718, 860, 156); W.Panel(c, bar, C.Gold, 22); Gfx.Glow(c, bar, 22, C.Gold, 14, .25f); Gfx.Text(c, $"{p.Name} - {hint}", 740, 706, 22, C.Gold.Light(.3f), Al.C, true, 5);
            if (Legal(toAct).canRaise)
            {
                Gfx.Rect(c, Track, 7, Col.White.A(.15f)); float t = sliderMax == sliderMin ? 1 : (sliderVal - sliderMin) / (float)(sliderMax - sliderMin);
                var fill = Gfx.Fill(C.Gold); fill.Glow = 1.5f; c.DrawRoundRect(Gfx.R(Track.Left, Track.Top, Track.Width * t, Track.Height), 7, 7, fill);
                float kx = Track.Left + Track.Width * t; Gfx.Light(c, kx, Track.MidY, 40, C.Gold, .45f); c.DrawCircle(kx, Track.MidY, 20, Gfx.Fill(C.Gold)); c.DrawCircle(kx, Track.MidY, 20, Gfx.Line(Col.White, 3));
                Gfx.Text(c, (sliderVal >= sliderMax ? "All-In " : "") + sliderVal, 1140, 726, 20, C.Gold, Al.R, true);
            }
        }
        void DrawOverlay(Canvas2D c)
        {
            c.DrawRect(-2000, -2000, 5600, 4900, Gfx.Fill(new Col(6, 1, 15, 235))); var r = Gfx.Ctr(800, 450, 900, 420);
            Gfx.Shadow(c, r, 30, 30, .6f, 0, 16); Gfx.Glow(c, r, 30, C.Magenta, 26, .5f); W.Panel(c, r, C.Magenta, 30);
            Gfx.Text(c, overlay.Value.t, 800, 330, 60, C.Magenta.Light(.4f), Al.C, true, 20, true); Gfx.Text(c, overlay.Value.s, 800, 430, 28, Col.White, Al.C, false); Gfx.Text(c, "Die anderen Spieler schauen weg!", 800, 480, 22, C.Dim, Al.C, false);
        }
    }
#endif
}
