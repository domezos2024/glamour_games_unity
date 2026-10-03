using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlamourGames
{
    /// <summary>
    /// Echter 3D-Siegerpokal: prozeduraler Drehkoerper (Fuss, Nodus, Kelch mit Lippe und Innenwand), Henkel als
    /// Rohr entlang einer Bezierkurve, aufgesetzter Stern und Plakette auf zweistufigem Klavierlack-Sockel.
    /// PBR-Gold (Metallic 1, hohe Glaette) spiegelt eine eigene HDR-Studioumgebung (Softbox, Lichtstreifen,
    /// Bokeh-Punkte); Glanzlichter ueber 1.0 erfasst der URP-Bloom. Eigene Kamera und Render-Textur in
    /// Bildschirmaufloesung (gestochen scharf), Einblendung ueber den 2D-Canvas (Shape-Typ HERO).
    /// </summary>
    public static class RewardRig
    {
        const int Layer = 29; const float MeshH = 4.12f, Fov = 17f, Pitch = 8f;
        static readonly Vector3 Origin = new Vector3(600, 0, 0);
        static Camera cam; static RenderTexture rt; static Material shapeMat; static Transform pivot; static int usedFrame = -1; static bool hdr;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.SetupHero = Setup;

        static void Setup(Camera main, Material shape)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset ua)) return;
            var baseMat = Resources.Load<Material>("Glamour/DiceMat");
            if (baseMat == null) { var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh != null) baseMat = new Material(sh); }
            if (baseMat == null) { Log.I("pokal3d: kein Lit-Shader"); return; }
            shapeMat = shape; main.cullingMask &= ~(1 << Layer);
            hdr = ua.supportsHDR && ua.hdrColorBufferPrecision == HDRColorBufferPrecision._64Bits;

            var root = new GameObject("Glamour Trophy3D"); Object.DontDestroyOnLoad(root); root.transform.position = Origin;
            var cg = new GameObject("Trophy Camera"); cg.transform.SetParent(root.transform, false);
            cam = cg.AddComponent<Camera>();
            cam.fieldOfView = Fov; cam.nearClipPlane = 1; cam.farClipPlane = 60; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer; cam.allowHDR = hdr; cam.allowMSAA = true; cam.depth = main.depth - 3; cam.useOcclusionCulling = false;
            var cd = cam.GetUniversalAdditionalCameraData(); cd.renderPostProcessing = false; cd.antialiasing = AntialiasingMode.None; cd.renderShadows = false; cd.requiresDepthTexture = false; cd.requiresColorTexture = false;
            float dist = (MeshH * .56f) / Mathf.Tan(Fov * .5f * Mathf.Deg2Rad);
            var rot = Quaternion.Euler(Pitch, 0, 0); var target = Origin + new Vector3(0, MeshH * .5f, 0);
            cam.transform.SetPositionAndRotation(target - rot * Vector3.forward * dist, rot);
            cam.enabled = false;

            Light(root, "Key", new Vector3(.5f, -.45f, .75f), new Color(1f, .96f, .88f), 1.6f);
            Light(root, "Fill", new Vector3(-.7f, -.1f, .7f), new Color(.7f, .78f, 1f), .45f);
            Light(root, "RimL", new Vector3(.75f, -.2f, -.6f), new Color(1f, .9f, .75f), 1.3f);
            Light(root, "RimR", new Vector3(-.75f, -.25f, -.6f), new Color(.85f, .8f, 1f), 1.1f);

            var cube = BuildShowroom();
            var pg = new GameObject("Trophy Reflection") { layer = Layer }; pg.transform.SetParent(root.transform, false);
            var probe = pg.AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Custom; probe.customBakedTexture = cube; probe.size = new Vector3(30, 30, 30); probe.importance = 200; probe.intensity = 1.15f; probe.boxProjection = false;

            Material Mk(string n, Color col, float metal, float smooth)
            {
                var m = new Material(baseMat) { name = "Trophy " + n }; m.SetColor("_BaseColor", col); m.SetTexture("_BaseMap", Texture2D.whiteTexture);
                m.SetTexture("_BumpMap", FlatNormal()); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth); return m;
            }
            var mats = new[] { Mk("Gold", new Color(1f, .8f, .42f), 1, .93f), Mk("Satin", new Color(1f, .76f, .38f), 1, .66f), Mk("Lack", new Color(.018f, .016f, .022f), 0, .96f) };

            pivot = new GameObject("Trophy") { layer = Layer }.transform; pivot.SetParent(root.transform, false);
            pivot.gameObject.AddComponent<MeshFilter>().sharedMesh = BuildTrophy();
            var r = pivot.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes; r.lightProbeUsage = LightProbeUsage.Off;
            pivot.gameObject.SetActive(false);

            Trophy3D.Render3D = Render;
            var prevEnd = Die3D.FrameEnd; Die3D.FrameEnd = () => { prevEnd?.Invoke(); EndFrame(); };
            Log.I("pokal3d bereit" + (hdr ? " (HDR)" : ""));
        }

        static void Light(GameObject root, string name, Vector3 dir, Color col, float intensity)
        {
            var g = new GameObject("Trophy " + name + " Light"); g.transform.SetParent(root.transform, false); g.transform.rotation = Quaternion.LookRotation(dir.normalized);
            var l = g.AddComponent<Light>(); l.type = LightType.Directional; l.color = col; l.intensity = intensity; l.shadows = LightShadows.None; l.cullingMask = 1 << Layer;
        }

        static bool Render(Canvas2D c, Box view, float spin, float alpha)
        {
            if (cam == null || usedFrame == Time.frameCount) return false;
            float k = Mathf.Clamp(Screen.height / 900f, 1f, 2.5f);
            int w = Mathf.Max(64, Mathf.RoundToInt(view.Width * k)), h = Mathf.Max(64, Mathf.RoundToInt(view.Height * k));
            // in 64er-Schritten aufrunden, damit Skalier-Animationen keine Neuanlage pro Frame ausloesen
            if (rt == null || !rt.IsCreated() || w > rt.width || h > rt.height || w < rt.width * .6f)
            {
                if (rt != null) { cam.targetTexture = null; rt.Release(); Object.Destroy(rt); }
                w = (w + 63) / 64 * 64; h = (h + 63) / 64 * 64;
                rt = new RenderTexture(w, h, 24, hdr ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32, hdr ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB) { name = "GlamourTrophyRT", antiAliasing = 8, useMipMap = false, filterMode = FilterMode.Bilinear };
                rt.Create(); cam.targetTexture = rt; shapeMat.SetTexture("_HeroTex", rt);
            }
            cam.aspect = view.Width / view.Height;
            if (!pivot.gameObject.activeSelf) pivot.gameObject.SetActive(true);
            pivot.localRotation = Quaternion.Euler(0, -spin * Mathf.Rad2Deg, 0);
            cam.enabled = true; usedFrame = Time.frameCount;
            c.DrawHero(view, Gfx.Fill(Col.White.A(alpha)));
            return true;
        }

        static void EndFrame()
        {
            if (cam == null || usedFrame == Time.frameCount) return;
            cam.enabled = false; if (pivot.gameObject.activeSelf) pivot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Modell
        sealed class MB
        {
            public readonly List<Vector3> P = new List<Vector3>(), N = new List<Vector3>(); public readonly List<Vector2> U = new List<Vector2>(); public readonly List<Vector4> T = new List<Vector4>();
            public readonly List<int>[] Sub = { new List<int>(), new List<int>(), new List<int>() };
            public int V(Vector3 p, Vector3 n, Vector2 uv, Vector4 t) { P.Add(p); N.Add(n.normalized); U.Add(uv); T.Add(t); return P.Count - 1; }
            /// <summary>Dreieck mit Ausrichtung nach 'outward'.</summary>
            public void Tri(int s, int a, int b, int c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(P[b] - P[a], P[c] - P[a]), outward) < 0) { int x = b; b = c; c = x; }
                Sub[s].Add(a); Sub[s].Add(b); Sub[s].Add(c);
            }
        }

        /// <summary>Drehkoerper aus Profil (r, y). Glatt mit Profilnormalen oder facettiert (seg = 4: quadratischer Sockel).</summary>
        static void Lathe(MB m, int sub, Vector2[] prof, int seg, bool flat, float a0 = 0)
        {
            int P = prof.Length;
            if (flat)
            {
                for (int k = 0; k < seg; k++)
                {
                    float aa = a0 + k * Mathf.PI * 2 / seg, ab = a0 + (k + 1) * Mathf.PI * 2 / seg;
                    for (int j = 0; j < P - 1; j++)
                    {
                        Vector3 A(Vector2 q, float an) => new Vector3(Mathf.Cos(an) * q.x, q.y, Mathf.Sin(an) * q.x);
                        Vector3 p0 = A(prof[j], aa), p1 = A(prof[j], ab), p2 = A(prof[j + 1], ab), p3 = A(prof[j + 1], aa);
                        var n = Vector3.Cross(p2 - p0, p1 - p3); var mid = (p0 + p1 + p2 + p3) / 4; var outward = new Vector3(mid.x, 0, mid.z) * .2f + Vector3.up * (prof[j + 1].x < prof[j].x ? 1 : prof[j + 1].x > prof[j].x ? -1 : 0);
                        if (Vector3.Dot(n, outward) < 0) n = -n; if (n.sqrMagnitude < 1e-8f) n = Vector3.up;
                        var t = new Vector4(-Mathf.Sin(aa), 0, Mathf.Cos(aa), 1);
                        int i0 = m.V(p0, n, new Vector2(0, 0), t), i1 = m.V(p1, n, new Vector2(1, 0), t), i2 = m.V(p2, n, new Vector2(1, 1), t), i3 = m.V(p3, n, new Vector2(0, 1), t);
                        m.Tri(sub, i0, i1, i2, n); m.Tri(sub, i0, i2, i3, n);
                    }
                }
                return;
            }
            var pn = new Vector2[P];
            for (int j = 0; j < P; j++) { var d = prof[Mathf.Min(P - 1, j + 1)] - prof[Mathf.Max(0, j - 1)]; pn[j] = new Vector2(d.y, -d.x).normalized; }
            int b0 = m.P.Count;
            for (int k = 0; k <= seg; k++)
            {
                float a = a0 + k * Mathf.PI * 2 / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int j = 0; j < P; j++) m.V(new Vector3(ca * prof[j].x, prof[j].y, sa * prof[j].x), new Vector3(ca * pn[j].x, pn[j].y, sa * pn[j].x), new Vector2(k / (float)seg, j / (float)(P - 1)), new Vector4(-sa, 0, ca, 1));
            }
            for (int k = 0; k < seg; k++)
                for (int j = 0; j < P - 1; j++)
                {
                    int q = b0 + k * P + j; var o = m.N[q] + m.N[q + P + 1]; if (o.sqrMagnitude < 1e-6f) o = m.N[q + 1];
                    m.Tri(sub, q, q + P, q + 1, o); m.Tri(sub, q + 1, q + P, q + P + 1, o);
                }
        }

        /// <summary>Rohr (Henkel) entlang einer kubischen Bezierkurve in der xy-Ebene.</summary>
        static void Tube(MB m, int sub, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float rad, int steps = 48, int ring = 20)
        {
            Vector3 B(float t) { float u = 1 - t; return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3; }
            int b0 = m.P.Count;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps; var c = B(t); var tg = (B(Mathf.Min(1, t + .01f)) - B(Mathf.Max(0, t - .01f))).normalized;
                var nx = new Vector3(-tg.y, tg.x, 0).normalized; var bz = Vector3.forward; float rr = rad * (1 + .25f * Mathf.Sin(t * Mathf.PI));
                for (int j = 0; j <= ring; j++)
                {
                    float a = j * Mathf.PI * 2 / ring; var n = nx * Mathf.Cos(a) + bz * Mathf.Sin(a);
                    m.V(c + n * rr, n, new Vector2(t, j / (float)ring), new Vector4(tg.x, tg.y, tg.z, 1));
                }
            }
            int R = ring + 1;
            for (int i = 0; i < steps; i++)
                for (int j = 0; j < ring; j++) { int q = b0 + i * R + j; var o = m.N[q] + m.N[q + R + 1]; m.Tri(sub, q, q + R, q + 1, o); m.Tri(sub, q + 1, q + R, q + R + 1, o); }
        }

        /// <summary>Flacher Quader (Plakette, Leisten).</summary>
        static void Slab(MB m, int sub, Vector3 c, Vector3 half)
        {
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in dirs)
            {
                Vector3 u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up, v = Vector3.Cross(n, u);
                Vector3 S(Vector3 d) => Vector3.Scale(d, half);
                Vector3 f = c + S(n), du = S(u), dv = S(v); var t = new Vector4(u.x, u.y, u.z, 1);
                int a = m.V(f - du - dv, n, new Vector2(0, 0), t), b = m.V(f + du - dv, n, new Vector2(1, 0), t), cc = m.V(f + du + dv, n, new Vector2(1, 1), t), d = m.V(f - du + dv, n, new Vector2(0, 1), t);
                m.Tri(sub, a, b, cc, n); m.Tri(sub, a, cc, d, n);
            }
        }

        /// <summary>Erhabener fuenfzackiger Stern auf der Kelchvorderseite (-z), der Wandneigung folgend.</summary>
        static void Star(MB m, int sub, Vector3 c, float r, float depth, float tilt)
        {
            var rotq = Quaternion.Euler(-tilt, 0, 0); var fn = rotq * Vector3.back; var t = new Vector4(1, 0, 0, 1);
            var outer = new Vector3[10];
            for (int i = 0; i < 10; i++) { float a = Mathf.PI / 2 + i * Mathf.PI / 5, rr = i % 2 == 0 ? r : r * .42f; outer[i] = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0); }
            var tip = c + rotq * new Vector3(0, 0, -depth * 1.6f);
            for (int i = 0; i < 10; i++)
            {
                Vector3 a = c + rotq * (outer[i] + new Vector3(0, 0, -depth)), b = c + rotq * (outer[(i + 1) % 10] + new Vector3(0, 0, -depth));
                var n = Vector3.Cross(b - tip, a - tip).normalized; if (Vector3.Dot(n, fn) < 0) n = -n;
                int i0 = m.V(tip, n, new Vector2(.5f, .5f), t), i1 = m.V(a, n, new Vector2(0, 0), t), i2 = m.V(b, n, new Vector2(1, 0), t); m.Tri(sub, i0, i1, i2, n);
                Vector3 a2 = c + rotq * outer[i], b2 = c + rotq * outer[(i + 1) % 10]; var sn = Vector3.Cross(b - a, a2 - a).normalized; var mid = (a + b) / 2 - c; if (Vector3.Dot(sn, mid) < 0) sn = -sn;
                int s0 = m.V(a, sn, Vector2.zero, t), s1 = m.V(b, sn, Vector2.right, t), s2 = m.V(b2, sn, Vector2.one, t), s3 = m.V(a2, sn, Vector2.up, t); m.Tri(sub, s0, s1, s2, sn); m.Tri(sub, s0, s2, s3, sn);
            }
        }

        static Mesh BuildTrophy()
        {
            var m = new MB(); const float q45 = Mathf.PI / 4, sq = 1.41421356f;
            Vector2[] Box(float hw, float y0, float y1, float bev) => new[] { new Vector2(hw * sq, y0), new Vector2(hw * sq, y1 - bev), new Vector2((hw - bev) * sq, y1), new Vector2(0, y1) };
            Lathe(m, 2, Box(1.06f, 0, .56f, .03f), 4, true, q45);
            Lathe(m, 1, Box(.99f, .56f, .63f, .015f), 4, true, q45);
            Lathe(m, 2, Box(.8f, .63f, 1.0f, .025f), 4, true, q45);
            Lathe(m, 1, Box(.7f, 1.0f, 1.05f, .012f), 4, true, q45);
            Slab(m, 1, new Vector3(0, .28f, -1.06f - .012f), new Vector3(.62f, .15f, .012f));
            Lathe(m, 1, new[] { new Vector2(.66f, 1.05f), new Vector2(.665f, 1.08f), new Vector2(.66f, 1.11f) }, 96, false);
            Lathe(m, 0, new[] {
                new Vector2(.66f, 1.11f), new Vector2(.61f, 1.16f), new Vector2(.47f, 1.23f), new Vector2(.33f, 1.32f), new Vector2(.21f, 1.44f), new Vector2(.145f, 1.58f), new Vector2(.12f, 1.73f), new Vector2(.115f, 1.88f),
                new Vector2(.17f, 1.9f), new Vector2(.235f, 1.955f), new Vector2(.262f, 2.03f), new Vector2(.235f, 2.105f), new Vector2(.17f, 2.16f), new Vector2(.115f, 2.18f),
                new Vector2(.11f, 2.3f), new Vector2(.13f, 2.4f), new Vector2(.2f, 2.48f), new Vector2(.31f, 2.53f), new Vector2(.46f, 2.59f), new Vector2(.62f, 2.7f), new Vector2(.76f, 2.88f), new Vector2(.86f, 3.1f),
                new Vector2(.92f, 3.35f), new Vector2(.95f, 3.6f), new Vector2(.965f, 3.85f), new Vector2(.985f, 3.98f), new Vector2(1.02f, 4.03f), new Vector2(1.032f, 4.07f), new Vector2(1.0f, 4.11f), new Vector2(.96f, 4.09f),
                new Vector2(.94f, 4.04f), new Vector2(.93f, 3.8f), new Vector2(.9f, 3.5f), new Vector2(.83f, 3.2f), new Vector2(.7f, 2.95f), new Vector2(.5f, 2.79f), new Vector2(.25f, 2.71f), new Vector2(.0f, 2.69f) }, 128, false);
            for (int sd = -1; sd <= 1; sd += 2)
                Tube(m, 0, new Vector3(sd * .9f, 3.78f, 0), new Vector3(sd * 1.6f, 3.92f, 0), new Vector3(sd * 1.62f, 2.92f, 0), new Vector3(sd * .64f, 2.74f, 0), .068f);
            Star(m, 0, new Vector3(0, 3.32f, -.9f), .3f, .05f, 13f);
            var mesh = new Mesh { name = "GlamourTrophy", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(m.P); mesh.SetNormals(m.N); mesh.SetUVs(0, m.U); mesh.SetTangents(m.T);
            mesh.subMeshCount = 3; for (int s = 0; s < 3; s++) mesh.SetTriangles(m.Sub[s], s);
            mesh.RecalculateBounds(); return mesh;
        }

        static Texture2D flatN;
        static Texture2D FlatNormal()
        {
            if (flatN != null) return flatN;
            flatN = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "TrophyFlatNormal" }; var px = new Color[16]; for (int k = 0; k < 16; k++) px[k] = new Color(.5f, .5f, 1, 1); flatN.SetPixels(px); flatN.Apply(false, true); return flatN;
        }

        // ------------------------------------------------------------------ HDR-Studioumgebung fuer Metallspiegelungen
        static Cubemap BuildShowroom()
        {
            const int S = 128; var cube = new Cubemap(S, TextureFormat.RGBAHalf, true) { name = "GlamourShowroom" };
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            var bokeh = new List<(Vector3 d, Color c, float s)>(); var rnd = new System.Random(7);
            for (int i = 0; i < 40; i++)
            {
                var d = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 1.2f - .2f, (float)rnd.NextDouble() * 2 - 1).normalized;
                var c = Color.HSVToRGB((float)rnd.NextDouble(), .35f + .4f * (float)rnd.NextDouble(), 1) * (2.5f + 4 * (float)rnd.NextDouble()); bokeh.Add((d, c, .9975f - .004f * (float)rnd.NextDouble()));
            }
            var buf = new Color[S * S];
            foreach (var f in faces)
            {
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float u = (x + .5f) / S * 2 - 1, v = (y + .5f) / S * 2 - 1; Vector3 d;
                        switch (f)
                        {
                            case CubemapFace.PositiveX: d = new Vector3(1, -v, -u); break;
                            case CubemapFace.NegativeX: d = new Vector3(-1, -v, u); break;
                            case CubemapFace.PositiveY: d = new Vector3(u, 1, v); break;
                            case CubemapFace.NegativeY: d = new Vector3(u, -1, -v); break;
                            case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1); break;
                            default: d = new Vector3(-u, -v, -1); break;
                        }
                        d.Normalize(); float h = d.y;
                        // Boden dunkel-warm, Horizont goldbraun, Himmel tief violett
                        var c = h < 0 ? Color.Lerp(new Color(.42f, .3f, .18f), new Color(.06f, .04f, .04f), Mathf.Clamp01(-h * 1.6f)) : Color.Lerp(new Color(.62f, .46f, .3f), new Color(.1f, .07f, .16f), Mathf.Clamp01(h * 1.4f));
                        c += new Color(1f, .78f, .5f) * (1.1f * Mathf.Exp(-h * h * 60));
                        if (d.z < -.4f) c += new Color(1f, .93f, .82f) * (.9f * Mathf.SmoothStep(0, 1, (-d.z - .4f) * 2.5f) * Mathf.Clamp01(1 - Mathf.Abs(h - .15f) * 1.6f));
                        // Softbox oben (leicht nach vorne), Lichtstreifen links/rechts vorne, Gegenlicht-Streifen hinten
                        if (h > .78f && d.z < .25f) c += new Color(5f, 4.8f, 4.5f) * Mathf.SmoothStep(0, 1, (h - .78f) * 12);
                        float sx = Mathf.Abs(d.x);
                        if (d.z < -.3f && sx > .55f && sx < .72f && h > -.25f && h < .55f) c += new Color(3.4f, 3.5f, 4f);
                        if (d.z > .45f && sx > .35f && sx < .45f && h > -.1f && h < .5f) c += new Color(2.6f, 2f, 1.4f);
                        if (d.z < -.85f && h > .05f && h < .3f) c += new Color(.9f, .85f, .8f);
                        foreach (var b in bokeh) { float k = Vector3.Dot(d, b.d); if (k > b.s) c += b.c * Mathf.SmoothStep(0, 1, (k - b.s) / (1 - b.s) * 2.5f); }
                        c.a = 1; buf[y * S + x] = c;
                    }
                cube.SetPixels(buf, f);
            }
            cube.Apply(true, true);
            return cube;
        }
    }
}
