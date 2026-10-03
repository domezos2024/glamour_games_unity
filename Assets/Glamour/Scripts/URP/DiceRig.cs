using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlamourGames
{
    /// <summary>
    /// Echte 3D-Wuerfel: abgerundeter, geschlossener Wuerfelkoerper (Mesh), URP-Lit-Material mit eingelassenen Augen
    /// (Normal-Map + Ambient-Occlusion im Albedo), Hauptlicht, Fuell- und Kantenlicht sowie Studio-Spiegelung.
    /// Eine eigene Kamera rendert bis zu 8 Wuerfel pro Frame in Kacheln einer Render-Textur, die der 2D-Canvas
    /// an der Stelle des bisherigen Wuerfels einblendet (Reihenfolge/Ueberlagerung bleiben dadurch erhalten).
    /// </summary>
    public static class DiceRig
    {
        const int Layer = 31, Cols = 4, Rows = 2, TilePx = 448, TexTile = 256;
        const float TileW = 4f, Bevel = .2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.SetupDice = Setup;

        static Camera cam; static RenderTexture rt; static Material shapeMat, baseMat; static Mesh mesh; static Texture2D normalTex;
        static readonly Transform[] dice = new Transform[Cols * Rows];
        static readonly MeshRenderer[] rends = new MeshRenderer[Cols * Rows];
        static readonly Dictionary<(uint, uint), Material> mats = new Dictionary<(uint, uint), Material>();
        static int used;

        static void Setup(Camera main, Material shape)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;
            baseMat = Resources.Load<Material>("Glamour/DiceMat");
            if (baseMat == null) { var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh != null) baseMat = new Material(sh); }
            if (baseMat == null) { Log.I("dice3d: kein Lit-Shader"); return; }
            shapeMat = shape;
            main.cullingMask &= ~(1 << Layer);

            mesh = BuildMesh(); normalTex = BuildNormalMap();
            rt = new RenderTexture(Cols * TilePx, Rows * TilePx, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "GlamourDiceRT", antiAliasing = 8, useMipMap = false, filterMode = FilterMode.Bilinear };
            rt.Create();
            shapeMat.SetTexture("_DiceTex", rt);

            var root = new GameObject("Glamour Dice3D"); UnityEngine.Object.DontDestroyOnLoad(root);
            var cg = new GameObject("Dice Camera"); cg.transform.SetParent(root.transform, false);
            cam = cg.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Rows * TileW / 2; cam.transform.position = new Vector3(0, 0, -20);
            cam.nearClipPlane = 1; cam.farClipPlane = 40; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer; cam.targetTexture = rt; cam.allowHDR = false; cam.allowMSAA = true; cam.depth = main.depth - 1; cam.useOcclusionCulling = false;
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = false; cd.antialiasing = AntialiasingMode.None; cd.renderShadows = false; cd.requiresDepthTexture = false; cd.requiresColorTexture = false;

            Light(root, "Key", new Vector3(.45f, -.62f, .65f), new Color(1f, .97f, .92f), 1.55f);
            Light(root, "Fill", new Vector3(-.6f, -.15f, .75f), new Color(.75f, .82f, 1f), .38f);
            Light(root, "Rim", new Vector3(-.25f, -.35f, -.9f), new Color(1f, .85f, 1f), .55f);

            var sh2 = new SphericalHarmonicsL2(); sh2.AddAmbientLight(new Color(.16f, .15f, .2f)); sh2.AddDirectionalLight(Vector3.up, new Color(.22f, .21f, .26f), 1); sh2.AddDirectionalLight(Vector3.down, new Color(.05f, .03f, .08f), 1);
            RenderSettings.ambientMode = AmbientMode.Custom; RenderSettings.ambientProbe = sh2;

            var cube = BuildStudioCubemap();
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = cube; RenderSettings.reflectionIntensity = 1;
            var pg = new GameObject("Dice Reflection"); pg.transform.SetParent(root.transform, false); pg.layer = Layer;
            var probe = pg.AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Custom; probe.customBakedTexture = cube; probe.size = new Vector3(100, 100, 100); probe.importance = 100; probe.intensity = 1;

            for (int i = 0; i < dice.Length; i++)
            {
                var g = new GameObject("Die " + i) { layer = Layer }; g.transform.SetParent(root.transform, false);
                g.transform.position = TileCenter(i);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = baseMat;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes; r.lightProbeUsage = LightProbeUsage.Off;
                g.SetActive(false); dice[i] = g.transform; rends[i] = r;
            }
            Die3D.Render3D = Render; Die3D.FrameBegin = () => used = 0; Die3D.FrameEnd = EndFrame;
            cam.enabled = false;
            Log.I("dice3d bereit");
        }

        static void Light(GameObject root, string name, Vector3 dir, Color col, float intensity)
        {
            var g = new GameObject(name + " Light"); g.transform.SetParent(root.transform, false); g.transform.rotation = Quaternion.LookRotation(dir.normalized);
            var l = g.AddComponent<Light>(); l.type = LightType.Directional; l.color = col; l.intensity = intensity; l.shadows = LightShadows.None; l.cullingMask = 1 << Layer;
        }

        static Vector3 TileCenter(int i) => new Vector3(-Cols * TileW / 2 + TileW * (i % Cols + .5f), Rows * TileW / 2 - TileW * (i / Cols + .5f), 0);

        static void EndFrame()
        {
            for (int i = used; i < dice.Length; i++) if (dice[i].gameObject.activeSelf) dice[i].gameObject.SetActive(false);
            if (cam != null) cam.enabled = used > 0;
            if (rt != null && !rt.IsCreated()) { rt.Create(); shapeMat.SetTexture("_DiceTex", rt); }
        }

        static bool Render(Canvas2D c, float cx, float cy, float size, float[] m, Col body, Col pip)
        {
            if (used >= dice.Length || cam == null) return false;
            int i = used++;
            var t = dice[i]; if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
            // m bildet Wuerfelraum (z zum Betrachter) auf Sichtraum ab; Unity: z vom Betrachter weg -> R = S*m*S mit S = diag(1,1,-1)
            var R = new Matrix4x4();
            R.m00 = m[0]; R.m01 = m[1]; R.m02 = -m[2];
            R.m10 = m[3]; R.m11 = m[4]; R.m12 = -m[5];
            R.m20 = -m[6]; R.m21 = -m[7]; R.m22 = m[8]; R.m33 = 1;
            t.rotation = Quaternion.LookRotation(R.GetColumn(2), R.GetColumn(1));
            rends[i].sharedMaterial = MaterialFor(body, pip);
            float u0 = (i % Cols) / (float)Cols, v0 = 1 - (i / Cols) / (float)Rows;
            c.DrawDice(Gfx.Ctr(cx, cy, size * 2, size * 2), new Vector4(u0, v0, u0 + 1f / Cols, v0 - 1f / Rows), Gfx.Fill(Col.White));
            return true;
        }

        static uint Key(Col c) => (uint)(c.Red << 24 | c.Green << 16 | c.Blue << 8 | c.Alpha);

        static Material MaterialFor(Col body, Col pip)
        {
            var k = (Key(body), Key(pip));
            if (mats.TryGetValue(k, out var mat)) return mat;
            mat = new Material(baseMat) { name = "Die " + k };
            mat.SetTexture("_BaseMap", BuildAlbedo(body, pip)); mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", normalTex); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_Smoothness", .82f); mat.SetFloat("_Metallic", 0f); mat.SetFloat("_EnvironmentReflections", 1f); mat.SetFloat("_SpecularHighlights", 1f);
            mats[k] = mat; return mat;
        }

        // ------------------------------------------------------------------ Geometrie: abgerundeter Wuerfel
        static Vector3 U(float[] v) => new Vector3(v[0], v[1], -v[2]); // Wuerfelraum (z zum Betrachter) -> Unity

        static Mesh BuildMesh()
        {
            var grid = new List<float>(); const int K = 7;
            for (int k = 0; k <= K; k++) grid.Add(-1 + Bevel * k / K);
            for (int k = 1; k < 6; k++) grid.Add(-(1 - Bevel) + 2 * (1 - Bevel) * k / 6f);
            for (int k = 0; k <= K; k++) grid.Add((1 - Bevel) + Bevel * k / K);
            int n = grid.Count;
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var uv = new List<Vector2>(); var tan = new List<Vector4>(); var idx = new List<int>();
            for (int f = 0; f < 6; f++)
            {
                var fn = U(Die3D.Normal(f)); var fu = U(Die3D.UAxis(f)); var fv = U(Die3D.VAxis(f));
                int val = Die3D.FaceValues[f]; float tx = (val - 1) % 3, ty = (val - 1) / 3;
                float w = Mathf.Sign(Vector3.Dot(Vector3.Cross(fn, fu), fv));
                int b0 = pos.Count;
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float a = grid[i], b = grid[j];
                        var p = fn + fu * a + fv * b;
                        var inner = new Vector3(Mathf.Clamp(p.x, -(1 - Bevel), 1 - Bevel), Mathf.Clamp(p.y, -(1 - Bevel), 1 - Bevel), Mathf.Clamp(p.z, -(1 - Bevel), 1 - Bevel));
                        var d = p - inner; var dn = d.sqrMagnitude > 1e-8f ? d.normalized : fn;
                        pos.Add(inner + dn * Bevel); nor.Add(dn);
                        uv.Add(new Vector2((tx + (a + 1) / 2) / 3f, (ty + (b + 1) / 2) / 2f));
                        var tg = (fu - dn * Vector3.Dot(fu, dn)).normalized; tan.Add(new Vector4(tg.x, tg.y, tg.z, w));
                    }
                // Unity: Vorderseite, wenn Cross(B-A, C-A) nach aussen zeigt (an einer flachen Mittelzelle bestimmt)
                int qc = b0 + (n / 2) * n + n / 2;
                bool cw = Vector3.Dot(Vector3.Cross(pos[qc + 1] - pos[qc], pos[qc + n] - pos[qc]), fn) > 0;
                for (int j = 0; j < n - 1; j++)
                    for (int i = 0; i < n - 1; i++)
                    {
                        int q = b0 + j * n + i;
                        if (cw) { idx.Add(q); idx.Add(q + 1); idx.Add(q + n); idx.Add(q + 1); idx.Add(q + n + 1); idx.Add(q + n); }
                        else { idx.Add(q); idx.Add(q + n); idx.Add(q + 1); idx.Add(q + 1); idx.Add(q + n); idx.Add(q + n + 1); }
                    }
            }
            var mesh = new Mesh { name = "GlamourDie" };
            mesh.SetVertices(pos); mesh.SetNormals(nor); mesh.SetUVs(0, uv); mesh.SetTangents(tan); mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ Texturen: 3x2 Kacheln (Augenzahl 1..6)
        const float PipR = .1f, DentR = .16f;

        static IEnumerable<(float x, float y)> PipsTex(int val)
        {
            foreach (var (px, py) in Die3D.Pips(val)) yield return (px, 1 - py);
        }

        static Texture2D BuildAlbedo(Col body, Col pip)
        {
            int W = TexTile * 3, H = TexTile * 2; var px = new Color32[W * H];
            var bc = new Color(body.Red / 255f, body.Green / 255f, body.Blue / 255f); var pc = new Color(pip.Red / 255f, pip.Green / 255f, pip.Blue / 255f);
            for (int val = 1; val <= 6; val++)
            {
                int ox = (val - 1) % 3 * TexTile, oy = (val - 1) / 3 * TexTile; var ps = new List<(float, float)>(PipsTex(val));
                for (int y = 0; y < TexTile; y++)
                    for (int x = 0; x < TexTile; x++)
                    {
                        float fx = (x + .5f) / TexTile, fy = (y + .5f) / TexTile, dmin = 9;
                        foreach (var (qx, qy) in ps) dmin = Mathf.Min(dmin, Mathf.Sqrt((fx - qx) * (fx - qx) + (fy - qy) * (fy - qy)));
                        float edge = Mathf.Clamp01((PipR - dmin) * TexTile / 1.5f + .5f);
                        float ao = 1 - .22f * Mathf.Exp(-Mathf.Pow(Mathf.Max(0, dmin - PipR) / .018f, 2));
                        float inner = Mathf.Lerp(.62f, 1f, Mathf.Clamp01(dmin / PipR));
                        var c = Color.Lerp(bc * ao, pc * inner, edge);
                        px[(oy + y) * W + ox + x] = c;
                    }
            }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true, false) { name = "DieAlbedo", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels32(px); t.Apply(true, true); return t;
        }

        static Texture2D BuildNormalMap()
        {
            int W = TexTile * 3, H = TexTile * 2; var px = new Color[W * H];
            float rs = DentR, c0 = Mathf.Sqrt(rs * rs - PipR * PipR);
            for (int val = 1; val <= 6; val++)
            {
                int ox = (val - 1) % 3 * TexTile, oy = (val - 1) / 3 * TexTile; var ps = new List<(float, float)>(PipsTex(val));
                for (int y = 0; y < TexTile; y++)
                    for (int x = 0; x < TexTile; x++)
                    {
                        float fx = (x + .5f) / TexTile, fy = (y + .5f) / TexTile; var nrm = new Vector3(0, 0, 1);
                        foreach (var (qx, qy) in ps)
                        {
                            float dx = fx - qx, dy = fy - qy, d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < PipR) { float s = Mathf.Sqrt(rs * rs - d * d); nrm = new Vector3(-dx / s, -dy / s, 1).normalized; }
                            else if (d < PipR + 1.5f / TexTile) { float k = (d - PipR) * TexTile / 1.5f, s = c0; var e = new Vector3(-dx / s, -dy / s, 1).normalized; nrm = Vector3.Lerp(e, Vector3.forward, k).normalized; }
                        }
                        px[(oy + y) * W + ox + x] = new Color(nrm.x * .5f + .5f, nrm.y * .5f + .5f, nrm.z * .5f + .5f, 1);
                    }
            }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true, true) { name = "DieNormal", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels(px); t.Apply(true, true); return t;
        }

        // ------------------------------------------------------------------ Studio-Umgebung fuer Spiegelungen
        static Cubemap BuildStudioCubemap()
        {
            const int S = 64; var cube = new Cubemap(S, TextureFormat.RGBAHalf, true) { name = "GlamourStudio" };
            var soft = new[] { (dir: new Vector3(-.5f, .7f, -.5f).normalized, col: new Color(1.6f, 1.55f, 1.5f), size: .82f), (dir: new Vector3(.7f, .3f, -.6f).normalized, col: new Color(.7f, .45f, 1.1f), size: .9f), (dir: new Vector3(0, .2f, 1).normalized, col: new Color(.3f, .45f, .55f), size: .9f) };
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
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
                        d.Normalize();
                        float h = d.y;
                        var c = Color.Lerp(new Color(.05f, .03f, .09f), new Color(.32f, .26f, .42f), Mathf.Clamp01(h * .5f + .5f));
                        foreach (var s in soft) { float k = Vector3.Dot(d, s.dir); if (k > s.size) c += s.col * Mathf.SmoothStep(0, 1, (k - s.size) / (1 - s.size) * 3); }
                        c.a = 1; buf[y * S + x] = c;
                    }
                cube.SetPixels(buf, f);
            }
            cube.Apply(true, true);
            return cube;
        }
    }
}
