// Glamour Preview: fuehrt Szenen ohne Unity aus und rendert sie per Software-Rasterizer, der den Shader
// "Glamour/Shape" und "Glamour/Backdrop" sowie Bloom/Tonemapping nachbildet. Fuer Screenshots, visuelle Tests
// und schnelle Iteration in Umgebungen ohne Unity-Editor (z. B. CI).
//
//   dotnet run -- --scene=menu|options|0..9 --t=2.5 --out=bild.png [--script="c,800,450@1.2;k,Space@2"] [--scale=1]
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEngine;

namespace GlamourGames
{
    // ------------------------------------------------------------------ Ersatz fuer App und Sfx
    public static class App
    {
        public const float VW = 1600, VH = 900;
        public const string Version = "2.0.0", Credit = "erstellt von Michael Bergfeld @ DoMeZos-Ware 2026";
        public static float VX0 = 0, VX1 = VW, VY0 = 0, VY1 = VH, MX = -100, MY = -100;
        public static Scene Current, Pending;
        public static float FlashA, ShakeA; public static Col FlashCol = Col.White; public static string ToastText = ""; public static float ToastT;
        public static Action<UnityEngine.Object> SetupPostFx;
        public static void Go(Scene s) { if (Pending == null) Pending = s; }
        public static void Flash(Col c, float a = .5f) { FlashA = a; FlashCol = c; }
        public static void Shake(float a = 14) { ShakeA = Math.Max(ShakeA, a); }
        public static void Toast(string t) { ToastText = t; ToastT = 2.4f; }
        public static bool Fullscreen => false;
        public static void ToggleFullscreen() { }
        public static void Quit() { }
    }
    public enum S { Click, Hover, Flip, Match, NoMatch, PlaceX, PlaceO, Win, Lose, Drop, Hit, Miss, Sunk, Eat, Die, Dice, Deal, Coin, Spin, Stop, Big, Chip, Take, Turn, Tick, Splash, Blub, Boom, Crackle, Thunder, Zap, Launch, FwPop, Cheer, Clap, Step, Shake, Sparkle, Fanfare, Creak, Gurgle, Whoosh, Pop, Boing, Gong }
    public static class Sfx
    {
        public static bool Muted, MusicOff, SfxOff; public static float Vol = .5f, MusicVol = .8f; public static float SfxVol => Vol; public static int Track;
        public static readonly string[] TrackNames = { "Glamour", "Oase", "Casino-Groove", "Arcade", "Mystik" };
        public static void Play(S s, float vol = 1, float pitch = 1) { }
        public static void ToggleMute() => Muted = !Muted;
        public static string NextTrack() { Track = Track >= 4 ? -1 : Track + 1; return "Musik"; }
        public static void SetTrack(int t) => Track = t; public static void SetMusicOff(bool o) => MusicOff = o; public static void SetMusicVol(float v) => MusicVol = v; public static void SetSfxVol(float v) => Vol = v; public static void SetSfxOff(bool o) => SfxOff = o;
    }

    public static class PreviewMain
    {
        static int W = 1600, H = 900; static float S = 1;
        static float[] buf; // lineares RGB
        static byte[] font, img; const int AT = 2048;

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            string scene = "menu", outp = "preview.png", script = ""; float T = 2.5f;
            foreach (var a in args)
            {
                if (a.StartsWith("--scene=")) scene = a.Substring(8);
                else if (a.StartsWith("--t=")) T = float.Parse(a.Substring(4), CultureInfo.InvariantCulture);
                else if (a.StartsWith("--out=")) outp = a.Substring(6);
                else if (a.StartsWith("--script=")) script = a.Substring(9);
                else if (a.StartsWith("--scale=")) S = float.Parse(a.Substring(8), CultureInfo.InvariantCulture);
            }
            W = (int)(1600 * S); H = (int)(900 * S);
            string root = FindRoot();
            Resources.Root = Path.Combine(root, "Assets", "Resources");
            var cache = Path.Combine(Environment.GetEnvironmentVariable("TMPDIR") ?? "/tmp", "glamour_preview_cache");
            font = File.ReadAllBytes(Path.Combine(cache, "font.raw")); img = File.ReadAllBytes(Path.Combine(cache, "img.raw"));
            FontAtlas.Load(); Assets.Load();

