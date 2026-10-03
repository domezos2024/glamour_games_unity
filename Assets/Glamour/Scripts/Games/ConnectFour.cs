using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GlamourGames
{
    public class ConnectFour : Scene
    {
        public override string Title => "Vier Gewinnt";
        public override Col Acc1 => C.Blue; public override Col Acc2 => C.Magenta;
        public override string OppKey => "c4";
        const int Rows = 6, Cols = 7; const float CS = 104, BX = 800 - CS * 3.5f, BY = 168;
        class Disc { public int Col, Row, P; public float Y, Vy; public bool Settled, Landed_; public float Pulse; }
        int[] b = new int[42]; readonly List<Disc> discs = new List<Disc>(); int cur = 1, over; int[] score = new int[2]; List<int> winCells; float winT; bool busy; int hoverCol = -1; string status = "";
        static float CX(int c) => BX + c * CS + CS / 2; static float CY(int r) => BY + r * CS + CS / 2;
        // Computer-Gegner: Suche laeuft im Hintergrund (Task), Ergebnis wird in Update abgeholt
        int gen, aiGen; bool tossing, cpuDue; Task<int> aiTask; int cpuCol = -1; float cpuAimT, cpuX = CX(3);
        public override void Enter()
        {
            base.Enter();
            Opponents.AddSwitch(this, OppKey, 40, 585, 260, 70, NewMatch);
            Ui.Add(new Button(40, 700, 260, 62, "Neue Runde", C.Green, NextRound, 24)); Ui.Add(new Button(40, 780, 260, 62, "Punkte zurück", C.Purple, NewMatch, 22));
            Opponents.Pick(this, OppKey, o => NewMatch());
        }
        int starter = 1;
        void NextRound() { starter = 3 - starter; Reset(); }
        void NewMatch() { score = new int[2]; starter = 1; tossing = true; Reset(); CoinToss.Start(this, f => { tossing = false; starter = f + 1; Reset(); }); }
        void Reset()
        {
            gen++; CancelCpuThink(); b = new int[42]; discs.Clear(); cur = starter; over = 0; winCells = null; busy = false; winT = 0; status = $"{PName(cur - 1)} ist dran"; Modal = null; parade = null; resShown = false;
            aiTask = null; cpuDue = false; cpuCol = -1; hoverCol = -1; CpuCheck();
        }
        bool CpuTurn => VsCpu && cur == 2 && over == 0 && !tossing;
        bool Locked => CpuTurn || CpuThinking || cpuCol >= 0;
        public override bool WantsHand => hoverCol >= 0;
        public override void MouseMove(float x, float y) { hoverCol = -1; if (over != 0 || busy || Modal != null || Locked) return; if (x > BX && x < BX + Cols * CS && y > BY - 100 && y < BY + Rows * CS + 30) hoverCol = (int)((x - BX) / CS); }
        public override void MouseUp(float x, float y) { if (parade != null && parade.T > 1.5f) { FireRes(); return; } if (Locked) return; if (hoverCol >= 0) Drop(hoverCol); }
        public override void KeyDown(Key k)
        {
            if (Locked) return;
            if (k >= Key.Number1 && k <= Key.Number7) Drop(k - Key.Number1);
            else if (k >= Key.Keypad1 && k <= Key.Keypad7) Drop(k - Key.Keypad1);
        }

        // ---------------------------------------------------------------- Computer
        void CpuCheck()
        {
            if (!CpuTurn) return; hoverCol = -1; int g = gen;
            var copy = (int[])b.Clone(); int lvl = (int)Opp; var rnd = new Random(Rng.Shared.Next());
            aiGen = g; cpuDue = false; aiTask = Task.Run(() => C4Ai.BestMove(copy, 2, lvl, rnd, 600));
            CpuThink(Opponents.ThinkTime(Opp), () => { if (g == gen) cpuDue = true; });
        }
        void CpuUpdate(float dt)
        {
            if (cpuDue && aiTask != null && aiGen == gen && CpuTurn && Modal == null && !busy)
            {
                if (aiTask.IsCompleted)
                {
                    int col = -1;
                    if (aiTask.Status == TaskStatus.RanToCompletion) col = aiTask.Result; else Log.I("KI-Fehler: " + aiTask.Exception?.GetBaseException().Message);
                    if (col < 0 || col >= Cols || b[col] != 0) { var free = Enumerable.Range(0, Cols).Where(cc => b[cc] == 0).ToList(); col = free.Count > 0 ? free[Rng.I(free.Count)] : -1; }
                    aiTask = null; cpuDue = false; cpuCol = col; cpuAimT = 0; cpuX = CX(3);
                }
                else if (!CpuThinking) CpuThink(.25f, () => { });   // Anzeige verlaengern, bis die Suche fertig ist
            }
            // schwebender Computer-Stein: erscheint ueber der Mitte, faehrt ueber die gewaehlte Spalte und faellt
            if (cpuCol >= 0)
            {
                cpuX = Ease.Lerp(cpuX, CX(cpuCol), 1 - MathF.Exp(-dt * 12)); cpuAimT += dt;
                if (cpuAimT > .55f && MathF.Abs(cpuX - CX(cpuCol)) < 3) { int col = cpuCol; cpuCol = -1; if (CpuTurn) Drop(col); }
            }
        }

        void Drop(int col)
        {
            if (over != 0 || busy || col < 0 || col >= Cols) return; int row = -1; for (int r = Rows - 1; r >= 0; r--) if (b[r * Cols + col] == 0) { row = r; break; }
            if (row < 0) { Sfx.Play(S.NoMatch, .5f); App.Shake(4); return; }
            b[row * Cols + col] = cur; busy = true; discs.Add(new Disc { Col = col, Row = row, P = cur, Y = BY - CS, Vy = 0 }); Sfx.Play(S.Turn, .4f);
        }
        void Landed(Disc d)
        {
            Sfx.Play(S.Drop); Fx.Spark(CX(d.Col), CY(d.Row) + CS / 2 - 8, d.P == 1 ? C.Cyan : C.Pink, 10, 180);
            var w = Check(d.Row, d.Col, d.P); int g = gen;
            if (w != null)
            {
                over = d.P; winCells = w; score[d.P - 1]++; status = $"{PName(d.P - 1)} GEWINNT!"; Sfx.Play(S.Win); App.Flash(d.P == 1 ? C.Cyan : C.Pink, .3f); App.Shake(9);
                foreach (var i in w) Fx.Burst(CX(i % Cols), CY(i / Cols), 30, new[] { d.P == 1 ? C.Cyan : C.Pink, Col.White, C.Gold }, 460);
                for (int j = 0; j < w.Count; j++) { int ii = w[j]; Tm.After(.15f * (j + 1), () => { Fx.Explosion(CX(ii % Cols), CY(ii / Cols), .6f); Sfx.Play(S.Boom, .3f, 1.3f); }); }
                int wp = d.P;
                Tm.After(2.2f, () =>
                {
                    if (g != gen || over != wp) return;
                    parade = new DiceParade(this, wp - 1, new[] { C.Cyan, C.Pink }, new[] { PName(0), PName(1) }, new[] { score[0], score[1] }, PKind.Disc, 1.35f);
                    showRes = () => Result($"{PName(wp - 1)} gewinnt!", $"Stand: {score[0]} : {score[1]}", wp == 1 ? C.Cyan : C.Pink, ("Nächste Runde", C.Green, NextRound), ("Menü", C.Purple, () => App.Go(new Menu())));
                });
            }
            else if (b.All(v => v != 0)) { over = 3; status = "UNENTSCHIEDEN!"; Sfx.Play(S.Lose); Tm.After(1.2f, () => { if (g == gen) Result("Unentschieden", $"Stand: {score[0]} : {score[1]}", C.Gold, ("Nochmal", C.Green, NextRound), ("Menü", C.Purple, () => App.Go(new Menu()))); }); }
            else { cur = 3 - cur; status = $"{PName(cur - 1)} ist dran"; CpuCheck(); }
            busy = false;
            if (over == 0 && !Locked) MouseMove(App.MX, App.MY);
        }
        List<int> Check(int row, int col, int p)
        {
            foreach (var (dr, dc) in new[] { (0, 1), (1, 0), (1, 1), (1, -1) })
            {
                var cells = new List<int> { row * Cols + col };
                for (int s = -1; s <= 1; s += 2) for (int d = 1; d < 4; d++) { int r = row + dr * d * s, c = col + dc * d * s; if (r < 0 || r >= Rows || c < 0 || c >= Cols || b[r * Cols + c] != p) break; cells.Add(r * Cols + c); }
                if (cells.Count >= 4) return cells;
            }
            return null;
        }
        DiceParade parade; bool resShown; Action showRes;
        public override void DebugWin() { Modal = null; parade = new DiceParade(this, 0, new[] { C.Cyan, C.Pink }, new[] { "Spieler 1", "Spieler 2" }, new[] { 200, 150 }, PKind.Disc, 1.35f); }
        void FireRes() { if (resShown) return; resShown = true; showRes?.Invoke(); }
        public override void Update(float dt)
        {
            parade?.Update(dt); if (parade != null && parade.T > parade.Total) FireRes();
            for (int i = 0; i < discs.Count; i++)
            {
                var d = discs[i];
                if (d.Settled) { d.Pulse += dt; continue; }
                d.Vy += 3200 * dt; d.Y += d.Vy * dt; float ty = CY(d.Row);
                if (d.Y >= ty) { d.Y = ty; if (d.Vy > 260) { d.Vy = -d.Vy * .32f; if (!d.Landed_) { d.Landed_ = true; Landed(d); } } else { d.Vy = 0; d.Settled = true; } }
            }
            if (winCells != null) winT += dt;
            CpuUpdate(dt);
        }
        public override void Draw(Canvas2D c) { DrawBoard(c); parade?.Draw(c); }
        void DrawBoard(Canvas2D c)
        {
            W.PlayerBox(c, Gfx.R(40, 115, 260, 190), PName(0), score[0].ToString(), C.Cyan, over == 0 && cur == 1, Time, "Siege");
            W.PlayerBox(c, Gfx.R(40, 330, 260, 190), PName(1), score[1].ToString(), C.Pink, over == 0 && cur == 2, Time, "Siege");
            Gfx.Text(c, "Spalte klicken / Tasten 1-7", 170, 550, 17, C.Dim, Al.C, false);
            var frame = Gfx.R(BX - 16, BY - 6, Cols * CS + 32, Rows * CS + 30);
            Gfx.Shadow(c, frame, 26, 22, .55f, 0, 18);
            // Rueckwand: dunkler Schacht, durch die Loecher sichtbar
            Gfx.RectGrad(c, Gfx.Inflate(frame, -10), 20, new Col(6, 8, 30), new Col(2, 2, 12));
            if (hoverCol >= 0 && !Locked)
            {
                var col = cur == 1 ? C.Cyan : C.Pink; Gfx.Rect(c, Gfx.R(BX + hoverCol * CS + 4, BY, CS - 8, Rows * CS), 14, col.A(.10f));
                Gfx.Light(c, CX(hoverCol), BY - CS * .55f, CS * .8f, col, .25f);
                Gfx.Disc(c, CX(hoverCol), BY - CS * .55f + MathF.Sin(Time * 6) * 4, CS * .4f, col);
            }
            if (cpuCol >= 0 && CpuTurn && !busy) DrawCpuHover(c);
            foreach (var d in discs) DrawDisc(c, d);
            Gfx.Glow(c, frame, 26, C.Blue, 22, .5f);
            var p = Gfx.Fill(Col.White); p.Shader = Grad.Linear(frame.Left, frame.Top, frame.Right, frame.Bottom, new Col(50, 90, 255), new Col(10, 20, 100));
            c.DrawPerforated(frame, 26, BX, BY, CS, CS, Cols, Rows, CS * .4f, p);
            // Glanz von oben auf dem Kunststoff
            var sh = Gfx.Fill(Col.White); sh.Shader = Grad.Linear(frame.Left, frame.Top, frame.Left, frame.Top + frame.Height * .55f, Col.White.A(.13f), Col.White.A(0));
            c.DrawPerforated(frame, 26, BX, BY, CS, CS, Cols, Rows, CS * .4f, sh);
            for (int r = 0; r < Rows; r++) for (int cc = 0; cc < Cols; cc++) { c.DrawCircle(CX(cc), CY(r), CS * .4f, Gfx.Line(new Col(120, 170, 255, 120), 3)); c.DrawCircle(CX(cc), CY(r) + 2, CS * .41f, Gfx.Line(new Col(0, 0, 40, 130), 3)); }
            var fs = Gfx.Line(new Col(140, 180, 255), 3); fs.Glow = 1.4f; c.DrawRoundRect(frame, 26, 26, fs);
            if (winCells != null)
            {
                foreach (var i in winCells)
                {
                    float k = .5f + .5f * MathF.Sin(winT * 9); var col = over == 1 ? C.Cyan : C.Pink;
                    var rg = Gfx.Line(col.A(.8f), 14); rg.Blur = 8; rg.Glow = 2.2f; c.DrawCircle(CX(i % Cols), CY(i / Cols), CS * .43f, rg);
                    var rw = Gfx.Line(Col.White.A(.6f + .4f * k), 6); rw.Glow = 1.5f + k; c.DrawCircle(CX(i % Cols), CY(i / Cols), CS * .43f, rw);
                    Gfx.Light(c, CX(i % Cols), CY(i / Cols), CS * .8f, col, .45f * k, 2f);
                }
            }
            Gfx.Text(c, status, 800, 122, 38, over == 1 ? C.Cyan : over == 2 ? C.Pink : over == 3 ? C.Gold : Col.White, Al.C, true, 8);
        }
        void DrawCpuHover(Canvas2D c)
        {
            // Zielanzeige: Stein schwebt ueber der gewaehlten Spalte, bevor er faellt
            float y = BY - CS * .55f + MathF.Sin(Time * 6) * 4, aim = Ease.OutCubic(cpuAimT / .25f), x = cpuX;
            Gfx.Rect(c, Gfx.R(BX + cpuCol * CS + 4, BY, CS - 8, Rows * CS), 14, C.Pink.A(.10f * aim));
            c.SaveLayer(aim);
            Gfx.Light(c, x, y, CS * .8f, C.Pink, .3f);
            Gfx.Disc(c, x, y, CS * .4f, C.Pink);
            var ring = Gfx.Line(C.Pink.Light(.5f).A(.8f), 2); ring.Glow = 1.8f; c.DrawCircle(x, y, CS * .4f, ring);
            c.Restore();
        }
        static void DrawDisc(Canvas2D c, Disc d)
        {
            var col = d.P == 1 ? C.Cyan : C.Pink; float r = CS * .4f, x = CX(d.Col);
            Gfx.Light(c, x, d.Y, r * 1.8f, col, .3f, 1.4f);
            Gfx.Disc(c, x, d.Y, r, col);
            c.DrawCircle(x, d.Y, r, Gfx.Line(col.Dark(.35f).A(.7f), 1.5f));
        }
    }

    /// <summary>
    /// KI fuer Vier Gewinnt: Negamax mit Alpha-Beta, Bitboards, Transpositionstabelle, iterativer Vertiefung.
    /// Unity-frei und threadsicher (jede Suche eigene Instanz) - laeuft per Task.Run im Hintergrund.
    /// </summary>
    public sealed class C4Ai
    {
        public const int Wd = 7, Ht = 6; const int H1 = Ht + 1;
        public const int Win = 1000000; const int Inf = int.MaxValue / 2;
        static readonly ulong BottomAll, BoardMask, OddRows, EvenRows; static readonly ulong[] ColMask = new ulong[Wd]; static readonly ulong[] Windows;
        static readonly int[] BaseOrder = { 3, 2, 4, 1, 5, 0, 6 };
        static readonly int[] WinW = { 0, 1, 8, 50, 0 };   // Fensterbewertung nach Anzahl eigener Steine
        static C4Ai()
        {
            for (int c = 0; c < Wd; c++) { BottomAll |= 1UL << (c * H1); ColMask[c] = ((1UL << Ht) - 1) << (c * H1); }
            BoardMask = BottomAll * ((1UL << Ht) - 1);
            OddRows = BottomAll | (BottomAll << 2) | (BottomAll << 4); EvenRows = (BottomAll << 1) | (BottomAll << 3) | (BottomAll << 5);
            var w = new List<ulong>();
            int[][] dirs = { new[] { 1, 0 }, new[] { 0, 1 }, new[] { 1, 1 }, new[] { 1, -1 } };
            foreach (var d in dirs)
                for (int c = 0; c < Wd; c++) for (int r = 0; r < Ht; r++)
                {
                    int ec = c + 3 * d[0], er = r + 3 * d[1]; if (ec < 0 || ec >= Wd || er < 0 || er >= Ht) continue;
                    ulong m = 0; for (int k = 0; k < 4; k++) m |= Bit(c + k * d[0], r + k * d[1]); w.Add(m);
                }
            Windows = w.ToArray();   // 69 Fenster
        }
        static ulong Bit(int col, int rowFromBottom) => 1UL << (col * H1 + rowFromBottom);
        static int Pop(ulong x) { x -= (x >> 1) & 0x5555555555555555UL; x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL); x = (x + (x >> 4)) & 0x0F0F0F0F0F0F0F0FUL; return (int)((x * 0x0101010101010101UL) >> 56); }
        static int ColOf(ulong bit) { for (int c = 0; c < Wd; c++) if ((bit & ColMask[c]) != 0) return c; return -1; }

        /// <summary>Felder (leer), auf denen pos eine Viererreihe vollenden wuerde.</summary>
        static ulong WinPos(ulong pos, ulong mask)
        {
            ulong r = (pos << 1) & (pos << 2) & (pos << 3);
            ulong p = (pos << H1) & (pos << 2 * H1); r |= p & (pos << 3 * H1); r |= p & (pos >> H1);
            p = (pos >> H1) & (pos >> 2 * H1); r |= p & (pos << H1); r |= p & (pos >> 3 * H1);
            p = (pos << Ht) & (pos << 2 * Ht); r |= p & (pos << 3 * Ht); r |= p & (pos >> Ht);
            p = (pos >> Ht) & (pos >> 2 * Ht); r |= p & (pos << Ht); r |= p & (pos >> 3 * Ht);
            p = (pos << (Ht + 2)) & (pos << 2 * (Ht + 2)); r |= p & (pos << 3 * (Ht + 2)); r |= p & (pos >> (Ht + 2));
            p = (pos >> (Ht + 2)) & (pos >> 2 * (Ht + 2)); r |= p & (pos << (Ht + 2)); r |= p & (pos >> 3 * (Ht + 2));
            return r & (BoardMask ^ mask);
        }

        /// <summary>Statische Bewertung aus Sicht des Spielers am Zug.</summary>
        static int Eval(ulong cur, ulong mask)
        {
            ulong opp = cur ^ mask; int s = 0;
            foreach (var w in Windows)
            {
                int m = Pop(cur & w), o = Pop(opp & w);
                if (o == 0) s += WinW[m]; else if (m == 0) s -= WinW[o];
            }
            // Mitte bevorzugen
            s += 4 * (Pop(cur & ColMask[3]) - Pop(opp & ColMask[3])) + 2 * (Pop(cur & (ColMask[2] | ColMask[4])) - Pop(opp & (ColMask[2] | ColMask[4])));
            // Drohungen: offene Gewinnfelder; uebereinander liegende Drohungen sind besonders stark
            ulong mt = WinPos(cur, mask), ot = WinPos(opp, mask);
            s += 12 * Pop(mt) - 12 * Pop(ot);
            s += 60 * Pop(mt & (mt >> 1)) - 60 * Pop(ot & (ot >> 1));
            // Zugzwang-Regel: der Anziehende profitiert von Drohungen in ungeraden Reihen (1,3,5 von unten), der Nachziehende von geraden
            bool first = (Pop(mask) & 1) == 0; ulong mine = first ? OddRows : EvenRows, theirs = first ? EvenRows : OddRows;
            s += 25 * Pop(mt & mine) - 25 * Pop(ot & theirs);
            return s;
        }

        // Transpositionstabelle
        const int TTBits = 18; readonly ulong[] ttKey = new ulong[1 << TTBits]; readonly int[] ttVal = new int[1 << TTBits]; readonly sbyte[] ttDepth = new sbyte[1 << TTBits], ttBest = new sbyte[1 << TTBits]; readonly byte[] ttFlag = new byte[1 << TTBits];
        readonly Stopwatch sw = new Stopwatch(); long limitMs; int minDepth; bool abort; long nodes;
        public long Nodes => nodes; public int Depth { get; private set; }
        /// <summary>Tiefe und Wert der letzten vollstaendigen Iteration (Diagnose/Tests).</summary>
        public int DoneDepth, Score;

        static int ToTT(int v, int ply) => v > Win - 1000 ? v + ply : v < -(Win - 1000) ? v - ply : v;
        static int FromTT(int v, int ply) => v > Win - 1000 ? v - ply : v < -(Win - 1000) ? v + ply : v;

        int Neg(ulong cur, ulong mask, int depth, int alpha, int beta, int ply)
        {
            if ((++nodes & 1023) == 0 && Depth > minDepth && sw.ElapsedMilliseconds > limitMs) abort = true;
            if (abort) return 0;
            ulong possible = (mask + BottomAll) & BoardMask;
            if (possible == 0) return 0;                                           // Brett voll: remis
            if ((WinPos(cur, mask) & possible) != 0) return Win - ply - 1;         // sofort gewinnen
            ulong oppWin = WinPos(cur ^ mask, mask), forced = possible & oppWin;
            if (forced != 0) { if ((forced & (forced - 1)) != 0) return -(Win - ply - 2); possible = forced; }   // Doppeldrohung = verloren
            possible &= ~(oppWin >> 1);                                            // nicht unter eine gegnerische Drohung setzen
            if (possible == 0) return -(Win - ply - 2);
            if (depth <= 0) return Eval(cur, mask);
            ulong key = cur + mask; int idx = (int)((key * 0x9E3779B97F4A7C15UL) >> (64 - TTBits)); int ttMove = -1;
            if (ttKey[idx] == key)
            {
                ttMove = ttBest[idx];
                if (ttDepth[idx] >= depth)
                {
                    int v = FromTT(ttVal[idx], ply);
                    if (ttFlag[idx] == 0) return v;
                    if (ttFlag[idx] == 1) alpha = Math.Max(alpha, v); else beta = Math.Min(beta, v);
                    if (alpha >= beta) return v;
                }
            }
            int a0 = alpha, best = -Inf, bestCol = -1;
            for (int i = -1; i < Wd; i++)
            {
                int col = i < 0 ? ttMove : BaseOrder[i]; if (col < 0 || (i >= 0 && col == ttMove)) continue;
                ulong mv = possible & ColMask[col]; if (mv == 0) continue;
                int v = -Neg(cur ^ mask, mask | mv, depth - 1, -beta, -alpha, ply + 1);
                if (abort) return 0;
                if (v > best) { best = v; bestCol = col; }
                if (v > alpha) alpha = v;
                if (alpha >= beta) break;
            }
            ttKey[idx] = key; ttVal[idx] = ToTT(best, ply); ttDepth[idx] = (sbyte)Math.Min(depth, 127); ttBest[idx] = (sbyte)bestCol;
            ttFlag[idx] = (byte)(best <= a0 ? 2 : best >= beta ? 1 : 0);
            return best;
        }

        /// <summary>
        /// Bester Zug (Spalte 0..6) fuer Spieler me. board: 42 Felder, Zeile 0 oben, 0 leer / 1 / 2.
        /// level: 1 Leicht (Tiefe 2 + Zufallsfehler), 2 Mittel (Tiefe 5), 3 Schwer (iterative Vertiefung bis timeMs, min. Tiefe 8).
        /// </summary>
        public static int BestMove(int[] board, int me, int level, Random rnd, int timeMs = 600) => new C4Ai().Search(board, me, level, rnd, timeMs);
        /// <summary>Wie BestMove, liefert zusaetzlich die Suchinstanz (Tiefe, Wert, Knoten).</summary>
        public static int BestMove(int[] board, int me, int level, Random rnd, int timeMs, out C4Ai info) { info = new C4Ai(); return info.Search(board, me, level, rnd, timeMs); }

        int Search(int[] board, int me, int level, Random rnd, int timeMs)
        {
            sw.Start(); ulong cur = 0, mask = 0;
            for (int r = 0; r < Ht; r++) for (int c = 0; c < Wd; c++) { int v = board[r * Wd + c]; if (v == 0) continue; ulong bt = Bit(c, Ht - 1 - r); mask |= bt; if (v == me) cur |= bt; }
            ulong possible = (mask + BottomAll) & BoardMask; if (possible == 0) return -1;
            var legal = Enumerable.Range(0, Wd).Where(c => (possible & ColMask[c]) != 0).ToList();
            // 1. Sofort-Gewinn, 2. Sofort-Block (Leicht nur meistens)
            ulong win = WinPos(cur, mask) & possible;
            if (win != 0 && (level != 1 || rnd.NextDouble() < .9)) return ColOf(win);
            ulong block = WinPos(cur ^ mask, mask) & possible;
            if (block != 0 && (level != 1 || rnd.NextDouble() < .8)) return ColOf(block);
            if (level == 1 && rnd.NextDouble() < .3) return legal[rnd.Next(legal.Count)];   // Leicht: Zufallsfehler
            int maxDepth = level == 1 ? 2 : level == 2 ? 5 : 42; limitMs = level >= 3 || level <= 0 ? timeMs : long.MaxValue; minDepth = level >= 3 || level <= 0 ? 8 : 99;
            // Zugreihenfolge: Mitte zuerst, gleich weite Spalten zufaellig vertauscht (Abwechslung)
            var order = new List<int> { 3 };
            for (int dd = 1; dd <= 3; dd++) { if (rnd.Next(2) == 0) { order.Add(3 - dd); order.Add(3 + dd); } else { order.Add(3 + dd); order.Add(3 - dd); } }
            order = order.Where(legal.Contains).ToList();
            var bestCols = new List<int> { order[0] };
            for (int d = 1; d <= Math.Min(maxDepth, 42 - Pop(mask)); d++)
            {
                Depth = d; int best = -Inf; var cols = new List<int>();
                foreach (var col in order)
                {
                    ulong mv = possible & ColMask[col];
                    // Fenster (best-1, Inf): gleich gute Zuege bekommen ihren exakten Wert -> zufaellige Auswahl unter Gleichen
                    int v = -Neg(cur ^ mask, mask | mv, d - 1, -Inf, -(best == -Inf ? -Inf : best - 1), 1);
                    if (abort) break;
                    if (v > best) { best = v; cols.Clear(); cols.Add(col); } else if (v == best) cols.Add(col);
                }
                if (abort) break;
                bestCols = cols; DoneDepth = d; Score = best; order.Remove(cols[0]); order.Insert(0, cols[0]);
                if (Math.Abs(best) > Win - 1000) break;   // erzwungenes Ergebnis gefunden
            }
            return bestCols[rnd.Next(bestCols.Count)];
        }
    }
}
