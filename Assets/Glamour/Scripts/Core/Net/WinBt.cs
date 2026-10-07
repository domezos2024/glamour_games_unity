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

        static bool started; readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>(); readonly object sendLock = new object();
        IntPtr listenSock = Invalid, sock = Invalid; volatile bool connected; volatile string status = "bereit"; volatile int gen;
        WSAQUERYSET reg; bool registered; IntPtr regVer, regHandle, regSet, regBlob;

        public WinBt() { if (!started) { WSAStartup(0x0202, new byte[1024]); started = true; } }
        bool? supported;
        public bool Supported { get { if (supported == null) { var s = socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM); supported = s != Invalid; if (s != Invalid) closesocket(s); } return supported.Value; } }
        public bool Enabled => Supported;
        public bool Connected => connected;
        public string Status => status;
        public bool EnsurePermission() => true;

        public List<(string, string)> Paired()
        {
            var l = new List<(string, string)>();
            try
            {
                var p = new SEARCH_PARAMS { fReturnAuthenticated = 1, fReturnRemembered = 1, fReturnConnected = 1 }; p.dwSize = Marshal.SizeOf(p);
                var d = new DEVICE_INFO(); d.dwSize = Marshal.SizeOf(d);
                var h = BluetoothFindFirstDevice(ref p, ref d); if (h == IntPtr.Zero) return l;
                do { l.Add((string.IsNullOrEmpty(d.szName) ? Addr(d.Address) : d.szName, Addr(d.Address))); d = new DEVICE_INFO(); d.dwSize = Marshal.SizeOf(d); } while (BluetoothFindNextDevice(h, ref d));
                BluetoothFindDeviceClose(h);
            }
            catch (Exception e) { status = "Fehler: " + e.Message; }
            return l;
        }
        static string Addr(ulong a) { var b = new string[6]; for (int i = 0; i < 6; i++) b[5 - i] = ((a >> (8 * i)) & 0xFF).ToString("X2"); return string.Join(":", b); }
        static ulong Addr(string s) { ulong v = 0; foreach (var part in s.Split(':')) v = (v << 8) | Convert.ToUInt64(part, 16); return v; }

        public void Host()
        {
            Stop(); int g = ++gen;
            var s = socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM); if (s == Invalid) { status = "Bluetooth nicht verfügbar (" + WSAGetLastError() + ")"; return; }
            var sa = new SOCKADDR_BTH { family = AF_BTH, port = BT_PORT_ANY }; int len = Marshal.SizeOf(sa);
            if (bind(s, ref sa, len) != 0 || getsockname(s, ref sa, ref len) != 0 || listen(s, 1) != 0) { status = "Fehler beim Eröffnen (" + WSAGetLastError() + ")"; closesocket(s); return; }
            listenSock = s; Register(sa); status = "wartet auf Mitspieler";
            new Thread(() =>
            {
                var c = accept(s, IntPtr.Zero, IntPtr.Zero);
                if (g != gen) { if (c != Invalid) closesocket(c); return; }
                Unregister(); closesocket(s); listenSock = Invalid;
                if (c == Invalid) { status = "Fehler beim Warten (" + WSAGetLastError() + ")"; return; }
                Run(c, g);
            }) { IsBackground = true, Name = "WinBt-host" }.Start();
        }

        public void Join(string addr)
        {
            Stop(); int g = ++gen; status = "verbindet ...";
            new Thread(() =>
            {
                var s = socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM); if (s == Invalid) { status = "Bluetooth nicht verfügbar"; return; }
                var sa = new SOCKADDR_BTH { family = AF_BTH, addr = Addr(addr), service = Service, port = 0 };
                if (connect(s, ref sa, Marshal.SizeOf(sa)) != 0) { if (g == gen) status = "Verbindung fehlgeschlagen (" + WSAGetLastError() + ")"; closesocket(s); return; }
                if (g != gen) { closesocket(s); return; }
                Run(s, g);
            }) { IsBackground = true, Name = "WinBt-join" }.Start();
        }

        void Run(IntPtr s, int g)
        {
            sock = s; connected = true; status = "verbunden";
            var buf = new byte[4096]; var acc = new List<byte>();
            while (g == gen)
            {
                int n = recv(s, buf, buf.Length, 0); if (n <= 0) break;
                for (int i = 0; i < n; i++)
                {
                    if (buf[i] == (byte)'\n') { inbox.Enqueue(Encoding.UTF8.GetString(acc.ToArray()).TrimEnd('\r')); acc.Clear(); }
                    else acc.Add(buf[i]);
                }
            }
            if (g == gen) { connected = false; status = "getrennt"; closesocket(s); sock = Invalid; }
        }

        public void Send(string line)
        {
            var s = sock; if (!connected || s == Invalid) return; var b = Encoding.UTF8.GetBytes(line + "\n");
            lock (sendLock) { int off = 0; while (off < b.Length) { var part = b; if (off > 0) { part = new byte[b.Length - off]; Array.Copy(b, off, part, 0, part.Length); } int n = send(s, part, part.Length, 0); if (n <= 0) { connected = false; status = "getrennt"; return; } off += n; } }
        }
        public bool Poll(out string line) => inbox.TryDequeue(out line);

        public void Stop()
        {
            gen++; connected = false; Unregister();
            if (listenSock != Invalid) { closesocket(listenSock); listenSock = Invalid; }
            if (sock != Invalid) { closesocket(sock); sock = Invalid; }
            while (inbox.TryDequeue(out _)) { }
            status = "bereit";
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
