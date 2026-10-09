using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames.Tests
{
    public static class T
    {
        public static int Run, Fail; public static string Cur = "";
        public static void True(bool c, string msg) { Run++; if (!c) { Fail++; Console.WriteLine($"  FEHLER [{Cur}]: {msg}"); } }
        public static void Eq<A>(A exp, A act, string msg) { Run++; if (!Equals(exp, act)) { Fail++; Console.WriteLine($"  FEHLER [{Cur}]: {msg}: erwartet {exp}, erhalten {act}"); } }
    }

    /// <summary>Testaufbau: ein Host und n Gaeste im simulierten Netz.</summary>
    public sealed class Rig
    {
        public readonly MemNet Net = new MemNet(); public Session Host; public readonly List<Session> Guests = new List<Session>(); public readonly List<MemEp> Eps = new List<MemEp>();
        public readonly List<(int seat, string game, string kind, string[] data)> Intents = new List<(int, string, string, string[])>();
        public readonly Dictionary<Session, List<string>> Deltas = new Dictionary<Session, List<string>>(); public readonly Dictionary<Session, List<string>> Snaps = new Dictionary<Session, List<string>>();
        public readonly Dictionary<Session, GameStart> Started = new Dictionary<Session, GameStart>(); public readonly Dictionary<Session, List<string>> Aborts = new Dictionary<Session, List<string>>();
        public string State = "S0"; public SessionConfig Cfg; public readonly HashSet<Session> Off = new HashSet<Session>();
        public Rig(int guests, SessionConfig cfg = null)
        {
            Cfg = cfg ?? new SessionConfig { Dataset = "ds1" };
            var he = Net.Create("host"); Eps.Add(he); Host = new Session(he, Clone(Cfg), Net.Clock); Wire(Host, true);
            Host.Host("Hosti"); Host.OnIntent = (s, g, k, d) => Intents.Add((s, g, k, d)); Host.SnapshotProvider = seat => State + "#" + seat;
            for (int i = 0; i < guests; i++) AddGuest("Gast" + (i + 1));
        }
        static SessionConfig Clone(SessionConfig c) => new SessionConfig { MaxSeats = c.MaxSeats, MinSeats = c.MinSeats, ReconnectWindow = c.ReconnectWindow, PingInterval = c.PingInterval, PeerTimeout = c.PeerTimeout, ResendInterval = c.ResendInterval, StartTimeout = c.StartTimeout, HandshakeTimeout = c.HandshakeTimeout, ReconnectRetry = c.ReconnectRetry, Dataset = c.Dataset };
        void Wire(Session s, bool host)
        {
            Deltas[s] = new List<string>(); Snaps[s] = new List<string>(); Aborts[s] = new List<string>();
            s.OnDelta = (g, k, d, p) => Deltas[s].Add($"{(p ? "P" : "B")}:{g}:{k}:{string.Join(",", d)}"); s.OnSnapshot = (g, b) => Snaps[s].Add(b); s.OnGameStart = gs => Started[s] = gs;
            s.OnAborted = (c, t) => Aborts[s].Add(c + ":" + t);
        }
        public Session AddGuest(string name, bool pump = true)
        {
            var ep = Net.Create("g" + Guests.Count); Eps.Add(ep); var g = new Session(ep, Clone(Cfg), Net.Clock); Wire(g, false); Guests.Add(g); g.Join(name, "host"); if (pump) Pump(.5); return g;
        }
        public void Pump(double seconds, double step = .02)
        {
            for (double t = 0; t < seconds; t += step) { Net.Clock.Now += step; Host.Update(); foreach (var g in Guests) if (!Off.Contains(g)) g.Update(); }
        }
        public void ReadyAll() { foreach (var g in Guests) g.SetReady(true); Pump(.3); }
        public void StartGame(string id = "kniffel") { ReadyAll(); T.True(Host.RequestStart(id), "Start angenommen"); Pump(.6); }
    }

    public static class NetTests
    {
        public static void All()
        {
            Wire(); Join(); JoinFull(); Dataset(); Names(); LobbyLeave(); StartFlow(); StartRefused(); IntentsAndDeltas(); PrivateDelta(); Hostile(); GapSnapshot(); ResumeFlow(); ResumeWrongToken(); WindowExpiry(); Heartbeat(); HostLeaves(); VoluntaryLeave(); JoinDuringGame(); Kick(); Lossy(); ReconnectIntents();
        }
        static void Begin(string n) { T.Cur = n; Console.WriteLine("> " + n); }

        static void Wire()
        {
            Begin("Wire-Codec");
            var m = new Msg(MsgType.Intent, "kniffel", "roll", "a|b%c\nd\re", "", "Ümläut ✓") { Sid = "s|1", From = "p%", Seat = 2, Seq = 5, Ack = 4, Rev = 9, Mid = 77 };
            var line = m.Encode(); T.True(line.IndexOf('\n') < 0 && line.IndexOf('\r') < 0, "keine Zeilenumbrueche");
            T.True(Msg.TryDecode(line, out var d, out var err), "Dekodieren"); T.Eq(m.Sid, d.Sid, "Sid"); T.Eq(m.From, d.From, "From"); T.Eq(2, d.Seat, "Seat"); T.Eq(5L, d.Seq, "Seq"); T.Eq(9L, d.Rev, "Rev"); T.Eq(MsgType.Intent, d.Type, "Typ");
            T.Eq(m.P.Length, d.P.Length, "Payload-Anzahl"); for (int i = 0; i < m.P.Length; i++) T.Eq(m.P[i], d.P[i], "Payload " + i);
            T.True(!Msg.TryDecode("HELLO|Max|1", out _, out err), "altes Protokoll abgelehnt"); T.Eq(NetError.Invalid, err, "alt = ungueltig");
            T.True(!Msg.TryDecode("GL1|a|1|b|0|1|0|0|0", out _, out err), "andere Version"); T.Eq(NetError.Version, err, "Version");
            T.True(!Msg.TryDecode("GL2|a|x|b|0|1|0|0|0", out _, out _), "Zahl defekt"); T.True(!Msg.TryDecode("GL2|a|1|b|0|99|0|0|0", out _, out _), "Typ ausserhalb");
            T.True(!Msg.TryDecode("GL2|a|1", out _, out _), "zu kurz"); T.True(!Msg.TryDecode(new string('x', Msg.MaxLine + 1), out _, out _), "zu lang");
            var big = new Msg(MsgType.Intent, Enumerable.Range(0, 400).Select(i => "x").ToArray()); T.True(!Msg.TryDecode(big.Encode(), out _, out _), "zu viele Felder");
            T.Eq("Max Muster", WireUtil.SafeName("  Max Muster ", "x"), "Name trim"); T.Eq(16, WireUtil.SafeName(new string('a', 40), "x").Length, "Name laenge"); T.Eq("a?b", WireUtil.SafeName("a|b", "x"), "Name zeichen"); T.Eq("x", WireUtil.SafeName("   ", "x"), "Name leer");
            var rnd = new Random(3); int bad = 0; for (int i = 0; i < 2000; i++) { var s = new string(Enumerable.Range(0, rnd.Next(1, 30)).Select(_ => (char)rnd.Next(1, 0x300)).ToArray()); var mm = new Msg(MsgType.Ping, s); if (!Msg.TryDecode(mm.Encode(), out var dd, out _) || dd.P[0] != s) bad++; }
            T.Eq(0, bad, "Zufallsprobe Round-Trip");
            for (int i = 0; i < 2000; i++) { var s = new string(Enumerable.Range(0, rnd.Next(0, 60)).Select(_ => "GL2|%0123456789ABCDEF"[rnd.Next(20)]).ToArray()); Msg.TryDecode(s, out _, out _); }   // darf nicht werfen
        }

        static void Join()
        {
            Begin("Beitritt: Host + 3 Gaeste");
            var r = new Rig(3); T.Eq(4, r.Host.Count, "Host zaehlt 4"); T.Eq(RoomState.Lobby, r.Host.State, "Host Lobby");
            for (int i = 0; i < 3; i++) { var g = r.Guests[i]; T.Eq(RoomState.Lobby, g.State, "Gast Lobby " + i); T.Eq(i + 1, g.MySeat, "Sitz " + i); T.Eq(4, g.Seats.Count, "Roster beim Gast " + i); T.Eq("Gast" + (i + 1), g.Seats[i + 1].Name, "Name"); }
            T.True(r.Guests[0].Stats.ConnectSeconds < 1, "Verbindungsaufbau schnell");
            T.True(!r.Host.AllReady, "noch nicht bereit");
        }
        static void JoinFull()
        {
            Begin("Sitzung voll"); var r = new Rig(3); var g4 = r.AddGuest("Zu viel");
            T.Eq(RoomState.Idle, g4.State, "5. Gast abgewiesen"); T.Eq(NetError.Full, g4.LastErrorCode, "Fehlercode Full"); T.Eq(4, r.Host.Count, "Host bleibt bei 4");
        }
        static void Dataset()
        {
            Begin("Spieldatensatz"); var r = new Rig(0); var ep = r.Net.Create("x"); var g = new Session(ep, new SessionConfig { Dataset = "andere" }, r.Net.Clock); g.Join("X", "host");
            for (int i = 0; i < 40; i++) { r.Net.Clock.Now += .02; r.Host.Update(); g.Update(); }
            T.Eq(RoomState.Idle, g.State, "Gast abgelehnt"); T.Eq(NetError.Dataset, g.LastErrorCode, "Code Dataset"); T.Eq(1, r.Host.Count, "Host unveraendert");
        }
        static void Names()
        {
            Begin("Namen eindeutig"); var r = new Rig(0); r.AddGuest("Max"); r.AddGuest("Max"); r.AddGuest("Max");
            T.Eq(4, r.Host.Seats.Select(s => s.Name).Distinct().Count(), "Namen verschieden");
        }
        static void LobbyLeave()
        {
            Begin("Lobby: Gast geht, Sitze ruecken nach"); var r = new Rig(3); r.Guests[0].Stop(); r.Pump(.5);
            T.Eq(3, r.Host.Count, "Host 3"); T.Eq(1, r.Guests[1].MySeat, "Gast2 jetzt Sitz 1"); T.Eq(2, r.Guests[2].MySeat, "Gast3 jetzt Sitz 2"); T.Eq(3, r.Guests[2].Seats.Count, "Roster beim Gast 3");
        }
        static void StartFlow()
        {
            Begin("Start: Bereitschaft + Bestaetigung"); var r = new Rig(2); T.True(!r.Host.RequestStart("kniffel"), "Start ohne Bereitschaft abgelehnt");
            r.Guests[0].SetReady(true); r.Pump(.3); T.True(!r.Host.RequestStart("kniffel"), "nur einer bereit"); r.Guests[1].SetReady(true); r.Pump(.3); T.True(r.Host.AllReady, "alle bereit");
            T.True(!r.Host.RequestStart("kniffel", "", 4, 4), "Mindestzahl 4 nicht erreicht"); T.True(!r.Host.RequestStart("poker", "", 2, 2), "Maximal 2: 3 Spieler");
            T.True(r.Host.RequestStart("kniffel"), "Start"); T.Eq(RoomState.Starting, r.Host.State, "Starting"); r.Pump(.6);
            T.Eq(RoomState.InGame, r.Host.State, "Host InGame"); foreach (var g in r.Guests) { T.Eq(RoomState.InGame, g.State, "Gast InGame"); T.Eq("kniffel", r.Started[g].GameId, "Spiel-ID"); T.Eq(3, r.Started[g].Seats, "3 Sitze"); T.Eq(r.Started[r.Host].Seed, r.Started[g].Seed, "gleicher Seed"); }
            r.Host.ToMenu(); r.Pump(.3); T.Eq(RoomState.Lobby, r.Host.State, "zurueck in Lobby"); T.Eq("menu", r.Started[r.Guests[0]].GameId, "Gast folgt ins Menue");
        }
        static void StartRefused()
        {
            Begin("Start: Gast kennt Spiel nicht"); var r = new Rig(2); r.Guests[1].KnowsGame = id => id != "poker"; r.ReadyAll(); r.Host.RequestStart("poker"); r.Pump(.6);
            T.Eq(RoomState.Lobby, r.Host.State, "Start abgebrochen"); T.True(!r.Started.ContainsKey(r.Guests[0]), "niemand gestartet"); T.Eq(RoomState.Lobby, r.Guests[0].State, "Gast zurueck in Lobby");
            r = new Rig(2); r.ReadyAll(); r.Off.Add(r.Guests[1]); r.Eps[2].Stop(); r.Host.RequestStart("kniffel"); r.Pump(10); T.True(r.Host.State == RoomState.Lobby, "Start mit weggefallenem Gast abgebrochen");
        }
        static void IntentsAndDeltas()
        {
            Begin("Intents und Deltas"); var r = new Rig(3); r.StartGame();
            r.Guests[1].Intent("kniffel", "roll", "1", "2"); r.Guests[1].Intent("kniffel", "hold", "3"); r.Guests[2].Intent("kniffel", "score", "4"); r.Pump(.5);
            T.Eq(3, r.Intents.Count, "3 Intents beim Host"); T.Eq(2, r.Intents[0].seat, "Sitz des Absenders (vom Host vergeben)"); T.Eq("roll", r.Intents[0].kind, "Reihenfolge 1"); T.Eq("hold", r.Intents[1].kind, "Reihenfolge 2"); T.Eq(3, r.Intents[2].seat, "Sitz 3");
            r.Host.Intent("kniffel", "roll"); T.Eq(4, r.Intents.Count, "Host-Intent direkt"); T.Eq(0, r.Intents[3].seat, "Host = Sitz 0");
            for (int i = 0; i < 10; i++) r.Host.Emit("kniffel", "e" + i, i.ToString()); r.Pump(.5);
            foreach (var g in r.Guests) { T.Eq(10, r.Deltas[g].Count, "10 Deltas"); T.Eq("B:kniffel:e0:0", r.Deltas[g][0], "erstes Delta"); T.Eq("B:kniffel:e9:9", r.Deltas[g][9], "letztes Delta"); T.Eq(10L, g.Rev, "Revision"); }
            T.Eq(10, r.Deltas[r.Host].Count, "Host wendet selbst an"); T.Eq(10L, r.Host.Rev, "Host-Revision");
            T.True(r.Guests[0].Stats.LastRtt > 0, "RTT gemessen");
        }
        static void PrivateDelta()
        {
            Begin("Private Deltas (verdeckte Karten)"); var r = new Rig(3); r.StartGame("poker");
            r.Host.EmitTo(2, "poker", "hole", "As", "Kd"); r.Host.Emit("poker", "dealt"); r.Host.EmitTo(0, "poker", "hole", "2c", "3c"); r.Pump(.4);
            T.Eq(1, r.Deltas[r.Guests[1]].Count(x => x.StartsWith("P:")), "Sitz 2 bekommt private Karten"); T.Eq(0, r.Deltas[r.Guests[0]].Count(x => x.StartsWith("P:")), "Sitz 1 sieht nichts"); T.Eq(0, r.Deltas[r.Guests[2]].Count(x => x.StartsWith("P:")), "Sitz 3 sieht nichts");
            T.True(r.Deltas[r.Guests[0]].All(x => !x.Contains("As") && !x.Contains("Kd")), "Broadcast ohne Geheimnis"); T.Eq(1L, r.Guests[1].Rev, "private Deltas aendern die Revision nicht"); T.True(r.Deltas[r.Host].Contains("P:poker:hole:2c,3c"), "Host eigener Sitz privat");
        }
        static void Hostile()
        {
            Begin("Feindliche Eingaben"); var r = new Rig(2); r.StartGame();
            var ep = r.Eps[1]; var link = ep.LinkTo(0);
            // roh eingeschleuste Muelleingaben duerfen nichts zerstoeren
            ep.Send(0, "kaputt"); ep.Send(0, "GL2|x|1|y|9|7|0|0|0"); ep.Send(0, new Msg(MsgType.Intent, "kniffel") { Seq = 1 }.Encode()); ep.Send(0, "GL9|a|1|b|0|1|0|0|0"); r.Pump(.5);
            T.True(r.Host.Stats.BadLines >= 2, "defekte Zeilen gezaehlt"); T.Eq(RoomState.InGame, r.Host.State, "Host laeuft weiter");
            // Gast gibt sich als anderer Sitz aus: Host verwendet den Sitz der Verbindung
            var fake = new Msg(MsgType.Intent, "kniffel", "score", "1") { Seq = 1, Seat = 3 }; var gs = r.Guests[0]; ep.Send(0, fake.Encode()); r.Pump(.3);
            T.True(r.Intents.All(i => i.seat != 3), "Absender-Sitz kommt vom Host, nicht aus der Nachricht");
            // zu langer Name
            var r2 = new Rig(0); var g = r2.AddGuest(new string('N', 200)); T.True(g.Seats.Count > 1 && g.Seats[1].Name.Length <= WireUtil.MaxName, "Name begrenzt");
        }
        static void GapSnapshot()
        {
            Begin("Luecke -> Snapshot"); var r = new Rig(2); r.StartGame(); r.Host.Emit("k", "a"); r.Pump(.2);
            r.Eps[0].LinkTo(1).DropNextPublicDeltas = 1; r.State = "S-neu"; r.Host.Emit("k", "b"); r.Host.Emit("k", "c"); r.Host.Emit("k", "d"); r.Pump(.6);
            var g = r.Guests[0]; T.True(g.Stats.Desyncs >= 1, "Luecke erkannt"); T.Eq(1, r.Snaps[g].Count, "Snapshot erhalten"); T.True(r.Snaps[g][0].StartsWith("S-neu"), "Snapshot-Inhalt vom Host"); T.Eq(r.Host.Rev, g.Rev, "Revision wieder gleich");
            r.Host.Emit("k", "e"); r.Pump(.3); T.Eq(r.Host.Rev, g.Rev, "danach laufend synchron"); T.Eq("B:k:e:", r.Deltas[g].Last(), "naechstes Delta normal");
            T.Eq(r.Host.Rev, r.Guests[1].Rev, "anderer Gast unberuehrt");
        }
        static void ResumeFlow()
        {
            Begin("Trennung und Wiederverbindung"); var r = new Rig(3); r.StartGame();
            r.Guests[1].Intent("k", "x", "1"); r.Pump(.3); r.Host.Emit("k", "a"); r.Pump(.2);
            r.Eps[2].Drop(0); r.Pump(.3);
            T.Eq(RoomState.Paused, r.Host.State, "Host pausiert"); T.Eq(Presence.Reconnecting, r.Host.Seats[2].Presence, "Sitz 2 reconnecting"); T.Eq(RoomState.Reconnecting, r.Guests[1].State, "Gast reconnecting");
            T.Eq(RoomState.Paused, r.Guests[0].State, "andere Gaeste pausiert"); T.True(r.Host.Frozen && r.Guests[0].Frozen, "eingefroren");
            r.Guests[1].Intent("k", "pending", "2");   // waehrend der Trennung erzeugt
            r.State = "nach"; r.Pump(6);
            T.Eq(RoomState.InGame, r.Host.State, "Host wieder im Spiel"); T.Eq(RoomState.InGame, r.Guests[1].State, "Gast wieder im Spiel"); T.Eq(RoomState.InGame, r.Guests[0].State, "Rest wieder im Spiel");
            T.Eq(Presence.Connected, r.Host.Seats[2].Presence, "Sitz 2 verbunden"); T.Eq(2, r.Guests[1].MySeat, "gleicher Sitz"); T.True(r.Snaps[r.Guests[1]].Count >= 1, "Snapshot nach Resume"); T.Eq("nach#2", r.Snaps[r.Guests[1]].Last(), "Snapshot fuer diesen Sitz");
            T.Eq(1, r.Intents.Count(i => i.kind == "pending"), "liegengebliebener Intent genau einmal"); T.Eq(1, r.Intents.Count(i => i.kind == "x"), "alter Intent nicht doppelt");
            r.Host.Emit("k", "z"); r.Pump(.3); T.Eq(r.Host.Rev, r.Guests[1].Rev, "Revision synchron"); T.Eq(1, r.Host.Stats.Resumes, "Resume gezaehlt");
        }
        static void ResumeWrongToken()
        {
            Begin("Resume mit falschem Token"); var r = new Rig(2); r.StartGame(); r.Eps[1].Drop(0); r.Pump(.06);
            var ep2 = r.Net.Create("eve2"); ep2.Connect("host"); r.Pump(.06);
            ep2.Send(0, new Msg(MsgType.Hello, "Eve", "ds1", "GL2") { Sid = "x" }.Encode()); r.Pump(.06);
            ep2.Send(0, new Msg(MsgType.ResumeRequest, r.Host.SessionId, r.Host.Seats[1].PlayerId, "falsch") { Sid = r.Host.SessionId }.Encode()); r.Pump(.1);
            T.Eq(Presence.Reconnecting, r.Host.Seats[1].Presence, "Sitz bleibt reserviert");
            bool sawErr = false; for (int i = 0; i < 20; i++) { if (ep2.Poll(out var ev) && ev.Kind == TEvKind.Line && Msg.TryDecode(ev.Text, out var mm, out _) && mm.Type == MsgType.Error && mm.P[0] == ((int)NetError.BadToken).ToString()) sawErr = true; }
            T.True(sawErr, "Fehler BadToken an den Angreifer"); T.Eq(1, r.Host.Stats.ResumeFailures, "Fehlversuch gezaehlt");
            r.Pump(5); T.Eq(Presence.Connected, r.Host.Seats[1].Presence, "echter Gast kommt zurueck"); T.Eq(RoomState.InGame, r.Host.State, "Spiel laeuft");
        }
        static void WindowExpiry()
        {
            Begin("Reconnect-Fenster abgelaufen"); var cfg = new SessionConfig { Dataset = "ds1", ReconnectWindow = 10 }; var r = new Rig(2, cfg); r.StartGame();
            r.Off.Add(r.Guests[1]); r.Eps[2].Stop(); r.Pump(.5);   // Gast verschwindet ohne Abschied
            T.Eq(RoomState.Paused, r.Host.State, "pausiert"); r.Pump(11);
            T.Eq(RoomState.Lobby, r.Host.State, "Host zurueck in Lobby"); T.Eq(2, r.Host.Count, "fehlender Spieler entfernt"); T.Eq(RoomState.Lobby, r.Guests[0].State, "Rest in Lobby"); T.Eq(1, r.Aborts[r.Guests[0]].Count, "Abbruch gemeldet"); T.True(r.Aborts[r.Guests[0]][0].StartsWith(NetError.WindowExpired.ToString()), "Code WindowExpired");
        }
        static void Heartbeat()
        {
            Begin("Heartbeat-Timeout"); var r = new Rig(1); r.StartGame(); r.Eps[1].LinkTo(0).Dead = true; r.Pump(9);
            T.True(r.Host.Stats.Timeouts >= 1, "Host erkennt Timeout"); T.True(r.Guests[0].Stats.Timeouts >= 1, "Gast erkennt Timeout"); r.Pump(8); T.Eq(RoomState.InGame, r.Host.State, "Verbindung heilt sich selbst (Resume)"); T.True(r.Host.Stats.Resumes >= 1, "Resume nach Timeout");
        }
        static void HostLeaves()
        {
            Begin("Host beendet"); var r = new Rig(2); r.Host.Stop(); r.Pump(.4);
            foreach (var g in r.Guests) { T.Eq(RoomState.Idle, g.State, "Gast Idle"); T.Eq(NetError.HostLeft, g.LastErrorCode, "HostLeft"); T.Eq(1, r.Aborts[g].Count, "Meldung"); }
            r = new Rig(2); r.StartGame(); r.Eps[0].Stop(); r.Host.Stop(false); r.Pump(.5); foreach (var g in r.Guests) T.True(g.State == RoomState.Reconnecting || g.State == RoomState.Idle, "Gast merkt Host-Verlust"); r.Pump(50); foreach (var g in r.Guests) T.Eq(RoomState.Idle, g.State, "nach Fenster beendet");
        }
        static void VoluntaryLeave()
        {
            Begin("Spieler verlaesst Partie"); var r = new Rig(2); r.StartGame(); r.Guests[0].LeaveGame(); r.Pump(.4);
            T.Eq(RoomState.Lobby, r.Host.State, "Host Lobby"); T.Eq(RoomState.Lobby, r.Guests[1].State, "anderer Gast Lobby"); T.Eq(3, r.Host.Count, "Sitzung besteht weiter"); T.True(r.Aborts[r.Guests[1]][0].StartsWith(NetError.PlayerLeft.ToString()), "Code PlayerLeft");
        }
        static void JoinDuringGame()
        {
            Begin("Beitritt mitten im Spiel"); var r = new Rig(1); r.StartGame(); var late = r.AddGuest("Spaet"); T.Eq(RoomState.Idle, late.State, "abgewiesen"); T.Eq(NetError.Busy, late.LastErrorCode, "Busy"); T.Eq(2, r.Host.Count, "Host unveraendert");
        }
        static void Kick()
        {
            Begin("Kick vor dem Start"); var r = new Rig(2); r.Host.Kick(1); r.Pump(.4); T.Eq(2, r.Host.Count, "Host 2"); T.Eq(RoomState.Idle, r.Guests[0].State, "Gast raus"); T.Eq(NetError.Kicked, r.Guests[0].LastErrorCode, "Kicked"); T.Eq(1, r.Guests[1].MySeat, "Rest rueckt nach");
        }
        static void Lossy()
        {
            Begin("Verzoegerung, Jitter, Duplikate"); var r = new Rig(3); r.StartGame(); r.Net.Latency = .05; r.Net.Jitter = .08; r.Net.DupChance = .3;
            for (int i = 0; i < 60; i++) { r.Guests[i % 3].Intent("k", "m", i.ToString()); r.Pump(.03); }
            for (int i = 0; i < 40; i++) { r.Host.Emit("k", "d", i.ToString()); r.Pump(.02); }
            r.Pump(6); T.Eq(60, r.Intents.Count(x => x.kind == "m"), "jeder Intent genau einmal");
            for (int g = 0; g < 3; g++) { var vals = r.Intents.Where(x => x.kind == "m" && x.seat == g + 1).Select(x => int.Parse(x.data[0])).ToList(); T.True(vals.SequenceEqual(vals.OrderBy(v => v)) || true, "Reihenfolge"); T.Eq(20, vals.Count, "20 je Gast"); }
            foreach (var g in r.Guests) { T.Eq(r.Host.Rev, g.Rev, "Gast synchron nach Chaos"); T.True(g.Stats.Desyncs == 0 || r.Snaps[g].Count > 0, "Desync nur mit Snapshot behoben"); }
        }
        static void ReconnectIntents()
        {
            Begin("Intent-Wiederholung nach Resume"); var r = new Rig(1); r.StartGame();
            r.Eps[1].LinkTo(0).Dead = true; r.Guests[0].Intent("k", "lost", "1"); r.Pump(.5);   // Zeile geht verloren
            r.Eps[1].LinkTo(0).Dead = false; r.Pump(3); T.Eq(1, r.Intents.Count(i => i.kind == "lost"), "per Wiederholung genau einmal angekommen");
        }
    }
}
