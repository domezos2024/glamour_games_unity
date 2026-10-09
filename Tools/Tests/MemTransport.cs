using System;
using System.Collections.Generic;
using System.Linq;

namespace GlamourGames.Tests
{
    public sealed class FakeClock : IClock { public double Now { get; set; } }

    /// <summary>Simuliertes Netz fuer Tests: Verzoegerung, Jitter (Umordnung), Duplikate, stilles Verlieren, harte Trennung.</summary>
    public sealed class MemNet
    {
        public readonly FakeClock Clock = new FakeClock();
        public readonly Dictionary<string, MemEp> Hosts = new Dictionary<string, MemEp>();
        public double Latency = 0.02, Jitter = 0, DupChance = 0; public Random Rnd = new Random(7); long ord;
        public MemEp Create(string addr) => new MemEp(this, addr);
        internal long Ord() => ++ord;
    }

    public sealed class MemLink { public MemEp Host, Guest; public int HostPeer; public bool Dead, Closed; public int DropNextPublicDeltas; }

    public sealed class MemEp : ISessionTransport
    {
        readonly MemNet net; public readonly string Addr; bool listening; int nextPeer = 1;
        readonly List<(double at, long ord, TransportEvent e)> inbox = new List<(double, long, TransportEvent)>();
        public readonly Dictionary<int, MemLink> Links = new Dictionary<int, MemLink>();
        public MemEp(MemNet n, string addr) { net = n; Addr = addr; }
        public bool Supported => true; public bool Enabled => true; public string Status => listening ? "wartet" : "bereit"; public BtErr LastError { get; private set; }
        public int MaxPeers => 3; public bool Listening => listening;
        public int SentLines, DroppedLines;

        public void StartHost() { Stop(); listening = true; net.Hosts[Addr] = this; }
        public void Connect(string addr)
        {
            foreach (var l in Links.Values.ToList()) Kill(l, false);
            Links.Clear();
            if (!net.Hosts.TryGetValue(addr, out var h) || !h.listening) { Push(TransportEvent.Close(0, BtErr.ConnectFailed, "kein Host")); return; }
            var link = new MemLink { Host = h, Guest = this, HostPeer = h.nextPeer++ };
            h.Links[link.HostPeer] = link; Links[0] = link;
            Push(TransportEvent.Conn(0, "Host", addr)); h.Push(TransportEvent.Conn(link.HostPeer, Addr, Addr));
        }
        public void Stop()
        {
            foreach (var l in Links.Values.ToList()) Kill(l, false);
            Links.Clear(); listening = false; if (net.Hosts.TryGetValue(Addr, out var me) && me == this) net.Hosts.Remove(Addr); inbox.Clear();
        }
        public void Send(int peer, string line)
        {
            if (!Links.TryGetValue(peer, out var l) || l.Closed) return; SentLines++;
            if (l.Dead) { DroppedLines++; return; }
            var other = l.Host == this ? l.Guest : l.Host; int op = l.Host == this ? 0 : l.HostPeer;
            if (l.Host == this && l.DropNextPublicDeltas > 0 && line.Contains("|8|") ) { var f = line.Split('|'); if (f.Length > 9 && f[5] == ((int)MsgType.StateDelta).ToString() && f[9] == "B") { l.DropNextPublicDeltas--; DroppedLines++; return; } }
            double at = net.Clock.Now + net.Latency + (net.Jitter > 0 ? net.Rnd.NextDouble() * net.Jitter : 0);
            other.Push(TransportEvent.Data(op, line), at);
            if (net.DupChance > 0 && net.Rnd.NextDouble() < net.DupChance) other.Push(TransportEvent.Data(op, line), at + net.Latency);
        }
        public void Close(int peer) { if (Links.TryGetValue(peer, out var l)) { Kill(l, true); } }
        void Kill(MemLink l, bool graceful)
        {
            if (l.Closed) return; l.Closed = true; var other = l.Host == this ? l.Guest : l.Host; int op = l.Host == this ? 0 : l.HostPeer; int mine = l.Host == this ? l.HostPeer : 0;
            Links.Remove(mine); other.Links.Remove(op);
            if (!l.Dead) other.Push(TransportEvent.Close(op, BtErr.SocketClosed), net.Clock.Now + net.Latency);
        }
        /// <summary>Test: Verbindung abrupt trennen (beide Seiten erhalten Closed).</summary>
        public void Drop(int peer) { if (Links.TryGetValue(peer, out var l)) { l.Dead = false; Kill(l, false); var o = l.Host == this ? l.Guest : l.Host; int op = l.Host == this ? 0 : l.HostPeer; Push(TransportEvent.Close(peer, BtErr.SocketClosed), net.Clock.Now + net.Latency); } }
        public MemLink LinkTo(int peer) => Links[peer];

        internal void Push(TransportEvent e, double at = -1) { inbox.Add((at < 0 ? net.Clock.Now : at, net.Ord(), e)); }
        public bool Poll(out TransportEvent e)
        {
            int best = -1; for (int i = 0; i < inbox.Count; i++) if (inbox[i].at <= net.Clock.Now && (best < 0 || inbox[i].at < inbox[best].at || inbox[i].at == inbox[best].at && inbox[i].ord < inbox[best].ord)) best = i;
            if (best < 0) { e = default; return false; }
            e = inbox[best].e; inbox.RemoveAt(best); return true;
        }
    }
}
