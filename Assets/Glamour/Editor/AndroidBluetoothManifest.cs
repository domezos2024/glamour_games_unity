#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace GlamourGames.EditorTools
{
    /// <summary>
    /// Ergaenzt das bei jedem Build neu erzeugte Android-Manifest um die Bluetooth-Berechtigungen fuer den Mehrspieler
    /// (Android 12+: BLUETOOTH_CONNECT als Laufzeit-Berechtigung; aeltere Versionen: BLUETOOTH/BLUETOOTH_ADMIN).
    /// </summary>
    public class AndroidBluetoothManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var f = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(f)) { Debug.LogWarning("[Glamour] Bluetooth: Manifest nicht gefunden: " + f); return; }
            var s = File.ReadAllText(f); if (s.Contains("BLUETOOTH_CONNECT")) return;
            const string add =
                "  <uses-permission android:name=\"android.permission.BLUETOOTH\" android:maxSdkVersion=\"30\" />\n" +
                "  <uses-permission android:name=\"android.permission.BLUETOOTH_ADMIN\" android:maxSdkVersion=\"30\" />\n" +
                "  <uses-permission android:name=\"android.permission.BLUETOOTH_CONNECT\" />\n" +
                "  <uses-feature android:name=\"android.hardware.bluetooth\" android:required=\"false\" />\n  ";
            int i = s.IndexOf("<application"); if (i < 0) { Debug.LogWarning("[Glamour] Bluetooth: <application> fehlt im Manifest"); return; }
            File.WriteAllText(f, s.Insert(i, add)); Debug.Log("[Glamour] Bluetooth-Berechtigungen ins Manifest eingetragen");
        }
    }
}
#endif
