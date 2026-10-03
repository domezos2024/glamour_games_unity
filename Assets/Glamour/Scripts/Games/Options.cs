using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    public class Options : Scene
    {
        public override string Title => "Optionen";
        public override Col Acc1 => C.Cyan; public override Col Acc2 => C.Purple;
        static readonly Box MusTrack = Gfx.R(620, 300, 600, 16), SfxTrack = Gfx.R(620, 452, 600, 16);
        Button bPrev, bNext, bMusOn, bSfxOn, bHap, bName0, bName1;
        int drag = -1;
        public override void Enter()
        {
            base.Enter();
            bPrev = Ui.Add(new Button(300, 195, 96, 76, "<", C.Cyan, () => Step(-1), 40));
            bNext = Ui.Add(new Button(1140, 195, 96, 76, ">", C.Cyan, () => Step(1), 40));
            bMusOn = Ui.Add(new Button(1300, 195, 260, 76, "", C.Green, () => { Sfx.SetMusicOff(!Sfx.MusicOff); }, 28));
            bSfxOn = Ui.Add(new Button(1300, 350, 260, 76, "", C.Green, () => { Sfx.SetSfxOff(!Sfx.SfxOff); Sfx.Play(S.Coin); }, 28));
            bName0 = Ui.Add(new Button(300, 556, 500, 84, "", C.Cyan, () => Edit(0), 32));
            bName1 = Ui.Add(new Button(830, 556, 500, 84, "", C.Pink, () => Edit(1), 32));
            bHap = Ui.Add(new Button(300, 722, 460, 84, "", C.Green, App.ToggleFullscreen, 28));
            Ui.Add(new Button(1000, 722, 330, 84, "Fertig", C.Gold, () => App.Go(new Menu()), 34));
        }
        void Step(int d)
        {
            int n = Sfx.TrackNames.Length, t = Sfx.Track + d; if (t < 0) t = n - 1; if (t >= n) t = 0;
            Sfx.SetTrack(t); if (Sfx.MusicOff) Sfx.SetMusicOff(false);
        }
        void Edit(int i)
        {
            var m = new Modal { Title = $"Name für Spieler {i + 1}", Sub = "Gib den Namen ein (max. 10 Zeichen)", Col = i == 0 ? C.Cyan : C.Pink, Input = true, H = 262, Max = 10, Keep = true, AllowEmpty = true, Text = Pl.Name(i) == $"Spieler {i + 1}" ? "" : Pl.Name(i) };
            m.Submit = n => { Pl.SetName(i, n); Modal = null; Sfx.Play(S.Coin); };
            m.Cancel = () => Modal = null;
            m.Btns.Add(new Button { Text = "Speichern", Col = C.Green, Click = () => m.Submit(m.Text.Trim()) });
            m.Btns.Add(new Button { Text = "Abbrechen", Col = C.Dim, Click = () => m.Cancel() });
            Modal = m;
        }
        public override bool Escape() { App.Go(new Menu()); return true; }
        float Val(Box tr, float x) => Ease.Clamp((x - tr.Left) / tr.Width);
        public override void MouseDown(float x, float y)
        {
            if (Gfx.Inflate(MusTrack, 44).Contains(x, y)) { drag = 0; Set(0, x); }
            else if (Gfx.Inflate(SfxTrack, 44).Contains(x, y)) { drag = 1; Set(1, x); }
        }
        public override void MouseMove(float x, float y) { if (drag >= 0) Set(drag, x); }
        public override void MouseUp(float x, float y) { if (drag == 1) Sfx.Play(S.Coin); drag = -1; }
        void Set(int which, float x)
        {
            if (which == 0) Sfx.SetMusicVol(Val(MusTrack, x)); else Sfx.SetSfxVol(Val(SfxTrack, x));
        }
        public override void Update(float dt)
        {
            bMusOn.Text = Sfx.MusicOff ? "Musik: AUS" : "Musik: AN"; bMusOn.Col = Sfx.MusicOff ? C.Red : C.Green;
            bSfxOn.Text = Sfx.SfxOff ? "Effekte: AUS" : "Effekte: AN"; bSfxOn.Col = Sfx.SfxOff ? C.Red : C.Green;
            bHap.Text = App.Fullscreen ? "Vollbild: AN" : "Vollbild: AUS"; bHap.Col = App.Fullscreen ? C.Green : C.Red;
            bName0.Text = "1:  " + Pl.Name(0); bName1.Text = "2:  " + Pl.Name(1);
        }
        void Slider(Canvas2D c, Box tr, float v, Col col, string label)
        {
            Gfx.Text(c, label, 300, tr.MidY, 28, C.Dim, Al.L, false);
            Gfx.Rect(c, tr, 8, Col.White.A(.15f)); Gfx.Rect(c, Gfx.R(tr.Left, tr.Top, tr.Width * v, tr.Height), 8, col);
            float kx = tr.Left + tr.Width * v; Gfx.Radial(c, kx, tr.MidY, 44, col, .5f); c.DrawCircle(kx, tr.MidY, 26, Gfx.Fill(col)); c.DrawCircle(kx, tr.MidY, 26, Gfx.Line(Col.White, 3.5f));
            Gfx.Text(c, $"{(int)Math.Round(v * 100)}%", tr.Right + 60, tr.MidY, 26, Col.White, Al.C, true);
        }
        public override void Draw(Canvas2D c)
        {
            Gfx.Text(c, "MUSIK", 300, 168, 30, C.Cyan, Al.L, true, 6, true);
            Gfx.Text(c, Sfx.Track < 0 ? "-" : Sfx.TrackNames[Sfx.Track], 768, 233, 40, Sfx.MusicOff ? C.Dim : Col.White, Al.C, true, Sfx.MusicOff ? 0 : 6);
            Slider(c, MusTrack, Sfx.MusicVol, C.Cyan, "Lautstärke");
            Gfx.Text(c, "EFFEKTE", 300, 388, 30, C.Gold, Al.L, true, 6, true);
            Slider(c, SfxTrack, Sfx.SfxVol, C.Gold, "Lautstärke");
            Gfx.Text(c, "SPIELERNAMEN  (zum Ändern anklicken)", 300, 524, 30, C.Pink, Al.L, true, 6, true);
            Gfx.Text(c, "ANZEIGE  (auch mit F11)", 300, 696, 30, C.Green, Al.L, true, 6, true);
            Gfx.Text(c, $"Glamour Games v{App.Version}  -  {App.Credit}", 800, 106, 20, C.Dim, Al.C, false);
        }
    }
}
