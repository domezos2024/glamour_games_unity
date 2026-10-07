using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    /// <summary>Basis aller Bildschirme (Menue, Optionen, Spiele). Koordinaten: 1600x900, y nach unten.</summary>
    public abstract class Scene
    {
        public readonly Ui Ui = new Ui(); public Modal Modal; public readonly Particles Fx = new Particles(); public readonly Timers Tm = new Timers(); public readonly Coro Co = new Coro(); public float Time;
        /// <summary>Laufende Siegesinszenierung (oder null).</summary>
        public RewardShow Reward;
        public virtual string Title => ""; public virtual Col Acc1 => C.Cyan; public virtual Col Acc2 => C.Pink; public virtual bool Chrome => true;
        /// <summary>Wie viele Lichtblumen der Hintergrund zeigt (0..1).</summary>
        public virtual float Meadow => 1;
        protected Button Back;
        readonly List<(string s, float x, float y, Col c, float t, float size)> pops = new List<(string, float, float, Col, float, float)>();
        public void Pop(string s, float x, float y, Col c, float size = 40) => pops.Add((s, x, y, c, 0, size));
        public virtual void Enter() { Log.I("enter " + GetType().Name); if (Chrome) Back = Ui.Add(new Button(24, 14, 260, 84, "<  Menü", C.Purple, () => App.Go(new Menu()), 32)); }
        public virtual void Leave() { Log.I("leave " + GetType().Name); if (Remote && Link.Connected) { if (Link.IsHost) Link.BackToMenu(); else if (!Link.HostDriven) Net("left"); } Link.HostDriven = false; }
        public virtual void Update(float dt) { }
        public abstract void Draw(Canvas2D c);
        public virtual void MouseMove(float x, float y) { }
        public virtual void MouseDown(float x, float y) { }
        public virtual void MouseUp(float x, float y) { }
        public virtual void Wheel(float d) { }
        public virtual void KeyDown(Key k) { }
        /// <summary>true = Escape selbst behandelt; sonst geht es zurueck ins Menue.</summary>
        public virtual bool Escape() => false;
        public virtual void DebugWin() { }
        public virtual bool WantsHand => false;

        // ---------------------------------------------------------------- Computer-Gegner
        /// <summary>Spielschluessel fuer gespeicherte Gegner-Einstellung (z.B. "ttt"). null = Spiel ohne Gegnerwahl.</summary>
        public virtual string OppKey => null;
        /// <summary>Aktueller Gegner fuer Platz 2 (Mensch oder Computer mit Stufe).</summary>
        public Opponent Opp = Opponent.Human;
        public bool VsCpu => Opp != Opponent.Human && Opp != Opponent.Remote;
        /// <summary>Gegner spielt per Bluetooth an einem anderen Geraet (sitzt auf Platz 2, jedes Geraet sieht sich als Spieler 1).</summary>
        public bool Remote => Opp == Opponent.Remote;
        /// <summary>Name fuer Platz i (0 oder 1); Platz 2 heisst "Computer", wenn der Computer spielt, bzw. wie der Bluetooth-Mitspieler.</summary>
        public string PName(int i) => i == 1 && Remote ? Link.PeerName : i == 1 && VsCpu ? Opponents.CpuName(Opp) : Pl.Name(i);

        // ---------------------------------------------------------------- Bluetooth-Mehrspieler
        /// <summary>Spielnachricht an den Mitspieler (nur im Bluetooth-Spiel).</summary>
        protected void Net(string kind, params object[] data) { if (Remote && OppKey != null) Link.Game(OppKey, kind, data); }
        Action<int> netToss; int netTossPending = -1;
        readonly Dictionary<string, Action> netActs = new Dictionary<string, Action>(); int netSeq;
        /// <summary>
        /// Aktion, die im Bluetooth-Spiel auf beiden Geraeten genau einmal ausgefuehrt wird (z. B. "Naechste Runde"):
        /// lokal sofort, beim Mitspieler per Folgenummer - druecken beide gleichzeitig, zaehlt nur ein Druck.
        /// Bei Enter registrieren und die gelieferte Aktion fuer Buttons verwenden.
        /// </summary>
        protected Action Shared(string kind, Action act) { netActs[kind] = act; return () => { if (Remote) Net("sync", kind, netSeq); netSeq++; act(); }; }
        /// <summary>Muenzwurf im Bluetooth-Spiel: der Host lost aus, beide Geraete zeigen dasselbe Ergebnis (gespiegelt).</summary>
        internal void NetToss(Action<int> done)
        {
            if (Link.IsHost) { int f = Rng.I(2); Net("toss", f); CoinToss.Fixed(this, f, done); return; }
            if (netTossPending >= 0) { int f = netTossPending; netTossPending = -1; CoinToss.Fixed(this, f, done); } else netToss = done;
        }
        /// <summary>Nachricht vom Mitspieler. Spiele ueberschreiben das und rufen fuer Unbekanntes base.NetRecv auf.</summary>
        public virtual void NetRecv(string kind, string[] a)
        {
            switch (kind)
            {
                case "toss": { int f = 1 - Link.Int(a[0]); if (netToss != null) { var d = netToss; netToss = null; CoinToss.Fixed(this, f, d); } else netTossPending = f; break; }
                case "sync": if (a.Length > 1 && Link.Int(a[1]) == netSeq && netActs.TryGetValue(a[0], out var act)) { netSeq++; Modal = null; act(); } break;
                case "left": App.Toast($"{Link.PeerName} hat das Spiel verlassen"); App.Go(new Menu()); break;
            }
        }
        /// <summary>Verbindung abgerissen: zurueck ins Menue.</summary>
        public virtual void NetLost() { if (Remote) { Opp = Opponent.Human; App.Go(new Menu()); } }
        /// <summary>Laesst den Computer "nachdenken" (Anzeige + Verzoegerung) und fuehrt dann die Aktion aus.</summary>
        public void CpuThink(float seconds, Action act) { int tok = ++thinkTok; cpuThinkT = seconds; cpuThinkMax = seconds; Tm.After(seconds, () => { if (tok == thinkTok) cpuThinkT = 0; act(); }); }
        int thinkTok;
        public bool CpuThinking => cpuThinkT > 0;
        /// <summary>Blendet die Denk-Anzeige sofort aus (z. B. bei Neustart). Geplante Aktionen per Generationszaehler verwerfen.</summary>
        public void CancelCpuThink() { cpuThinkT = 0; thinkTok++; }
        float cpuThinkT, cpuThinkMax;

        public void Celebrate(Col col, float dur = 5, float power = 1, string banner = null)
        {
            bool cpuWon = VsCpu && banner != null && banner.StartsWith(PName(1));
            Reward = new RewardShow(col, dur, power, banner, !cpuWon); Lens.Kick?.Invoke(power);
            Log.I("celebrate " + GetType().Name + (Reward.Trophy ? " +pokal" : "")); Sfx.Play(S.Fanfare, .8f); Sfx.Play(S.Cheer, .7f); Tm.After(.6f, () => Sfx.Play(S.Sparkle, .6f));
            var rnd = Rng.Shared; var cols = new[] { col, C.Gold, C.Pink, C.Cyan, C.Green, C.Purple };
            Fx.Cannon(-10, 900, -1.05f, (int)(80 * power)); Fx.Cannon(1610, 900, -2.09f, (int)(80 * power)); Sfx.Play(S.Pop, .8f, .8f);
            Fx.Shockwave(800, 450, col, 700, .9f); Fx.Shockwave(800, 450, C.Gold, 500, .7f); App.Flash(col.Light(.5f), .5f); App.Shake(12);
            Fx.CoinShower(1600, (int)(45 * power), dur * .45f); Fx.Streamers(1600, (int)(16 * power), dur * .3f);
            if (Reward.Trophy) Tm.After(1f, () => { var b = Reward?.TrophyBox ?? Gfx.Ctr(800, 525, 376, 470); Fx.CoinFountain(b.MidX, b.Top + b.Height * .12f, (int)(34 * power)); Fx.Glints(b, 10, Metal.GoldLight, .4f); Sfx.Play(S.Coin, .7f); Sfx.Play(S.Sparkle, .8f, 1.2f); Lens.Kick?.Invoke(power * 1.1f); });
            for (int i = 0; i < (int)(dur * 3.4f * power); i++) { float d = .25f + i * .3f + (float)rnd.NextDouble() * .15f; Tm.After(d, () => Fx.Rocket(200 + (float)rnd.NextDouble() * 1200, 120 + (float)rnd.NextDouble() * 320, cols[rnd.Next(cols.Length)])); }
            for (int i = 0; i < 4; i++) { int k = i; float d = .5f + i * 1.4f * (dur / 5); Tm.After(d, () => { float x = 250 + (float)rnd.NextDouble() * 1100; Fx.Lightning(x + (float)rnd.NextDouble() * 200 - 100, -20, x, 300 + (float)rnd.NextDouble() * 350, k % 2 == 0 ? col.Light(.3f) : C.Gold); Sfx.Play(S.Zap, .35f); App.Shake(8); }); }
            for (int i = 0; i < 3; i++) { float d = 1.2f + i * 1.6f; Tm.After(d, () => { Fx.Cannon(-10, 900, -1.05f, 40); Fx.Cannon(1610, 900, -2.09f, 40); Fx.Petals(800, 500, 30, cols, 500); Sfx.Play(S.Pop, .6f, 1.1f); }); }
        }
        public void BaseUpdate(float dt)
        {
            Time += dt; if (Reward != null) { Reward.Update(dt); if (Reward.Done) Reward = null; }
            if (cpuThinkT > 0) cpuThinkT = Math.Max(0, cpuThinkT - dt);
            for (int i = pops.Count - 1; i >= 0; i--) { var q = pops[i]; q.t += dt; if (q.t > 1.4f) pops.RemoveAt(i); else pops[i] = q; }
            Tm.Update(dt); Co.Update(dt); Fx.Update(dt); Ui.Update(dt); Modal?.Update(dt); Update(dt);
        }
        public void BaseDraw(Canvas2D c)
        {
            Backdrop.DrawMeadow(c, Time, Acc1, Acc2, Meadow); Reward?.DrawBack(c);
            if (Chrome && Title.Length > 0) { Gfx.Text(c, Title, 800, 52, 44, Col.White, Al.C, true, 14, true); Gfx.Text(c, Title, 800, 52, 44, Acc1.Light(.55f), Al.C, true, 0, true); }
            Draw(c); Ui.Draw(c);
            if (cpuThinkT > 0) DrawThinking(c);
            bool hideT = Modal != null && Modal.Win; Reward?.DrawMid(c, hideT);
            Fx.Draw(c); Reward?.DrawTop(c, hideT);
            foreach (var q in pops)
            {
                float a = 1 - Ease.InCubic((q.t - .8f) / .6f), sc = Ease.OutBack(q.t / .3f); c.Save(); c.Translate(q.x, q.y - q.t * 70); c.Scale(sc, sc);
                Gfx.Text(c, q.s, 0, 0, q.size, q.c.A(a), Al.C, true, 10); c.Restore();
            }
            Modal?.Draw(c);
        }
        /// <summary>Position der "Computer denkt nach"-Anzeige; Spiele koennen sie ueberschreiben.</summary>
        public virtual Pt ThinkPos => new Pt(800, 880);
        void DrawThinking(Canvas2D c)
        {
            var p = ThinkPos; float a = Ease.Clamp((cpuThinkMax - cpuThinkT) / .2f);
            var r = Gfx.Ctr(p.X, p.Y, 330, 44); Gfx.Rect(c, r, 22, C.Panel.A(.85f * a)); Gfx.Glow(c, r, 22, C.Pink, 10, .5f * a); Gfx.Stroke(c, r, 22, C.Pink.A(.8f * a), 2);
            Gfx.Text(c, "Computer denkt nach", p.X - 22, p.Y, 22, Col.White.A(a), Al.C, true);
            for (int i = 0; i < 3; i++) { float k = .5f + .5f * MathF.Sin(Time * 8 - i * .8f); c.DrawCircle(p.X + 118 + i * 14, p.Y + 2, 3.5f + k * 1.5f, Gfx.Fill(C.Pink.Light(.3f).A(a * (.4f + .6f * k)))); }
        }

        public void Result(string title, string sub, Col col, params (string, Col, Action)[] btns) => Result(title, sub, col, null, btns);
        public void Result(string title, string sub, Col col, List<string> lines, params (string, Col, Action)[] btns)
        {
            var m = new Modal { Title = title, Sub = sub, Col = col, H = 380 + (lines?.Count ?? 0) * 34, Win = IsWin(title, col) };
            if (m.Win) { Fx.CoinShower(1600, 36, 1.2f); Lens.Kick?.Invoke(.6f); Tm.After(.35f, () => Sfx.Play(S.Sparkle, .7f, 1.1f)); }
            if (lines != null) m.Lines = lines;
            foreach (var (t, cc, a) in btns) { var act = a; m.Btns.Add(new Button { Text = t, Col = cc, Click = () => { Modal = null; act?.Invoke(); }, Size = 26 }); }
            Modal = m; Sfx.Play(S.Turn);
        }
        /// <summary>Sieger-Dialog (mit Pokal), wenn ein Mensch gewinnt.</summary>
        bool IsWin(string title, Col col)
        {
            var t = title.ToLowerInvariant(); if (col == C.Red || t.Contains("ausgeschieden") || VsCpu && title.StartsWith(PName(1))) return false;
            return t.Contains("gewinnt") || t.Contains("bestwert") || t.Contains("sieg");
        }
        public void NameEntry(string key, int score, string title, Action done)
        {
            Fx.Confetti(1600, 90);
            var m = new Modal { Title = title, Sub = $"{score} Punkte - Name eintragen (max. 5 Zeichen)", Col = C.Gold, Input = true, H = 262, Text = Save.Str("lastname", "") };
            m.Submit = n => { Save.Set("lastname", n); Save.AddScore(key, n, score); Modal = null; done?.Invoke(); };
            m.Cancel = () => { Modal = null; done?.Invoke(); };
            m.Btns.Add(new Button { Text = "Speichern", Col = C.Green, Click = () => m.Submit(m.Text.Trim().Length == 0 ? "ANON" : m.Text.Trim()) });
            m.Btns.Add(new Button { Text = "Überspringen", Col = C.Dim, Click = () => m.Cancel() });
            Modal = m;
        }
        public static void HighscoreList(Canvas2D c, string key, float x, float y, float w, Col col)
        {
            var l = Save.Scores(key); Gfx.Text(c, "HIGHSCORES", x + w / 2, y, 30, col, Al.C, true, 8, true);
            for (int i = 0; i < 10; i++)
            {
                float yy = y + 44 + i * 32; bool have = i < l.Count; var cc = i == 0 ? C.Gold : i == 1 ? new Col(220, 220, 230) : i == 2 ? new Col(230, 150, 90) : C.Dim;
                Gfx.Text(c, $"{i + 1}.", x + 12, yy, 22, cc, Al.L); Gfx.Text(c, have ? l[i].name : "-----", x + 56, yy, 22, have ? Col.White : C.Dim.A(.4f), Al.L);
                Gfx.Text(c, have ? l[i].score.ToString() : "", x + w - 12, yy, 22, cc, Al.R);
            }
        }
    }

    public class Button
    {
        public Box R; public string Text; public string Sub; public Col Col = C.Cyan; public Action Click; public bool Enabled = true, Visible = true, Selected; public float Size = 26;
        public Spring Hov = new Spring(0) { K = 320, D = 24 }; public float Press, Pulse, Alpha = 1; public bool Round;
        /// <summary>Eigene Zeichnung des Inhalts (Canvas, Rechteck, Hover 0..1).</summary>
        public Func<Canvas2D, Box, float, bool> Custom;
        public Button() { }
        public Button(float x, float y, float w, float h, string t, Col col, Action a, float size = 26) { R = Gfx.R(x, y, w, h); Text = t; Col = col; Click = a; Size = size; }
        public bool Hit(float x, float y) => Visible && Enabled && R.Contains(x, y);
        public const float MinTouch = 104;
        public bool HitPad(float x, float y)
        {
            if (!Visible || !Enabled) return false;
            float px = Math.Max(0, (MinTouch - R.Width) / 2), py = Math.Max(0, (MinTouch - R.Height) / 2);
            return x >= R.Left - px && x <= R.Right + px && y >= R.Top - py && y <= R.Bottom + py;
        }
        public static Button Pick(IList<Button> items, float x, float y)
        {
            for (int i = items.Count - 1; i >= 0; i--) if (items[i].Hit(x, y)) return items[i];
            Button best = null; float bd = float.MaxValue;
            foreach (var b in items) if (b.HitPad(x, y)) { float d = (b.R.MidX - x) * (b.R.MidX - x) + (b.R.MidY - y) * (b.R.MidY - y); if (d < bd) { bd = d; best = b; } }
            return best;
        }
        public void Update(float dt, bool hot, bool down)
        {
            Hov.Target = hot && Enabled ? 1 : 0; Hov.Update(dt);
            Press = Ease.Lerp(Press, down && hot ? 1 : 0, Math.Min(1, dt * 30)); Pulse += dt;
        }
        public void Draw(Canvas2D c)
        {
            if (!Visible) return;
            if (Alpha < 1) c.SaveLayer(Alpha);
            float sc = 1 + Hov.V * .045f - Press * .06f; var col = Enabled ? Col : new Col(90, 85, 110);
            c.Save(); c.Translate(R.MidX, R.MidY); c.Scale(sc, sc); c.Translate(-R.MidX, -R.MidY);
            float rad = Round ? R.Height / 2 : Math.Min(16, R.Height / 2.2f);
            float pulse = Selected ? .5f + .5f * MathF.Sin(Pulse * 5) : 0;
            if (Enabled) Gfx.Glow(c, R, rad, col, 12 + Hov.V * 8, .35f + Hov.V * .35f + pulse * .3f);
            if (Round) Gfx.Ball(c, R.MidX, R.MidY, R.Height / 2, col.Dark(.8f + Hov.V * .15f + pulse * .05f), .95f, .3f - Hov.V * .08f);
            else
            {
                Gfx.RectGrad(c, R, rad, col.Dark(.42f + Hov.V * .15f + pulse * .1f), col.Dark(.16f + Hov.V * .08f));
                var hl = new Box(R.Left + 3, R.Top + 2, R.Right - 3, R.Top + R.Height * .5f);
                Gfx.RectGrad(c, hl, rad - 2, Col.White.A(.16f), Col.White.A(0));
            }
            var st = Gfx.Line(col.A(Enabled ? .7f + Hov.V * .3f + pulse * .3f : .5f), Selected ? 3.5f : 2); st.Glow = 1 + Hov.V * .5f; c.DrawRoundRect(R, rad, rad, st);
            if (Custom != null) Custom(c, R, Hov.V);
            else if (Sub == null) Gfx.Text(c, Text, R.MidX, R.MidY, Size, Enabled ? Col.White : C.Dim, Al.C, true, Enabled ? 3 * Hov.V : 0);
            else { Gfx.Text(c, Text, R.MidX, R.MidY - Size * .32f, Size, Col.White); Gfx.Text(c, Sub, R.MidX, R.MidY + Size * .72f, Size * .55f, col.Light(.4f), Al.C, false); }
            c.Restore();
            if (Alpha < 1) c.Restore();
        }
    }

    public class Ui
    {
        public readonly List<Button> Items = new List<Button>(); Button hot, down;
        public bool Hot => hot != null;
        public Button Add(Button b) { Items.Add(b); return b; }
        public void Remove(Button b) { Items.Remove(b); if (hot == b) hot = null; if (down == b) down = null; }
        public void Clear() { Items.Clear(); hot = down = null; }
        public bool Move(float x, float y)
        {
            Button h = Button.Pick(Items, x, y);
            if (h != hot && h != null) Sfx.Play(S.Hover, .25f);
            hot = h; return h != null;
        }
        public bool Down(float x, float y) { Move(x, y); down = hot; return down != null; }
        public bool Up(float x, float y)
        {
            Move(x, y); var d = down; down = null;
            if (d != null && d == hot) { Sfx.Play(S.Click, .6f); Haptics.Tap(); d.Click?.Invoke(); return true; }
            return false;
        }
        public void Update(float dt) { for (int i = 0; i < Items.Count; i++) { var b = Items[i]; b.Update(dt, b == hot, b == down); } }
        public void Draw(Canvas2D c) { foreach (var b in Items.ToList()) b.Draw(c); }
    }

    /// <summary>Dialogfenster (Ergebnis, Namenseingabe, Muenzwurf, Gegnerwahl ...).</summary>
    public class Modal
    {
        public string Title, Sub; public Col Col = C.Gold; public readonly List<Button> Btns = new List<Button>(); Button hot, down;
        public bool Input, Keep, AllowEmpty, Win; public string Text = ""; public int Max = 5; public Action<string> Submit; public Action Cancel;
        public float T; public float W = 760, H = 420; public float CY => Input ? 131 : 450;
        public List<string> Lines = new List<string>();
        public Action<Canvas2D, Box> Extra;
        // Bildschirmtastatur (Touch-Geraete): QWERTZ mit Umlauten, Umschalter, Loeschen und Leerzeichen
        readonly List<Button> keys = new List<Button>(), pickList = new List<Button>(); bool keysBuilt, shift = true; Button shiftKey, backKey, spaceKey;
        public bool Keyboard => Input && Platform.VirtualKeyboard;
        static readonly string[] KeyRows = { "1234567890-", "QWERTZUIOPÜ", "ASDFGHJKLÖÄ", "YXCVBNM_" };
        const float KeyW = 108, KeyH = 88, KeyGap = 8, KeyX0 = 166, KeyY0 = 312, KeyRowStep = 96;
        /// <summary>Wenn gesetzt: eigene Button-Anordnung statt einer Reihe.</summary>
        public Action<Modal> CustomLayout;
        public void Update(float dt)
        {
            T += dt; foreach (var b in Btns.ToList()) b.Update(dt, b == hot, b == down);
            if (Keyboard) { BuildKeys(); for (int i = 0; i < keys.Count; i++) keys[i].Update(dt, keys[i] == hot, keys[i] == down); }
        }

        void BuildKeys()
        {
            if (keysBuilt) return; keysBuilt = true;
            foreach (var row in KeyRows)
                foreach (char ch in row) { char c0 = ch; keys.Add(new Button { Col = char.IsLetter(c0) ? C.Blue : C.Purple, Size = 40, Text = c0.ToString(), Click = () => Type(c0) }); }
            shiftKey = new Button { Col = C.Gold, Click = () => shift = !shift };
            shiftKey.Custom = (c, r, h) =>
            {
                float x0 = r.MidX, y0 = r.MidY;
                using (var p = new Path2D())
                {
                    p.MoveTo(x0, y0 - 22); p.LineTo(x0 + 24, y0 + 2); p.LineTo(x0 + 10, y0 + 2); p.LineTo(x0 + 10, y0 + 20); p.LineTo(x0 - 10, y0 + 20); p.LineTo(x0 - 10, y0 + 2); p.LineTo(x0 - 24, y0 + 2); p.Close();
                    if (shift) c.DrawPath(p, Gfx.Fill(Col.White)); else c.DrawPath(p, Gfx.Line(Col.White, 3.5f));
                }
                return true;
            };
            backKey = new Button { Col = C.Red, Click = () => { Back(); if (Text.Length == 0) shift = true; } };
            backKey.Custom = (c, r, h) =>
            {
                float x0 = r.MidX, y0 = r.MidY;
                using (var p = new Path2D()) { p.MoveTo(x0 - 30, y0); p.LineTo(x0 - 12, y0 - 20); p.LineTo(x0 + 28, y0 - 20); p.LineTo(x0 + 28, y0 + 20); p.LineTo(x0 - 12, y0 + 20); p.Close(); c.DrawPath(p, Gfx.Line(Col.White, 3.5f)); }
                c.DrawLine(x0 - 6, y0 - 9, x0 + 14, y0 + 9, Gfx.Line(Col.White, 3.5f)); c.DrawLine(x0 + 14, y0 - 9, x0 - 6, y0 + 9, Gfx.Line(Col.White, 3.5f));
                return true;
            };
            spaceKey = new Button { Col = C.Dim, Text = "Leerzeichen", Size = 30, Click = () => { Char(' '); shift = true; } };
            keys.Add(shiftKey); keys.Add(backKey); keys.Add(spaceKey);
        }
        void Type(char c0)
        {
            char ch = Keep && !shift && char.IsLetter(c0) ? char.ToLowerInvariant(c0) : c0; int before = Text.Length;
            Char(ch); if (Text.Length > before && char.IsLetter(c0)) shift = false;
        }
        void LayoutKeys()
        {
            BuildKeys(); int i = 0;
            for (int r = 0; r < 3; r++) foreach (char ch in KeyRows[r]) { keys[i].R = Gfx.R(KeyX0 + (i - Offs(r)) * (KeyW + KeyGap), KeyY0 + r * KeyRowStep, KeyW, KeyH); i++; }
            float u = KeyW + KeyGap, y3 = KeyY0 + 3 * KeyRowStep, wide = 1.5f * u - KeyGap;
            shiftKey.R = Gfx.R(KeyX0, y3, wide, KeyH); backKey.R = Gfx.R(KeyX0 + wide + KeyGap + 8 * u, y3, 11 * u - KeyGap - wide - KeyGap - 8 * u, KeyH);
            for (int k = 0; k < KeyRows[3].Length; k++) { keys[i].R = Gfx.R(KeyX0 + wide + KeyGap + k * u, y3, KeyW, KeyH); i++; }
            spaceKey.R = Gfx.R(800 - 350, KeyY0 + 4 * KeyRowStep, 700, KeyH);
            bool up = !Keep || shift; i = 0;
            foreach (var row in KeyRows) foreach (char ch in row) { keys[i].Text = char.IsLetter(ch) && !up ? char.ToLowerInvariant(ch).ToString() : ch.ToString(); i++; }
            shiftKey.Selected = shift;
        }
        static int Offs(int row) { int n = 0; for (int r = 0; r < row; r++) n += KeyRows[r].Length; return n; }

        void Layout()
        {
            LayoutBtns(); if (Keyboard) LayoutKeys();
        }
        void LayoutBtns()
        {
            if (CustomLayout != null) { CustomLayout(this); return; }
            float n = Btns.Count, bw = Math.Min(260, (W - 60 - (n - 1) * 20) / Math.Max(1, n)), x0 = 800 - (n * bw + (n - 1) * 20) / 2, y = CY + H / 2 - (Input ? 78 : 100);
            for (int i = 0; i < Btns.Count; i++) Btns[i].R = Gfx.R(x0 + i * (bw + 20), y, bw, 64);
        }
        public void Draw(Canvas2D c)
        {
            Layout(); float a = Ease.OutCubic(T / .25f), sc = .8f + .2f * Ease.OutBack(T / .4f);
            c.DrawRect(-2000, -2000, 5600, 4900, Gfx.Fill(Col.Black.A(.62f * a)));
            var r = Gfx.Ctr(800, CY, W, H);
            if (Win) { Metal.Rays(c, 800, r.Top + 10, T, Metal.Gold, Col.Light(.3f), a * .45f, 1100, 20); Gfx.Light(c, 800, r.Top, 380, Metal.Gold, .16f * a, 1.3f); }
            c.Save(); c.Translate(800, CY); c.Scale(sc, sc); c.Translate(-800, -CY);
            Gfx.Shadow(c, r, 28, 24, .6f * a, 0, 16);
            Gfx.Glow(c, r, 28, Col, 26, .6f * a); Gfx.RectGrad(c, r, 28, new Col(38, 16, 72, 250), new Col(12, 4, 28, 252)); Gfx.Stroke(c, r, 28, Col.A(.9f), 3);
            Gfx.RectGrad(c, new Box(r.Left + 4, r.Top + 3, r.Right - 4, r.Top + 70), 24, Col.White.A(.07f), Col.White.A(0));
            if (Win) DrawWin(c, r, a);
            else Gfx.Text(c, Title, 800, r.Top + (Input ? 44 : 70), Input ? 44 : 54, Col, Al.C, true, 14, true);
            float y = r.Top + (Input ? 92 : 140);
            if (Sub != null) { Gfx.Text(c, Sub, 800, y, Input ? 26 : 28, Col.White, Al.C, false); y += Input ? 34 : 46; }
            foreach (var l in Lines) { Gfx.Text(c, l, 800, y, 24, C.Dim, Al.C, false); y += 36; }
            if (Input)
            {
                var ir = FieldRect; Gfx.Rect(c, ir, 14, Col.Black.A(.5f)); Gfx.Stroke(c, ir, 14, C.Cyan, 2.5f);
                string s = Text + (((int)(T * 2)) % 2 == 0 ? "|" : ""); Gfx.Text(c, s, 800, ir.MidY, 40, Col.White, Al.C, true, 6);
            }
            Extra?.Invoke(c, r);
            foreach (var b in Btns.ToList()) b.Draw(c);
            c.Restore();
            if (Keyboard)
            {
                c.SaveLayer(a);
                var kb = Gfx.R(KeyX0 - 28, KeyY0 - 16, 11 * (KeyW + KeyGap) - KeyGap + 56, 5 * KeyRowStep + 24); Gfx.Rect(c, kb, 26, Col.Black.A(.6f)); GlamourGames.W.Panel(c, kb, Col, 26);
                for (int i = 0; i < keys.Count; i++) keys[i].Draw(c);
                c.Restore();
            }
        }
        void DrawWin(Canvas2D c, Box r, float a)
        {
            float u = (T % 3.2f) / 1.4f;
            if (u <= 1) { float sx = r.Left - 120 + u * (r.Width + 240); c.Save(); c.ClipRect(new Box(sx - 90, r.Top - 10, sx + 90, r.Bottom + 10)); var sp = Gfx.Line(Col.White.A(.9f * a), 4); sp.Additive = true; sp.Glow = 2.4f; sp.Blur = 2; c.DrawRoundRect(r, 28, 28, sp); c.Restore(); }
            var gl = Gfx.Line(Metal.Gold.A(.85f * a), 2); gl.Glow = 1.5f; c.DrawRoundRect(Gfx.Inflate(r, -9), 21, 21, gl);
            var tb = Trophy3D.Rect(800, r.Top - 74, 210); float rise = (1 - Ease.OutBack(Ease.Clamp((T - .1f) / .6f))) * 60;
            Trophy3D.Draw(c, tb.Offset(0, rise), .5f + T * .8f, Ease.Clamp((T - .1f) / .3f));
            for (int i = 0; i < 4; i++) { float ph = (T * .9f + i * .27f) % 1; Metal.Glint(c, tb.Left + tb.Width * Metal.H(i * 7 + (int)(T * .9f + i * .27f)), tb.Top + tb.Height * (.1f + .5f * Metal.H(i * 13 + (int)(T * .9f + i * .27f))), 22 * MathF.Sin(ph * MathF.PI), Metal.GoldLight, MathF.Sin(ph * MathF.PI) * a); }
            c.Save(); c.Translate(800, r.Top + 70); Metal.Text(c, Title, 54, Col, 1, T > .6f ? ((T - .6f) % 3.2f) / 1.3f : -1); c.Restore();
        }
        public Box FieldRect => Gfx.Ctr(800, CY - H / 2 + 150, 380, 62);
        public bool AnyHover => Btns.Any(b => b.Hov.V > .3f) || (Keyboard && keys.Any(b => b.Hov.V > .3f));
        public void Move(float x, float y)
        {
            Layout();
            if (!Keyboard) { hot = Button.Pick(Btns, x, y); return; }
            pickList.Clear(); pickList.AddRange(Btns); pickList.AddRange(keys); hot = Button.Pick(pickList, x, y);
        }
        public void MDown(float x, float y) { Move(x, y); down = hot; }
        public void MUp(float x, float y) { Move(x, y); var d = down; down = null; if (d != null && d == hot) { Sfx.Play(S.Click, .6f); Haptics.Tap(); d.Click?.Invoke(); } }
        public void Char(char ch) { if (Input && Text.Length < Max && (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || (ch == ' ' && Text.Length > 0))) Text += Keep ? ch : char.ToUpperInvariant(ch); }
        public void Back() { if (Input && Text.Length > 0) Text = Text.Substring(0, Text.Length - 1); }
        public void Enter() { if (Input) Submit?.Invoke(Text.Trim().Length == 0 && !AllowEmpty ? "ANON" : Text.Trim()); else Btns.FirstOrDefault(b => b.Enabled)?.Click?.Invoke(); }
    }

    /// <summary>Wiederverwendbare Widgets.</summary>
    public static class W
    {
        public static void PlayerBox(Canvas2D c, Box r, string name, string big, Col col, bool active, float t, string sub = null)
        {
            float pulse = active ? .5f + .5f * MathF.Sin(t * 5) : 0;
            Gfx.Shadow(c, r, 20, 14, .35f, 0, 8);
            if (active) Gfx.Glow(c, r, 20, col, 18, .45f + .35f * pulse);
            Gfx.RectGrad(c, r, 20, col.Dark(active ? .38f : .16f), new Col(10, 3, 24));
            Gfx.RectGrad(c, new Box(r.Left + 3, r.Top + 3, r.Right - 3, r.Top + r.Height * .4f), 18, Col.White.A(active ? .1f : .05f), Col.White.A(0));
            Gfx.Stroke(c, r, 20, col.A(active ? 1 : .4f), active ? 3.5f : 2);
            Gfx.Text(c, name, r.MidX, r.Top + 30, 26, active ? Col.White : C.Dim, Al.C, true, active ? 5 : 0);
            Gfx.Text(c, big, r.MidX, r.MidY + 8, Math.Min(72, r.Height * .5f), col.Light(active ? .5f : .1f), Al.C, true, active ? 12 : 0);
            if (sub != null) Gfx.Text(c, sub, r.MidX, r.Bottom - 26, 20, C.Dim, Al.C, false);
        }
        public static void Panel(Canvas2D c, Box r, Col col, float rad = 22)
        {
            Gfx.Shadow(c, r, rad, 16, .35f, 0, 10);
            Gfx.RectGrad(c, r, rad, new Col(28, 12, 56, 235), new Col(10, 4, 24, 240)); Gfx.Stroke(c, r, rad, col.A(.6f), 2);
        }
    }
}
