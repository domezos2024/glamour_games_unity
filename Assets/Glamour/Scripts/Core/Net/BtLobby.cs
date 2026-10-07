using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    /// <summary>
    /// Bluetooth-Verbindung herstellen: ein Geraet eroeffnet, das andere tritt einem gekoppelten Geraet bei (PC und Handy
    /// beliebig gemischt). Nach dem Verbinden waehlt der Eroeffner im Menue das Spiel, der Mitspieler folgt automatisch.
    /// </summary>
    public class BtLobby : Scene
    {
        public override string Title => "Bluetooth-Mehrspieler";
        public override Col Acc1 => C.Blue; public override Col Acc2 => C.Cyan;
        List<(string name, string addr)> paired = new List<(string, string)>(); readonly List<Button> devBtns = new List<Button>();
        Button bHost, bStop, bRefresh, bMenu; float refreshT;

        public override void Enter()
        {
            base.Enter(); Link.Init();
            bHost = Ui.Add(new Button(140, 250, 520, 96, "Spiel eröffnen", C.Green, () => { Link.Host(); Sfx.Play(S.Click); }, 34));
            bStop = Ui.Add(new Button(140, 370, 520, 70, "Trennen / Abbrechen", C.Red, () => { Link.Stop(); Sfx.Play(S.Take); }, 26));
            bRefresh = Ui.Add(new Button(940, 250, 520, 62, "Geräteliste aktualisieren", C.Purple, Refresh, 24));
            bMenu = Ui.Add(new Button(140, 470, 520, 80, "Spiel auswählen", C.Cyan, () => App.Go(new Menu()), 30));
            Refresh();
        }
        void Refresh()
        {
            paired = Link.T?.Paired() ?? new List<(string, string)>();
            foreach (var b in devBtns) Ui.Remove(b); devBtns.Clear();
            for (int i = 0; i < Math.Min(7, paired.Count); i++)
            {
                var d = paired[i];
                devBtns.Add(Ui.Add(new Button(940, 330 + i * 74, 520, 62, "Beitreten: " + d.name, C.Blue, () => { Link.Join(d.addr); Sfx.Play(S.Click); }, 22)));
            }
            refreshT = 0;
        }
        public override void Update(float dt)
        {
            refreshT += dt; if (refreshT > 4 && !Link.Busy) Refresh();   // nach Erteilen der Berechtigung / neu gekoppelten Geraeten
            bool busy = Link.Busy, con = Link.Connected;
            bHost.Visible = !busy; bStop.Visible = busy; bMenu.Visible = con; bMenu.Text = Link.IsHost ? "Spiel auswählen" : "Zum Menü";
            foreach (var b in devBtns) b.Visible = !busy;
            bRefresh.Visible = !busy;
        }
        public override void Draw(Canvas2D c)
        {
            var left = Gfx.R(110, 200, 580, 600); var right = Gfx.R(910, 200, 580, 600);
            W.Panel(c, left, C.Green); W.Panel(c, right, C.Blue);
            Gfx.Text(c, "Eröffnen", left.MidX, left.Top + 24, 22, C.Green.Light(.4f), Al.C, true);
            Gfx.Text(c, "Beitreten (gekoppelte Geräte)", right.MidX, right.Top + 24, 22, C.Blue.Light(.5f), Al.C, true);
            string st = Link.Connected ? $"Verbunden mit {Link.PeerName}" : Link.Status;
            var col = Link.Connected ? C.Green : st.StartsWith("Fehler") || st.StartsWith("Verbindung fehl") || st.Contains("nicht") || st.Contains("fehlt") || st.Contains("aus") ? C.Red : C.Cyan;
            if (Link.Busy && !Link.Connected) { float k = .5f + .5f * MathF.Sin(Time * 5); Gfx.Light(c, left.MidX, 600, 120, C.Cyan, .2f * k, 1.5f); }
            Gfx.Text(c, st, left.MidX, 620, 26, col, Al.C, true, 6);
            if (Link.Connected) Gfx.Text(c, Link.IsHost ? "Du wählst das Spiel - der Mitspieler folgt automatisch." : $"{Link.PeerName} wählt das Spiel ...", left.MidX, 664, 20, C.Dim, Al.C, false);
            if (paired.Count == 0 && !Link.Busy) Gfx.Text(c, "Keine gekoppelten Geräte gefunden", right.MidX, 400, 22, C.Dim, Al.C, false);
            string[] help =
            {
                "1. Beide Geräte in den Bluetooth-Einstellungen koppeln (PC und Handy beliebig).",
                "2. Auf einem Gerät \"Spiel eröffnen\", auf dem anderen \"Beitreten\" wählen.",
                "3. Der Eröffner wählt im Menü das Spiel - jeder spielt an seinem Gerät.",
            };
            for (int i = 0; i < help.Length; i++) Gfx.Text(c, help[i], 800, 830 + i * 26 - 40, 19, C.Dim, Al.C, false);
        }
    }
}
