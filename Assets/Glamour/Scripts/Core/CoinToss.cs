using System;

namespace GlamourGames
{
    /// <summary>
    /// Muenzwurf mit Wurfphysik: senkrechter Wurf unter Schwerkraft mit konstanter Drehrate (Drehimpulserhaltung in der
    /// Luft), Aufprall mit Restitution und kleiner werdenden Huepfern, danach gedaempftes Nachwippen auf dem Tisch.
    /// Spieler 1 waehlt Kopf oder Zahl, der Gewinner beginnt. done(0|1) = Startspieler.
    /// </summary>
    public static class CoinToss
    {
        public static void Start(Scene s, Action<int> done)
        {
            if (s.Remote) { s.NetToss(done); return; }
            Run(s, done, -1);
        }
        /// <summary>Muenzwurf mit vorgegebenem Ergebnis (Bluetooth: vom Host ausgelost). first = Startspieler aus Sicht dieses Geraets.</summary>
        public static void Fixed(Scene s, int first, Action<int> done) => Run(s, done, first);

        static void Run(Scene s, Action<int> done, int forced)
        {
            if (Opponents.Skip > 1) { done(Math.Max(0, forced)); return; }
            int choice = -1, res = 0, first = 0; float t0 = 0, target = 0; bool fin = false, moving = false; float ang = 0, lift = 0; int hops = 0;
            const float Flight = 1.05f, Peak = 240, Gv = 8 * Peak / (Flight * Flight), V0 = Gv * Flight / 2, Rest = .35f, Wob = 1.6f;
            // Hoehe ueber dem Tisch zur Zeit t (geschlossen geloest, unabhaengig von der Bildrate); hop = Anzahl Aufpralle bis t
            float Lift(float t, out int hop)
            {
                hop = 0; float v = V0;
                while (v >= 60) { float d = 2 * v / Gv; if (t < d) return v * t - .5f * Gv * t * t; t -= d; hop++; v *= Rest; }
                hop++; return 0;
            }
            var m = new Modal { Title = "Münzwurf", Col = C.Gold, W = 860, H = 640 };
            m.Sub = $"{s.PName(0)} wählt: Kopf oder Zahl";
            m.Lines.Add("Gewinnt der Wurf, beginnt das Spiel.");
            m.Cancel = () => { s.Modal = null; App.Go(new Menu()); };
            Action<int> pick = c =>
            {
                if (choice >= 0) return; choice = c; res = forced < 0 ? Rng.Shared.Next(2) : forced == 0 ? c : 1 - c; t0 = m.T; moving = true; target = MathF.PI * (10 + res); lift = 0; ang = 0; hops = 0; m.Btns.Clear(); m.Sub = $"{s.PName(0)} hat {(c == 0 ? "KOPF" : "ZAHL")} gewählt ..."; m.Lines.Clear(); Sfx.Play(S.Chip); Haptics.Toss();
            };
            if (forced < 0)
            {
                m.Btns.Add(new Button { Text = "KOPF", Col = C.Gold, Click = () => pick(0), Size = 30 });
                m.Btns.Add(new Button { Text = "ZAHL", Col = C.Gold, Click = () => pick(1), Size = 30 });
            }
            else { m.Cancel = null; s.Tm.After(.5f, () => pick(0)); }
            m.Extra = (c, r) =>
            {
                if (moving && !fin)
                {
                    // in der Luft konstante Drehrate (Drehimpulserhaltung), nach dem ersten Aufprall flach mit gedaempftem Nachwippen
                    float t = m.T - t0, prevA = ang; lift = Lift(t, out int hop);
                    ang = t < Flight ? target * t / Flight : target + Phys.Ring(Wob, t - Flight, 2.6f, .22f);
                    if (t < Flight && (int)(ang / MathF.PI) != (int)(prevA / MathF.PI)) Sfx.Play(S.Tick, .15f, 1.4f);
                    if (hop > hops) { Impact.Play(Mat.Metal, MathF.Pow(Rest, hops)); hops = hop; }
                    if (t > Flight + 1.1f)
                    {
                        fin = true; moving = false; ang = target; lift = 0; first = res == choice ? 0 : 1; Haptics.Win();
                        s.Fx.Burst(800, r.MidY + 40, 40, new[] { C.Gold, C.Yellow, Col.White }, 380, 2, 300);
                        m.Sub = $"{(res == 0 ? "KOPF" : "ZAHL")}!  {s.PName(first)} beginnt!"; m.Col = first == 0 ? C.Cyan : C.Pink;
                        bool closed = false; void Close() { if (closed) return; closed = true; if (s.Modal == m) s.Modal = null; done(first); }
                        m.Btns.Add(new Button { Text = "Los geht's", Col = C.Green, Click = Close, Size = 30 });
                        if (forced >= 0) s.Tm.After(1.6f, Close);   // Bluetooth: beide Geraete starten gleichzeitig
                    }
                }
                Gfx.Light(c, 800, r.MidY + 20, 210, C.Gold, .16f + .06f * MathF.Sin(m.T * 2), 1.3f);
                Coin3D.Draw(c, 800, r.MidY + 40, 95, ang, lift);
            };
            s.Modal = m;
        }
    }
}
