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
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>(); TcpClient cl; StreamWriter w; volatile bool con; volatile string st = "bereit"; readonly int port;
        public TcpBt(int port) { this.port = port; }
        public bool Supported => true; public bool Enabled => true; public bool Connected => con; public string Status => st;
        public bool EnsurePermission() => true;
        public List<BtDev> Paired() => new List<BtDev> { new BtDev { Name = "Kopfhörer", Addr = "00:00:00:00:7C:D3", Major = 0x400 }, new BtDev { Name = "Testpartner", Addr = "127.0.0.1", Major = 0x100, Glamour = true }, new BtDev { Name = "Altes Gerät", Addr = "00:00:00:00:7D:02", Major = 0x100 } };
        public void Scan() { }
        string role = "Testgerät"; public string LocalName => role;
        public void Host() { role = "Eröffner"; st = "wartet auf Mitspieler"; new Thread(() => { var l = new TcpListener(IPAddress.Loopback, port); l.Start(); var c = l.AcceptTcpClient(); l.Stop(); Run(c); }) { IsBackground = true }.Start(); }
        public void Join(string addr)
        {
            role = "Gast"; st = "verbindet ...";
            new Thread(() => { for (int i = 0; i < 100; i++) { try { Run(new TcpClient("127.0.0.1", port)); return; } catch { Thread.Sleep(100); } } st = "Verbindung fehlgeschlagen"; }) { IsBackground = true }.Start();
        }
        void Run(TcpClient c)
        {
            cl = c; var s = c.GetStream(); w = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" }; con = true; st = "verbunden";
            var r = new StreamReader(s, Encoding.UTF8); string line;
            try { while ((line = r.ReadLine()) != null) inbox.Enqueue(line); } catch { }
            con = false; st = "getrennt";
        }
        public void Stop() { con = false; try { cl?.Close(); } catch { } st = "bereit"; }
        public void Send(string line) { try { lock (this) w?.WriteLine(line); } catch { con = false; } }
        public bool Poll(out string line) => inbox.TryDequeue(out line);
    }
}
