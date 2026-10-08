using System;
using System.Collections.Generic;

namespace GlamourGames
{
    /// <summary>Strukturierte Transportfehler (Lese-/Schreibfehler, Socket-Ende, Berechtigung, Adapter).</summary>
    public enum BtErr { None = 0, PermissionMissing = 1, BluetoothOff = 2, NoAdapter = 3, ConnectFailed = 4, Timeout = 5, SocketClosed = 6, ReadFailed = 7, WriteFailed = 8, Full = 9, Unknown = 10 }

    public enum TEvKind { Connected, Line, Closed }

    /// <summary>Ereignis eines Transports. Peer 0 = Host (Gast-Sicht); als Host sind Gaeste 1, 2, 3 ... (Nummern werden nicht wiederverwendet).</summary>
    public struct TransportEvent
    {
        public TEvKind Kind; public int Peer; public string Text, Addr; public BtErr Err;
        public static TransportEvent Conn(int peer, string name, string addr) => new TransportEvent { Kind = TEvKind.Connected, Peer = peer, Text = name, Addr = addr };
        public static TransportEvent Data(int peer, string line) => new TransportEvent { Kind = TEvKind.Line, Peer = peer, Text = line };
        public static TransportEvent Close(int peer, BtErr err, string text = null) => new TransportEvent { Kind = TEvKind.Closed, Peer = peer, Err = err, Text = text };
    }

    /// <summary>
    /// Transportunabhaengige Mehr-Verbindungs-Schicht: ein Host nimmt bis zu MaxPeers Gaeste an (Stern), ein Gast verbindet sich mit genau
    /// einem Host. Jede Zeile ist eine Nachricht. Alle Ereignisse kommen ueber Poll (thread-sicher, vom Spiel-Thread abzuholen).
    /// </summary>
    public interface ISessionTransport
    {
        bool Supported { get; }
        bool Enabled { get; }
        string Status { get; }
        BtErr LastError { get; }
        int MaxPeers { get; }
        /// <summary>Host nimmt weiter Verbindungen an (auch waehrend des Spiels, fuer Wiederverbindung).</summary>
        bool Listening { get; }
        void StartHost();
        /// <summary>Als Gast mit dem Host verbinden (alte Verbindung wird ersetzt). Ergebnis als Connected- oder Closed-Ereignis.</summary>
        void Connect(string addr);
        /// <summary>Alles schliessen (Host-Dienst und alle Verbindungen).</summary>
        void Stop();
        void Send(int peer, string line);
        void Close(int peer);
        bool Poll(out TransportEvent e);
    }

    /// <summary>Bluetooth-Variante mit Geraeteliste (Android-Plugin, Windows-Winsock).</summary>
    public interface IBtTransport : ISessionTransport
    {
        /// <summary>Gekoppelte Geraete.</summary>
        List<BtDev> Paired();
        /// <summary>Dienstliste der gekoppelten PCs/Handys neu abfragen (Android: SDP), damit Glamour-Geraete erkennbar sind.</summary>
        void Scan();
        /// <summary>Bluetooth-Name dieses Geraets (so sieht es der Mitspieler in seiner Liste).</summary>
        string LocalName { get; }
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

    public interface IClock { double Now { get; } }
    public sealed class StopwatchClock : IClock
    {
        readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        public double Now => sw.Elapsed.TotalSeconds;
    }

    /// <summary>Menschenlesbarer Text zu einem Transportfehler.</summary>
    public static class NetText
    {
        public static string Of(BtErr e) => e switch
        {
            BtErr.PermissionMissing => "Bluetooth-Berechtigung fehlt", BtErr.BluetoothOff => "Bluetooth ist ausgeschaltet", BtErr.NoAdapter => "Kein Bluetooth-Adapter",
            BtErr.ConnectFailed => "Verbindung fehlgeschlagen", BtErr.Timeout => "Zeitüberschreitung", BtErr.SocketClosed => "Verbindung beendet", BtErr.ReadFailed => "Lesefehler",
            BtErr.WriteFailed => "Schreibfehler", BtErr.Full => "Host ist voll", _ => e == BtErr.None ? "" : "Unbekannter Fehler"
        };
        public static string Of(NetError e) => e switch
        {
            NetError.Version => "Inkompatible Version", NetError.Full => "Sitzung ist voll (4/4)", NetError.Dataset => "Andere Spielversion", NetError.NoSession => "Sitzung nicht gefunden",
            NetError.BadToken => "Wiederverbindung abgelehnt", NetError.WindowExpired => "Wiederverbindungs-Zeit abgelaufen", NetError.Paused => "Spiel ist pausiert", NetError.NotHost => "Nur der Host darf das",
            NetError.Invalid => "Ungültige Nachricht", NetError.Kicked => "Vom Host entfernt", NetError.HostLeft => "Host hat die Sitzung beendet", NetError.Aborted => "Partie abgebrochen",
            NetError.Timeout => "Zeitüberschreitung", NetError.Busy => "Host ist beschäftigt", NetError.PlayerLeft => "Ein Spieler hat die Partie verlassen", NetError.StartRefused => "Start abgelehnt", _ => ""
        };
    }
}
