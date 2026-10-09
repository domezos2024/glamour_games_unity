using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames
{
    /// <summary>Raumzustand der Mehrspieler-Sitzung.</summary>
    public enum RoomState { Idle, Discovering, Lobby, Starting, InGame, Paused, Reconnecting, Finished }
    public enum Presence { Empty, Connected, Reconnecting }

    public sealed class SessionConfig
    {
        public int MaxSeats = 4, MinSeats = 2;
        /// <summary>Wie lange ein getrennter Platz auf die Wiederverbindung wartet (Sekunden). Spielregel, konfigurierbar.</summary>
        public double ReconnectWindow = 45;
        public double PingInterval = 2, PeerTimeout = 7, ResendInterval = 1.5, StartTimeout = 4, HandshakeTimeout = 10, ReconnectRetry = 2.5;
        /// <summary>Kennung des Spieldatensatzes (Liste der Spiel-IDs); Host und Gaeste muessen uebereinstimmen.</summary>
        public string Dataset = "";
    }

    public sealed class Seat
    {
        public int Index, Color; public string PlayerId = "", Name = "", Token = ""; public bool Ready; public Presence Presence = Presence.Empty;
        internal int Peer = -1; internal double LastRx, LostAt, LastPing; internal long LastIntent;
        public bool Connected => Presence == Presence.Connected;
        public bool Taken => Presence != Presence.Empty;
    }

    /// <summary>Messwerte fuer Verbindungszeit, Reconnect, Timeouts, Verluste und Desyncs.</summary>
    public sealed class SessionStats
    {
        public int Sent, Received, Duplicates, BadLines, Timeouts, Resumes, ResumeFailures, SnapshotsSent, SnapshotRequests, Desyncs, Resends;
        public double ConnectSeconds, LastRtt, MaxRtt;
        public override string ToString() => $"gesendet {Sent}, empfangen {Received}, Duplikate {Duplicates}, defekt {BadLines}, Timeouts {Timeouts}, Resume {Resumes}/{Resumes + ResumeFailures}, Snapshots {SnapshotsSent}/{SnapshotRequests}, Desync {Desyncs}, RTT {LastRtt * 1000:0} ms (max {MaxRtt * 1000:0}), Verbindungsaufbau {ConnectSeconds:0.00} s";
    }

    public sealed class GameStart { public string GameId = "", Rules = ""; public int Seats, Seed; }

    /// <summary>
    /// Sitzungslogik fuer bis zu vier Personen (Stern: ein Host, bis zu drei Gaeste). Unity-frei und ueber ISessionTransport testbar.
    /// Der Host ist alleinige Autoritaet: er vergibt Sitzplaetze, nimmt Eingabeabsichten (Intents) entgegen, erhoeht die Zustandsrevision
    /// und verteilt Deltas und Snapshots. Gaeste senden ausschliesslich Intents (mit Folgenummer, quittiert und bei Bedarf wiederholt).
    /// </summary>
    public sealed class Session
    {
        public readonly ISessionTransport T; public readonly SessionConfig Cfg; public readonly SessionStats Stats = new SessionStats(); readonly IClock clk;
        public bool IsHost { get; private set; }
        public RoomState State { get; private set; } = RoomState.Idle;
        public string SessionId = "", HostName = "", LocalName = "Spieler", MyPlayerId = "", MyToken = "", GameId = "", Rules = "", LastError = "";
        public int MySeat = -1, Seed; public long Rev; public NetError LastErrorCode;
        public readonly List<Seat> Seats = new List<Seat>();
        public double Window = 45;

        public Action<string> Log, OnNotice; public Action OnRoster; public Action<RoomState, RoomState> OnState; public Action<GameStart> OnGameStart;
        /// <summary>(Spiel, Art, Daten, privat) - wird bei Host UND Gaesten ausgefuehrt (Host per Schleife).</summary>
        public Action<string, string, string[], bool> OnDelta;
        /// <summary>Nur beim Host: (Sitz, Spiel, Art, Daten) - Eingabeabsicht eines Sitzes (auch des eigenen).</summary>
        public Action<int, string, string, string[]> OnIntent;
        /// <summary>Nur beim Host: Zustand des Spiels fuer einen Sitz (nur sichtbare Informationen).</summary>
        public Func<int, string> SnapshotProvider; public Action<string, string> OnSnapshot; public Action<NetError, string> OnAborted;
        public Func<string, bool> KnowsGame = _ => true;

        string hostAddr = ""; long midCtr, intentSeq; double connStart, lastPing, lastRx, reconnectDeadline, nextTry, startDeadline, handshakeStart; bool connecting, awaitSnap, helloSent, resuming;
        sealed class Pend { public double T; public bool Hello; public string Name = "", Addr = ""; }
        readonly Dictionary<int, Pend> pend = new Dictionary<int, Pend>();
        sealed class Out { public Msg M; public double Sent; }
        readonly List<Out> unacked = new List<Out>();
        string startId = "", startGame = "", startRules = ""; readonly HashSet<int> startAcks = new HashSet<int>();

        public Session(ISessionTransport t, SessionConfig cfg = null, IClock clock = null) { T = t; Cfg = cfg ?? new SessionConfig(); clk = clock ?? new StopwatchClock(); }

        // ---------------------------------------------------------------- Abfragen
        public int Count => Seats.Count(s => s.Taken);
        public int Connected => Seats.Count(s => s.Connected);
        public bool Frozen => State == RoomState.Paused || State == RoomState.Reconnecting;
        public bool Active => State != RoomState.Idle && State != RoomState.Finished;
        public bool InGame => State == RoomState.InGame || State == RoomState.Paused || State == RoomState.Reconnecting;
        public string SeatName(int i) => i >= 0 && i < Seats.Count && Seats[i].Name.Length > 0 ? Seats[i].Name : "Spieler " + (i + 1);
        public double ReconnectLeft(int seat) => IsHost && seat >= 0 && seat < Seats.Count && Seats[seat].Presence == Presence.Reconnecting ? Math.Max(0, Window - (clk.Now - Seats[seat].LostAt)) : State == RoomState.Reconnecting ? Math.Max(0, reconnectDeadline - clk.Now) : 0;
        public bool AllReady => IsHost && Count >= Cfg.MinSeats && Seats.All(s => !s.Taken || s.Ready || s.Index == 0) && Seats.All(s => !s.Taken || s.Connected);
        void L(string s) => Log?.Invoke(s);
        void Note(string s) { L(s); OnNotice?.Invoke(s); }
        public static string DatasetHash(IEnumerable<string> ids) { uint h = 2166136261; foreach (var c in string.Join(",", ids)) { h ^= c; h *= 16777619; } return h.ToString("x8"); }

        void SetState(RoomState s) { if (State == s) return; var o = State; State = s; L($"zustand {o} -> {s}"); OnState?.Invoke(o, s); }

        void Reset()
        {
            Seats.Clear(); pend.Clear(); unacked.Clear(); startAcks.Clear(); SessionId = HostName = MyPlayerId = MyToken = GameId = Rules = ""; MySeat = -1; Rev = 0; intentSeq = 0; connecting = awaitSnap = helloSent = resuming = false;
            hostAddr = ""; Seed = 0; Window = Cfg.ReconnectWindow; lastRx = 0; lastPing = 0;
        }

        // ---------------------------------------------------------------- Host / Beitritt / Ende
        public void Host(string name)
        {
            Stop(false); Reset(); IsHost = true; LocalName = WireUtil.SafeName(name, "Host"); SessionId = WireUtil.RandomId(4); HostName = LocalName; MyPlayerId = WireUtil.RandomId(4); MySeat = 0;
            Seats.Add(new Seat { Index = 0, PlayerId = MyPlayerId, Name = LocalName, Ready = true, Presence = Presence.Connected });
            T.StartHost(); SetState(RoomState.Lobby); OnRoster?.Invoke();
        }
        public void Join(string name, string addr)
        {
            Stop(false); Reset(); IsHost = false; LocalName = WireUtil.SafeName(name, "Gast"); hostAddr = addr; connStart = clk.Now;
            SetState(RoomState.Discovering); LastError = ""; LastErrorCode = NetError.None; connecting = true; T.Connect(addr);
        }
        /// <summary>Sitzung verlassen bzw. (als Host) beenden.</summary>
        public void Stop(bool notify = true)
        {
            if (State != RoomState.Idle && notify)
            {
                if (IsHost) BroadcastRaw(Make(MsgType.Disconnect, "session", ((int)NetError.HostLeft).ToString(), NetText.Of(NetError.HostLeft)));
                else if (T.Supported) SendHost(Make(MsgType.Disconnect, "session", "0", ""));
            }
            try { T.Stop(); } catch (Exception e) { L("stop: " + e.Message); }
            Reset(); IsHost = false; SetState(RoomState.Idle);
        }

        // ---------------------------------------------------------------- Senden
        Msg Make(MsgType t, params string[] p) => new Msg(t, p);
        string Stamp(Msg m, bool keepRev = false) { m.Sid = SessionId; m.Mid = ++midCtr; m.From = MyPlayerId; m.Seat = MySeat; if (!keepRev) m.Rev = Rev; return m.Encode(); }
        void SendTo(int peer, Msg m, bool keepRev = false) { if (peer < 0 && IsHost) return; var line = Stamp(m, keepRev); Stats.Sent++; try { T.Send(peer, line); } catch (Exception e) { L("senden: " + e.Message); } }
        void SendHost(Msg m, bool keepRev = false) => SendTo(0, m, keepRev);
        void SendSeat(Seat s, Msg m, bool keepRev = false) { if (s != null && s.Peer >= 0 && s.Presence == Presence.Connected) SendTo(s.Peer, m, keepRev); }
        void BroadcastRaw(Msg m, bool keepRev = false, int except = -1)
        {
            var line = Stamp(m, keepRev);
            foreach (var s in Seats) if (s.Index != 0 && s.Peer >= 0 && s.Presence == Presence.Connected && s.Index != except) { Stats.Sent++; try { T.Send(s.Peer, line); } catch (Exception e) { L("senden: " + e.Message); } }
        }
        void SendError(int peer, NetError code, string text = null) { SendTo(peer, Make(MsgType.Error, ((int)code).ToString(), text ?? NetText.Of(code))); }
        Seat SeatOfPeer(int peer) { foreach (var s in Seats) if (s.Index != 0 && s.Peer == peer && s.Presence != Presence.Empty) return s; return null; }

        // ---------------------------------------------------------------- Roster
        string[] RosterPayload()
        {
            var p = new List<string> { ((int)State).ToString(), GameId, Count.ToString() };
            foreach (var s in Seats) if (s.Taken) { p.Add(s.Name); p.Add(s.Ready ? "1" : "0"); p.Add(((int)s.Presence).ToString()); p.Add(s.Color.ToString()); p.Add(s.PlayerId); }
            return p.ToArray();
        }
        void BroadcastRoster() { BroadcastRaw(Make(MsgType.Roster, RosterPayload())); OnRoster?.Invoke(); }
        void Compact() { Seats.RemoveAll(s => !s.Taken); for (int i = 0; i < Seats.Count; i++) { Seats[i].Index = i; Seats[i].Color = i; } }

        // ---------------------------------------------------------------- Spielsteuerung (Host)
        /// <summary>Host: Spiel starten. Prueft Mindestzahl/Bereitschaft, holt die Bestaetigung aller Gaeste ein und startet dann gemeinsam.</summary>
        public bool RequestStart(string gameId, string rules = "", int minSeats = 0, int maxSeats = 0)
        {
            if (!IsHost || State != RoomState.Lobby) return false;
            int n = Count; if (minSeats <= 0) minSeats = Cfg.MinSeats; if (maxSeats <= 0) maxSeats = Cfg.MaxSeats;
            if (n < minSeats) { Note($"Mindestens {minSeats} Spieler nötig"); return false; }
            if (n > maxSeats) { Note($"Dieses Spiel ist nur für {maxSeats} Spieler"); return false; }
            if (!Seats.All(s => !s.Taken || s.Connected)) { Note("Nicht alle Spieler sind verbunden"); return false; }
            if (!Seats.All(s => !s.Taken || s.Ready)) { Note("Nicht alle Spieler sind bereit"); return false; }
            if (!KnowsGame(gameId)) { Note("Unbekanntes Spiel"); return false; }
            startId = WireUtil.RandomId(3); startGame = gameId; startRules = rules ?? ""; startAcks.Clear(); startDeadline = clk.Now + Cfg.StartTimeout; SetState(RoomState.Starting);
            BroadcastRaw(Make(MsgType.StartRequest, startId, gameId, startRules, n.ToString()));
            if (n == 1 || Seats.Count(s => s.Connected && s.Index != 0) == 0) FinishStart();
            return true;
        }
        void FinishStart()
        {
            GameId = startGame; Rules = startRules; Seed = new Random().Next(1, int.MaxValue); Rev = 0; foreach (var s in Seats) s.LastIntent = 0;
            SetState(RoomState.InGame);
            BroadcastRaw(Make(MsgType.StartGame, GameId, Count.ToString(), Seed.ToString(), Rules, startId)); BroadcastRoster();
            OnGameStart?.Invoke(new GameStart { GameId = GameId, Rules = Rules, Seats = Count, Seed = Seed });
        }
        void CancelStart(string why) { startAcks.Clear(); SetState(RoomState.Lobby); Note(why); BroadcastRoster(); }

        /// <summary>Host: Partie beenden, alle kehren ins Menue (Lobby) zurueck.</summary>
        public void ToMenu()
        {
            if (!IsHost || State == RoomState.Idle) return;
            if (State == RoomState.Lobby && GameId.Length == 0) return;
            GameId = ""; Rev = 0; SetState(RoomState.Lobby); BroadcastRaw(Make(MsgType.StartGame, "menu", Count.ToString(), "0", "", "")); BroadcastRoster();
        }
        /// <summary>Gast: die laufende Partie verlassen (Abbruch fuer alle, die Sitzung bleibt bestehen).</summary>
        public void LeaveGame() { if (IsHost) { ToMenu(); return; } if (State == RoomState.InGame || State == RoomState.Paused) SendHost(Make(MsgType.Disconnect, "game", ((int)NetError.PlayerLeft).ToString(), "")); }

        /// <summary>Host: Partie wegen Fehler/Abwesenheit abbrechen; getrennte Plaetze werden entfernt, die Sitzung bleibt in der Lobby.</summary>
        public void AbortGame(NetError code, string text = null)
        {
            if (!IsHost || !InGame) return; text ??= NetText.Of(code);
            foreach (var s in Seats) if (s.Presence == Presence.Reconnecting) { s.Presence = Presence.Empty; if (s.Peer >= 0) T.Close(s.Peer); s.Peer = -1; }
            Compact(); GameId = ""; Rev = 0; SetState(RoomState.Lobby);
            BroadcastRaw(Make(MsgType.Disconnect, "game", ((int)code).ToString(), text)); BroadcastRoster(); Note(text); OnAborted?.Invoke(code, text);
        }
        public void Kick(int seat)
        {
            if (!IsHost || seat <= 0 || seat >= Seats.Count || !Seats[seat].Taken) return; var s = Seats[seat];
            if (s.Peer >= 0) { SendTo(s.Peer, Make(MsgType.Disconnect, "session", ((int)NetError.Kicked).ToString(), NetText.Of(NetError.Kicked))); T.Close(s.Peer); }
            s.Presence = Presence.Empty; s.Peer = -1; Compact(); if (State == RoomState.Starting) CancelStart("Start abgebrochen"); else BroadcastRoster();
        }
        public void SetReady(bool r)
        {
            if (IsHost) return; if (State != RoomState.Lobby) return;
            if (MySeat >= 0 && MySeat < Seats.Count) Seats[MySeat].Ready = r; SendHost(Make(MsgType.Ready, r ? "1" : "0")); OnRoster?.Invoke();
        }

        // ---------------------------------------------------------------- Spielnachrichten
        /// <summary>Eingabeabsicht eines Sitzes. Gast: an den Host (quittiert, wiederholt). Host: direkt verarbeiten.</summary>
        public void Intent(string game, string kind, params string[] data)
        {
            if (!InGame && State != RoomState.Starting) return;
            if (IsHost) { OnIntent?.Invoke(0, game, kind, data); return; }
            var p = new string[data.Length + 2]; p[0] = game; p[1] = kind; Array.Copy(data, 0, p, 2, data.Length);
            var m = Make(MsgType.Intent, p); m.Seq = ++intentSeq; var o = new Out { M = m, Sent = clk.Now }; unacked.Add(o);
            if (State == RoomState.InGame || State == RoomState.Paused) SendHost(m);
        }
        /// <summary>Host: oeffentliches Delta (neue Revision) an alle und lokal anwenden.</summary>
        public void Emit(string game, string kind, params string[] data)
        {
            if (!IsHost || !InGame) return; Rev++;
            var p = new string[data.Length + 3]; p[0] = "B"; p[1] = game; p[2] = kind; Array.Copy(data, 0, p, 3, data.Length);
            var m = Make(MsgType.StateDelta, p); m.Rev = Rev; BroadcastRaw(m, true);
            OnDelta?.Invoke(game, kind, data, false);
        }
        /// <summary>Host: privates Delta nur fuer einen Sitz (z. B. verdeckte Pokerkarten); veraendert die Revision nicht.</summary>
        public void EmitTo(int seat, string game, string kind, params string[] data)
        {
            if (!IsHost || !InGame) return;
            if (seat == MySeat) { OnDelta?.Invoke(game, kind, data, true); return; }
            if (seat < 0 || seat >= Seats.Count) return;
            var p = new string[data.Length + 3]; p[0] = "P"; p[1] = game; p[2] = kind; Array.Copy(data, 0, p, 3, data.Length);
            var m = Make(MsgType.StateDelta, p); m.Rev = Rev; SendSeat(Seats[seat], m, true);
        }
        /// <summary>Host: vollstaendigen Zustand an einen Sitz schicken (Beitritt, Wiederverbindung, Desync).</summary>
        public void SendSnapshot(int seat)
        {
            if (!IsHost || seat <= 0 || seat >= Seats.Count) return; string blob = SnapshotProvider?.Invoke(seat) ?? "";
            Stats.SnapshotsSent++; var m = Make(MsgType.StateSnapshot, GameId, Rev.ToString(), blob); SendSeat(Seats[seat], m);
        }

        // ---------------------------------------------------------------- Pumpe
        public void Update()
        {
            double now = clk.Now; int n = 0;
            while (n++ < 512 && T.Poll(out var e))
            {
                try { OnEvent(e, now); } catch (Exception x) { L("fehler: " + x); }
            }
            try { if (IsHost) HostTick(now); else if (State != RoomState.Idle) GuestTick(now); } catch (Exception x) { L("tick: " + x); }
        }

        void OnEvent(TransportEvent e, double now)
        {
            if (State == RoomState.Idle) return;
            if (IsHost)
            {
                switch (e.Kind)
                {
                    case TEvKind.Connected: pend[e.Peer] = new Pend { T = now, Name = e.Text ?? "", Addr = e.Addr ?? "" }; break;
                    case TEvKind.Closed: pend.Remove(e.Peer); var s = SeatOfPeer(e.Peer); if (s != null) PeerLost(s, e.Err == BtErr.None ? BtErr.SocketClosed : e.Err); break;
                    case TEvKind.Line:
                        if (!Msg.TryDecode(e.Text, out var m, out var err)) { Stats.BadLines++; if (err == NetError.Version) { SendError(e.Peer, NetError.Version); T.Close(e.Peer); } return; }
                        Stats.Received++; var st = SeatOfPeer(e.Peer); if (st != null) st.LastRx = now; HostHandle(e.Peer, st, m, now); break;
                }
            }
            else
            {
                switch (e.Kind)
                {
                    case TEvKind.Connected: connecting = false; lastRx = now; lastPing = now; helloSent = true; SendHost(Make(MsgType.Hello, LocalName, Cfg.Dataset, Msg.Ver)); break;
                    case TEvKind.Closed: connecting = false; ConnectionLost(e.Err, e.Text); break;
                    case TEvKind.Line:
                        if (!Msg.TryDecode(e.Text, out var m, out var err)) { Stats.BadLines++; return; }
                        Stats.Received++; lastRx = now; GuestHandle(m, now); break;
                }
            }
        }

        // ---------------------------------------------------------------- Host: Nachrichten
        void HostHandle(int peer, Seat seat, Msg m, double now)
        {
            switch (m.Type)
            {
                case MsgType.Hello:
                    {
                        if (!pend.TryGetValue(peer, out var pe)) pend[peer] = pe = new Pend { T = now };
                        string ds = m.P.Length > 1 ? m.P[1] : "";
                        if (Cfg.Dataset.Length > 0 && ds != Cfg.Dataset) { SendError(peer, NetError.Dataset); T.Close(peer); return; }
                        pe.Hello = true; pe.Name = WireUtil.SafeName(m.P.Length > 0 ? m.P[0] : "", "Gast");
                        SendTo(peer, Make(MsgType.Hello, HostName, Cfg.Dataset, Msg.Ver, SessionId)); break;
                    }
                case MsgType.JoinRequest:
                    {
                        if (!pend.TryGetValue(peer, out var pe) || !pe.Hello) { SendError(peer, NetError.Invalid); T.Close(peer); return; }
                        if (State != RoomState.Lobby) { SendError(peer, NetError.Busy, "Partie läuft bereits"); T.Close(peer); return; }
                        if (Count >= Cfg.MaxSeats) { SendError(peer, NetError.Full); T.Close(peer); return; }
                        var s = new Seat { Index = Seats.Count, Color = Seats.Count, PlayerId = WireUtil.RandomId(4), Token = WireUtil.RandomId(6), Name = UniqueName(WireUtil.SafeName(m.P.Length > 0 ? m.P[0] : pe.Name, "Gast")), Presence = Presence.Connected, Peer = peer, LastRx = now };
                        Seats.Add(s); pend.Remove(peer);
                        SendTo(peer, Make(MsgType.Welcome, SessionId, s.PlayerId, s.Token, s.Index.ToString(), HostName, Window.ToString("0")));
                        Note($"{s.Name} ist beigetreten"); BroadcastRoster(); break;
                    }
                case MsgType.ResumeRequest: HostResume(peer, m, now); break;
                case MsgType.Ready:
                    if (seat != null && State == RoomState.Lobby) { seat.Ready = m.P.Length > 0 && m.P[0] == "1"; BroadcastRoster(); }
                    break;
                case MsgType.Ack:
                    if (seat != null && m.P.Length > 1 && m.P[0] == "start" && m.P[1] == startId && State == RoomState.Starting)
                    {
                        if (m.P.Length > 2 && m.P[2] == "1") { startAcks.Add(seat.Index); if (Seats.Where(x => x.Connected && x.Index != 0).All(x => startAcks.Contains(x.Index))) FinishStart(); }
                        else { CancelStart($"{seat.Name}: " + (m.P.Length > 3 ? m.P[3] : NetText.Of(NetError.StartRefused))); }
                    }
                    break;
                case MsgType.Intent:
                    {
                        if (seat == null || m.P.Length < 2) return;
                        if (m.Seq <= seat.LastIntent) { Stats.Duplicates++; var ak = Make(MsgType.Ack); ak.Ack = m.Seq; SendSeat(seat, ak); return; }
                        if (State == RoomState.Paused) { return; }   // Gast wiederholt nach der Pause
                        if (State != RoomState.InGame) { return; }
                        seat.LastIntent = m.Seq; var ack = Make(MsgType.Ack); ack.Ack = m.Seq; SendSeat(seat, ack);
                        var data = new string[m.P.Length - 2]; Array.Copy(m.P, 2, data, 0, data.Length);
                        OnIntent?.Invoke(seat.Index, m.P[0], m.P[1], data); break;
                    }
                case MsgType.SnapshotRequest: if (seat != null) { Stats.SnapshotRequests++; SendSnapshot(seat.Index); } break;
                case MsgType.Ping: SendTo(peer, MakePong(m)); break;
                case MsgType.Pong: break;
                case MsgType.Disconnect:
                    if (seat == null) { T.Close(peer); return; }
                    if (m.P.Length > 0 && m.P[0] == "game") { if (InGame) AbortGame(NetError.PlayerLeft, $"{seat.Name} hat die Partie verlassen"); }
                    else { LeaveSeat(seat, $"{seat.Name} hat die Sitzung verlassen"); T.Close(peer); }
                    break;
            }
        }
        Msg MakePong(Msg ping) => Make(MsgType.Pong, ping.P.Length > 0 ? ping.P[0] : "0");
        string UniqueName(string n)
        {
            string b = n; int k = 2; while (Seats.Any(s => s.Taken && s.Name == n)) { n = b.Length > 12 ? b.Substring(0, 12) + " " + k : b + " " + k; k++; }
            return n;
        }

        void HostResume(int peer, Msg m, double now)
        {
            string sid = m.P.Length > 0 ? m.P[0] : "", pid = m.P.Length > 1 ? m.P[1] : "", tok = m.P.Length > 2 ? m.P[2] : "";
            pend.Remove(peer);
            if (sid != SessionId || (State != RoomState.InGame && State != RoomState.Paused)) { Stats.ResumeFailures++; SendError(peer, NetError.NoSession); T.Close(peer); return; }
            var s = Seats.FirstOrDefault(x => x.Taken && x.PlayerId == pid);
            if (s == null || s.Token != tok || s.Index == 0) { Stats.ResumeFailures++; SendError(peer, NetError.BadToken); T.Close(peer); return; }
            if (s.Presence == Presence.Reconnecting && now - s.LostAt > Window) { Stats.ResumeFailures++; SendError(peer, NetError.WindowExpired); T.Close(peer); return; }
            if (s.Peer >= 0 && s.Peer != peer) T.Close(s.Peer);   // alte, halb offene Verbindung ersetzen
            s.Peer = peer; s.Presence = Presence.Connected; s.LastRx = now; s.Token = WireUtil.RandomId(6); Stats.Resumes++;
            SendTo(peer, Make(MsgType.ResumeAccepted, s.Token, s.Index.ToString(), SessionId, GameId));
            SendSnapshot(s.Index);
            if (Seats.All(x => x.Presence != Presence.Reconnecting) && State == RoomState.Paused) SetState(RoomState.InGame);
            Note($"{s.Name} ist wieder verbunden"); BroadcastRoster();
        }

        void PeerLost(Seat s, BtErr err)
        {
            L($"peer verloren: {s.Name} ({err})"); s.Peer = -1;
            if (State == RoomState.Lobby || State == RoomState.Starting) { LeaveSeat(s, $"{s.Name} hat die Verbindung verloren"); return; }
            if (!InGame) return;
            s.Presence = Presence.Reconnecting; s.LostAt = clk.Now; SetState(RoomState.Paused);
            Note($"{s.Name} getrennt - Spiel pausiert, warte auf Wiederverbindung"); BroadcastRoster();
        }
        void LeaveSeat(Seat s, string text)
        {
            s.Presence = Presence.Empty; s.Peer = -1; bool starting = State == RoomState.Starting; Compact(); Note(text);
            if (starting) CancelStart("Start abgebrochen"); else BroadcastRoster();
        }

        void HostTick(double now)
        {
            if (State == RoomState.Starting && now > startDeadline) { CancelStart("Start nicht bestätigt - Zeitüberschreitung"); }
            foreach (var kv in pend.ToList()) if (now - kv.Value.T > Cfg.HandshakeTimeout) { pend.Remove(kv.Key); T.Close(kv.Key); }
            foreach (var s in Seats.ToList())
            {
                if (s.Index == 0) continue;
                if (s.Presence == Presence.Connected)
                {
                    if (now - s.LastRx > Cfg.PeerTimeout) { Stats.Timeouts++; int p = s.Peer; T.Close(p); PeerLost(s, BtErr.Timeout); }
                    else if (now - s.LastPing > Cfg.PingInterval) { s.LastPing = now; SendSeat(s, Make(MsgType.Ping, now.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))); }
                }
                else if (s.Presence == Presence.Reconnecting && now - s.LostAt > Window && InGame) { AbortGame(NetError.WindowExpired, $"{s.Name} ist nicht zurückgekehrt - Partie abgebrochen"); break; }
            }
        }

        // ---------------------------------------------------------------- Gast: Nachrichten
        void GuestHandle(Msg m, double now)
        {
            switch (m.Type)
            {
                case MsgType.Hello:
                    if (m.P.Length > 3) SessionId = SessionId.Length == 0 ? m.P[3] : SessionId;
                    HostName = m.P.Length > 0 ? m.P[0] : HostName;
                    if (resuming) SendHost(Make(MsgType.ResumeRequest, SessionId, MyPlayerId, MyToken)); else SendHost(Make(MsgType.JoinRequest, LocalName));
                    break;
                case MsgType.Welcome:
                    if (m.P.Length < 6) return; SessionId = m.P[0]; MyPlayerId = m.P[1]; MyToken = m.P[2]; MySeat = WireUtil.Int(m.P[3]); HostName = m.P[4]; Window = WireUtil.Int(m.P[5], 45);
                    Stats.ConnectSeconds = now - connStart; SetState(RoomState.Lobby); L($"beigetreten als Sitz {MySeat}"); break;
                case MsgType.Roster: ApplyRoster(m); break;
                case MsgType.StartRequest:
                    {
                        string id = m.P.Length > 0 ? m.P[0] : "", game = m.P.Length > 1 ? m.P[1] : "";
                        var ak = Make(MsgType.Ack, "start", id, KnowsGame(game) ? "1" : "0", KnowsGame(game) ? "" : "Spiel unbekannt (andere Version)"); SendHost(ak);
                        if (KnowsGame(game)) SetState(RoomState.Starting); break;
                    }
                case MsgType.StartGame:
                    {
                        string game = m.P.Length > 0 ? m.P[0] : ""; int n = m.P.Length > 1 ? WireUtil.Int(m.P[1]) : 0;
                        if (game == "menu") { GameId = ""; Rev = 0; unacked.Clear(); SetState(RoomState.Lobby); OnGameStart?.Invoke(new GameStart { GameId = "menu" }); break; }
                        GameId = game; Seed = m.P.Length > 2 ? WireUtil.Int(m.P[2]) : 0; Rules = m.P.Length > 3 ? m.P[3] : ""; Rev = 0; intentSeq = 0; unacked.Clear(); awaitSnap = false;
                        SetState(RoomState.InGame); OnGameStart?.Invoke(new GameStart { GameId = GameId, Rules = Rules, Seats = n, Seed = Seed }); break;
                    }
                case MsgType.StateDelta:
                    {
                        if (m.P.Length < 3) return; bool priv = m.P[0] == "P"; var data = new string[m.P.Length - 3]; Array.Copy(m.P, 3, data, 0, data.Length);
                        if (!priv)
                        {
                            if (awaitSnap) return;
                            if (m.Rev <= Rev) { Stats.Duplicates++; return; }
                            if (m.Rev != Rev + 1) { Stats.Desyncs++; awaitSnap = true; Stats.SnapshotRequests++; L($"luecke: erwartet {Rev + 1}, erhalten {m.Rev} - Snapshot angefordert"); SendHost(Make(MsgType.SnapshotRequest)); return; }
                            Rev = m.Rev;
                        }
                        OnDelta?.Invoke(m.P[1], m.P[2], data, priv); break;
                    }
                case MsgType.StateSnapshot:
                    if (m.P.Length < 3) return; Rev = WireUtil.Long(m.P[1]); awaitSnap = false; OnSnapshot?.Invoke(m.P[0], m.P[2]); break;
                case MsgType.Ack: unacked.RemoveAll(o => o.M.Seq <= m.Ack); break;
                case MsgType.Ping: SendHost(MakePong(m)); break;
                case MsgType.Pong:
                    if (m.P.Length > 0 && double.TryParse(m.P[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sent)) { Stats.LastRtt = Math.Max(0, now - sent); Stats.MaxRtt = Math.Max(Stats.MaxRtt, Stats.LastRtt); }
                    break;
                case MsgType.ResumeAccepted:
                    if (m.P.Length < 2) return; MyToken = m.P[0]; MySeat = WireUtil.Int(m.P[1]); resuming = false; Stats.Resumes++; awaitSnap = true;
                    SetState(RoomState.InGame); Note("Wieder verbunden"); foreach (var o in unacked) { o.Sent = now; SendHost(o.M); Stats.Resends++; } break;
                case MsgType.Error:
                    {
                        var code = (NetError)WireUtil.Int(m.P.Length > 0 ? m.P[0] : "0"); string txt = m.P.Length > 1 ? m.P[1] : NetText.Of(code);
                        LastErrorCode = code; LastError = txt; L($"fehler vom host: {code} {txt}");
                        if (code == NetError.Paused) break;
                        if (State == RoomState.Reconnecting || resuming) { Stats.ResumeFailures++; GiveUp(code, txt); }
                        else if (State == RoomState.Discovering || State == RoomState.Lobby) { T.Stop(); Reset(); SetState(RoomState.Idle); }
                        break;
                    }
                case MsgType.Disconnect:
                    {
                        string scope = m.P.Length > 0 ? m.P[0] : "session"; var code = (NetError)WireUtil.Int(m.P.Length > 1 ? m.P[1] : "0"); string txt = m.P.Length > 2 && m.P[2].Length > 0 ? m.P[2] : NetText.Of(code);
                        if (scope == "game") { GameId = ""; Rev = 0; unacked.Clear(); awaitSnap = false; SetState(RoomState.Lobby); Note(txt); OnAborted?.Invoke(code, txt); }
                        else { LastErrorCode = code; LastError = txt; T.Stop(); Reset(); SetState(RoomState.Idle); Note(txt); OnAborted?.Invoke(code, txt); }
                        break;
                    }
            }
        }

        void ApplyRoster(Msg m)
        {
            if (m.P.Length < 3) return; int hs = WireUtil.Int(m.P[0]), n = WireUtil.Int(m.P[2]); if (m.P.Length < 3 + n * 5) return;
            var ns = new List<Seat>();
            for (int i = 0; i < n; i++)
            {
                int o = 3 + i * 5; ns.Add(new Seat { Index = i, Name = m.P[o], Ready = m.P[o + 1] == "1", Presence = (Presence)WireUtil.Int(m.P[o + 2]), Color = WireUtil.Int(m.P[o + 3]), PlayerId = m.P[o + 4] });
            }
            Seats.Clear(); Seats.AddRange(ns); int me = Seats.FindIndex(s => s.PlayerId == MyPlayerId); if (me >= 0) MySeat = me;
            if (!Enum.IsDefined(typeof(RoomState), hs)) hs = (int)RoomState.Lobby; var host = (RoomState)hs;
            if (State != RoomState.Reconnecting && State != RoomState.Idle && host != RoomState.Discovering && host != RoomState.Idle) { if (host == RoomState.Lobby) GameId = ""; SetState(host); }
            OnRoster?.Invoke();
        }

        // ---------------------------------------------------------------- Gast: Verbindungsverlust / Wiederverbindung
        void ConnectionLost(BtErr err, string text)
        {
            L($"verbindung verloren: {err} {text}");
            if (State == RoomState.Reconnecting) return;   // laufender Versuch ist fehlgeschlagen
            if (State == RoomState.InGame || State == RoomState.Paused)
            {
                resuming = true; SetState(RoomState.Reconnecting); reconnectDeadline = clk.Now + Window; nextTry = clk.Now + .5; Note("Verbindung zum Host verloren - versuche Wiederverbindung"); return;
            }
            LastErrorCode = NetError.None; LastError = NetText.Of(err) + (string.IsNullOrEmpty(text) ? "" : " (" + text + ")");
            string e = State == RoomState.Discovering ? LastError : NetText.Of(NetError.HostLeft); bool wasLobby = State != RoomState.Discovering;
            T.Stop(); Reset(); SetState(RoomState.Idle); if (wasLobby) { LastError = e; Note(e); OnAborted?.Invoke(NetError.HostLeft, e); }
        }
        void GiveUp(NetError code, string txt)
        {
            T.Stop(); Reset(); LastErrorCode = code; LastError = txt; SetState(RoomState.Idle); Note(txt); OnAborted?.Invoke(code, txt);
        }

        void GuestTick(double now)
        {
            if (State == RoomState.Reconnecting)
            {
                if (now > reconnectDeadline) { GiveUp(NetError.WindowExpired, "Host nicht erreichbar - Partie beendet"); return; }
                if (!connecting && now >= nextTry) { connecting = true; nextTry = now + Cfg.ReconnectRetry; try { T.Connect(hostAddr); } catch (Exception x) { L("connect: " + x.Message); connecting = false; } }
                return;
            }
            if (State == RoomState.Discovering) { if (now - connStart > Cfg.HandshakeTimeout * 2) { T.Stop(); Reset(); LastError = NetText.Of(NetError.Timeout); SetState(RoomState.Idle); } return; }
            if (lastRx > 0 && now - lastRx > Cfg.PeerTimeout) { Stats.Timeouts++; T.Close(0); lastRx = now; ConnectionLost(BtErr.Timeout, ""); return; }
            if (now - lastPing > Cfg.PingInterval) { lastPing = now; SendHost(Make(MsgType.Ping, now.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))); }
            if ((State == RoomState.InGame || State == RoomState.Paused) && unacked.Count > 0)
                foreach (var o in unacked) if (now - o.Sent > Cfg.ResendInterval) { o.Sent = now; Stats.Resends++; SendHost(o.M); }
        }
    }
}
