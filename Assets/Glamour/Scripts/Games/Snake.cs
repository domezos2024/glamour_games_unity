using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class SnakeGame : Scene
    {
        public override string Title => "Snake";
        public override Col Acc1 => C.Green; public override Col Acc2 => C.Yellow;
        public override string OppKey => "snake";
        const int N = 16; const float CS = 44, BX = 800 - N * CS / 2, BY = 128;
        List<(int x, int y)> body = new List<(int x, int y)>(), prev = new List<(int x, int y)>(); (int x, int y) dir = (1, 0), next = (1, 0), food; int score, best; float speed = .17f, acc; bool running, dead, started; float deadT, eatT; readonly Queue<(int, int)> turns = new Queue<(int, int)>();
        // Duell: Computer-Schlange (pink), startet gegenueber
        List<(int x, int y)> body2 = new List<(int x, int y)>(), prev2 = new List<(int x, int y)>(); (int x, int y) dir2 = (-1, 0); int score2; bool dead1, dead2; int gen;
        bool Duel => VsCpu;
        static readonly Col Pink2 = new Col(150, 10, 90);
        public override void Enter()
        {
            base.Enter(); best = Save.Int("sn_best", 0);
            Ui.Add(new Button(40, 700, 260, 62, "Neustart", C.Green, Reset, 24)); Ui.Add(new Button(40, 780, 260, 62, "Start / Pause", C.Cyan, Toggle, 24));
            (string t, float x, float y, Key k)[] pad = { ("▲", 1360, 560, Key.Up), ("▼", 1360, 780, Key.Down), ("◀", 1250, 670, Key.Left), ("▶", 1470, 670, Key.Right) };
            foreach (var (t, x, y, k) in pad) { var kk = k; Ui.Add(new Button(x, y, 110, 110, t, C.Green, () => KeyDown(kk), 46) { Alpha = .6f }); }
            Opponents.AddSwitch(this, OppKey, 1290, 150, 260, 70, Reset, "Solo");
            Reset();
            Opponents.Pick(this, OppKey, o => Reset(), "Solo", "allein spielen");
        }
        void Reset()
        {
            gen++; body = new List<(int x, int y)> { (4, 8), (3, 8), (2, 8) }; prev = body.ToList(); dir = next = (1, 0); turns.Clear(); score = 0; speed = .17f; acc = 0; running = false; dead = false; started = false; Modal = null;
            score2 = 0; dead1 = dead2 = false; dir2 = (-1, 0);
            body2 = Duel ? new List<(int x, int y)> { (N - 5, N - 9), (N - 4, N - 9), (N - 3, N - 9) } : new List<(int x, int y)>(); prev2 = body2.ToList();
            PlaceFood();
        }
        void Toggle() { if (dead) { Reset(); return; } running = !running; started = true; Sfx.Play(S.Turn); }
        void PlaceFood()
        {
            var free = new List<(int x, int y)>();
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (!body.Contains((x, y)) && !body2.Contains((x, y))) free.Add((x, y));
            if (free.Count > 0) food = free[Rng.I(free.Count)];   // Feld voll: Futter bleibt liegen
        }
        public override void KeyDown(Key k)
        {
            (int, int)? d = k switch { Key.Up or Key.W => (0, -1), Key.Down or Key.S => (0, 1), Key.Left or Key.A => (-1, 0), Key.Right or Key.D => (1, 0), _ => null };
            if (k == Key.Space || k == Key.Enter) { Toggle(); return; }
            if (d == null || dead) return;
            if (!running && !started) { running = true; started = true; }
            (int x, int y) last = turns.Count > 0 ? turns.Last() : dir; if (turns.Count < 3 && !(d.Value.Item1 == -last.x && d.Value.Item2 == -last.y) && d.Value != last) turns.Enqueue(d.Value);
        }
        Pt sw; bool swActive;
        public override void MouseDown(float x, float y) { sw = new Pt(x, y); swActive = true; }
        public override void MouseUp(float x, float y) { swActive = false; }
        public override void MouseMove(float x, float y)
        {
            if (!swActive) return;
            float dx = x - sw.X, dy = y - sw.Y;
            if (MathF.Abs(dx) < 40 && MathF.Abs(dy) < 40) return;
            sw = new Pt(x, y);
            KeyDown(MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? Key.Right : Key.Left) : (dy > 0 ? Key.Down : Key.Up));
        }
        static bool Out((int x, int y) p) => p.x < 0 || p.x >= N || p.y < 0 || p.y >= N;
        void Tick()
        {
            if (turns.Count > 0) next = turns.Dequeue(); dir = next; prev = body.ToList();
            if (Duel) { TickDuel(); return; }
            var h = body[0]; var nh = (x: h.x + dir.x, y: h.y + dir.y);
            if (Out(nh) || body.Take(body.Count - 1).Contains(nh)) { Die(); return; }
            body.Insert(0, nh);
            if (nh == food) Eat(0);
            else body.RemoveAt(body.Count - 1);
            Sfx.Play(S.Tick, .12f);
        }
        void Eat(int who)
        {
            if (who == 0) { score++; prev.Add(prev[prev.Count - 1]); } else { score2++; prev2.Add(prev2[prev2.Count - 1]); }
            eatT = 0; Sfx.Play(S.Eat); speed = Math.Max(.095f, .17f - (score + score2) * .003f);
            var fc = P(food); Fx.Burst(fc.X, fc.Y, 24, who == 0 ? new[] { C.Yellow, C.Green, Col.White } : new[] { C.Yellow, C.Pink, Col.White }, 260); Fx.Shockwave(fc.X, fc.Y, who == 0 ? C.Green : C.Pink, 70, .35f);
            Pop("+1", fc.X, fc.Y - 20, who == 0 ? C.Yellow : C.Pink.Light(.4f), 34); PlaceFood();
        }
        /// <summary>Beide Schlangen ziehen gleichzeitig; wer gegen Wand/Koerper faehrt, verliert, Kopf an Kopf = Unentschieden.</summary>
        void TickDuel()
        {
            dir2 = SnakeAI.Decide(N, body2, dir2, body, food, (int)Opp, Rng.Shared); prev2 = body2.ToList();
            var h1 = (x: body[0].x + dir.x, y: body[0].y + dir.y); var h2 = (x: body2[0].x + dir2.x, y: body2[0].y + dir2.y);
            bool e1 = h1 == food, e2 = h2 == food;
            bool d1 = Out(h1) || body.Take(body.Count - 1).Contains(h1) || body2.Take(body2.Count - (e2 ? 0 : 1)).Contains(h1);
            bool d2 = Out(h2) || body2.Take(body2.Count - 1).Contains(h2) || body.Take(body.Count - (e1 ? 0 : 1)).Contains(h2);
            if (h1 == h2 || (h1 == body2[0] && h2 == body[0])) d1 = d2 = true;
            if (d1 || d2) { DieDuel(d1, d2); return; }
            body.Insert(0, h1); body2.Insert(0, h2);
            if (e1) Eat(0); else body.RemoveAt(body.Count - 1);
            if (e2) Eat(1); else body2.RemoveAt(body2.Count - 1);
            Sfx.Play(S.Tick, .12f);
        }
        void Die()
        {
            dead = true; running = false; deadT = 0; dead1 = true; Sfx.Play(S.Die); App.Shake(16); App.Flash(C.Red, .35f); var hp = P(body[0]); Fx.Burst(hp.X, hp.Y, 60, new[] { C.Green, C.Red, Col.White }, 500); Fx.Explosion(hp.X, hp.Y, 1f); Sfx.Play(S.Boom, .6f);
            bool nb = score > best; if (nb) { best = score; Save.Set("sn_best", best); }
            int g = gen;
            Tm.After(.9f, () => { if (g != gen) return; if (nb) { Celebrate(C.Gold, 4, 1f); Sfx.Play(S.Big); } Result(nb ? "NEUER BESTWERT!" : "GAME OVER", $"Länge {score + 3}  -  Punkte {score}", nb ? C.Gold : C.Red, new List<string> { $"Bestwert: {best}" }, ("Nochmal", C.Green, Reset), ("Menü", C.Purple, () => App.Go(new Menu()))); });
        }
        void DieDuel(bool d1, bool d2)
        {
            dead = true; running = false; deadT = 0; dead1 = d1; dead2 = d2; Sfx.Play(S.Die); App.Shake(16); App.Flash(C.Red, .35f); Sfx.Play(S.Boom, .6f);
            if (d1) { var hp = P(body[0]); Fx.Burst(hp.X, hp.Y, 60, new[] { C.Green, C.Red, Col.White }, 500); Fx.Explosion(hp.X, hp.Y, 1f); }
            if (d2) { var hp = P(body2[0]); Fx.Burst(hp.X, hp.Y, 60, new[] { C.Pink, C.Red, Col.White }, 500); Fx.Explosion(hp.X, hp.Y, 1f); }
            int w = d1 && d2 ? -1 : d1 ? 1 : 0; var col = w == 0 ? C.Green : w == 1 ? C.Pink : C.Gold;
            string why = w < 0 ? "Kopf an Kopf - beide gecrasht!" : $"{PName(1 - w)} ist gecrasht";
            int g = gen;
            Tm.After(.9f, () =>
            {
                if (g != gen) return;
                if (w >= 0) { Celebrate(col, 4, 1f, $"{PName(w)} gewinnt!"); Sfx.Play(S.Big); }
                Result(w < 0 ? "UNENTSCHIEDEN!" : $"{PName(w)} GEWINNT!", $"{PName(0)}: {score}  |  {PName(1)}: {score2}", col, new List<string> { why }, ("Nochmal", C.Green, Reset), ("Menü", C.Purple, () => App.Go(new Menu())));
            });
        }
        static Pt P((int x, int y) c) => new Pt(BX + c.x * CS + CS / 2, BY + c.y * CS + CS / 2);
        public override void Update(float dt)
        {
            eatT += dt; if (dead) { deadT += dt; return; }
            if (running) { acc += dt; while (acc >= speed && running) { acc -= speed; Tick(); } }
            if (running && Rng.F() < dt * 30) { var h = Pos(body, prev, 0); Fx.Spark(h.X, h.Y, C.Green, 1, 30); }
            if (running && Duel && body2.Count > 0 && Rng.F() < dt * 30) { var h = Pos(body2, prev2, 0); Fx.Spark(h.X, h.Y, C.Pink, 1, 30); }
        }
        Pt Pos(List<(int x, int y)> b, List<(int x, int y)> pv, int i)
        {
            float a = running ? Ease.Clamp(acc / speed) : 1; var to = P(b[i]); var from = P(i < pv.Count ? pv[i] : pv[pv.Count - 1]); if (!running) from = to;
            return new Pt(from.X + (to.X - from.X) * a, from.Y + (to.Y - from.Y) * a);
        }
        public override void Draw(Canvas2D c)
        {
            if (Duel)
            {
                W.PlayerBox(c, Gfx.R(40, 140, 260, 200), PName(0), score.ToString(), C.Green, running && !dead1, Time, $"Länge {body.Count}");
                W.PlayerBox(c, Gfx.R(40, 370, 260, 200), PName(1), score2.ToString(), C.Pink, running && !dead2, Time, $"Länge {body2.Count}");
            }
            else
            {
                W.PlayerBox(c, Gfx.R(40, 140, 260, 200), "Punkte", score.ToString(), C.Green, running, Time, $"Länge {body.Count}");
                W.PlayerBox(c, Gfx.R(40, 370, 260, 200), "Bestwert", best.ToString(), C.Gold, false, Time);
            }
            Gfx.Text(c, $"Tempo {(.17f / speed * 100):0}%", 170, 620, 24, C.Dim, Al.C, false);
            if (Duel) Gfx.Text(c, $"Solo-Bestwert: {best}", 170, 656, 20, C.Gold.A(.8f), Al.C, false);
            var fr = Gfx.R(BX - 10, BY - 10, N * CS + 20, N * CS + 20); Gfx.Shadow(c, fr, 16, 20, .55f, 0, 14); Gfx.Glow(c, fr, 16, C.Green, 20, .45f); Gfx.RectGrad(c, fr, 16, new Col(4, 22, 14), new Col(2, 8, 10));
            for (int i = 0; i < N * N; i += 1) if ((i / N + i % N) % 2 == 0) c.DrawRect(BX + (i % N) * CS, BY + (i / N) * CS, CS, CS, Gfx.Fill(C.Green.A(.025f)));
            var gl = Gfx.Line(C.Green.A(.10f), 1.2f); gl.Glow = 1.2f; for (int k = 0; k <= N; k++) { c.DrawLine(BX + k * CS, BY, BX + k * CS, BY + N * CS, gl); c.DrawLine(BX, BY + k * CS, BX + N * CS, BY + k * CS, gl); }
            var fs = Gfx.Line(C.Green.A(.9f), 3); fs.Glow = 1.8f; c.DrawRoundRect(fr, 16, 16, fs);
            var fp = P(food); float pl = 1 + .12f * MathF.Sin(Time * 6);
            Gfx.Light(c, fp.X, fp.Y, CS * 1.4f, C.Yellow, .4f * pl, 1.6f); Gfx.Ball(c, fp.X, fp.Y, CS * .32f * pl, C.Yellow.Mix(C.Orange, .3f)); Gfx.Star(c, fp.X + MathF.Cos(Time * 3) * CS * .5f, fp.Y + MathF.Sin(Time * 3) * CS * .5f, 5, C.White.A(.8f), Time);
            float fade1 = dead1 ? Math.Max(0, 1 - deadT * .8f) : 1, fade2 = dead2 ? Math.Max(0, 1 - deadT * .8f) : 1;
            if (Duel && body2.Count > 0) DrawSnake(c, body2, prev2, dir2, C.Pink, Pink2, fade2);
            DrawSnake(c, body, prev, dir, C.Green, new Col(0, 120, 90), fade1);
            if (!started && !dead)
            {
                Gfx.Rect(c, Gfx.Ctr(800, 460, 640, Duel ? 170 : 130), 24, Col.Black.A(.6f)); Gfx.Text(c, "Pfeiltasten / WASD oder Kreuz", 800, 435, 34, Col.White, Al.C, true, 6); Gfx.Text(c, "Leertaste = Start / Pause", 800, 485, 26, C.Dim, Al.C, false);
                if (Duel) Gfx.Text(c, "Duell: Wer zuerst crasht, verliert!", 800, 525, 24, C.Pink.Light(.3f), Al.C, true, 4);
            }
            else if (!running && !dead) Gfx.Text(c, "PAUSE", 800, 460, 80, C.Yellow, Al.C, true, 20);
        }
        /// <summary>Schlange aus beleuchteten 3D-Kugeln mit weichem Schatten und Neon-Schein.</summary>
        void DrawSnake(Canvas2D c, List<(int x, int y)> b, List<(int x, int y)> pv, (int x, int y) d, Col head, Col tail, float fade)
        {
            int n = b.Count; if (n == 0 || fade <= 0) return; var pts = new Pt[n]; for (int i = 0; i < n; i++) pts[i] = Pos(b, pv, i);
            var shp = Gfx.Fill(Col.Black.A(.35f * fade)); shp.Blur = 6;
            for (int i = n - 1; i >= 0; i--) { float t = i / (float)Math.Max(1, n - 1); c.DrawCircle(pts[i].X + 4, pts[i].Y + 7, CS * (.46f - .17f * t), shp); }
            for (int i = n - 1; i >= 0; i--)
            {
                float t = i / (float)Math.Max(1, n - 1); float r = CS * (.46f - .17f * t); var col = head.Mix(tail, t * .8f);
                for (int s = 3; s >= 0; s--)
                {
                    var q = s == 0 ? pts[i] : i + 1 < n ? new Pt(pts[i].X + (pts[i + 1].X - pts[i].X) * s / 4f, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * s / 4f) : pts[i];
                    if (s == 0 || i + 1 < n) Gfx.Ball(c, q.X, q.Y, r, col.A(fade));
                }
                if (i % 3 == 0) Gfx.Light(c, pts[i].X, pts[i].Y, r * 2, head, .12f * fade, 1.4f);
            }
            var h = pts[0]; Gfx.Light(c, h.X, h.Y, CS * 1.1f, head, .25f * fade, 1.7f);
            float ex = d.x, ey = d.y, px = -ey, py = ex; float er = CS * .11f;
            foreach (var sd in new[] { -1, 1 }) { float ox = h.X + ex * CS * .12f + px * sd * CS * .2f, oy = h.Y + ey * CS * .12f + py * sd * CS * .2f; c.DrawCircle(ox, oy, er * 1.4f, Gfx.Fill(Col.White.A(fade))); c.DrawCircle(ox + ex * er * .5f, oy + ey * er * .5f, er * .8f, Gfx.Fill(Col.Black.A(fade))); }
        }
    }

    // ==== KI-BEGIN (Unity-frei, wird im Konsolentest mitkompiliert)
    /// <summary>KI der Computer-Schlange. Entscheidet pro Tick, 16x16-Feld, deutlich unter 1 ms.</summary>
    public static class SnakeAI
    {
        static readonly (int x, int y)[] Dirs = { (1, 0), (0, 1), (-1, 0), (0, -1) };
        /// <summary>
        /// Naechste Richtung. me/foe: Koerper (Kopf zuerst), foe darf leer sein. level 1 = gierig + Zufall,
        /// 2 = BFS zum Futter + Flood-Fill-Fluchtcheck, 3 = zusaetzlich Gegnerkopf meiden + Schwanz folgen.
        /// </summary>
        public static (int x, int y) Decide(int n, IList<(int x, int y)> me, (int x, int y) dir, IList<(int x, int y)> foe, (int x, int y) food, int level, Random rnd)
        {
            var head = me[0]; var block = new bool[n * n];
            for (int i = 0; i < me.Count - 1; i++) block[me[i].y * n + me[i].x] = true;   // eigener Schwanz rueckt nach
            bool[] danger = null;
            if (foe != null && foe.Count > 0)
            {
                var fh = foe[0]; bool foeEats = Math.Abs(fh.x - food.x) + Math.Abs(fh.y - food.y) == 1;
                for (int i = 0; i < foe.Count - (foeEats ? 0 : 1); i++) block[foe[i].y * n + foe[i].x] = true;
                danger = new bool[n * n];
                foreach (var d in Dirs) { int x = fh.x + d.x, y = fh.y + d.y; if (x >= 0 && x < n && y >= 0 && y < n) danger[y * n + x] = true; }
            }
            var safe = new List<(int x, int y)>(3);
            foreach (var d in Dirs)
            {
                if (d.x == -dir.x && d.y == -dir.y && me.Count > 1) continue;
                int x = head.x + d.x, y = head.y + d.y; if (x >= 0 && x < n && y >= 0 && y < n && !block[y * n + x]) safe.Add(d);
            }
            if (safe.Count == 0) return dir;
            int Dist((int x, int y) d) => Math.Abs(head.x + d.x - food.x) + Math.Abs(head.y + d.y - food.y);
            if (level <= 1)
            {
                // Leicht: gierig zum Futter, nur 1 Feld vorausschauen, gelegentlich Zufall
                if (rnd.NextDouble() < .12) return safe[rnd.Next(safe.Count)];
                int bd = int.MaxValue; var best = safe[0];
                foreach (var d in safe) { int v = Dist(d) * 4 + rnd.Next(3); if (v < bd) { bd = v; best = d; } }
                return best;
            }
            if (level == 2)
            {
                var path = Bfs(n, head, food, block);
                if (path != null)
                {
                    var p = path[0]; block[p.y * n + p.x] = true; int area = Flood(n, p, block, true); block[p.y * n + p.x] = false;
                    if (area >= me.Count) return (p.x - head.x, p.y - head.y);
                }
                return MaxArea(n, head, safe, block, Dist);
            }
            // Schwer: Felder meiden, die der Gegnerkopf im naechsten Schritt erreicht
            if (danger != null)
            {
                var bh = (bool[])block.Clone(); for (int i = 0; i < bh.Length; i++) if (danger[i]) bh[i] = true;
                var path = Bfs(n, head, food, bh);
                if (path != null && TailOk(n, me, path, foe, food)) return (path[0].x - head.x, path[0].y - head.y);
            }
            else
            {
                var path = Bfs(n, head, food, block);
                if (path != null && TailOk(n, me, path, foe, food)) return (path[0].x - head.x, path[0].y - head.y);
            }
            // Notfall: dem eigenen Schwanz folgen (Zug, nach dem der Schwanz erreichbar bleibt, mit viel Platz)
            float bs = float.MinValue; var bdir = safe[0];
            foreach (var d in safe)
            {
                var p = (x: head.x + d.x, y: head.y + d.y);
                int reach = TailDist(n, me, new List<(int x, int y)> { p }, foe, food);
                block[p.y * n + p.x] = true; int area = Flood(n, p, block, true); block[p.y * n + p.x] = false;
                float sc = (reach >= 0 ? 100000 : 0) + area * 100 + Math.Max(reach, 0) - (danger != null && danger[p.y * n + p.x] ? 50000 : 0);
                if (sc > bs) { bs = sc; bdir = d; }
            }
            return bdir;
        }
        static (int x, int y) MaxArea(int n, (int x, int y) head, List<(int x, int y)> safe, bool[] block, Func<(int x, int y), int> dist)
        {
            int ba = -1, bd = int.MaxValue; var best = safe[0];
            foreach (var d in safe)
            {
                var p = (x: head.x + d.x, y: head.y + d.y); block[p.y * n + p.x] = true; int a = Flood(n, p, block, true); block[p.y * n + p.x] = false;
                int di = dist(d); if (a > ba || (a == ba && di < bd)) { ba = a; bd = di; best = d; }
            }
            return best;
        }
        /// <summary>Kuerzester Weg (ohne Start) oder null.</summary>
        public static List<(int x, int y)> Bfs(int n, (int x, int y) from, (int x, int y) to, bool[] block)
        {
            var prev = new int[n * n]; for (int i = 0; i < prev.Length; i++) prev[i] = -2;
            var q = new int[n * n]; int qh = 0, qt = 0, s = from.y * n + from.x, t = to.y * n + to.x;
            q[qt++] = s; prev[s] = -1;
            while (qh < qt)
            {
                int cur = q[qh++]; if (cur == t) break; int cx = cur % n, cy = cur / n;
                foreach (var d in Dirs)
                {
                    int x = cx + d.x, y = cy + d.y; if (x < 0 || x >= n || y < 0 || y >= n) continue; int j = y * n + x;
                    if (prev[j] != -2 || (block[j] && j != t)) continue; prev[j] = cur; q[qt++] = j;
                }
            }
            if (prev[t] == -2 || block[t]) return null;
            var path = new List<(int x, int y)>(); for (int c = t; c != s; c = prev[c]) path.Add((c % n, c / n)); path.Reverse(); return path;
        }
        /// <summary>Anzahl erreichbarer freier Felder ab p (p selbst zaehlt mit).</summary>
        public static int Flood(int n, (int x, int y) p, bool[] block, bool startBlocked)
        {
            var seen = new bool[n * n]; var q = new int[n * n]; int qh = 0, qt = 0; int s = p.y * n + p.x; q[qt++] = s; seen[s] = true;
            while (qh < qt)
            {
                int cur = q[qh++]; int cx = cur % n, cy = cur / n;
                foreach (var d in Dirs) { int x = cx + d.x, y = cy + d.y; if (x < 0 || x >= n || y < 0 || y >= n) continue; int j = y * n + x; if (seen[j] || block[j]) continue; seen[j] = true; q[qt++] = j; }
            }
            return qt;
        }
        /// <summary>Nach dem Abfahren des Weges: ist der eigene Schwanz noch erreichbar?</summary>
        static bool TailOk(int n, IList<(int x, int y)> me, List<(int x, int y)> path, IList<(int x, int y)> foe, (int x, int y) food) => TailDist(n, me, path, foe, food) >= 0;
        static int TailDist(int n, IList<(int x, int y)> me, List<(int x, int y)> path, IList<(int x, int y)> foe, (int x, int y) food)
        {
            var v = new List<(int x, int y)>(me);
            foreach (var p in path) { v.Insert(0, p); if (p != food) v.RemoveAt(v.Count - 1); }
            if (v.Count < 2) return 0;
            var block = new bool[n * n];
            for (int i = 0; i < v.Count - 1; i++) block[v[i].y * n + v[i].x] = true;
            if (foe != null) foreach (var f in foe) block[f.y * n + f.x] = true;
            var tail = v[v.Count - 1]; block[v[0].y * n + v[0].x] = false; block[tail.y * n + tail.x] = false;
            var pt = Bfs(n, v[0], tail, block);
            return pt == null ? -1 : pt.Count;
        }
    }
    // ==== KI-END
}
