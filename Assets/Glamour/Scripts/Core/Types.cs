using System;
using System.Collections.Generic;
using System.Linq;

namespace System.Runtime.CompilerServices
{
    // Erlaubt 'init'-Accessoren und Records unter Unitys .NET-Standard-Profil.
    internal static class IsExternalInit { }
}

namespace GlamourGames
{
    /// <summary>sRGB-Farbe mit 8 Bit pro Kanal (Designfarben wie im Original).</summary>
    public struct Col : IEquatable<Col>
    {
        public byte Red, Green, Blue, Alpha;
        public Col(int r, int g, int b, int a = 255) { Red = (byte)Math.Clamp(r, 0, 255); Green = (byte)Math.Clamp(g, 0, 255); Blue = (byte)Math.Clamp(b, 0, 255); Alpha = (byte)Math.Clamp(a, 0, 255); }
        public Col WithAlpha(byte a) => new Col(Red, Green, Blue, a);
        public static readonly Col White = new Col(255, 255, 255), Black = new Col(0, 0, 0), Clear = new Col(0, 0, 0, 0);
        public static Col FromHsv(float h, float s, float v)
        {
            h = ((h % 360) + 360) % 360; s /= 100f; v /= 100f;
            float c = v * s, x = c * (1 - Math.Abs(h / 60f % 2 - 1)), m = v - c, r, g, b;
            if (h < 60) { r = c; g = x; b = 0; } else if (h < 120) { r = x; g = c; b = 0; } else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; } else if (h < 300) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }
            return new Col((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }
        public bool Equals(Col o) => Red == o.Red && Green == o.Green && Blue == o.Blue && Alpha == o.Alpha;
        public override bool Equals(object o) => o is Col c && Equals(c);
        public override int GetHashCode() => (Red << 24) | (Green << 16) | (Blue << 8) | Alpha;
        public static bool operator ==(Col a, Col b) => a.Equals(b);
        public static bool operator !=(Col a, Col b) => !a.Equals(b);
        public static explicit operator uint(Col c) => (uint)c.GetHashCode();
        public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}{Alpha:X2}";
    }

    /// <summary>Achsenparalleles Rechteck im 1600x900-Designraum (y nach unten).</summary>
    public struct Box
    {
        public float Left, Top, Right, Bottom;
        public Box(float l, float t, float r, float b) { Left = l; Top = t; Right = r; Bottom = b; }
        public float Width => Right - Left;
        public float Height => Bottom - Top;
        public float MidX => (Left + Right) * .5f;
        public float MidY => (Top + Bottom) * .5f;
        public bool Contains(float x, float y) => x >= Left && x < Right && y >= Top && y < Bottom;
        public bool IntersectsWith(Box o) => o.Left < Right && Left < o.Right && o.Top < Bottom && Top < o.Bottom;
        public Box Offset(float dx, float dy) => new Box(Left + dx, Top + dy, Right + dx, Bottom + dy);
        public override string ToString() => $"[{Left},{Top},{Right},{Bottom}]";
    }

    public struct Pt
    {
        public float X, Y;
        public Pt(float x, float y) { X = x; Y = y; }
        public static Pt operator +(Pt a, Pt b) => new Pt(a.X + b.X, a.Y + b.Y);
        public static Pt operator -(Pt a, Pt b) => new Pt(a.X - b.X, a.Y - b.Y);
        public static Pt operator *(Pt a, float k) => new Pt(a.X * k, a.Y * k);
        public float Length => MathF.Sqrt(X * X + Y * Y);
    }

    public enum Al { L, C, R }

    /// <summary>Tastatur-Abstraktion (unabhaengig vom Unity-Input-Backend).</summary>
    public enum Key
    {
        Unknown, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Number0, Number1, Number2, Number3, Number4, Number5, Number6, Number7, Number8, Number9,
        Keypad0, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,
        Space, Enter, KeypadEnter, Escape, Backspace, Tab, Left, Right, Up, Down,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, Plus, Minus
    }

    /// <summary>Gemeinsamer Zufallsgenerator (System.Random, nicht UnityEngine.Random).</summary>
    public static class Rng
    {
        public static readonly Random Shared = new Random();
        public static float F() => (float)Shared.NextDouble();
        public static float F(float a, float b) => a + (float)Shared.NextDouble() * (b - a);
        public static int I(int maxExcl) => Shared.Next(maxExcl);
        public static int I(int min, int maxExcl) => Shared.Next(min, maxExcl);
        public static float NextSingle(this Random r) => (float)r.NextDouble();
        public static void Shuffle<T>(IList<T> l, Random r = null)
        {
            r ??= Shared;
            for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }
    }

    public static class Ease
    {
        public static float Clamp(float t) => t < 0 ? 0 : t > 1 ? 1 : t;
        public static float OutCubic(float t) { t = Clamp(t); return 1 - MathF.Pow(1 - t, 3); }
        public static float InCubic(float t) { t = Clamp(t); return t * t * t; }
        public static float InOutCubic(float t) { t = Clamp(t); return t < .5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2; }
        public static float OutBack(float t) { t = Clamp(t); const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * MathF.Pow(t - 1, 3) + c1 * MathF.Pow(t - 1, 2); }
        public static float OutQuad(float t) { t = Clamp(t); return 1 - (1 - t) * (1 - t); }
        public static float OutBounce(float t)
        {
            t = Clamp(t); const float n = 7.5625f, d = 2.75f;
            if (t < 1 / d) return n * t * t;
            if (t < 2 / d) { t -= 1.5f / d; return n * t * t + .75f; }
            if (t < 2.5f / d) { t -= 2.25f / d; return n * t * t + .9375f; }
            t -= 2.625f / d; return n * t * t + .984375f;
        }
        public static float OutElastic(float t) { t = Clamp(t); if (t == 0 || t == 1) return t; return MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - .75f) * (2 * MathF.PI / 3)) + 1; }
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public class Spring
    {
        public float V, Target, Vel, K = 260, D = 22;
        public Spring(float v = 0) { V = Target = v; }
        public void Snap(float v) { V = Target = v; Vel = 0; }
        public void Update(float dt)
        {
            int n = Math.Max(1, (int)MathF.Ceiling(dt / .008f)); float h = dt / n;
            for (int i = 0; i < n; i++) { Vel += ((Target - V) * K - Vel * D) * h; V += Vel * h; }
        }
    }

    public class Tween
    {
        public float T, Dur, Delay; public bool Done => T >= Dur + Delay;
        public Tween(float dur, float delay = 0) { Dur = dur; Delay = delay; }
        public float P => Ease.Clamp((T - Delay) / Dur);
        public void Update(float dt) { if (T < Dur + Delay) T += dt; }
        public void Reset() { T = 0; }
    }

    /// <summary>Verzoegerte Aktionen (Sekunden), werden pro Szene aktualisiert.</summary>
    public class Timers
    {
        readonly List<(float t, Action a)> l = new List<(float, Action)>();
        public void After(float sec, Action a) => l.Add((sec, a));
        public void Clear() => l.Clear();
        public int Count => l.Count;
        public void Update(float dt)
        {
            if (l.Count == 0) return;
            for (int i = 0; i < l.Count; i++) l[i] = (l[i].t - dt, l[i].a);
            var due = l.Where(x => x.t <= 0).ToList();
            l.RemoveAll(x => x.t <= 0);
            foreach (var d in due) d.a();
        }
    }

    /// <summary>Einfache Koroutinen: yield float = warten, yield Func&lt;bool&gt; = warten bis wahr.</summary>
    public class Coro
    {
        class Run { public Stack<IEnumerator<object>> St = new Stack<IEnumerator<object>>(); public float Wait; public Func<bool> Until; }
        readonly List<Run> l = new List<Run>();
        public void Start(IEnumerator<object> e) { var r = new Run(); r.St.Push(e); l.Add(r); }
        public void Clear() => l.Clear();
        public bool Busy => l.Count > 0;
        public void Update(float dt)
        {
            for (int i = 0; i < l.Count; i++)
            {
                var r = l[i];
                if (r.Wait > 0) { r.Wait -= dt; if (r.Wait > 0) continue; }
                if (r.Until != null) { if (!r.Until()) continue; r.Until = null; }
                while (r.St.Count > 0)
                {
                    var e = r.St.Peek();
                    if (!e.MoveNext()) { r.St.Pop(); continue; }
                    var y = e.Current;
                    if (y is IEnumerator<object> n) { r.St.Push(n); continue; }
                    if (y is float f) r.Wait = f; else if (y is Func<bool> fn) r.Until = fn;
                    break;
                }
                if (r.St.Count == 0) l.RemoveAt(i--);
            }
        }
    }
}
