using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GlamourGames
{
    /// <summary>Nachrichtentypen des Sitzungsprotokolls (Version 2).</summary>
    public enum MsgType
    {
        Hello, Welcome, JoinRequest, Roster, Ready, StartRequest, StartGame, Intent, StateDelta, StateSnapshot, Ack, Ping, Pong,
        Disconnect, ResumeRequest, ResumeAccepted, Error, SnapshotRequest
    }

    /// <summary>Strukturierte Fehlercodes (werden im Protokoll als Zahl uebertragen).</summary>
    public enum NetError
    {
        None = 0, Version = 1, Full = 2, Dataset = 3, NoSession = 4, BadToken = 5, WindowExpired = 6, Paused = 7, NotHost = 8, Invalid = 9,
        Kicked = 10, HostLeft = 11, Aborted = 12, Timeout = 13, Busy = 14, PlayerLeft = 15, StartRefused = 16
    }

    /// <summary>
    /// Versioniertes Nachrichten-Umschlag-Format. Eine Zeile je Nachricht:
    /// GL2|sessionId|messageId|senderPlayerId|seat|type|sequence|ack|stateRevision|payload0|payload1|...
    /// Jedes Feld ist prozent-kodiert ('%', '|', CR, LF), damit beliebige Texte (Namen, Spielzustand) sicher uebertragen werden.
    /// </summary>
    public sealed class Msg
    {
        public const string Ver = "GL2";
        public const int MaxLine = 32768, MaxFields = 160, MaxHeader = 9;
        static readonly string[] None = new string[0];
        public string Sid = "", From = ""; public long Mid, Seq, Ack, Rev; public int Seat = -1; public MsgType Type; public string[] P = None;

        public Msg() { }
        public Msg(MsgType t, params string[] p) { Type = t; P = p ?? None; }

        public string Encode()
        {
            var sb = new StringBuilder(64);
            sb.Append(Ver).Append('|'); Esc(sb, Sid); sb.Append('|').Append(Mid.ToString(CultureInfo.InvariantCulture)).Append('|'); Esc(sb, From);
            sb.Append('|').Append(Seat.ToString(CultureInfo.InvariantCulture)).Append('|').Append(((int)Type).ToString(CultureInfo.InvariantCulture))
              .Append('|').Append(Seq.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Ack.ToString(CultureInfo.InvariantCulture)).Append('|').Append(Rev.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < P.Length; i++) { sb.Append('|'); Esc(sb, P[i] ?? ""); }
            return sb.ToString();
        }

        /// <summary>Zeile dekodieren. Falsche Version, zu lange oder fehlerhafte Zeilen liefern false mit Fehlercode.</summary>
        public static bool TryDecode(string line, out Msg m, out NetError err)
        {
            m = null; err = NetError.Invalid;
            if (line == null || line.Length < 6 || line.Length > MaxLine) return false;
            if (!line.StartsWith("GL", StringComparison.Ordinal)) return false;
            if (!line.StartsWith(Ver + "|", StringComparison.Ordinal)) { err = NetError.Version; return false; }
            var f = line.Split('|'); if (f.Length < MaxHeader || f.Length > MaxFields + MaxHeader) return false;
            var r = new Msg();
            if (!long.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Mid)) return false;
            if (!int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Seat)) return false;
            if (!int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int t) || t < 0 || t > (int)MsgType.SnapshotRequest) return false;
            if (!long.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Seq)) return false;
            if (!long.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Ack)) return false;
            if (!long.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out r.Rev)) return false;
            r.Type = (MsgType)t; r.Sid = Unesc(f[1]); r.From = Unesc(f[3]);
            if (r.Sid.Length > 64 || r.From.Length > 64) return false;
            r.P = f.Length == MaxHeader ? None : new string[f.Length - MaxHeader];
            for (int i = MaxHeader; i < f.Length; i++) r.P[i - MaxHeader] = Unesc(f[i]);
            m = r; err = NetError.None; return true;
        }

        static void Esc(StringBuilder sb, string s)
        {
            foreach (char c in s)
            {
                if (c == '%') sb.Append("%25"); else if (c == '|') sb.Append("%7C"); else if (c == '\n') sb.Append("%0A"); else if (c == '\r') sb.Append("%0D"); else sb.Append(c);
            }
        }
        static string Unesc(string s)
        {
            if (s.IndexOf('%') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '%' && i + 2 < s.Length)
                {
                    string h = s.Substring(i + 1, 2);
                    if (h == "25") { sb.Append('%'); i += 2; continue; }
                    if (h == "7C") { sb.Append('|'); i += 2; continue; }
                    if (h == "0A") { sb.Append('\n'); i += 2; continue; }
                    if (h == "0D") { sb.Append('\r'); i += 2; continue; }
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>Hilfen fuer Namen, Zahlen und Zufallskennungen im Protokoll.</summary>
    public static class WireUtil
    {
        public const int MaxName = 16;
        /// <summary>Anzeigename auf Laenge und Zeichensatz begrenzen (Buchstaben, Ziffern, Leerzeichen, - _ . ' ( )).</summary>
        public static string SafeName(string s, string fallback)
        {
            if (s == null) return fallback; var sb = new StringBuilder();
            foreach (char c in s.Trim())
            {
                if (sb.Length >= MaxName) break;
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' || c == '\'' || c == '(' || c == ')') sb.Append(c); else if (!char.IsControl(c)) sb.Append('?');
            }
            var r = sb.ToString().Trim(); return r.Length == 0 ? fallback : r;
        }
        public static string RandomId(int bytes = 6)
        {
            var b = new byte[bytes]; using (var g = System.Security.Cryptography.RandomNumberGenerator.Create()) g.GetBytes(b);
            var sb = new StringBuilder(); foreach (var x in b) sb.Append(x.ToString("x2")); return sb.ToString();
        }
        public static int Int(string s, int d = 0) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : d;
        public static long Long(string s, long d = 0) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : d;
        public static string S(long v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
