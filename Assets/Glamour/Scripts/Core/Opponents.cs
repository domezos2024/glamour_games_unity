using System;

namespace GlamourGames
{
    /// <summary>Wer auf Platz 2 spielt: ein Mensch am selben PC oder der Computer in drei Staerken.</summary>
    public enum Opponent { Human = 0, Easy = 1, Medium = 2, Hard = 3 }

    /// <summary>Auswahl und Speicherung des Gegners pro Spiel sowie gemeinsame KI-Hilfen.</summary>
    public static class Opponents
    {
        public static string Label(Opponent o) => o switch { Opponent.Easy => "Leicht", Opponent.Medium => "Mittel", Opponent.Hard => "Schwer", _ => "2 Spieler" };
        public static string CpuName(Opponent o) => "Computer";
        public static string Describe(Opponent o) => o == Opponent.Human ? "Gegner: Mensch" : $"Gegner: Computer ({Label(o)})";
        public static Opponent Load(string key) => (Opponent)Math.Clamp(Save.Int("opp_" + key, 0), 0, 3);
        public static void Store(string key, Opponent o) => Save.Set("opp_" + key, (int)o);

        /// <summary>Typische Bedenkzeit des Computers in Sekunden (wirkt natuerlicher als sofortige Zuege).</summary>
        public static float ThinkTime(Opponent o) => o switch { Opponent.Easy => .55f, Opponent.Medium => .75f, _ => .9f } + (float)Rng.Shared.NextDouble() * .35f;

        /// <summary>Wahrscheinlichkeit, dass der Computer den besten Zug waehlt (Rest: plausibler Fehler).</summary>
        public static float Accuracy(Opponent o) => o switch { Opponent.Easy => .35f, Opponent.Medium => .75f, Opponent.Hard => 1f, _ => 1f };

        /// <summary>
        /// Zeigt den Dialog "Gegner waehlen". done wird mit der Auswahl aufgerufen; Escape fuehrt zurueck ins Menue.
        /// humanLabel z.B. "2 Spieler" oder "Solo" (bei Spielen, die allein spielbar sind).
        /// </summary>
        public static void Pick(Scene s, string key, Action<Opponent> done, string humanLabel = "2 Spieler", string humanSub = "an einem PC", string title = "Gegner wählen")
        {
            var last = Load(key);
            var m = new Modal { Title = title, Col = C.Cyan, W = 1180, H = 470, Sub = "Gegen wen möchtest du spielen?" };
            m.Lines.Add("Die Wahl wird gespeichert und lässt sich im Spiel jederzeit ändern.");
            m.Cancel = () => { s.Modal = null; App.Go(new Menu()); };
            void Add(Opponent o, string text, string sub, Col col)
            {
                var b = new Button { Text = text, Sub = sub, Col = col, Size = 30, Selected = o == last };
                b.Click = () => { Store(key, o); s.Opp = o; s.Modal = null; done(o); };
                m.Btns.Add(b);
            }
            Add(Opponent.Human, humanLabel, humanSub, C.Cyan);
            Add(Opponent.Easy, "Computer", "Leicht", C.Green);
            Add(Opponent.Medium, "Computer", "Mittel", C.Gold);
            Add(Opponent.Hard, "Computer", "Schwer", C.Red);
            m.CustomLayout = mm =>
            {
                float bw = 250, gap = 22, x0 = 800 - (4 * bw + 3 * gap) / 2, y = mm.CY + mm.H / 2 - 150;
                for (int i = 0; i < mm.Btns.Count; i++) mm.Btns[i].R = Gfx.R(x0 + i * (bw + gap), y, bw, 112);
            };
            m.Extra = (c, r) =>
            {
                // kleine Symbole ueber den Buttons
                float bw = 250, gap = 22, x0 = 800 - (4 * bw + 3 * gap) / 2, y = m.CY + m.H / 2 - 150;
                for (int i = 0; i < 4; i++)
                {
                    float cx = x0 + i * (bw + gap) + bw / 2, cy = y - 34;
                    if (i == 0) { Person(c, cx - 18, cy, C.Cyan); Person(c, cx + 18, cy, C.Pink); }
                    else Chip(c, cx, cy, i == 1 ? C.Green : i == 2 ? C.Gold : C.Red, i, m.T);
                }
            };
            s.Modal = m;
        }

        static void Person(Canvas2D c, float x, float y, Col col)
        {
            Gfx.Ball(c, x, y - 10, 9, col); var p = Gfx.Fill(col.Dark(.8f)); c.DrawRoundRect(Gfx.Ctr(x, y + 10, 26, 18), 9, 9, p);
        }
        static void Chip(Canvas2D c, float x, float y, Col col, int lvl, float t)
        {
            var r = Gfx.Ctr(x, y, 40, 40); Gfx.Glow(c, r, 8, col, 8, .5f + .3f * MathF.Sin(t * 4 + lvl));
            Gfx.RectGrad(c, r, 8, col.Dark(.5f), col.Dark(.2f)); Gfx.Stroke(c, r, 8, col, 2);
            for (int k = -1; k <= 1; k++) { c.DrawLine(x + k * 10, y - 26, x + k * 10, y - 20, Gfx.Line(col, 3)); c.DrawLine(x + k * 10, y + 20, x + k * 10, y + 26, Gfx.Line(col, 3)); }
            for (int k = 0; k < lvl; k++) Gfx.Star(c, x - (lvl - 1) * 7 + k * 14, y, 6, Col.White);
        }

        /// <summary>Fuegt einen Button "Gegner: ..." hinzu, der die Auswahl erneut oeffnet und dann restart aufruft.</summary>
        public static Button AddSwitch(Scene s, string key, float x, float y, float w, float h, Action restart, string humanLabel = "2 Spieler", string humanSub = "an einem PC")
        {
            Button b = null;
            b = s.Ui.Add(new Button(x, y, w, h, "", C.Cyan, () => Pick(s, key, o => { s.Opp = o; restart(); }, humanLabel, humanSub), 20));
            b.Custom = (c, r, hv) =>
            {
                Gfx.Text(c, "GEGNER", r.MidX, r.Top + 18, 15, C.Dim, Al.C, true);
                Gfx.Text(c, s.Opp == Opponent.Human ? humanLabel : "Computer · " + Label(s.Opp), r.MidX, r.MidY + 9, 22, Col.White, Al.C, true, 3 * hv);
                return true;
            };
            return b;
        }
    }
}
