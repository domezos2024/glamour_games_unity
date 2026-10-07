using System;
using System.Collections.Generic;

namespace GlamourGames
{
    /// <summary>Plattformtransport fuer Bluetooth (RFCOMM): Android-Plugin oder Windows-Winsock.</summary>
    public interface IBtTransport
    {
        bool Supported { get; }
        bool Enabled { get; }
        bool Connected { get; }
        string Status { get; }
        /// <summary>Gekoppelte Geraete.</summary>
        List<BtDev> Paired();
        /// <summary>Dienstliste der gekoppelten PCs/Handys neu abfragen (Android: SDP), damit Glamour-Geraete erkennbar sind.</summary>
        void Scan();
        /// <summary>Bluetooth-Name dieses Geraets (so sieht es der Mitspieler in seiner Liste).</summary>
        string LocalName { get; }
        void Host();
        void Join(string addr);
        void Stop();
        void Send(string line);
        bool Poll(out string line);
        /// <summary>Fehlende Laufzeit-Berechtigungen anfragen; true = vorhanden.</summary>
        bool EnsurePermission();
    }

    /// <summary>Gekoppeltes Geraet: Klasse 0x100 = Computer, 0x200 = Telefon (Bluetooth-Hauptklasse); Glamour = bietet den Spieldienst an (zuletzt bekannte Dienstliste).</summary>
    public sealed class BtDev
    {
        public string Name, Addr; public int Major; public bool Glamour;
        public bool Player => Glamour || Major == 0x100 || Major == 0x200 || Major == 0 || Major == 0x1F00;
        public string Kind => Major == 0x100 ? "PC" : Major == 0x200 ? "Handy" : "Gerät";
        /// <summary>Kurzform der Adresse (letzte zwei Bytes), um gleichnamige Eintraege zu unterscheiden.</summary>
        public string Short => Addr != null && Addr.Length >= 5 ? Addr.Substring(Addr.Length - 5) : Addr;
    }

    /// <summary>
    /// Mehrspieler-Verbindung zweier Geraete (PC oder Handy) per Bluetooth. Nach dem Verbinden tauschen beide ihren Namen
    /// aus (HELLO). Der Eroeffner (Host) waehlt das Spiel (GO), der Beitretende folgt automatisch. Spielnachrichten
    /// ("G|spiel|art|daten...") werden der laufenden Spielszene zugestellt, sobald sie aktiv ist.
    /// Jedes Geraet sieht sich selbst als Spieler 1; der Mitspieler sitzt auf Platz 2 (wie sonst der Computer).
    /// </summary>
    public static class Link
    {
        public const string Proto = "1";
        public static IBtTransport T;
        public static bool IsHost { get; private set; }
        public static string PeerName = "Mitspieler";
        /// <summary>Eigener Name im Bluetooth-Spiel: der in den Optionen gesetzte Name, sonst der Bluetooth-Name des Geraets.</summary>
        public static string MyName { get { var s = Save.Str("pname0", "").Trim(); if (s.Length > 0) return s; var n = T?.LocalName; return string.IsNullOrWhiteSpace(n) ? Pl.Name(0) : n.Trim(); } }
        static bool hello, wasConnected;
        public static bool Connected => T != null && T.Connected && hello;
        public static bool Busy => T != null && (T.Connected || T.Status.StartsWith("wartet") || T.Status.StartsWith("verbindet"));
        public static string Status => T == null ? "Bluetooth wird auf diesem Gerät nicht unterstützt" : T.Status;
        static readonly List<string[]> queue = new List<string[]>();
        /// <summary>Gemeinsamer Startwert fuer Zufall (vom Host festgelegt), z. B. Kartenmischen.</summary>
        public static int Seed;
        /// <summary>Szenenwechsel kam vom Host (GO/MENU): der Mitspieler meldet dann kein "verlassen".</summary>
        public static bool HostDriven;

