using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    /// <summary>
    /// Bluetooth-Lobby fuer 2 bis 4 Spieler: ein Geraet eroeffnet (Host), bis zu drei weitere treten einem gekoppelten Geraet bei
    /// (PC und Handy beliebig gemischt). Teilnehmerkarten zeigen Name, Farbe, Bereit-Zustand und Verbindungsstatus; der Host kann
    /// Mitspieler entfernen und waehlt im Menue das Spiel, sobald alle bereit sind. Der Start erfolgt fuer alle gemeinsam.
    /// </summary>
    public class BtLobby : Scene
    {
        public override string Title => "Bluetooth-Mehrspieler";
        public override Col Acc1 => C.Blue; public override Col Acc2 => C.Cyan;
        static readonly Col[] SeatCol = { C.Cyan, C.Pink, C.Gold, C.Green };
        List<BtDev> paired = new List<BtDev>(); readonly List<Button> devBtns = new List<Button>(); readonly Button[] kick = new Button[4];
        Button bHost, bStop, bRefresh, bMenu, bReady; float refreshT;
        const float CardX = 130, CardY = 262, CardW = 540, CardH = 92, CardStep = 102;

        public override void Enter()
        {
            base.Enter(); Link.Init();
            bHost = Ui.Add(new Button(140, 250, 520, 96, "Spiel eröffnen", C.Green, () => { Link.Host(); Sfx.Play(S.Click); }, 34));
            bStop = Ui.Add(new Button(140, 676, 250, 70, "Verlassen", C.Red, () => { Link.Stop(); Sfx.Play(S.Take); }, 26));
            bReady = Ui.Add(new Button(410, 676, 250, 70, "Bereit", C.Green, () => { var me = Link.Sess?.Seats.ElementAtOrDefault(Link.MySeat); if (me != null) { Link.SetReady(!me.Ready); Sfx.Play(S.Click); } }, 26));
            bMenu = Ui.Add(new Button(410, 676, 250, 70, "Spiel auswählen", C.Cyan, () => App.Go(new Menu()), 26));
            bRefresh = Ui.Add(new Button(940, 250, 520, 62, "Geräteliste aktualisieren", C.Purple, () => { Link.T?.Scan(); Refresh(); }, 24));
            for (int i = 1; i < 4; i++) { int seat = i; kick[i] = Ui.Add(new Button(CardX + CardW - 96, CardY + i * CardStep + 24, 84, 44, "Raus", C.Red, () => { Link.Kick(seat); Sfx.Play(S.Take); }, 18)); }
            Link.T?.Scan(); Refresh();
        }
        void Refresh()
        {
            // nur PCs/Handys (keine Kopfhoerer o. Ae.); Geraete mit Glamour-Dienst zuerst und gruen, dann PCs, dann Handys
            paired = (Link.T?.Paired() ?? new List<BtDev>()).Where(d => d.Player)
                .OrderByDescending(d => d.Glamour).ThenBy(d => d.Major == 0x100 ? 0 : d.Major == 0x200 ? 1 : 2).ThenBy(d => d.Name).ToList();
            foreach (var b in devBtns) Ui.Remove(b); devBtns.Clear();
            for (int i = 0; i < Math.Min(7, paired.Count); i++)
            {
                var d = paired[i]; string label = $"{d.Name}  ({d.Kind} · {d.Short})";
                devBtns.Add(Ui.Add(new Button(940, 330 + i * 74, 520, 62, label, d.Glamour ? C.Green : C.Blue, () => { Link.Join(d.Addr); Sfx.Play(S.Click); }, 22)));
            }
            refreshT = 0;
        }
        public override void Update(float dt)
        {
            refreshT += dt; if (refreshT > 4 && !Link.Busy) Refresh();   // nach Erteilen der Berechtigung / neu gekoppelten Geraeten
            var s = Link.Sess; bool busy = Link.Busy, inLobby = busy && s.State != RoomState.Discovering;
            bHost.Visible = !busy; bStop.Visible = busy; bRefresh.Visible = !busy;
            foreach (var b in devBtns) b.Visible = !busy;
            bReady.Visible = inLobby && !s.IsHost && s.State == RoomState.Lobby;
            if (bReady.Visible) { var me = s.Seats.ElementAtOrDefault(s.MySeat); bool r = me != null && me.Ready; bReady.Text = r ? "Nicht bereit" : "Bereit"; bReady.Col = r ? C.Orange : C.Green; }
            bMenu.Visible = inLobby && s.IsHost && s.State == RoomState.Lobby;
            for (int i = 1; i < 4; i++) kick[i].Visible = inLobby && s.IsHost && s.State == RoomState.Lobby && i < s.Seats.Count && s.Seats[i].Taken;
        }
        static string Err(Session s) { if (s == null) return ""; if (s.LastError.Length > 0) return s.LastError; return ""; }
        public override void Draw(Canvas2D c)
        {
            var left = Gfx.R(110, 200, 580, 570); var right = Gfx.R(910, 200, 580, 570);
            W.Panel(c, left, C.Green); W.Panel(c, right, C.Blue);
            var s = Link.Sess; bool busy = Link.Busy;
            Gfx.Text(c, busy ? $"Sitzung  ·  {s.Count}/4 Spieler" : "Eröffnen", left.MidX, left.Top + 24, 22, C.Green.Light(.4f), Al.C, true);
            Gfx.Text(c, busy && s.IsHost ? "Gekoppelte Geräte können beitreten" : "Beitreten (gekoppelte Geräte)", right.MidX, right.Top + 24, 22, C.Blue.Light(.5f), Al.C, true);

            if (busy && s.State != RoomState.Discovering) for (int i = 0; i < 4; i++) Card(c, s, i);
            else if (busy) { float k = .5f + .5f * MathF.Sin(Time * 5); Gfx.Light(c, left.MidX, 420, 140, C.Cyan, .22f * k, 1.5f); Gfx.Text(c, "verbindet ...", left.MidX, 420, 30, C.Cyan, Al.C, true, 6); }

            string st = busy ? Link.Status : (Err(s).Length > 0 ? Err(s) : Link.Status);
            bool bad = !busy && (Err(s).Length > 0 || st.StartsWith("Fehler") || st.StartsWith("Verbindung fehl") || st.Contains("nicht") || st.Contains("fehlt") || st.Contains("aus"));
            if (!busy) Gfx.Text(c, st, left.MidX, 420, 24, bad ? C.Red : C.Cyan, Al.C, true, 6);
            if (!busy && st.StartsWith("Verbindung fehl")) Gfx.Text(c, "Läuft auf dem anderen Gerät \"Spiel eröffnen\"? Richtiges Gerät gewählt?", left.MidX, 470, 19, C.Dim, Al.C, false);
            if (busy && s.State == RoomState.Lobby) Gfx.Text(c, Hint(s), left.MidX, 760 - 6, 18, C.Dim, Al.C, false);
            if (paired.Count == 0 && !busy) Gfx.Text(c, "Keine gekoppelten PCs oder Handys gefunden", right.MidX, 400, 22, C.Dim, Al.C, false);
            else if (!busy) Gfx.Text(c, paired.Any(d => d.Glamour) ? "Grün = bietet Glamour Games an" : "Tippen = beitreten", right.MidX, right.Bottom - 26, 18, C.Dim, Al.C, false);
            if (busy && s.IsHost) { Gfx.Text(c, "Andere Geräte wählen dieses Gerät:", right.MidX, 420, 24, C.Dim, Al.C, false); Gfx.Text(c, Link.MyName, right.MidX, 470, 34, C.Cyan, Al.C, true); }
            if (busy && !s.IsHost) Gfx.Text(c, "Verbunden mit " + s.HostName, right.MidX, 440, 26, C.Cyan, Al.C, true);
            if (!busy) Gfx.Text(c, "Dieses Gerät: " + Link.MyName, left.MidX, left.Bottom - 26, 18, C.Dim, Al.C, false);
            string[] help =
            {
                "1. Alle Geräte in den Bluetooth-Einstellungen mit dem Eröffner koppeln (PC und Handy beliebig, bis zu 4 Spieler).",
                "2. Ein Gerät \"Spiel eröffnen\", die anderen tippen dessen Namen an und drücken \"Bereit\".",
                "3. Der Eröffner wählt im Menü das Spiel - jeder spielt an seinem Gerät.",
            };
            for (int i = 0; i < help.Length; i++) Gfx.Text(c, help[i], 800, 806 + i * 26, 19, C.Dim, Al.C, false);
        }
        static string Hint(Session s)
        {
            if (!s.IsHost) return s.Seats.ElementAtOrDefault(s.MySeat)?.Ready == true ? "Du bist bereit - warte auf den Start." : "Drücke \"Bereit\", wenn du spielen willst.";
            if (s.Count < 2) return "Warte auf mindestens einen Mitspieler.";
            if (!s.AllReady) return "Warte, bis alle bereit und verbunden sind.";
            return "Alle bereit - wähle ein Spiel.";
        }
        void Card(Canvas2D c, Session s, int i)
        {
            float y = CardY + i * CardStep; var r = new Box(CardX, y, CardX + CardW, y + CardH);
            var seat = i < s.Seats.Count ? s.Seats[i] : null;
            if (seat == null || !seat.Taken)
            {
                Gfx.Stroke(c, r, 18, C.Dim.A(.35f), 2); Gfx.Text(c, "Platz frei", r.MidX, r.MidY + 2, 22, C.Dim.A(.7f), Al.C, false); return;
            }
            var col = SeatCol[i % 4]; bool lost = seat.Presence == Presence.Reconnecting;
            Gfx.RectGrad(c, r, 18, col.Dark(.55f).A(.9f), new Col(12, 5, 28)); Gfx.Stroke(c, r, 18, col.A(lost ? .35f : .9f), 2.5f);
            Gfx.Glow(c, new Box(r.Left + 18, r.MidY - 16, r.Left + 50, r.MidY + 16), 16, col, 10, lost ? .2f : .7f);
            Gfx.Text(c, seat.Name, r.Left + 70, r.Top + 30, 26, Col.White, Al.L, true);
            string role = (i == 0 ? "Host" : "Gast") + (i == s.MySeat ? "  ·  du" : "");
            Gfx.Text(c, role, r.Left + 70, r.Top + 64, 18, col.Light(.5f), Al.L, false);
            // Verbindungsstatus: drei Balken (verbunden) bzw. Text (Wiederverbindung)
            float bx = r.Right - 290;
            if (lost) { Gfx.Text(c, $"Wiederverbindung {(int)Math.Ceiling(s.ReconnectLeft(i))} s", bx + 40, r.Top + 30, 18, C.Orange, Al.L, true); }
            else
            {
                for (int b = 0; b < 3; b++) Gfx.RectGrad(c, new Box(bx + b * 10, r.Top + 38 - b * 6, bx + b * 10 + 7, r.Top + 46), 2, C.Green, C.Green);
                Gfx.Text(c, "verbunden", bx + 40, r.Top + 30, 18, C.Green.Light(.3f), Al.L, false);
            }
            bool ready = seat.Ready || i == 0;
            Gfx.Text(c, lost ? "WARTET" : ready ? "BEREIT" : "NICHT BEREIT", bx + 40, r.Top + 64, 18, lost ? C.Dim : ready ? C.Gold : C.Dim, Al.L, true);
        }
    }
}
