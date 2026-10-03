using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GlamourGames
{
    /// <summary>
    /// SDF-Schriftatlas (offline erzeugt mit Tools/build_assets.py). Schriften: 0 Selawik, 1 Selawik Bold,
    /// 2 PT Serif Bold Italic, 3 Noto Sans Symbols 2 (Fallback fuer Symbole).
    /// </summary>
    public static class FontAtlas
    {
        struct G { public float Adv, X, Y, W, H, Bx, By; }
        static readonly Dictionary<int, G>[] glyphs = { new Dictionary<int, G>(), new Dictionary<int, G>(), new Dictionary<int, G>(), new Dictionary<int, G>() };
        static readonly float[] asc = new float[4], desc = new float[4];
        static float aw = 2048, ah = 2048, baseSize = 56, spread = 14;
        public static Texture2D Texture;
        public static bool Loaded;

        public static void Load()
        {
            if (Loaded) return;
            var txt = Resources.Load<TextAsset>("Glamour/font_atlas");
            var png = Resources.Load<TextAsset>("Glamour/font_atlas.png");
            if (txt == null || png == null) { Debug.LogError("Glamour: Schriftatlas fehlt in Resources/Glamour"); return; }
            var ci = CultureInfo.InvariantCulture;
            foreach (var line in txt.text.Split('\n'))
            {
                var f = line.Trim().Split(' ');
                if (f.Length < 2) continue;
                if (f[0] == "atlas") { aw = float.Parse(f[1], ci); ah = float.Parse(f[2], ci); baseSize = float.Parse(f[3], ci); spread = float.Parse(f[4], ci); }
                else if (f[0] == "font") { int i = int.Parse(f[1], ci); asc[i] = float.Parse(f[2], ci); desc[i] = float.Parse(f[3], ci); }
                else if (f[0] == "g")
                {
                    int fi = int.Parse(f[1], ci), cp = int.Parse(f[2], ci);
                    glyphs[fi][cp] = new G { Adv = float.Parse(f[3], ci), X = float.Parse(f[4], ci), Y = float.Parse(f[5], ci), W = float.Parse(f[6], ci), H = float.Parse(f[7], ci), Bx = float.Parse(f[8], ci), By = float.Parse(f[9], ci) };
                }
            }
            Texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { name = "GlamourFontAtlas" };
            Texture.LoadImage(png.bytes, false);
            Texture.filterMode = FilterMode.Bilinear; Texture.wrapMode = TextureWrapMode.Clamp; Texture.anisoLevel = 0;
            Texture.Apply(false, true);
            Loaded = true;
        }

        /// <summary>1 wenn der geladene Atlas den Wert im Alphakanal traegt (Alpha8), sonst 0 (Rotkanal).</summary>
        public static float UsesAlpha => Texture != null && Texture.format == TextureFormat.Alpha8 ? 1 : 0;

        static bool Find(int font, int cp, out G g, out int usedFont)
        {
            usedFont = font;
            if (glyphs[font].TryGetValue(cp, out g)) return true;
            for (int f = 0; f < 4; f++) if (glyphs[f].TryGetValue(cp, out g)) { usedFont = f; return true; }
            usedFont = font; return glyphs[font].TryGetValue('?', out g);
        }

        static IEnumerable<int> Codepoints(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length) { yield return char.ConvertToUtf32(s[i], s[i + 1]); i++; }
                else yield return s[i];
            }
        }

        public static float Measure(string s, float size, int font)
        {
            if (string.IsNullOrEmpty(s) || !Loaded) return 0;
            float k = size / baseSize, w = 0;
            foreach (var cp in Codepoints(s)) if (Find(font, cp, out var g, out _)) w += g.Adv * k;
            return w;
        }

        public static float Ascent(int font, float size) => asc[font] * size / baseSize;
        public static float Descent(int font, float size) => desc[font] * size / baseSize;

        /// <summary>Zeichnet Text mit Grundlinie bei by, beginnend bei x (links).</summary>
        public static void Draw(Canvas2D c, string s, float x, float by, float size, int font, Paint p)
        {
            if (string.IsNullOrEmpty(s) || !Loaded) return;
            float k = size / baseSize, pen = x, so = spread * k;
            foreach (var cp in Codepoints(s))
            {
                if (!Find(font, cp, out var g, out _)) continue;
                if (g.W > 0)
                {
                    float u0 = g.X / aw, u1 = (g.X + g.W) / aw, v0 = 1 - g.Y / ah, v1 = 1 - (g.Y + g.H) / ah;
                    c.Glyph(pen + g.Bx * k, by + g.By * k, g.W * k, g.H * k, u0, v0, u1, v1, k, so, p);
                }
                pen += g.Adv * k;
            }
        }
    }

    /// <summary>Alle Spielgrafiken in einem Atlas (offline gepackt, Name = Dateiname ohne Endung).</summary>
    public static class Assets
    {
        static readonly Dictionary<string, Img> imgs = new Dictionary<string, Img>();
        public static Texture2D Texture;
        public static bool Loaded;

        public static void Load()
        {
            if (Loaded) return;
            var txt = Resources.Load<TextAsset>("Glamour/img_atlas");
            var png = Resources.Load<TextAsset>("Glamour/img_atlas.png");
            if (txt == null || png == null) { Debug.LogError("Glamour: Bildatlas fehlt in Resources/Glamour"); return; }
            var ci = CultureInfo.InvariantCulture; float aw = 2048, ah = 2048;
            foreach (var line in txt.text.Split('\n'))
            {
                var f = line.Trim().Split(' ');
                if (f.Length < 3) continue;
                if (f[0] == "atlas") { aw = float.Parse(f[1], ci); ah = float.Parse(f[2], ci); }
                else if (f[0] == "img")
                {
                    float x = float.Parse(f[2], ci), y = float.Parse(f[3], ci), w = float.Parse(f[4], ci), h = float.Parse(f[5], ci);
                    imgs[f[1]] = new Img { Name = f[1], U0 = x / aw, U1 = (x + w) / aw, V0 = 1 - y / ah, V1 = 1 - (y + h) / ah, Width = (int)w, Height = (int)h };
                }
            }
            Texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false) { name = "GlamourImageAtlas" };
            Texture.LoadImage(png.bytes, false);
            Texture.filterMode = FilterMode.Trilinear; Texture.wrapMode = TextureWrapMode.Clamp; Texture.anisoLevel = 8; Texture.mipMapBias = -.6f; // verkleinerte Symbole/Kartenbilder schaerfer
            Texture.Apply(true, true);
            Loaded = true;
        }

        /// <summary>Bild nach Namen, z.B. "slot_BOOK", "coin_H", "diamond". null wenn unbekannt.</summary>
        public static Img Img(string name) => name != null && imgs.TryGetValue(name, out var i) ? i : null;
        public static IEnumerable<string> Names => imgs.Keys;
    }
}
