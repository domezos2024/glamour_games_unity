using System;

namespace GlamourGames
{
    /// <summary>Animierter Muenzwurf: Spieler 1 waehlt Kopf oder Zahl, der Gewinner beginnt. done(0|1) = Startspieler.</summary>
    public static class CoinToss
    {
        public static void Start(Scene s, Action<int> done)
        {
            int choice = -1, res = 0, first = 0; float t0 = 0; bool fin = false, moving = false; float ang = 0, lift = 0;
            var m = new Modal { Title = "Münzwurf", Col = C.Gold, W = 860, H = 640 };
            m.Sub = $"{s.PName(0)} wählt: Kopf oder Zahl";
            m.Lines.Add("Gewinnt der Wurf, beginnt das Spiel.");
            m.Cancel = () => { s.Modal = null; App.Go(new Menu()); };
            Action<int> pick = c =>
            {
                if (choice >= 0) return; choice = c; res = Rng.Shared.Next(2); t0 = m.T; moving = true; m.Btns.Clear(); m.Sub = $"{s.PName(0)} hat {(c == 0 ? "KOPF" : "ZAHL")} gewählt ..."; m.Lines.Clear(); Sfx.Play(S.Chip); Haptics.Toss();
            };
            m.Btns.Add(new Button { Text = "KOPF", Col = C.Gold, Click = () => pick(0), Size = 30 });
            m.Btns.Add(new Button { Text = "ZAHL", Col = C.Gold, Click = () => pick(1), Size = 30 });
            m.Extra = (c, r) =>
            {
                if (moving && !fin)
                {
                    float p = Ease.Clamp((m.T - t0) / 2.4f); ang = Ease.OutCubic(p) * MathF.PI * (10 + (res == 0 ? 0 : 1)); lift = MathF.Sin(p * MathF.PI) * 220;
                    if ((int)(p * 22) != (int)((p - .016f) * 22) && p < .9f) Sfx.Play(S.Tick, .15f, 1.4f);
                    if (p >= 1)
                    {
                        fin = true; moving = false; ang = MathF.PI * (10 + (res == 0 ? 0 : 1)); lift = 0; first = res == choice ? 0 : 1; Sfx.Play(S.Coin); Haptics.Win();
                        s.Fx.Burst(800, r.MidY + 40, 40, new[] { C.Gold, C.Yellow, Col.White }, 380, 2, 300);
                        m.Sub = $"{(res == 0 ? "KOPF" : "ZAHL")}!  {s.PName(first)} beginnt!"; m.Col = first == 0 ? C.Cyan : C.Pink;
                        m.Btns.Add(new Button { Text = "Los geht's", Col = C.Green, Click = () => { s.Modal = null; done(first); }, Size = 30 });
                    }
                }
                Gfx.Light(c, 800, r.MidY + 20, 210, C.Gold, .16f + .06f * MathF.Sin(m.T * 2), 1.3f);
                Coin3D.Draw(c, 800, r.MidY + 40, 95, ang, lift);
            };
            s.Modal = m;
        }
    }
}