            Scene cur = scene == "menu" ? new Menu() : scene == "options" ? new Options() : Registry.All[int.Parse(scene)].Make();
            App.Current = cur; cur.Enter();
            var events = new List<(float t, string a, string p1, string p2)>();
            foreach (var e in script.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var q = e.Split('@'); var kv = q[0].Split(',');
                events.Add((float.Parse(q[1], CultureInfo.InvariantCulture), kv[0], kv.Length > 1 ? kv[1] : "", kv.Length > 2 ? kv[2] : ""));
            }
            events.Sort((a, b) => a.t.CompareTo(b.t));
            var canvas = new Canvas2D();
            float t = 0, dt = 1 / 60f; int shot = 0;
            while (t < T)
            {
                while (events.Count > 0 && events[0].t <= t)
                {
                    var ev = events[0]; events.RemoveAt(0);
                    try
                    {
                        if (ev.a == "m" || ev.a == "c") { App.MX = F(ev.p1); App.MY = F(ev.p2); Move(cur); }
                        if (ev.a == "c") { Down(cur); Up(cur); }
                        if (ev.a == "k") Key(cur, (Key)Enum.Parse(typeof(Key), ev.p1));
                        if (ev.a == "ch" && cur.Modal != null) foreach (var ch in ev.p1) cur.Modal.Char(ch);
                        if (ev.a == "win") { cur.Modal = null; cur.Celebrate(ev.p1 == "p" ? C.Pink : C.Cyan, 5, ev.p2 == "" ? 1.1f : F(ev.p2), "Spieler 1 gewinnt!"); }
                        if (ev.a == "bigwin") { cur.Modal = null; cur.Celebrate(C.Gold, 5, 1.2f); }
                        if (ev.a == "res") cur.Result("Spieler 1 gewinnt!", "Stand: 3 : 1", ev.p1 == "p" ? C.Pink : C.Cyan, ("Nochmal", C.Green, null), ("Menü", C.Purple, null));
                        if (ev.a == "s") { Render(canvas, cur, outp.Replace(".png", $"_{shot++}.png")); }
                    }
                    catch (Exception x) { Console.WriteLine("event error: " + x); }
                }
                if (App.Pending != null) { cur.Leave(); cur = App.Pending; App.Pending = null; App.Current = cur; cur.Enter(); }
                cur.BaseUpdate(dt);
                App.FlashA = Math.Max(0, App.FlashA - dt * 1.8f); App.ShakeA = Math.Max(0, App.ShakeA - dt * 40); App.ToastT -= dt;
                t += dt;
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int tris = Render(canvas, cur, outp);
            Console.WriteLine($"{outp}: {canvas.VertexCount} Vertices, {tris} Dreiecke, {sw.ElapsedMilliseconds} ms, Partikel {cur.Fx.Count}");
            return 0;
        }
        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static void Move(Scene cur) { if (cur.Modal != null) cur.Modal.Move(App.MX, App.MY); else { cur.Ui.Move(App.MX, App.MY); cur.MouseMove(App.MX, App.MY); } }
        static void Down(Scene cur) { if (cur.Modal != null) cur.Modal.MDown(App.MX, App.MY); else if (!cur.Ui.Down(App.MX, App.MY)) cur.MouseDown(App.MX, App.MY); }
        static void Up(Scene cur) { if (cur.Modal != null) cur.Modal.MUp(App.MX, App.MY); else if (!cur.Ui.Up(App.MX, App.MY)) cur.MouseUp(App.MX, App.MY); }
        static void Key(Scene cur, Key k)
        {
            if (cur.Modal != null) { if (k == GlamourGames.Key.Enter) cur.Modal.Enter(); else if (k == GlamourGames.Key.Escape) cur.Modal.Cancel?.Invoke(); else if (k == GlamourGames.Key.Backspace) cur.Modal.Back(); return; }
            cur.KeyDown(k);
        }
        static string FindRoot()
        {
            var env = Environment.GetEnvironmentVariable("GLAMOUR_ROOT"); if (!string.IsNullOrEmpty(env)) return env;
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Assets", "Glamour"))) d = d.Parent;
            return d?.FullName ?? Directory.GetCurrentDirectory();
        }