        public static void Init()
        {
            if (T != null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            T = new AndroidBt();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            T = new WinBt();
#endif
            if (T != null && !T.Supported) Log.I("bluetooth: kein Adapter");
        }

        public static void Host() { Init(); if (T == null || !T.EnsurePermission()) return; IsHost = true; hello = false; queue.Clear(); T.Host(); Log.I("bluetooth: eroeffnet"); }
        public static void Join(string addr) { Init(); if (T == null || !T.EnsurePermission()) return; IsHost = false; hello = false; queue.Clear(); T.Join(addr); Log.I("bluetooth: tritt bei " + addr); }
        public static void Stop() { if (T == null) return; if (Connected) Raw("BYE"); T.Stop(); hello = false; wasConnected = false; queue.Clear(); Log.I("bluetooth: getrennt"); }

        static string Esc(object o) => (o?.ToString() ?? "").Replace('|', '/').Replace('\n', ' ');
        static void Raw(params object[] parts) { if (T == null || !T.Connected) return; var a = new string[parts.Length]; for (int i = 0; i < a.Length; i++) a[i] = Esc(parts[i]); T.Send(string.Join("|", a)); }

        /// <summary>Spielnachricht fuer das Spiel key senden.</summary>
        public static void Game(string key, string kind, params object[] data)
        {
            var parts = new object[data.Length + 3]; parts[0] = "G"; parts[1] = key; parts[2] = kind; Array.Copy(data, 0, parts, 3, data.Length);
            Raw(parts); Log.I($"bt> {key} {kind} {string.Join(",", data)}");
        }

        /// <summary>Host startet Spiel i (Registry-Index) beim Mitspieler.</summary>
        public static void Go(int game) { Seed = Rng.I(1, int.MaxValue); queue.Clear(); Raw("GO", game, Seed); }

        /// <summary>Jeden Frame: Nachrichten abholen, Verbindungswechsel melden, Spielnachrichten zustellen.</summary>
        public static void Update()
        {
            if (T == null) return;
            if (T.Connected && !wasConnected) { wasConnected = true; Raw("HELLO", MyName, Proto); }
            if (!T.Connected && wasConnected)
            {
                wasConnected = false; bool had = hello; hello = false; queue.Clear();
                if (had) { App.Toast($"Verbindung zu {PeerName} getrennt"); App.Current?.NetLost(); }
            }
            int n = 0;
            while (n++ < 64 && T.Poll(out var line))
            {
                var a = line.Split('|');
                switch (a[0])
                {
                    case "HELLO": PeerName = a.Length > 1 && a[1].Length > 0 ? a[1] : "Mitspieler"; if (PeerName == MyName) PeerName += " (2)"; hello = true; App.Toast($"Verbunden mit {PeerName}"); Sfx.Play(S.Match, .6f); Log.I("bluetooth: verbunden mit " + PeerName); break;
                    case "GO": if (!IsHost && a.Length > 2 && int.TryParse(a[1], out int gi) && gi >= 0 && gi < Registry.All.Count) { int.TryParse(a[2], out Seed); queue.Clear(); HostDriven = true; App.Go(Registry.All[gi].Make()); } break;
                    case "MENU": if (!IsHost) { queue.Clear(); HostDriven = true; App.Go(new Menu()); } break;
                    case "BYE": App.Toast($"{PeerName} hat die Verbindung beendet"); T.Stop(); hello = false; wasConnected = false; App.Current?.NetLost(); break;
                    case "G": if (a.Length >= 3) queue.Add(a); break;
                }
            }
            // Zustellung an die aktive Szene (Nachrichten fuer ein noch nicht geladenes Spiel warten)
            var cur = App.Current; if (cur == null || queue.Count == 0) return;
            for (int i = 0; i < queue.Count; i++)
            {
                var a = queue[i]; if (a[1] != cur.OppKey) continue;
                queue.RemoveAt(i--); var data = new string[a.Length - 3]; Array.Copy(a, 3, data, 0, data.Length);
                Log.I($"bt< {a[1]} {a[2]} {string.Join(",", data)}");
                try { cur.NetRecv(a[2], data); } catch (Exception e) { Log.I("bt: Fehler in " + a[2] + ": " + e.Message); }
            }
        }

        /// <summary>Host meldet dem Mitspieler die Rueckkehr ins Menue.</summary>
        public static void BackToMenu() { if (Connected && IsHost) Raw("MENU"); }
        public static int Int(string s) => int.TryParse(s, out int v) ? v : 0;
        public static float Flt(string s) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0;
        public static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
