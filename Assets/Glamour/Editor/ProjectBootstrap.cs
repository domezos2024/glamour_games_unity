using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GlamourGames.EditorTools
{
    /// <summary>
    /// Richtet das Projekt beim ersten Oeffnen automatisch ein: URP-Pipeline (HDR, 4x MSAA), Startszene,
    /// Build-Einstellungen und Player-Einstellungen (Name, Firma, Icon, Fenster 1600x900, Linear-Farbraum).
    /// Laeuft idempotent bei jedem Laden der Skripte; manuell ueber das Menue "Glamour Games".
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        const string SettingsDir = "Assets/Glamour/Settings";
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string PipelinePath = SettingsDir + "/GlamourURP.asset";
        const string RendererPath = SettingsDir + "/GlamourURP_Renderer.asset";
        const string IconPath = "Assets/Glamour/Branding/icon.png";

        static ProjectBootstrap() { EditorApplication.delayCall += () => Setup(false); }

        [MenuItem("Glamour Games/Projekt einrichten", priority = 1)]
        static void SetupMenu() => Setup(true);

        static void Setup(bool verbose, bool force = false)
        {
            if (!force && (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)) { EditorApplication.delayCall += () => Setup(verbose); return; }
            try
            {
                EnsurePipeline();
                EnsureScene();
                EnsurePlayerSettings();
                if (verbose) Debug.Log("[Glamour] Projekt eingerichtet: URP, Szene, Player-Einstellungen.");
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        static void EnsurePipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (asset == null)
            {
                var rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(rd, RendererPath);
                asset = UniversalRenderPipelineAsset.Create(rd);
                asset.supportsHDR = true;
                asset.msaaSampleCount = 4;
                asset.renderScale = 1f;
                AssetDatabase.CreateAsset(asset, PipelinePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Glamour] URP-Pipeline angelegt: " + PipelinePath);
            }
            if (asset.hdrColorBufferPrecision != HDRColorBufferPrecision._64Bits || asset.colorGradingMode != ColorGradingMode.HighDynamicRange) { asset.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits; asset.colorGradingMode = ColorGradingMode.HighDynamicRange; EditorUtility.SetDirty(asset); }
            if (GraphicsSettings.defaultRenderPipeline != asset) { GraphicsSettings.defaultRenderPipeline = asset; EditorUtility.SetDirty(asset); }
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != asset) QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(cur, false);
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var active = SceneManager.GetActiveScene();
                bool emptyUntitled = string.IsNullOrEmpty(active.path) && !active.isDirty;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, emptyUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
                var cam = new GameObject("Main Camera") { tag = "MainCamera" };
                var c = cam.AddComponent<Camera>(); c.orthographic = true; c.orthographicSize = 450; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black;
                cam.transform.position = new Vector3(0, 0, -10);
                if (!emptyUntitled) SceneManager.MoveGameObjectToScene(cam, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (!emptyUntitled) EditorSceneManager.CloseScene(scene, true);
                else EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[Glamour] Startszene angelegt: " + ScenePath);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty) EditorSceneManager.OpenScene(ScenePath);
        }

        static void EnsurePlayerSettings()
        {
            if (PlayerSettings.productName != "Glamour Games") PlayerSettings.productName = "Glamour Games";
            if (PlayerSettings.companyName != "DoMeZos-Ware") PlayerSettings.companyName = "DoMeZos-Ware";
            if (PlayerSettings.bundleVersion != GlamourGames.App.Version) PlayerSettings.bundleVersion = GlamourGames.App.Version;
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                var cur = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
                if (cur == null || cur.Length == 0 || cur[0] != icon) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }
        }

        [MenuItem("Glamour Games/Windows-Build erstellen", priority = 20)]
        static void BuildWindows() => Build("Builds/Windows/GlamourGames.exe");

        static bool Build(string path)
        {
            Setup(false, true);
            AssetDatabase.SaveAssets();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Glamour] Build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB -> {opts.locationPathName}");
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            if (ok && !Application.isBatchMode) EditorUtility.RevealInFinder(opts.locationPathName);
            return ok;
        }

        /// <summary>
        /// Kommandozeilen-Build: Unity -batchmode -quit -projectPath . -executeMethod GlamourGames.EditorTools.ProjectBootstrap.CiBuild [-buildPath Pfad\GlamourGames.exe]
        /// Beendet Unity mit Exit-Code 1, wenn der Build fehlschlaegt.
        /// </summary>
        public static void CiBuild()
        {
            string path = "Builds/Windows/GlamourGames.exe";
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-buildPath") path = args[i + 1];
            bool ok = false;
            try { ok = Build(path); } catch (System.Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
