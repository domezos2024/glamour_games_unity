#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;

namespace GlamourGames
{
    /// <summary>Bluetooth-Transport auf Android ueber das Java-Plugin de.domezosware.glamourgames.GlamourBt (Mehr-Verbindungs-Variante).</summary>
    sealed class AndroidBt : IBtTransport
    {
        const string Connect_ = "android.permission.BLUETOOTH_CONNECT";
        readonly AndroidJavaObject j; readonly int sdk; string err; bool listening;
        public AndroidBt()
        {
            try { sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT"); j = new AndroidJavaObject("de.domezosware.glamourgames.GlamourBt"); j.Call("setMaxPeers", MaxPeers); }
            catch (Exception e) { err = e.Message; Log.I("bluetooth: Plugin fehlt: " + e.Message); }
        }
        public bool Supported => j != null && j.Call<bool>("supported");
        public bool Enabled => j != null && j.Call<bool>("enabled");
        public int MaxPeers => 3;
        public bool Listening => listening && j != null && j.Call<bool>("listening");
        public BtErr LastError => j == null ? BtErr.NoAdapter : (BtErr)j.Call<int>("lastError");
        public string Status => j == null ? "Bluetooth nicht verfügbar" + (err != null ? ": " + err : "") : !Enabled ? "Bluetooth ist ausgeschaltet" : j.Call<string>("status");
        public bool EnsurePermission()
        {
            if (sdk < 31 || Permission.HasUserAuthorizedPermission(Connect_)) return true;
            Permission.RequestUserPermission(Connect_); return false;
        }
        public List<BtDev> Paired()
        {
            var l = new List<BtDev>(); if (j == null || !EnsurePermission()) return l;
            foreach (var row in (j.Call<string>("paired") ?? "").Split('\n'))
            {
                var p = row.Split('\t'); if (p.Length < 4) continue;
                int.TryParse(p[2], out int major); l.Add(new BtDev { Name = p[0], Addr = p[1], Major = major, Glamour = p[3] == "1" });
            }
            return l;
        }
        public void Scan() { if (j != null && EnsurePermission()) j.Call("scan"); }
        public string LocalName => j == null ? null : j.Call<string>("localName");
        public void StartHost() { listening = true; j?.Call("startHost"); }
        public void Connect(string addr) { listening = false; j?.Call("connect", addr); }
        public void Stop() { listening = false; j?.Call("stop"); }
        public void Send(int peer, string line) => j?.Call("send", peer, line);
        public void Close(int peer) => j?.Call("close", peer);
        public bool Poll(out TransportEvent e)
        {
            e = default; var s = j?.Call<string>("poll"); if (s == null) return false;
            // C|peer|name|addr   L|peer|zeile   X|peer|code|text
            var a = s.Split(new[] { '|' }, 4); int peer = a.Length > 1 ? WireUtil.Int(a[1]) : 0;
            switch (a[0])
            {
                case "C": var b = s.Split(new[] { '|' }, 4); e = TransportEvent.Conn(peer, b.Length > 2 ? b[2] : "", b.Length > 3 ? b[3] : ""); return true;
                case "L": e = TransportEvent.Data(peer, a.Length > 2 ? (a.Length > 3 ? a[2] + "|" + a[3] : a[2]) : ""); return true;
                case "X": e = TransportEvent.Close(peer, a.Length > 2 ? (BtErr)WireUtil.Int(a[2]) : BtErr.Unknown, a.Length > 3 ? a[3] : null); return true;
            }
            return Poll(out e);
        }
    }
}
#endif
