#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;

namespace GlamourGames
{
    /// <summary>Bluetooth-Transport auf Android ueber das Java-Plugin de.domezosware.glamourgames.GlamourBt.</summary>
    sealed class AndroidBt : IBtTransport
    {
        const string Connect = "android.permission.BLUETOOTH_CONNECT";
        readonly AndroidJavaObject j; readonly int sdk; string err;
        public AndroidBt()
        {
            try { sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT"); j = new AndroidJavaObject("de.domezosware.glamourgames.GlamourBt"); }
            catch (Exception e) { err = e.Message; Log.I("bluetooth: Plugin fehlt: " + e.Message); }
        }
        public bool Supported => j != null && j.Call<bool>("supported");
        public bool Enabled => j != null && j.Call<bool>("enabled");
        public bool Connected => j != null && j.Call<bool>("isConnected");
        public string Status => j == null ? "Bluetooth nicht verfügbar" + (err != null ? ": " + err : "") : !Enabled ? "Bluetooth ist ausgeschaltet" : j.Call<string>("status");
        public bool EnsurePermission()
        {
            if (sdk < 31 || Permission.HasUserAuthorizedPermission(Connect)) return true;
            Permission.RequestUserPermission(Connect); return false;
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
        public void Host() => j?.Call("host");
        public void Join(string addr) => j?.Call("join", addr);
        public void Stop() => j?.Call("stop");
        public void Send(string line) => j?.Call("send", line);
        public bool Poll(out string line) { line = j?.Call<string>("poll"); return line != null; }
    }
}
#endif
