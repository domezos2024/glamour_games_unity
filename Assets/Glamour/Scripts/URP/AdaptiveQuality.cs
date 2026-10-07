using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GlamourGames
{
    /// <summary>
    /// Adaptive Aufloesung fuer Mobilgeraete: haelt die Bildrate stabil, indem der Render-Massstab der URP-Pipeline in Stufen
    /// abgesenkt wird, wenn viele Frames zu lang dauern (z.B. bei Waerme-Drosselung oder schwacher GPU), und bei ruhiger Last
    /// wieder angehoben wird. Auf starken Geraeten bleibt es bei voller Aufloesung. Am Desktop ohne Funktion.
    /// </summary>
    public static class AdaptiveQuality
    {
        static readonly float[] Steps = { 1f, .9f, .8f, .7f, .6f };
        static int idx; static float window, calm, blockUp; static int frames, slow;
        public static float Scale => Steps[idx];

        public static void Tick(float rawDt)
        {
            if (!Platform.Touch || Application.isEditor || rawDt <= 0) return;
            float limit = 1f / Mathf.Max(30, Application.targetFrameRate) * 1.35f;   // ab ~22 ms bei 60 FPS gilt ein Frame als langsam
            window += rawDt; frames++; if (rawDt > limit && rawDt < .5f) slow++;       // Spruenge > 0,5 s (Szenenwechsel, Pause) zaehlen nicht
            if (window < 2f) return;
            float ratio = frames > 0 ? slow / (float)frames : 0;
            window = 0; frames = 0; slow = 0;
            if (blockUp > 0) blockUp -= 2f;
            if (ratio > .2f && idx < Steps.Length - 1) { idx++; calm = 0; blockUp = 60; Apply(); }
            else if (ratio < .02f) { calm += 2f; if (calm >= 12f && idx > 0 && blockUp <= 0) { idx--; calm = 0; Apply(); } }
            else calm = 0;
        }

        static void Apply()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset ua) { ua.renderScale = Steps[idx]; Log.I("Render-Massstab " + Steps[idx].ToString("0.00")); }
        }
    }
}