        // ------------------------------------------------------------------ Rendern
        static int Render(Canvas2D canvas, Scene cur, string path)
        {
            canvas.Begin();
            cur.BaseDraw(canvas);
            if (App.FlashA > 0) { var fp = Gfx.Fill(App.FlashCol.A(App.FlashA)); fp.Additive = true; canvas.DrawRect(-10, -10, 1620, 920, fp); }
            var mesh = new Mesh(); canvas.Upload(mesh);
            buf = new float[W * H * 3];
            Backdrop(cur);
            int n = mesh.Idx == null ? 0 : mesh.Idx.Count / 3;
            for (int i = 0; i < n; i++) Tri(mesh, mesh.Idx[i * 3], mesh.Idx[i * 3 + 1], mesh.Idx[i * 3 + 2]);
            Post();
            WritePng(path);
            return n;
        }

        static float Lin(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float Srgb(float c) { c = Math.Clamp(c, 0, 1); return c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f; }

        // --- Hintergrund (Nachbildung von Glamour/Backdrop, halbe Aufloesung)
        static float Hash(float x, float y) { x = Frac(x * 123.34f); y = Frac(y * 456.21f); float d = x * (x + 45.32f) + y * (y + 45.32f); x += d; y += d; return Frac(x * y); }
        static float Frac(float v) => v - MathF.Floor(v);
        static float Noise(float x, float y)
        {
            float ix = MathF.Floor(x), iy = MathF.Floor(y), fx = x - ix, fy = y - iy;
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            return (a + (b - a) * ux) + ((c + (d - c) * ux) - (a + (b - a) * ux)) * uy;
        }
        static float Fbm(float x, float y) { float v = 0, a = .5f; for (int i = 0; i < 5; i++) { v += a * Noise(x, y); x = x * 2.03f + 17.1f; y = y * 2.03f + 9.2f; a *= .5f; } return v; }
        static float Blob(float dx, float dy, float cx, float cy, float r) => MathF.Max(0, 1 - MathF.Sqrt((dx - cx) * (dx - cx) + (dy - cy) * (dy - cy)) / r);
        static void Backdrop(Scene cur)
        {
            float t = cur.Time + 37; var a1 = cur.Acc1; var a2 = cur.Acc2;
            float[] A1 = { Lin(a1.Red / 255f), Lin(a1.Green / 255f), Lin(a1.Blue / 255f) }, A2 = { Lin(a2.Red / 255f), Lin(a2.Green / 255f), Lin(a2.Blue / 255f) }, P = { Lin(.627f), Lin(.125f), 1 };
            float w = 1600, h = 900;
            for (int py = 0; py < H; py += 2)
                for (int px = 0; px < W; px += 2)
                {
                    float dx = (px + 1) / S, dy = (py + 1) / S;
                    float r = Lin(6 / 255f), g = Lin(1 / 255f), b = Lin(15 / 255f);
                    float b1 = .22f * MathF.Pow(Blob(dx, dy, w * (.2f + .08f * MathF.Sin(t * .23f)), h * (.15f + .08f * MathF.Cos(t * .31f)), w * .55f), 1.4f);
                    float b2 = .18f * MathF.Pow(Blob(dx, dy, w * (.85f + .07f * MathF.Cos(t * .19f)), h * (.9f + .06f * MathF.Sin(t * .27f)), w * .5f), 1.4f);
                    float b3 = .09f * MathF.Pow(Blob(dx, dy, w * (.5f + .2f * MathF.Sin(t * .11f)), h * (.5f + .15f * MathF.Cos(t * .13f)), w * .35f), 1.4f);
                    float qx = dx / 520, qy = dy / 520, wx = Fbm(qx, qy + t * .03f), wy = Fbm(qx + 5.2f, qy - t * .025f);
                    float n = Fbm(qx * 1.4f + wx * 1.6f + t * .01f, qy * 1.4f + wy * 1.6f), n2 = Fbm(qx * 2.2f - wx - t * .015f, qy * 2.2f - wy + t * .008f);
                    float neb = (SS(.42f, .95f, n) * .55f + SS(.5f, 1, n2) * .25f) * .11f, mixv = Math.Clamp(n2 * 1.3f - .2f, 0, 1);
                    float[] o = new float[3];
                    for (int k = 0; k < 3; k++)
                    {
                        float nc = (A1[k] + (A2[k] - A1[k]) * mixv) * .65f + P[k] * .35f;
                        o[k] = (k == 0 ? r : k == 1 ? g : b) + A1[k] * b1 + A2[k] * b2 + P[k] * b3 + nc * neb;
                    }
                    float vr = MathF.Sqrt((dx - 800) * (dx - 800) + (dy - 450) * (dy - 450)) / (1600 * .75f), vig = 1 - .55f * SS(.55f, 1, vr);
                    for (int yy = py; yy < Math.Min(H, py + 2); yy++) for (int xx = px; xx < Math.Min(W, px + 2); xx++) { int i = (yy * W + xx) * 3; buf[i] = o[0] * vig; buf[i + 1] = o[1] * vig; buf[i + 2] = o[2] * vig; }
                }
            // Sterne
            var rnd = new System.Random(5);
            for (int i = 0; i < 260; i++)
            {
                float sx = (float)rnd.NextDouble() * 1600, sy = (float)rnd.NextDouble() * 900, z = .2f + (float)rnd.NextDouble() * .8f, tw = .5f + .5f * MathF.Sin(t * 1.5f * z + i);
                float br = (.15f + .6f * tw * z) * 1.4f, rad = (.8f + 1.6f * z) * S;
                int cx = (int)(sx * S), cy = (int)(sy * S);
                for (int yy = cy - 4; yy <= cy + 4; yy++) for (int xx = cx - 4; xx <= cx + 4; xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                        float d = MathF.Sqrt((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy)), c = MathF.Max(0, 1 - d / rad); c = c * c * 2.2f + MathF.Exp(-d * d / (rad * rad * 9)) * .35f;
                        int k = (yy * W + xx) * 3; buf[k] += .8f * c * br; buf[k + 1] += .85f * c * br; buf[k + 2] += c * br;
                    }
            }
        }
        static float SS(float a, float b, float x) { float t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

        // --- Dreieck rastern (affine Interpolation, Ableitungen analytisch)
        const int NA = 42;
        static readonly float[] va = new float[NA], vb = new float[NA], vc = new float[NA], at = new float[NA], ddx = new float[NA], ddy = new float[NA];
        static void Fill(float[] o, Mesh m, int i)
        {
            var c = m.Col[i]; o[0] = c.r / 255f; o[1] = c.g / 255f; o[2] = c.b / 255f; o[3] = c.a / 255f;
            for (int ch = 0; ch < 8; ch++) { var u = m.Uv[ch][i]; o[4 + ch * 4] = u.x; o[5 + ch * 4] = u.y; o[6 + ch * 4] = u.z; o[7 + ch * 4] = u.w; }
            var tg = m.Tan[i]; o[36] = tg.x; o[37] = tg.y; o[38] = tg.z; o[39] = tg.w;
            var p = m.Pos[i]; o[40] = p.x + 800; o[41] = 450 - p.y;
        }
        static void Tri(Mesh m, int i0, int i1, int i2)
        {
            Fill(va, m, i0); Fill(vb, m, i1); Fill(vc, m, i2);
            float x0 = va[40] * S, y0 = va[41] * S, x1 = vb[40] * S, y1 = vb[41] * S, x2 = vc[40] * S, y2 = vc[41] * S;
            float area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0); if (MathF.Abs(area) < 1e-6f) return;
            int minX = Math.Max(0, (int)MathF.Floor(Math.Min(x0, Math.Min(x1, x2)))), maxX = Math.Min(W - 1, (int)MathF.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int minY = Math.Max(0, (int)MathF.Floor(Math.Min(y0, Math.Min(y1, y2)))), maxY = Math.Min(H - 1, (int)MathF.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
            if (minX > maxX || minY > maxY) return;
            // Gradienten der baryzentrischen Koordinaten l1 (b), l2 (c)
            float l1dx = (y2 - y0) / area, l1dy = -(x2 - x0) / area;
            float l2dx = -(y1 - y0) / area, l2dy = (x1 - x0) / area;
            for (int k = 0; k < NA; k++) { ddx[k] = (vb[k] - va[k]) * l1dx + (vc[k] - va[k]) * l2dx; ddy[k] = (vb[k] - va[k]) * l1dy + (vc[k] - va[k]) * l2dy; }
            for (int py = minY; py <= maxY; py++)
                for (int px = minX; px <= maxX; px++)
                {
                    float sx = px + .5f, sy = py + .5f;
                    float l1 = ((sx - x0) * (y2 - y0) - (sy - y0) * (x2 - x0)) / area, l2 = ((x1 - x0) * (sy - y0) - (y1 - y0) * (sx - x0)) / area, l0 = 1 - l1 - l2;
                    if (l0 < -1e-5f || l1 < -1e-5f || l2 < -1e-5f) continue;
                    for (int k = 0; k < NA; k++) at[k] = va[k] * l0 + vb[k] * l1 + vc[k] * l2;
                    Frag(px, py);
                }
        }

        static float Erf(float x) { float s = MathF.Sign(x); x = MathF.Abs(x); float t = 1f / (1f + .47047f * x); float y = 1f - (.3480242f * t - .0958798f * t * t + .7478556f * t * t * t) * MathF.Exp(-x * x); return s * y; }
        static float Phi(float z) => .5f * (1 + Erf(z * .70710678f));
        static float RBox(float px, float py, float bx, float by, float r)
        {
            float qx = MathF.Abs(px) - bx + r, qy = MathF.Abs(py) - by + r;
            return MathF.Sqrt(MathF.Max(qx, 0) * MathF.Max(qx, 0) + MathF.Max(qy, 0) * MathF.Max(qy, 0)) + MathF.Min(MathF.Max(qx, qy), 0) - r;
        }
        static float Cover(float d, float sw, float sigma) => sw > 0 ? Math.Clamp(Phi((sw * .5f - d) / sigma) - Phi((-sw * .5f - d) / sigma), 0, 1) : Phi(-d / sigma);
        static float Sample(byte[] tex, int ch, int chans, float u, float v)
        {
            float x = u * AT - .5f, y = (1 - v) * AT - .5f; int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y); float fx = x - ix, fy = y - iy;
            float G(int xx, int yy) { xx = Math.Clamp(xx, 0, AT - 1); yy = Math.Clamp(yy, 0, AT - 1); return tex[(yy * AT + xx) * chans + ch] / 255f; }
            return (G(ix, iy) * (1 - fx) + G(ix + 1, iy) * fx) * (1 - fy) + (G(ix, iy + 1) * (1 - fx) + G(ix + 1, iy + 1) * fx) * fy;
        }

        static void Frag(int px, int py)
        {
            // at: 0-3 Farbe, 4-7 t0, 8-11 t1, 12-15 t2, 16-19 t3, 20-23 t4, 24-27 t5, 28-31 t6, 32-35 t7, 36-39 Tangente, 40-41 dpos
            int type = (int)MathF.Round(at[12]); float blur = at[13];
            float p0 = at[4], p1 = at[5], hx = at[8], hy = at[9], rad = at[10], sw = at[11];
            float pxs = MathF.Max(.5f * (MathF.Sqrt(ddx[4] * ddx[4] + ddy[4] * ddy[4]) + MathF.Sqrt(ddx[5] * ddx[5] + ddy[5] * ddy[5])), 1e-4f);
            float sigma = MathF.Max(blur, .45f * pxs);
            float dpx = MathF.Max(MathF.Sqrt(ddx[40] * ddx[40] + ddy[40] * ddy[40]), 1e-4f);
            float cr = at[0], cg = at[1], cb = at[2], a = at[3];
            int gm = (int)MathF.Round(at[35]);
            if (gm > 0)
            {
                float ox = at[32], oy = at[33], tt;
                if (gm == 1) { float gx = at[30] - at[28], gy = at[31] - at[29]; tt = ((ox - at[28]) * gx + (oy - at[29]) * gy) / MathF.Max(gx * gx + gy * gy, 1e-5f); }
                else tt = MathF.Sqrt((ox - at[28]) * (ox - at[28]) + (oy - at[29]) * (oy - at[29])) / MathF.Max(at[30], 1e-5f);
                tt = Math.Clamp(tt, 0, 1); cr += (at[24] - cr) * tt; cg += (at[25] - cg) * tt; cb += (at[26] - cb) * tt; a += (at[27] - a) * tt;
            }
            float r = Lin(cr), g = Lin(cg), b = Lin(cb), cov = 1;
            switch (type)
            {
                case 1: cov = Cover(RBox(p0, p1, hx, hy, rad), sw, sigma); break;
                case 2:
                    {
                        float k0 = MathF.Sqrt((p0 / hx) * (p0 / hx) + (p1 / hy) * (p1 / hy)), k1 = MathF.Sqrt((p0 / (hx * hx)) * (p0 / (hx * hx)) + (p1 / (hy * hy)) * (p1 / (hy * hy)));
                        float d = k1 > 1e-5f ? k0 * (k0 - 1) / k1 : -MathF.Min(hx, hy); cov = Cover(d, sw, sigma); break;
                    }
                case 3: { float t = MathF.Sqrt(p0 * p0 + p1 * p1) / MathF.Max(hx, 1e-4f), f = Math.Clamp(1 - t, 0, 1); cov = f * (.65f + .35f * f); break; }
                case 4:
                    {
                        float rr = MathF.Max(hx, 1e-4f), qx = p0 / rr, qy = p1 / rr, z = MathF.Sqrt(Math.Clamp(1 - qx * qx - qy * qy, 0, 1));
                        float lx = -.45f, ly = -.55f, lz = .75f, ll = MathF.Sqrt(lx * lx + ly * ly + lz * lz); lx /= ll; ly /= ll; lz /= ll;
                        float diff = MathF.Max(0, qx * lx + qy * ly + z * lz), rz = 2 * diff * z - lz, spec = MathF.Pow(MathF.Max(0, rz), 28), rim = MathF.Pow(1 - z, 3);
                        float gq = Math.Clamp(MathF.Sqrt((qx + .35f) * (qx + .35f) + (qy + .4f) * (qy + .4f)) / 1.5f, 0, 1);
                        float L(float c) => gq < .45f ? (c + (1 - c) * .75f) + (c - (c + (1 - c) * .75f)) * (gq / .45f) : c + (c * .4f - c) * ((gq - .45f) / .55f);
                        r = Lin(L(cr)) * (.55f + .6f * diff) + spec * .9f + rim * Lin(cr) * .5f; g = Lin(L(cg)) * (.55f + .6f * diff) + spec * .9f + rim * Lin(cg) * .5f; b = Lin(L(cb)) * (.55f + .6f * diff) + spec * .9f + rim * Lin(cb) * .5f;
                        cov = Cover(MathF.Sqrt(p0 * p0 + p1 * p1) - rr, 0, sigma); break;
                    }
                case 5:
                    {
                        float u = at[6], v = at[7];
                        r *= Lin(Sample(img, 0, 4, u, v)); g *= Lin(Sample(img, 1, 4, u, v)); b *= Lin(Sample(img, 2, 4, u, v)); a *= Sample(img, 3, 4, u, v);
                        cov = rad > 0 ? Cover(RBox(p0, p1, hx, hy, rad), 0, sigma) : 1; break;
                    }
                case 6:
                    {
                        float v = Sample(font, 0, 1, at[6], at[7]), so = at[16], d = (.5f - v) * 2 * so, s = MathF.Max(MathF.Min(blur, so / 3f), .5f * pxs);
                        cov = Cover(d, sw, s) * Math.Clamp((so - d) / (.3f * so), 0, 1); break;
                    }
                case 7: cov = Cover(MathF.Abs(p1) - hy, 0, sigma); break;
                case 8:
                    {
                        float d = RBox(p0, p1, hx, hy, rad), cw = at[16], ch = at[17], hr = at[18], cols = at[19] % 1000, rows = MathF.Floor(at[19] / 1000);
                        float qx = p0 - at[36], qy = p1 - at[37], cix = Math.Clamp(MathF.Floor(qx / cw), 0, cols - 1), ciy = Math.Clamp(MathF.Floor(qy / ch), 0, rows - 1);
                        float hole = MathF.Sqrt((qx - (cix + .5f) * cw) * (qx - (cix + .5f) * cw) + (qy - (ciy + .5f) * ch) * (qy - (ciy + .5f) * ch)) - hr;
                        cov = Cover(MathF.Max(d, -hole), sw, sigma); break;
                    }
                case 9:
                    {
                        float R = hx, ang = MathF.Atan2(p1, p0) * 180 / MathF.PI, rel = ((ang - at[16] + 720) % 360), d;
                        if (rel <= at[17]) d = MathF.Abs(MathF.Sqrt(p0 * p0 + p1 * p1) - R);
                        else
                        {
                            float a0 = at[16] * MathF.PI / 180, a1 = (at[16] + at[17]) * MathF.PI / 180;
                            d = MathF.Min(MathF.Sqrt((p0 - R * MathF.Cos(a0)) * (p0 - R * MathF.Cos(a0)) + (p1 - R * MathF.Sin(a0)) * (p1 - R * MathF.Sin(a0))), MathF.Sqrt((p0 - R * MathF.Cos(a1)) * (p0 - R * MathF.Cos(a1)) + (p1 - R * MathF.Sin(a1)) * (p1 - R * MathF.Sin(a1))));
                        }
                        cov = Cover(d - sw * .5f, 0, sigma); break;
                    }
            }
            if (blur > 0 && (type == 1 || type == 2 || type == 7 || type == 8))
            {
                float ex = (type == 7 ? 1e5f : hx) + 3 * blur + sw * .5f, ey = hy + 3 * blur + sw * .5f;
                cov *= Math.Clamp((ex - MathF.Abs(p0)) / blur, 0, 1) * Math.Clamp((ey - MathF.Abs(p1)) / blur, 0, 1);
            }
            if (at[22] < 9e4f)
            {
                float ccx = (at[20] + at[22]) * .5f, ccy = (at[21] + at[23]) * .5f, chx = MathF.Max((at[22] - at[20]) * .5f, 0), chy = MathF.Max((at[23] - at[21]) * .5f, 0), rr = MathF.Min(at[34], MathF.Min(chx, chy));
                cov *= Math.Clamp(.5f - RBox(at[40] - ccx, at[41] - ccy, chx, chy, rr) / dpx, 0, 1);
            }
            a *= cov; if (a <= .0005f) return;
            float inten = at[14], add = at[15];
            int i = (py * W + px) * 3; float k = 1 - a * (1 - add);
            buf[i] = r * inten * a + buf[i] * k; buf[i + 1] = g * inten * a + buf[i + 1] * k; buf[i + 2] = b * inten * a + buf[i + 2] * k;
        }

        // --- Bloom (Mip-Kette), Neutral-Tonemapping, Vignette
        static void Post()
        {
            var levels = new List<(float[] d, int w, int h)>();
            int w = W / 2, h = H / 2; var cur = new float[w * h * 3];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int c = 0; c < 3; c++)
                    {
                        float s = 0; for (int yy = 0; yy < 2; yy++) for (int xx = 0; xx < 2; xx++) s += buf[((y * 2 + yy) * W + x * 2 + xx) * 3 + c];
                        s /= 4; cur[(y * w + x) * 3 + c] = MathF.Max(0, s - .92f) / MathF.Max(1, 1);
                    }
            levels.Add((cur, w, h));
            for (int l = 0; l < 6 && w > 8; l++)
            {
                int nw = w / 2, nh = h / 2; var nx = new float[nw * nh * 3];
                for (int y = 0; y < nh; y++) for (int x = 0; x < nw; x++) for (int c = 0; c < 3; c++)
                        {
                            float s = 0; int n = 0;
                            for (int yy = -1; yy <= 2; yy++) for (int xx = -1; xx <= 2; xx++) { int sx = Math.Clamp(x * 2 + xx, 0, w - 1), sy = Math.Clamp(y * 2 + yy, 0, h - 1); s += cur[(sy * w + sx) * 3 + c]; n++; }
                            nx[(y * nw + x) * 3 + c] = s / n;
                        }
                cur = nx; w = nw; h = nh; levels.Add((cur, w, h));
            }
            float Samp((float[] d, int w, int h) L, float u, float v, int c)
            {
                float x = u * L.w - .5f, y = v * L.h - .5f; int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y); float fx = x - ix, fy = y - iy;
                float G(int xx, int yy) => L.d[(Math.Clamp(yy, 0, L.h - 1) * L.w + Math.Clamp(xx, 0, L.w - 1)) * 3 + c];
                return (G(ix, iy) * (1 - fx) + G(ix + 1, iy) * fx) * (1 - fy) + (G(ix, iy + 1) * (1 - fx) + G(ix + 1, iy + 1) * fx) * fy;
            }
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W, v = (y + .5f) / H;
                    float vr = MathF.Sqrt((u - .5f) * (u - .5f) * 1.6f + (v - .5f) * (v - .5f)) / .72f, vig = 1 - .22f * SS(.45f, 1.1f, vr);
                    for (int c = 0; c < 3; c++)
                    {
                        float bl = 0, wsum = 0; for (int l = 1; l < levels.Count; l++) { float wt = MathF.Pow(.72f, l - 1); bl += Samp(levels[l], u, v, c) * wt; wsum += wt; }
                        float col = buf[(y * W + x) * 3 + c] + bl / wsum * 1.15f * 2.2f;
                        buf[(y * W + x) * 3 + c] = Neutral(col) * vig;
                    }
                }
        }
        static float NC(float x) { const float a = .2f, b = .29f, c = .24f, d = .272f, e = .02f, f = .3f; return ((x * (a * x + c * b) + d * e) / (x * (a * x + b) + d * f)) - e / f; }
        static float Neutral(float x) { float ws = 1 / NC(5.3f); return NC(x * ws) * ws; }

