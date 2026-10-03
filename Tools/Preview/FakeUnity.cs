// Minimaler Ersatz fuer die von der Engine genutzten UnityEngine-Typen, damit die Spiele ohne Unity
// in einem .NET-Konsolenprogramm laufen und per Software-Rasterizer abgebildet werden koennen.
using System;
using System.Collections.Generic;
using System.IO;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float k) => new Vector2(a.x * k, a.y * k);
        public static Vector2 operator *(float k, Vector2 a) => new Vector2(a.x * k, a.y * k);
        public static Vector2 operator /(Vector2 a, float k) => new Vector2(a.x / k, a.y / k);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => MathF.Sqrt(x * x + y * y);
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { var n = normalized; x = n.x; y = n.y; }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
    }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z = 0) { this.x = x; this.y = y; this.z = z; } public static Vector3 zero => default; }
    public struct Vector4 { public float x, y, z, w; public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; } public static Vector4 zero => default; }
    public struct Color32 { public byte r, g, b, a; public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public static class Mathf
    {
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0, 1);
    }
    public struct Bounds { public Bounds(Vector3 c, Vector3 s) { } }
    public enum MeshTopology { Triangles }
    public class Mesh
    {
        public List<Vector3> Pos; public List<Color32> Col; public List<Vector4>[] Uv = new List<Vector4>[8]; public List<Vector4> Tan; public List<int> Idx;
        public Bounds bounds;
        public void Clear() { Pos = null; Idx = null; }
        public void SetVertices(List<Vector3> v) => Pos = v;
        public void SetColors(List<Color32> c) => Col = c;
        public void SetUVs(int ch, List<Vector4> u) => Uv[ch] = u;
        public void SetTangents(List<Vector4> t) => Tan = t;
        public void SetIndices(List<int> i, MeshTopology t, int sub, bool calc) => Idx = i;
    }
    public class Object { public string name; }
    public class TextAsset : Object { public string text; public byte[] bytes; }
    public static class Resources
    {
        public static string Root = "";
        public static T Load<T>(string path) where T : class
        {
            if (typeof(T) != typeof(TextAsset)) return null;
            foreach (var ext in new[] { ".txt", ".bytes" })
            {
                var f = Path.Combine(Root, path + ext);
                if (File.Exists(f)) return new TextAsset { bytes = File.ReadAllBytes(f), text = File.ReadAllText(f) } as T;
            }
            return null;
        }
    }
    public enum TextureFormat { RGBA32, Alpha8, R8 }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Clamp, Repeat }
    public class Texture2D : Object
    {
        public TextureFormat format; public FilterMode filterMode; public TextureWrapMode wrapMode; public int anisoLevel; public float mipMapBias;
        public Texture2D(int w, int h, TextureFormat f, bool mips, bool linear) { format = f; }
        public void Apply(bool a, bool b) { }
    }
    public static class ImageConversion { public static bool LoadImage(this Texture2D t, byte[] data, bool nonReadable) => true; }
    public static class Debug { public static void Log(object o) { if (Environment.GetEnvironmentVariable("PREVIEW_LOG") == "1") Console.WriteLine(o); } public static void LogError(object o) => Console.WriteLine("ERROR " + o); public static void LogException(Exception e) => Console.WriteLine(e); }
    public static class Application { public static string persistentDataPath => Path.Combine(Path.GetTempPath(), "glamour_preview"); }
}
