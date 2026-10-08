#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace GlamourGames
{
    /// <summary>
    /// Bluetooth-Transport unter Windows: RFCOMM-Sockets (Winsock AF_BTH). Beim Eroeffnen wird der Dienst mit derselben
    /// UUID wie auf Android im SDP-Verzeichnis angemeldet, damit ein Handy den PC findet; beim Beitreten loest Windows die
    /// UUID beim Gegenueber selbst auf. Gekoppelte Geraete liefert die Bluetooth-API von Windows.
    /// </summary>
    sealed class WinBt : IBtTransport
    {
        static readonly Guid Service = new Guid("7a3c2f5e-9b1d-4e8a-a6f2-3d5c8b9e1f42");
        const int AF_BTH = 32, SOCK_STREAM = 1, BTHPROTO_RFCOMM = 3, NS_BTH = 16, RNRSERVICE_REGISTER = 0, RNRSERVICE_DELETE = 2;
        const uint BT_PORT_ANY = 0xFFFFFFFF; static readonly IntPtr Invalid = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct SOCKADDR_BTH { public ushort family; public ulong addr; public Guid service; public uint port; }
        [StructLayout(LayoutKind.Sequential)] struct BLOB { public int cbSize; public IntPtr pBlobData; }
        [StructLayout(LayoutKind.Sequential)]
        struct WSAQUERYSET
        {
            public int dwSize; public IntPtr lpszServiceInstanceName, lpServiceClassId, lpVersion, lpszComment; public int dwNameSpace; public IntPtr lpNSProviderId, lpszContext;
            public int dwNumberOfProtocols; public IntPtr lpafpProtocols, lpszQueryString; public int dwNumberOfCsAddrs; public IntPtr lpcsaBuffer; public int dwOutputFlags; public IntPtr lpBlob;
        }
        [StructLayout(LayoutKind.Sequential)] struct SYSTEMTIME { public ushort y, mo, dow, d, h, mi, s, ms; }
        [StructLayout(LayoutKind.Sequential)] struct SEARCH_PARAMS { public int dwSize, fReturnAuthenticated, fReturnRemembered, fReturnUnknown, fReturnConnected, fIssueInquiry; public byte cTimeoutMultiplier; public IntPtr hRadio; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVICE_INFO { public int dwSize; public ulong Address; public uint ulClassofDevice; public int fConnected, fRemembered, fAuthenticated; public SYSTEMTIME stLastSeen, stLastUsed; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string szName; }

        [DllImport("ws2_32.dll")] static extern int WSAStartup(ushort ver, byte[] data);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern IntPtr socket(int af, int type, int proto);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern int bind(IntPtr s, ref SOCKADDR_BTH a, int len);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern int listen(IntPtr s, int backlog);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern IntPtr accept(IntPtr s, IntPtr addr, IntPtr len);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern int connect(IntPtr s, ref SOCKADDR_BTH a, int len);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern int getsockname(IntPtr s, ref SOCKADDR_BTH a, ref int len);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern int recv(IntPtr s, byte[] buf, int len, int flags);
        [DllImport("ws2_32.dll", SetLastError = true)] static extern int send(IntPtr s, byte[] buf, int len, int flags);
        [DllImport("ws2_32.dll")] static extern int closesocket(IntPtr s);
        [DllImport("ws2_32.dll")] static extern int WSAGetLastError();
        [DllImport("ws2_32.dll", CharSet = CharSet.Unicode)] static extern int WSASetServiceW(ref WSAQUERYSET qs, int op, int flags);
        [DllImport("BluetoothAPIs.dll", SetLastError = true)] static extern IntPtr BluetoothFindFirstDevice(ref SEARCH_PARAMS p, ref DEVICE_INFO info);
        [DllImport("BluetoothAPIs.dll", SetLastError = true)] static extern bool BluetoothFindNextDevice(IntPtr h, ref DEVICE_INFO info);
        [DllImport("BluetoothAPIs.dll")] static extern bool BluetoothFindDeviceClose(IntPtr h);

        sealed class Peer { public int Id; public IntPtr S; public readonly object Lock = new object(); public volatile bool Closed, Reported; public int G; }

        static bool started; readonly ConcurrentQueue<TransportEvent> events = new ConcurrentQueue<TransportEvent>();
        readonly ConcurrentDictionary<int, Peer> peers = new ConcurrentDictionary<int, Peer>();
        IntPtr listenSock = Invalid; volatile string status = "bereit"; volatile int gen; volatile bool listening; volatile BtErr lastErr; int nextPeer = 1;
        WSAQUERYSET reg; bool registered; IntPtr regVer, regHandle, regSet, regBlob;

        public WinBt() { if (!started) { WSAStartup(0x0202, new byte[1024]); started = true; } }
        bool? supported;
        public bool Supported { get { if (supported == null) { var s = socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM); supported = s != Invalid; if (s != Invalid) closesocket(s); } return supported.Value; } }
        public bool Enabled => Supported;
        public int MaxPeers => 3;
        public bool Listening => listening;
        public BtErr LastError => lastErr;
        public string Status => status;
        public bool EnsurePermission() => true;

        public void Scan() { }
        public string LocalName => Environment.MachineName;
        public List<BtDev> Paired()
        {
            var l = new List<BtDev>();
            try
            {
                var p = new SEARCH_PARAMS { fReturnAuthenticated = 1, fReturnRemembered = 1, fReturnConnected = 1 }; p.dwSize = Marshal.SizeOf(p);
                var d = new DEVICE_INFO(); d.dwSize = Marshal.SizeOf(d);
                var h = BluetoothFindFirstDevice(ref p, ref d); if (h == IntPtr.Zero) return l;
                do { l.Add(new BtDev { Name = string.IsNullOrEmpty(d.szName) ? Addr(d.Address) : d.szName, Addr = Addr(d.Address), Major = (int)(d.ulClassofDevice & 0x1F00) }); d = new DEVICE_INFO(); d.dwSize = Marshal.SizeOf(d); } while (BluetoothFindNextDevice(h, ref d));
                BluetoothFindDeviceClose(h);
            }
            catch (Exception e) { status = "Fehler: " + e.Message; }
            return l;
        }
        static string Addr(ulong a) { var b = new string[6]; for (int i = 0; i < 6; i++) b[5 - i] = ((a >> (8 * i)) & 0xFF).ToString("X2"); return string.Join(":", b); }
        static ulong Addr(string s) { ulong v = 0; foreach (var part in s.Split(':')) v = (v << 8) | Convert.ToUInt64(part, 16); return v; }

        /// <summary>Host: Dienst anmelden, Accept-Schleife nimmt bis MaxPeers Gaeste an und bleibt offen (Wiederverbindung).</summary>
        public void StartHost()
        {
            Stop(); int g = ++gen; lastErr = BtErr.None;
            var s = socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM); if (s == Invalid) { status = "Bluetooth nicht verfügbar (" + WSAGetLastError() + ")"; lastErr = BtErr.NoAdapter; events.Enqueue(TransportEvent.Close(0, BtErr.NoAdapter, status)); return; }
            var sa = new SOCKADDR_BTH { family = AF_BTH, port = BT_PORT_ANY }; int len = Marshal.SizeOf(sa);
            if (bind(s, ref sa, len) != 0 || getsockname(s, ref sa, ref len) != 0 || listen(s, MaxPeers) != 0) { status = "Fehler beim Eröffnen (" + WSAGetLastError() + ")"; lastErr = BtErr.Unknown; closesocket(s); events.Enqueue(TransportEvent.Close(0, BtErr.Unknown, status)); return; }
            listenSock = s; Register(sa); status = "wartet auf Mitspieler"; listening = true;
            new Thread(() =>
            {
                while (g == gen)
                {
                    var c = accept(s, IntPtr.Zero, IntPtr.Zero);
                    if (g != gen) { if (c != Invalid) closesocket(c); break; }
                    if (c == Invalid) { if (g == gen) { status = "Fehler beim Warten (" + WSAGetLastError() + ")"; lastErr = BtErr.Unknown; } break; }
                    if (peers.Count >= MaxPeers) { closesocket(c); continue; }
                    Attach(c, g, Interlocked.Increment(ref nextPeer) - 1, "Gast");
                }
            }) { IsBackground = true, Name = "WinBt-accept" }.Start();
        }

        public void Connect(string addr)
        {
            Stop(); int g = ++gen; status = "verbindet ..."; lastErr = BtErr.None;
            new Thread(() =>
            {
                var s = socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM); if (s == Invalid) { status = "Bluetooth nicht verfügbar"; if (g == gen) events.Enqueue(TransportEvent.Close(0, BtErr.NoAdapter)); return; }
                var sa = new SOCKADDR_BTH { family = AF_BTH, addr = Addr(addr), service = Service, port = 0 };
                if (connect(s, ref sa, Marshal.SizeOf(sa)) != 0) { int e = WSAGetLastError(); closesocket(s); if (g == gen) { status = "Verbindung fehlgeschlagen (" + e + ")"; lastErr = e == 10060 ? BtErr.Timeout : BtErr.ConnectFailed; events.Enqueue(TransportEvent.Close(0, lastErr, "WSA " + e)); } return; }
                if (g != gen) { closesocket(s); return; }
                Attach(s, g, 0, addr);
            }) { IsBackground = true, Name = "WinBt-join" }.Start();
        }

        void Attach(IntPtr s, int g, int id, string name)
        {
            var p = new Peer { Id = id, S = s, G = g }; peers[id] = p; status = "verbunden";
            events.Enqueue(TransportEvent.Conn(id, name, name));
            new Thread(() =>
            {
                var buf = new byte[4096]; var acc = new List<byte>(); BtErr why = BtErr.SocketClosed;
                while (!p.Closed && g == gen)
                {
                    int n = recv(s, buf, buf.Length, 0); if (n < 0) { why = p.Closed ? BtErr.SocketClosed : BtErr.ReadFailed; break; } if (n == 0) break;
                    for (int i = 0; i < n; i++)
                    {
                        if (buf[i] == (byte)'\n') { if (g == gen) events.Enqueue(TransportEvent.Data(id, Encoding.UTF8.GetString(acc.ToArray()).TrimEnd('\r'))); acc.Clear(); }
                        else if (acc.Count < 40000) acc.Add(buf[i]); else { why = BtErr.ReadFailed; p.Closed = true; break; }
                    }
                }
                Finish(p, why);
            }) { IsBackground = true, Name = "WinBt-read-" + id }.Start();
        }

        void Finish(Peer p, BtErr why)
        {
            lock (p.Lock) { if (p.Reported) return; p.Reported = true; p.Closed = true; closesocket(p.S); }
            peers.TryRemove(p.Id, out _);
            if (p.G == gen) { events.Enqueue(TransportEvent.Close(p.Id, why)); if (peers.IsEmpty && !listening) status = "getrennt"; }
        }

        public void Send(int peer, string line)
        {
            if (!peers.TryGetValue(peer, out var p) || p.Closed) return; var b = Encoding.UTF8.GetBytes(line + "\n");
            lock (p.Lock)
            {
                int off = 0;
                while (off < b.Length && !p.Closed)
                {
                    var part = b; if (off > 0) { part = new byte[b.Length - off]; Array.Copy(b, off, part, 0, part.Length); }
                    int n = send(p.S, part, part.Length, 0); if (n <= 0) { p.Closed = true; closesocket(p.S); break; }
                    off += n;
                }
            }
            // Schreibfehler: Lese-Thread bemerkt das geschlossene Socket und meldet Closed
        }

        /// <summary>Ordentlich beenden: Senden ist synchron (Daten sind im System-Puffer), dann Socket schliessen; Lese-Thread meldet Closed.</summary>
        public void Close(int peer) { if (peers.TryGetValue(peer, out var p)) { Thread.Sleep(30); Finish(p, BtErr.SocketClosed); } }

        public bool Poll(out TransportEvent e) => events.TryDequeue(out e);

        public void Stop()
        {
            gen++; listening = false; Unregister();
            if (listenSock != Invalid) { closesocket(listenSock); listenSock = Invalid; }
            foreach (var p in peers.Values) { lock (p.Lock) { p.Closed = true; p.Reported = true; closesocket(p.S); } }
            peers.Clear(); while (events.TryDequeue(out _)) { }
            Interlocked.Exchange(ref nextPeer, 1); status = "bereit";
        }

        // ---- SDP-Anmeldung des Dienstes (damit Android per UUID verbinden kann)
        // Fertiger SDP-Datensatz (BTH_SET_SERVICE): Dienstklasse = unsere UUID, Protokolle L2CAP + RFCOMM-Kanal, Name.
        // Die einfache Anmeldung nur mit Dienstklasse und Adresse lehnt Windows teils mit WSAEINVAL (10022) ab.
        static byte[] SdpRecord(byte channel)
        {
            var u = Service.ToByteArray(); var be = new byte[16];
            be[0] = u[3]; be[1] = u[2]; be[2] = u[1]; be[3] = u[0]; be[4] = u[5]; be[5] = u[4]; be[6] = u[7]; be[7] = u[6]; Array.Copy(u, 8, be, 8, 8);
            var b = new List<byte> { 0x09, 0x00, 0x01, 0x35, 0x11, 0x1C }; b.AddRange(be);                                         // ServiceClassIDList
            b.AddRange(new byte[] { 0x09, 0x00, 0x04, 0x35, 0x0C, 0x35, 0x03, 0x19, 0x01, 0x00, 0x35, 0x05, 0x19, 0x00, 0x03, 0x08, channel }); // L2CAP, RFCOMM(Kanal)
            b.AddRange(new byte[] { 0x09, 0x00, 0x05, 0x35, 0x03, 0x19, 0x10, 0x02 });                                           // PublicBrowseGroup
            var name = Encoding.ASCII.GetBytes("Glamour Games"); b.AddRange(new byte[] { 0x09, 0x01, 0x00, 0x25, (byte)name.Length }); b.AddRange(name);
            var r = new List<byte> { 0x35, (byte)b.Count }; r.AddRange(b); return r.ToArray();
        }
        void Register(SOCKADDR_BTH local)
        {
            var rec = SdpRecord((byte)local.port); const int Hdr = 44;   // BTH_SET_SERVICE (x64): pSdpVersion, pRecordHandle, fCodService, Reserved[5], ulRecordLength, pRecord[]
            regVer = Marshal.AllocHGlobal(4); Marshal.WriteInt32(regVer, 1);
            regHandle = Marshal.AllocHGlobal(8); Marshal.WriteInt64(regHandle, 0);
            regSet = Marshal.AllocHGlobal(Hdr + rec.Length); for (int i = 0; i < Hdr; i++) Marshal.WriteByte(regSet, i, 0);
            Marshal.WriteIntPtr(regSet, 0, regVer); Marshal.WriteIntPtr(regSet, 8, regHandle); Marshal.WriteInt32(regSet, 40, rec.Length);
            Marshal.Copy(rec, 0, regSet + Hdr, rec.Length);
            var blob = new BLOB { cbSize = Hdr + rec.Length, pBlobData = regSet }; regBlob = Marshal.AllocHGlobal(Marshal.SizeOf(blob)); Marshal.StructureToPtr(blob, regBlob, false);
            reg = new WSAQUERYSET { dwNameSpace = NS_BTH, lpBlob = regBlob }; reg.dwSize = Marshal.SizeOf(reg);
            registered = WSASetServiceW(ref reg, RNRSERVICE_REGISTER, 0) == 0;
            Log.I(registered ? $"bluetooth: SDP angemeldet (Kanal {local.port})" : "bluetooth: SDP-Anmeldung fehlgeschlagen " + WSAGetLastError());
        }
        void Unregister()
        {
            if (registered) { WSASetServiceW(ref reg, RNRSERVICE_DELETE, 0); registered = false; }
            foreach (var p in new[] { regVer, regHandle, regSet, regBlob }) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
            regVer = regHandle = regSet = regBlob = IntPtr.Zero;
        }
    }
}
#endif
