using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlamourGames
{
    /// <summary>
    /// URP-Postprocessing fuer den Neon-Look: HDR-Bloom (macht alle Leuchtwerte &gt; 1 zu echtem Glow),
    /// dezente Vignette, leichte chromatische Aberration und feines Filmkorn. Wird zur Laufzeit erzeugt,
    /// es sind keine Volume-Assets noetig.
    /// </summary>
    public static class PostFX
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => App.SetupPostFx = Setup;

        public static Volume Volume;

        static void Setup(Camera cam)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            {
                Debug.LogWarning("[Glamour] URP ist nicht aktiv - Bloom deaktiviert. Menue 'Glamour Games > Projekt einrichten' ausfuehren.");
                return;
            }
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None; // MSAA kommt aus dem Pipeline-Asset
            data.stopNaN = true;
            data.dithering = true;

            var go = new GameObject("Glamour PostFX");
            Object.DontDestroyOnLoad(go);
            Volume = go.AddComponent<Volume>();
            Volume.isGlobal = true; Volume.priority = 100;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GlamourNeon";

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.72f);
            bloom.clamp.Override(40f);
            bloom.highQualityFiltering.Override(true);
            bloom.maxIterations.Override(8);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);

            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.06f);

            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);

            Volume.sharedProfile = profile;
        }
    }
}
