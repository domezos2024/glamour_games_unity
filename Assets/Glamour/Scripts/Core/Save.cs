using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace GlamourGames
{
    /// <summary>Einstellungen, Spielernamen und Highscores (Datei save.txt im persistentDataPath).</summary>
    public static class Save
    {
        static Dictionary<string, string> d;
        static string file;

        static void Ensure()
        {
            if (d != null) return;
            d = new Dictionary<string, string>();
            try
            {
                file = Path.Combine(UnityEngine.Application.persistentDataPath, "save.txt");
                if (File.Exists(file))
                    foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
                    {
                        int i = line.IndexOf('\t'); if (i <= 0) continue;
                        d[Unescape(line.Substring(0, i))] = Unescape(line.Substring(i + 1));
                    }
            }
            catch (Exception e) { Log.I("save load " + e.Message); }
        }
        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n");
        static string Unescape(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length) { char n = s[++i]; sb.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n); }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
        static void Flush()
        {
            try { if (file != null) File.WriteAllLines(file, d.Select(kv => Escape(kv.Key) + "\t" + Escape(kv.Value)), Encoding.UTF8); }
            catch (Exception e) { Log.I("save write " + e.Message); }
        }

        public static int Int(string k, int def) { Ensure(); return d.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : def; }
        public static string Str(string k, string def = "") { Ensure(); return d.TryGetValue(k, out var v) ? v : def; }
        public static void Set(string k, object v) { Ensure(); d[k] = Convert.ToString(v, CultureInfo.InvariantCulture); Flush(); }

        public static List<(string name, int score)> Scores(string k)
        {
            var l = new List<(string, int)>();
            foreach (var e in Str(k).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = e.Split('|');
                if (p.Length == 2 && int.TryParse(p[1], out var s)) l.Add((p[0], s));
            }
            return l;
        }
        public static bool IsHigh(string k, int s) { var l = Scores(k); return s > 0 && (l.Count < 10 || s > l[l.Count - 1].score); }
        public static void AddScore(string k, string n, int s)
        {
            var l = Scores(k); l.Add((n, s)); l = l.OrderByDescending(x => x.score).Take(10).ToList();
            Set(k, string.Join(";", l.Select(x => x.name.Replace(";", "").Replace("|", "") + "|" + x.score)));
        }
    }

    public static class Log
    {
        public static void I(string m, [CallerFilePath] string f = "", [CallerMemberName] string fn = "")
            => UnityEngine.Debug.Log($"[Glamour] {Path.GetFileNameWithoutExtension(f)}.{fn}: {m}");
    }

    /// <summary>Spielernamen (in den Optionen aenderbar).</summary>
    public static class Pl
    {
        public static string Name(int i) { var s = Save.Str("pname" + i, "").Trim(); return s.Length == 0 ? "Spieler " + (i + 1) : s; }
        public static void SetName(int i, string n) => Save.Set("pname" + i, n.Trim());
    }

    /// <summary>Platzhalter fuer Vibrationsfeedback (am PC ohne Funktion, API wie Android-Version).</summary>
    public static class Haptics
    {
        public const bool Available = false;
        public static bool On;
        public static void Tap() { }
        public static void Hit() { }
        public static void Toss() { }
        public static void Win() { }
        public static void Lose() { }
    }
}
