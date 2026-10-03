using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlamourGames
{
    /// <summary>
    /// URP-Postprocessing fuer den Neon-Look: HDR-Bloom nur fuer Leuchtwerte deutlich &gt; 1 (normale Schrift und
    /// Flaechen bleiben scharf) mit prozeduralem Linsenschmutz, dezente Farbkorrektur und Vignette. Bei Siegen
    /// (<see cref="Lens.Kick"/>) steigen Bloom und Vignette kurz an und ein Screen-Space-Linsenreflex (Geisterbilder,
    /// anamorphotische Streifen) erscheint. Keine chromatische Aberration und kein Filmkorn im Normalbetrieb, da beides
    /// Kanten und Schrift unscharf wirken laesst. Wird zur Laufzeit erzeugt, es sind keine Volume-Assets noetig.
    /// </summary>
    public static class PostFX
    {
        const float BloomBase = 1.3f, VigBase = .22f;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.SetupPostFx = Setup;

        public static Volume Volume;
        static Bloom bloom; static Vignette vig; static ScreenSpaceLensFlare flare; static float kick;

        static void Setup(Camera cam)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset ua))
            {
                Debug.LogWarning("[Glamour] URP ist nicht aktiv - Bloom deaktiviert. Menue 'Glamour Games > Projekt einrichten' ausfuehren.");
                return;
            }
            // 64-Bit-HDR: keine Farbstufen in Verlaeufen, Alphakanal fuer HDR-Render-Texturen (3D-Pokal)
            if (ua.supportsHDR && ua.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits) ua.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None; // MSAA kommt aus dem Pipeline-Asset, Formen glaettet der SDF-Shader
            data.stopNaN = true;
            data.dithering = true;

            var go = new GameObject("Glamour PostFX");
            Object.DontDestroyOnLoad(go);
            Volume = go.AddComponent<Volume>();
            Volume.isGlobal = true; Volume.priority = 100;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GlamourNeon";

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(BloomBase);
            bloom.scatter.Override(0.62f);
            bloom.clamp.Override(40f);
            bloom.highQualityFiltering.Override(true);
            bloom.maxIterations.Override(8);
            bloom.dirtTexture.Override(LensDirt());
            bloom.dirtIntensity.Override(1.6f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.contrast.Override(9f);
            ca.saturation.Override(7f);

            vig = profile.Add<Vignette>(true);
            vig.intensity.Override(VigBase);
            vig.smoothness.Override(0.45f);

            if (ua.supportScreenSpaceLensFlare)
            {
                flare = profile.Add<ScreenSpaceLensFlare>(true);
                flare.intensity.Override(0f);
                flare.firstFlareIntensity.Override(.7f);
                flare.secondaryFlareIntensity.Override(.45f);
                flare.warpedFlareIntensity.Override(.35f);
                flare.vignetteEffect.Override(1f);
                flare.samples.Override(2);
                flare.streaksIntensity.Override(.55f);
                flare.streaksLength.Override(.45f);
                flare.streaksThreshold.Override(.35f);
                flare.chromaticAbberationIntensity.Override(.45f);
                flare.resolution.Override(ScreenSpaceLensFlareResolution.Half);
            }

            Volume.sharedProfile = profile;
            go.AddComponent<Pulse>();
            Lens.Kick = p => kick = Mathf.Max(kick, Mathf.Clamp(p, .3f, 1.6f));
        }

        /// <summary>Klingt den Sieges-Impuls ab (Bloom, Vignette, Linsenreflex).</summary>
        sealed class Pulse : MonoBehaviour
        {
            void Update()
            {
                if (bloom == null) return;
                kick = Mathf.Max(0, kick - Time.unscaledDeltaTime * .32f); float k = kick * kick;
                bloom.intensity.value = BloomBase + .7f * k;
                vig.intensity.value = VigBase + .1f * k;
                if (flare != null) flare.intensity.value = k > .01f ? .9f * k : 0;
            }
        }

        /// <summary>Prozeduraler Linsenschmutz: weiche Bokeh-Flecken, Schlieren und Staub (nur in hellem Bloom sichtbar).</summary>
        static Texture2D LensDirt()
        {
            const int W = 512, H = 288; var px = new Color[W * H]; var r = new System.Random(1982);
            float Rf(float a, float b) => a + (float)r.NextDouble() * (b - a);
            void Blob(float cx, float cy, float rad, float a, bool ring)
            {
                int x0 = Mathf.Max(0, (int)(cx - rad - 2)), x1 = Mathf.Min(W - 1, (int)(cx + rad + 2)), y0 = Mathf.Max(0, (int)(cy - rad - 2)), y1 = Mathf.Min(H - 1, (int)(cy + rad + 2));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rad; if (d > 1) continue;
                        float v = ring ? Mathf.SmoothStep(0, 1, (1 - d) * 6) * (.45f + .55f * d * d) : Mathf.SmoothStep(0, 1, (1 - d) * 2.2f);
                        px[y * W + x] += new Color(1, .97f, .92f) * (v * a);
                    }
            }
            for (int i = 0; i < 70; i++) Blob(Rf(0, W), Rf(0, H), Rf(10, 46), Rf(.05f, .2f), r.NextDouble() < .45);
            for (int i = 0; i < 420; i++) Blob(Rf(0, W), Rf(0, H), Rf(1, 3.2f), Rf(.15f, .5f), false);
            for (int i = 0; i < 9; i++)
            {
                float x = Rf(0, W), y = Rf(0, H), ang = Rf(0, Mathf.PI), len = Rf(60, 180);
                for (int k = 0; k < 60; k++) { float t = k / 59f; Blob(x + Mathf.Cos(ang) * len * t, y + Mathf.Sin(ang) * len * t + Mathf.Sin(t * 6) * 6, Rf(5, 11), .03f, false); }
            }
            for (int i = 0; i < px.Length; i++) { var c = px[i]; px[i] = new Color(Mathf.Min(1, c.r), Mathf.Min(1, c.g), Mathf.Min(1, c.b), 1); }
            var t2 = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "GlamourLensDirt", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            t2.SetPixels(px); t2.Apply(true, true); return t2;
        }
    }
}