        // --- PNG schreiben
        static void WritePng(string path)
        {
            var raw = new byte[(W * 3 + 1) * H];
            for (int y = 0; y < H; y++) { raw[y * (W * 3 + 1)] = 0; for (int x = 0; x < W * 3; x++) raw[y * (W * 3 + 1) + 1 + x] = (byte)Math.Clamp((int)(Srgb(buf[y * W * 3 + x]) * 255 + .5f), 0, 255); }
            byte[] z; using (var ms = new MemoryStream()) { using (var zs = new ZLibStream(ms, CompressionLevel.Fastest)) zs.Write(raw, 0, raw.Length); z = ms.ToArray(); }
            using var fs = File.Create(path);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            void Chunk(string type, byte[] data)
            {
                var len = BitConverter.GetBytes(data.Length); Array.Reverse(len); fs.Write(len);
                var td = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); fs.Write(td);
                var crc = BitConverter.GetBytes(Crc(td)); Array.Reverse(crc); fs.Write(crc);
            }
            var ih = new byte[13]; var wb = BitConverter.GetBytes(W); Array.Reverse(wb); var hb = BitConverter.GetBytes(H); Array.Reverse(hb);
            Array.Copy(wb, 0, ih, 0, 4); Array.Copy(hb, 0, ih, 4, 4); ih[8] = 8; ih[9] = 2;
            Chunk("IHDR", ih); Chunk("IDAT", z); Chunk("IEND", new byte[0]);
        }
        static uint[] crcT;
        static uint Crc(byte[] d)
        {
            if (crcT == null) { crcT = new uint[256]; for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; crcT[n] = c; } }
            uint crc = 0xFFFFFFFFu; foreach (var b in d) crc = crcT[(crc ^ b) & 0xFF] ^ (crc >> 8); return crc ^ 0xFFFFFFFFu;
        }
    }
}
