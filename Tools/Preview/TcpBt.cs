// Ersatz-Transport fuer Mehrspieler-Tests ohne Bluetooth: zwei Vorschau-Prozesse verbinden sich ueber TCP (localhost).
//   Host:      --net=host:47123     Mitspieler: --net=join:47123
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GlamourGames
{
    sealed class TcpBt : IBtTransport
    {
        sealed class P { public int Id; public TcpClient C; public StreamWriter W; public volatile bool Closed; }
        readonly ConcurrentQueue<TransportEvent> events = new ConcurrentQueue<TransportEvent>(); readonly ConcurrentDictionary<int, P> peers = new ConcurrentDictionary<int, P>();
        volatile string st = "bereit"; readonly int port; volatile int gen; int nextPeer = 1; TcpListener lis; volatile bool listening;
        public TcpBt(int port) { this.port = port; }
        public bool Supported => true; public bool Enabled => true; public string Status => st; public BtErr LastError => BtErr.None; public int MaxPeers => 3; public bool Listening => listening;
        public bool EnsurePermission() => true;
        public List<BtDev> Paired() => new List<BtDev> { new BtDev { Name = "Kopfhörer", Addr = "00:00:00:00:7C:D3", Major = 0x400 }, new BtDev { Name = "Testpartner", Addr = "127.0.0.1", Major = 0x100, Glamour = true }, new BtDev { Name = "Altes Gerät", Addr = "00:00:00:00:7D:02", Major = 0x100 } };
        public void Scan() { }
        string role = "Testgerät"; public string LocalName => role;
        public void StartHost()
        {
            Stop(); int g = ++gen; role = "Eröffner"; st = "wartet auf Mitspieler"; listening = true;
            new Thread(() =>
            {
                try
                {
                    var l = new TcpListener(IPAddress.Loopback, port); lis = l; l.Start();
                    while (g == gen)
                    {
                        var c = l.AcceptTcpClient(); if (g != gen) { c.Close(); break; }
                        if (peers.Count >= MaxPeers) { c.Close(); continue; }
                        Attach(c, g, Interlocked.Increment(ref nextPeer) - 1, "Gast");
                    }
                }
                catch { }
            }) { IsBackground = true }.Start();
        }
        public void Connect(string addr)
        {
            Stop(); int g = ++gen; role = "Gast"; st = "verbindet ...";
            new Thread(() =>
            {
                for (int i = 0; i < 100 && g == gen; i++) { try { Attach(new TcpClient("127.0.0.1", port), g, 0, "Host"); return; } catch { Thread.Sleep(100); } }
                if (g == gen) { st = "Verbindung fehlgeschlagen"; events.Enqueue(TransportEvent.Close(0, BtErr.ConnectFailed)); }
            }) { IsBackground = true }.Start();
        }
        void Attach(TcpClient c, int g, int id, string name)
        {
            var s = c.GetStream(); var p = new P { Id = id, C = c, W = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" } }; peers[id] = p; st = "verbunden";
            events.Enqueue(TransportEvent.Conn(id, name, "127.0.0.1"));
            new Thread(() =>
            {
                var r = new StreamReader(s, Encoding.UTF8); string line;
                try { while ((line = r.ReadLine()) != null) { if (g == gen) events.Enqueue(TransportEvent.Data(id, line)); } } catch { }
                Fin(p, g);
            }) { IsBackground = true }.Start();
        }
        void Fin(P p, int g) { lock (p) { if (p.Closed) return; p.Closed = true; } try { p.C.Close(); } catch { } peers.TryRemove(p.Id, out _); if (g == gen) events.Enqueue(TransportEvent.Close(p.Id, BtErr.SocketClosed)); }
        public void Stop() { gen++; listening = false; try { lis?.Stop(); } catch { } foreach (var p in peers.Values) { p.Closed = true; try { p.C.Close(); } catch { } } peers.Clear(); while (events.TryDequeue(out _)) { } Interlocked.Exchange(ref nextPeer, 1); st = "bereit"; }
        public void Send(int peer, string line) { if (peers.TryGetValue(peer, out var p) && !p.Closed) { try { lock (p) p.W.WriteLine(line); } catch { } } }
        public void Close(int peer) { if (peers.TryGetValue(peer, out var p)) { Thread.Sleep(20); Fin(p, gen); } }
        public bool Poll(out TransportEvent e) => events.TryDequeue(out e);
    }
}
