using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    /// <summary>
    /// Fassade der Mehrspieler-Verbindung (2 bis 4 Spieler per Bluetooth) fuer Szenen und Menues. Die eigentliche Arbeit macht
    /// <see cref="Session"/> (Host ist Autoritaet, Sitze, Intents/Deltas/Snapshots, Wiederverbindung) ueber den Transport <see cref="T"/>.
    /// Spielnachrichten werden der laufenden Szene zugestellt, sobald sie aktiv ist:
    ///  - Altspiele (2 Spieler, Scene.Seated == false): Net()/NetRecv() als einfaches Relay (Host: Delta, Gast: Intent).
    ///  - Sitzspiele (Scene.Seated == true): Act() -> Host NetIntent() -> Emit() -> NetDelta() bei allen; Snapshots fuer Nachzuegler.
    /// </summary>
    public static class Link
    {
        public static IBtTransport T { get => t; set { if (t == value) return; t = value; Sess = null; } }
        static IBtTransport t;
        public static Session Sess { get; private set; }
        public static bool IsHost => Sess != null && Sess.IsHost;
        public static string PeerName { get { if (Sess == null || !Sess.Active) return "Mitspieler"; return Sess.SeatName(Sess.MySeat == 0 ? 1 : 0); } }
        /// <summary>Eigener Name im Bluetooth-Spiel: der in den Optionen gesetzte Name, sonst der Bluetooth-Name des Geraets.</summary>
        public static string MyName { get { var s = Save.Str("pname0", "").Trim(); if (s.Length > 0) return s; var n = T?.LocalName; return string.IsNullOrWhiteSpace(n) ? Pl.Name(0) : n.Trim(); } }
        /// <summary>Mindestens zwei Spieler sitzen in der Sitzung (Altspiele: Gegenueber vorhanden).</summary>
        public static bool Connected => Sess != null && Sess.Active && Sess.State != RoomState.Discovering && Sess.Count >= 2 && Sess.MySeat >= 0;
        public static bool Busy => Sess != null && Sess.Active;
        public static bool InGame => Sess != null && Sess.InGame;
        public static bool Frozen => Sess != null && Sess.Frozen;
        public static int MySeat => Sess?.MySeat ?? 0;
        public static int Count => Sess != null && Sess.Active ? Sess.Count : 1;
        public static string Status => T == null ? "Bluetooth wird auf diesem Gerät nicht unterstützt" : Sess != null && Sess.Active && Sess.LastError.Length == 0 ? StateText() : Sess != null && Sess.LastError.Length > 0 ? Sess.LastError : T.Status;
        public static string Name(int seat) => Sess == null ? "Spieler " + (seat + 1) : Sess.SeatName(seat);
        /// <summary>Gemeinsamer Startwert fuer Zufall (vom Host festgelegt), z. B. Kartenmischen.</summary>
        public static int Seed => Sess?.Seed ?? 0;
        /// <summary>Szenenwechsel kam vom Host (Menue/Abbruch): die Szene meldet dann kein "verlassen".</summary>
        public static bool HostDriven;

        static string StateText()
        {
            switch (Sess.State)
            {
                case RoomState.Discovering: return "verbindet ...";
                case RoomState.Lobby: return Sess.IsHost ? "wartet auf Mitspieler" : "verbunden";
                case RoomState.Starting: return "Start ...";
                case RoomState.Paused: case RoomState.Reconnecting: return "Verbindung wird wiederhergestellt ...";
                default: return T.Status;
            }
        }

        struct Item { public int Kind, Seat; public string Game, Msg; public string[] Data; public bool Priv; }   // Kind: 0 Delta, 1 Intent, 2 Snapshot
        static readonly List<Item> queue = new List<Item>();
        const int MaxQueue = 4000;

        public static void Init()
        {
            if (T == null)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                T = new AndroidBt();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                T = new WinBt();
#endif
                if (T != null && !T.Supported) Log.I("bluetooth: kein Adapter");
            }
            if (T != null && Sess == null) Build();
        }

        static void Build()
        {
            var cfg = new SessionConfig { Dataset = Session.DatasetHash(Registry.All.Select(g => g.Id)) };
            Sess = new Session(T, cfg);
            Sess.Log = m => Log.I("net: " + m);
            Sess.OnNotice = m => App.Toast(m);
            Sess.KnowsGame = id => Registry.ById(id) != null;
            Sess.OnGameStart = OnStart;
            Sess.OnDelta = (g, k, d, p) => Enqueue(new Item { Kind = 0, Game = g, Msg = k, Data = d, Priv = p });
            Sess.OnIntent = (seat, g, k, d) => Enqueue(new Item { Kind = 1, Seat = seat, Game = g, Msg = k, Data = d });
            Sess.OnSnapshot = (g, blob) => Enqueue(new Item { Kind = 2, Game = g, Msg = "snapshot", Data = new[] { blob } });
            Sess.SnapshotProvider = seat => App.Current != null && App.Current.Seated ? App.Current.NetSnapshot(seat) ?? "" : "";
            Sess.OnAborted = OnAborted;
            Sess.OnState = (o, n) => { if (n == RoomState.Idle && o != RoomState.Idle) queue.Clear(); };
        }

        static void Enqueue(Item it) { if (queue.Count < MaxQueue) queue.Add(it); }

        static void OnStart(GameStart gs)
        {
            queue.Clear();
            if (gs.GameId == "menu") { HostDriven = true; if (!(App.Current is Menu)) App.Go(new Menu()); return; }
            var gi = Registry.ById(gs.GameId); if (gi == null) return;
            HostDriven = false; Sfx.Play(S.Match, .6f);
            App.Go(gi.Make());
        }

        static void OnAborted(NetError code, string text)
        {
            queue.Clear(); HostDriven = true;
            var cur = App.Current;
            if (cur != null && !(cur is Menu) && !(cur is BtLobby)) { cur.NetLost(); if (App.Current == cur) App.Go(new Menu()); }
        }

        public static void Host() { Init(); if (T == null || !T.EnsurePermission()) return; queue.Clear(); Sess.Host(MyName); Log.I("bluetooth: eroeffnet"); }
        public static void Join(string addr) { Init(); if (T == null || !T.EnsurePermission()) return; queue.Clear(); Sess.Join(MyName, addr); Log.I("bluetooth: tritt bei " + addr); }
        public static void Stop() { if (Sess == null) return; Sess.Stop(); queue.Clear(); Log.I("bluetooth: getrennt"); }
        public static void SetReady(bool r) => Sess?.SetReady(r);
        public static void Kick(int seat) => Sess?.Kick(seat);

        /// <summary>Host: Spiel starten (alle Sitze muessen bereit sein). Die Szene wechselt bei allen Geraeten gemeinsam, wenn der Start bestaetigt ist.</summary>
        public static bool Start(GameInfo g) => Sess != null && Sess.IsHost && Sess.RequestStart(g.Id, "", g.MinSeats, g.MaxSeats);

        /// <summary>Altspiel-Nachricht an das Gegenueber.</summary>
        public static void Game(string key, string kind, params object[] data)
        {
            if (Sess == null || !Sess.InGame) return; var d = Strs(data);
            if (Sess.IsHost) Sess.Emit(key, kind, d); else Sess.Intent(key, kind, d);
            Log.I($"bt> {key} {kind} {string.Join(",", d)}");
        }
        /// <summary>Sitzspiel: Eingabeabsicht (Gast an den Host; Host direkt an NetIntent).</summary>
        public static void Intent(string key, string kind, params object[] data) { if (Sess != null) Sess.Intent(key, kind, Strs(data)); }
        /// <summary>Sitzspiel, nur Host: Zustandsaenderung an alle (inklusive lokalem NetDelta).</summary>
        public static void Emit(string key, string kind, params object[] data) { if (Sess != null && Sess.IsHost) Sess.Emit(key, kind, Strs(data)); }
        /// <summary>Sitzspiel, nur Host: privates Delta nur fuer einen Sitz.</summary>
        public static void EmitTo(int seat, string key, string kind, params object[] data) { if (Sess != null && Sess.IsHost) Sess.EmitTo(seat, key, kind, Strs(data)); }
        static string[] Strs(object[] o) { var a = new string[o.Length]; for (int i = 0; i < a.Length; i++) a[i] = o[i] is float f ? F(f) : o[i]?.ToString() ?? ""; return a; }

        /// <summary>Host verlaesst das Spiel in Richtung Menue (alle folgen); Gast meldet das Verlassen der Partie.</summary>
        public static void BackToMenu() { if (Sess != null && Sess.IsHost) Sess.ToMenu(); }
        public static void LeaveGame() { if (Sess != null && !Sess.IsHost) Sess.LeaveGame(); }

        /// <summary>Jeden Frame: Transport/Sitzung pumpen und Spielnachrichten der aktiven Szene zustellen.</summary>
        public static void Update()
        {
            if (Sess == null) return;
            Sess.Update();
            var cur = App.Current; if (cur == null || queue.Count == 0 || cur.OppKey == null) return;
            for (int i = 0; i < queue.Count; i++)
            {
                var it = queue[i]; if (it.Game != cur.OppKey) continue;
                queue.RemoveAt(i--);
                try
                {
                    if (cur.Seated)
                    {
                        Log.I($"net< {(it.Kind == 0 ? "delta" : it.Kind == 1 ? "intent" : "snapshot")} {it.Game} {it.Msg} {string.Join(",", it.Data)}");
                        if (it.Kind == 0) cur.NetDelta(it.Msg, it.Data, it.Priv);
                        else if (it.Kind == 1) cur.NetIntent(it.Seat, it.Msg, it.Data);
                        else cur.NetApplySnapshot(it.Data.Length > 0 ? it.Data[0] : "");
                    }
                    else if (it.Kind == 0 && !Sess.IsHost || it.Kind == 1) { Log.I($"bt< {it.Game} {it.Msg} {string.Join(",", it.Data)}"); cur.NetRecv(it.Msg, it.Data); }
                }
                catch (Exception e) { Log.I("bt: Fehler in " + it.Msg + ": " + e.Message); }
            }
        }

        /// <summary>Overlay bei Verbindungsverlust: Spiel steht, Hinweis mit Restzeit des Wiederverbindungsfensters.</summary>
        public static void DrawFrozen(Canvas2D c, float time)
        {
            if (Sess == null) return;
            Gfx.RectGrad(c, new Box(0, 0, 1600, 900), 0, new Col(0, 0, 0, 170), new Col(0, 0, 0, 170));
            string who = ""; double left = 0;
            foreach (var s in Sess.Seats) if (s.Presence == Presence.Reconnecting) { who = who.Length == 0 ? s.Name : who + ", " + s.Name; left = Math.Max(left, Sess.ReconnectLeft(s.Index)); }
            if (who.Length == 0) { who = Sess.IsHost ? "Mitspieler" : "Host"; left = Sess.ReconnectLeft(Sess.MySeat); }
            float k = .5f + .5f * MathF.Sin(time * 5);
            Gfx.Text(c, "Verbindung unterbrochen", 800, 380, 54, Col.White, Al.C, true);
            Gfx.Text(c, $"Warte auf {who} ... noch {(int)Math.Ceiling(left)} s", 800, 450, 30, C.Gold.A(.6f + .4f * k), Al.C, false);
            Gfx.Text(c, "Das Spiel läuft weiter, sobald die Verbindung wieder steht.", 800, 500, 22, C.Dim, Al.C, false);
        }

        /// <summary>Schmaler Netzstatus je Spieler am oberen Rand (Farbe + Text), verdeckt keine Spielaktion.</summary>
        public static void DrawStatus(Canvas2D c)
        {
            if (Sess == null || !Sess.InGame || Sess.Count < 3) return; float x = 300;
            foreach (var s in Sess.Seats)
            {
                if (!s.Taken) continue; bool ok = s.Presence == Presence.Connected; var col = ok ? C.Green : C.Orange;
                string n = s.Name.Length > 7 ? s.Name.Substring(0, 6) + "." : s.Name;
                c.DrawRoundRect(Gfx.R(x, 3, 118, 20), 8, 8, Gfx.Fill(col.A(.3f)));
                Gfx.Text(c, n + (ok ? " · online" : " · wartet"), x + 59, 13, 14, Col.White, Al.C, false); x += 122;
            }
        }

        public static int Int(string s) => int.TryParse(s, out int v) ? v : 0;
        public static float Flt(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0;
        public static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
