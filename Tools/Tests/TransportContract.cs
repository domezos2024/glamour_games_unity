using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GlamourGames.Tests
{
    /// <summary>Gemeinsame Vertragstests fuer jeden ISessionTransport (Mehr-Peer-Semantik: Stern mit bis zu 3 Gaesten, Peer-Nummern, Schliessen, Reihenfolge).</summary>
    public interface IContractEnv
    {
        string Name { get; }
        ISessionTransport NewHost();
        ISessionTransport NewGuest(int i);
        /// <summary>Zeit vergehen lassen (simuliert oder echt).</summary>
        void Wait(double seconds);
        /// <summary>Verbindungsversuch zu einem nicht vorhandenen Host wird als Closed gemeldet (in der Umgebung pruefbar).</summary>
        bool CanTestNoHost { get; }
        void Connect(ISessionTransport guest);
        void ConnectNowhere(ISessionTransport guest);
    }

    public sealed class MemEnv : IContractEnv
    {
        readonly MemNet net = new MemNet();
        public string Name => "MemTransport";
        public ISessionTransport NewHost() { var h = net.Create("host"); return h; }
        public ISessionTransport NewGuest(int i) => net.Create("g" + i);
        public void Wait(double s) { for (double t = 0; t < s; t += .02) net.Clock.Now += .02; }
        public bool CanTestNoHost => true;
        public void Connect(ISessionTransport g) => g.Connect("host");
        public void ConnectNowhere(ISessionTransport g) => g.Connect("nirgends");
    }

    public sealed class TcpEnv : IContractEnv
    {
        const int Port = 49321;
        public string Name => "TcpBt (Vorschau-Transport)";
        public ISessionTransport NewHost() => new TcpBt(Port);
        public ISessionTransport NewGuest(int i) => new TcpBt(Port);
        public void Wait(double s) => Thread.Sleep((int)(s * 1000));
        public bool CanTestNoHost => false;
        public void Connect(ISessionTransport g) => g.Connect("127.0.0.1");
        public void ConnectNowhere(ISessionTransport g) { }
    }

    public static class TransportContract
    {
        static List<TransportEvent> Drain(ISessionTransport t, List<TransportEvent> sink) { while (t.Poll(out var e)) sink.Add(e); return sink; }

        public static void Run(IContractEnv env)
        {
            T.Cur = "Transportvertrag " + env.Name; Console.WriteLine("> " + T.Cur);
            var host = env.NewHost(); var hev = new List<TransportEvent>();
            host.StartHost(); env.Wait(.3);
            T.True(host.Listening, "Host nimmt Verbindungen an");
            T.Eq(3, host.MaxPeers, "Host erlaubt 3 Gaeste");

            // Drei Gaeste verbinden
            var g = new List<ISessionTransport>(); var gev = new List<List<TransportEvent>>();
            for (int i = 0; i < 3; i++) { var x = env.NewGuest(i); g.Add(x); gev.Add(new List<TransportEvent>()); env.Connect(x); env.Wait(.4); Drain(host, hev); Drain(x, gev[i]); }
            var peers = hev.Where(e => e.Kind == TEvKind.Connected).Select(e => e.Peer).ToList();
            T.Eq(3, peers.Count, "Host meldet drei Verbindungen");
            T.True(peers.Distinct().Count() == 3 && peers.All(p => p >= 1), "Peer-Nummern eindeutig und ab 1");
            for (int i = 0; i < 3; i++) T.True(gev[i].Any(e => e.Kind == TEvKind.Connected && e.Peer == 0), $"Gast {i + 1} sieht den Host als Peer 0");

            // Zeilen: Sonderzeichen und Reihenfolge, in beide Richtungen
            string special = "GL2|ä ö ü ß|100% \t Ende";
            g[0].Send(0, special); host.Send(peers[1], special); env.Wait(.3);
            Drain(host, hev); Drain(g[1], gev[1]);
            T.True(hev.Any(e => e.Kind == TEvKind.Line && e.Peer == peers[0] && e.Text == special), "Gast -> Host: Zeile unveraendert");
            T.True(gev[1].Any(e => e.Kind == TEvKind.Line && e.Peer == 0 && e.Text == special), "Host -> Gast: Zeile unveraendert");
            hev.Clear(); gev[2].Clear();
            for (int i = 0; i < 100; i++) { g[2].Send(0, "n" + i); host.Send(peers[2], "m" + i); }
            env.Wait(.8); Drain(host, hev); Drain(g[2], gev[2]);
            var fromGuest = hev.Where(e => e.Kind == TEvKind.Line && e.Peer == peers[2]).Select(e => e.Text).ToList();
            var fromHost = gev[2].Where(e => e.Kind == TEvKind.Line).Select(e => e.Text).ToList();
            T.True(fromGuest.SequenceEqual(Enumerable.Range(0, 100).Select(i => "n" + i)), "100 Zeilen Gast -> Host in Reihenfolge");
            T.True(fromHost.SequenceEqual(Enumerable.Range(0, 100).Select(i => "m" + i)), "100 Zeilen Host -> Gast in Reihenfolge");

            // Host schliesst einen Peer: Gast bekommt Closed; vorher gesendete Zeile kommt noch an
            gev[0].Clear(); host.Send(peers[0], "letzte"); host.Close(peers[0]); env.Wait(.5); Drain(g[0], gev[0]);
            T.True(gev[0].Any(e => e.Kind == TEvKind.Line && e.Text == "letzte"), "Close(peer) leert die Sendewarteschlange zuerst");
            T.True(gev[0].Any(e => e.Kind == TEvKind.Closed), "Gast sieht das Ende der Verbindung");

            // Gast beendet: Host sieht Closed fuer genau diesen Peer
            hev.Clear(); g[1].Stop(); env.Wait(.5); Drain(host, hev);
            T.True(hev.Any(e => e.Kind == TEvKind.Closed && e.Peer == peers[1]), "Host sieht das Ende von Gast 2");
            T.True(!hev.Any(e => e.Kind == TEvKind.Closed && e.Peer == peers[2]), "Gast 3 bleibt verbunden");

            // Peer-Nummern werden nicht wiederverwendet
            hev.Clear(); var neu = env.NewGuest(9); env.Connect(neu); env.Wait(.5); Drain(host, hev);
            var np = hev.Where(e => e.Kind == TEvKind.Connected).Select(e => e.Peer).ToList();
            T.True(np.Count == 1 && !peers.Contains(np[0]), "Neue Verbindung bekommt eine unbenutzte Peer-Nummer");

            // Host stoppt: alle Gaeste sehen Closed
            gev[2].Clear(); host.Stop(); env.Wait(.5); Drain(g[2], gev[2]);
            T.True(gev[2].Any(e => e.Kind == TEvKind.Closed), "Host.Stop beendet alle Verbindungen");
            T.True(!host.Listening, "Nach Stop kein Listening mehr");
            foreach (var x in g) x.Stop(); neu.Stop();

            if (env.CanTestNoHost)
            {
                var lost = env.NewGuest(20); var lev = new List<TransportEvent>(); env.ConnectNowhere(lost); env.Wait(.5); Drain(lost, lev);
                T.True(lev.Any(e => e.Kind == TEvKind.Closed && e.Err == BtErr.ConnectFailed), "Verbindung zu fehlendem Host: Closed(ConnectFailed)");
            }
        }
    }
}
