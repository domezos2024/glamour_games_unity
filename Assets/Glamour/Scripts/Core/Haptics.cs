using System;
using UnityEngine;

namespace GlamourGames
{
    /// <summary>
    /// Vibrationsfeedback. Auf Android ueber den System-Vibrator (VibrationEffect: Tick, Klick, Schwerer Klick, Muster),
    /// sonst ohne Funktion. Kann in den Optionen abgeschaltet werden (gespeichert unter "haptics").
    /// </summary>
    public static class Haptics
    {
        public static bool Available => Platform.Touch;
        public static bool On { get => Save.Int("haptics", 1) != 0; set => Save.Set("haptics", value ? 1 : 0); }

        public static void Tap() => Fire(Kind.Tick);
        public static void Hit() => Fire(Kind.Heavy);
        public static void Toss() => Fire(Kind.Click);
        public static void Win() => Fire(Kind.Win);
        public static void Lose() => Fire(Kind.Lose);

        enum Kind { Tick, Click, Heavy, Win, Lose }

        static float last;
        static void Fire(Kind k)
        {
            if (!Available || !On) return;
            float now = Time.realtimeSinceStartup; if (now - last < .03f) return; last = now; // keine Vibrationsflut (z.B. mehrere Wuerfel)
#if UNITY_ANDROID && !UNITY_EDITOR
            Android(k);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vib; static AndroidJavaClass effect; static int sdk; static bool init, failed;

        static void Init()
        {
            if (init) return; init = true;
            try
            {
                using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity")) vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vib == null || !vib.Call<bool>("hasVibrator")) failed = true;
                if (sdk >= 26) effect = new AndroidJavaClass("android.os.VibrationEffect");
            }
            catch (Exception e) { failed = true; Log.I("Vibration nicht verfuegbar: " + e.Message); }
        }

        static void Android(Kind k)
        {
            Init();
            if (failed) { if (vib == null) { try { Handheld.Vibrate(); } catch { } } return; }
            try
            {
                if (sdk >= 29 && (k == Kind.Tick || k == Kind.Click || k == Kind.Heavy))
                {
                    // vordefinierte Effekte des Geraeteherstellers: TICK = 2, CLICK = 0, HEAVY_CLICK = 5
                    using (var e = effect.CallStatic<AndroidJavaObject>("createPredefined", k == Kind.Tick ? 2 : k == Kind.Click ? 0 : 5)) vib.Call("vibrate", e);
                }
                else if (sdk >= 26)
                {
                    AndroidJavaObject e;
                    switch (k)
                    {
                        case Kind.Win: e = effect.CallStatic<AndroidJavaObject>("createWaveform", new long[] { 0, 70, 60, 70, 60, 160 }, new int[] { 0, 160, 0, 210, 0, 255 }, -1); break;
                        case Kind.Lose: e = effect.CallStatic<AndroidJavaObject>("createOneShot", 170L, 150); break;
                        case Kind.Heavy: e = effect.CallStatic<AndroidJavaObject>("createOneShot", 45L, 220); break;
                        case Kind.Click: e = effect.CallStatic<AndroidJavaObject>("createOneShot", 25L, 140); break;
                        default: e = effect.CallStatic<AndroidJavaObject>("createOneShot", 12L, 90); break;
                    }
                    using (e) vib.Call("vibrate", e);
                }
                else vib.Call("vibrate", k == Kind.Win ? 220L : k == Kind.Lose ? 170L : 20L);
            }
            catch (Exception e) { failed = true; Log.I("Vibration: " + e.Message); }
        }
#endif
    }
}
