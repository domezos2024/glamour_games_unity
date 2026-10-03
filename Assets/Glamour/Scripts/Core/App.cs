using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace GlamourGames
{
    /// <summary>
    /// Hauptprogramm: richtet Kamera, Renderer, Hintergrund, Audio und Postprocessing ein, verteilt Eingaben an die
    /// aktive Szene und zeichnet jeden Frame. Startet sich selbst in jeder Szene (keine Szenen-Einrichtung noetig).
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public const float VW = 1600, VH = 900;
        public const string Version = "2.0.0", Credit = "erstellt von Michael Bergfeld @ DoMeZos-Ware 2026";
        public static float VX0, VX1 = VW, VY0, VY1 = VH, MX, MY;
        /// <summary>Wird von PostFX (URP) gesetzt, um Bloom &amp; Co. an der Kamera einzurichten.</summary>
        public static Action<Camera> SetupPostFx;
        public static Action<Camera, Material> SetupDice;

        static App inst;
        static Scene cur, pending; static float fade = 1, flash, shake, toastT, fps, fpsAcc; static int fpsN; static Col flashCol = Col.White;
        static string toast = ""; static bool showFps;
        public static Scene Current => cur;

        Camera cam; Mesh mesh, bgMesh; Material shapeMat, bgMat; GameObject canvasGo, bgGo;
        readonly Canvas2D canvas = new Canvas2D();
        readonly Queue<(int kind, Key key, char ch, float val)> events = new Queue<(int, Key, char, float)>();
        Vector2 guiMouse; bool haveMouse; float autoT, shotAt = -1; string shotPath; bool shotDone; int startScene = -1;
        Texture2D handCursor; bool handOn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<App>() != null) return;
            var go = new GameObject("Glamour Games");
            DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }

        // ------------------------------------------------------------------ Oeffentliche API
        public static void Go(Scene s) { if (pending != null) return; pending = s; Log.I("go " + s.GetType().Name); }
        public static void Flash(Col c, float a = .5f) { flash = a; flashCol = c; }
        public static void Shake(float a = 14) { shake = Math.Max(shake, a); }
        public static void Toast(string t) { toast = t; toastT = 2.4f; }
        public static bool Fullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;
        public static void ToggleFullscreen()
        {
            if (Fullscreen) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
            else Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            Save.Set("fullscreen", Fullscreen ? 0 : 1);
        }
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Einrichtung
        void Awake()
        {
            inst = this;
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 1;
            Application.runInBackground = true;
            useGUILayout = false;

            FontAtlas.Load(); Assets.Load();

            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); DontDestroyOnLoad(cg); }
            cam.orthographic = true; cam.orthographicSize = 450; cam.transform.position = new Vector3(0, 0, -10); cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = .1f; cam.farClipPlane = 100; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.allowHDR = true; cam.allowMSAA = true; cam.useOcclusionCulling = false;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.enabled = false;
            try { SetupPostFx?.Invoke(cam); } catch (Exception e) { Log.I("postfx " + e.Message); }

            var shapeShader = Resources.Load<Shader>("Glamour/Shaders/GlamourShape") ?? Shader.Find("Glamour/Shape");
            var bgShader = Resources.Load<Shader>("Glamour/Shaders/GlamourBackdrop") ?? Shader.Find("Glamour/Backdrop");
            shapeMat = new Material(shapeShader) { name = "GlamourShape" };
            shapeMat.SetTexture("_FontTex", FontAtlas.Texture); shapeMat.SetTexture("_ImgTex", Assets.Texture); shapeMat.SetFloat("_FontAlpha", FontAtlas.UsesAlpha);
            bgMat = new Material(bgShader) { name = "GlamourBackdrop" };

            bgGo = MakeRenderer("Backdrop", bgMat, -100, out bgMesh);
            canvasGo = MakeRenderer("Canvas2D", shapeMat, 0, out mesh);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            try { SetupDice?.Invoke(cam, shapeMat); } catch (Exception e) { Log.I("dice3d " + e.Message); }
            Sfx.Init(gameObject);
            handCursor = MakeHandCursor();

            var ci = CultureInfo.InvariantCulture;
            foreach (var a in Environment.GetCommandLineArgs())
            {
                if (a.StartsWith("--scene=")) int.TryParse(a.Substring(8), out startScene);
                else if (a.StartsWith("--shot=")) shotPath = a.Substring(7);
                else if (a.StartsWith("--at=")) float.TryParse(a.Substring(5), NumberStyles.Float, ci, out shotAt);
            }
            if (Save.Int("fullscreen", 0) == 1 && !Application.isEditor) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            cur = startScene >= 0 && startScene < Registry.All.Count ? Registry.All[startScene].Make() : new Menu();
            cur.Enter(); fade = startScene >= 0 ? 0 : 1;
        }

        GameObject MakeRenderer(string name, Material mat, int order, out Mesh m)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            m = new Mesh { name = name }; m.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        // ------------------------------------------------------------------ Eingabe (IMGUI-Ereignisse: funktionieren mit jedem Input-Backend)
        void OnGUI()
        {
            var e = Event.current; if (e == null) return;
            if (e.isMouse || e.type == EventType.Repaint || e.type == EventType.ScrollWheel) { guiMouse = e.mousePosition; haveMouse = true; }
            switch (e.type)
            {
                case EventType.MouseDown: if (e.button == 0) events.Enqueue((1, Key.Unknown, '\0', 0)); break;
                case EventType.MouseUp: if (e.button == 0) events.Enqueue((2, Key.Unknown, '\0', 0)); break;
                case EventType.ScrollWheel: events.Enqueue((3, Key.Unknown, '\0', -e.delta.y)); break;
                case EventType.KeyDown:
                    if (e.keyCode != KeyCode.None) events.Enqueue((4, MapKey(e.keyCode), '\0', 0));
                    if (e.character != '\0' && e.character != '\n' && e.character != '\r' && e.character != '\t' && !char.IsControl(e.character)) events.Enqueue((5, Key.Unknown, e.character, 0));
                    break;
            }
        }

        static Key MapKey(KeyCode k)
        {
            if (k >= KeyCode.A && k <= KeyCode.Z) return Key.A + (k - KeyCode.A);
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return Key.Number0 + (k - KeyCode.Alpha0);
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return Key.Keypad0 + (k - KeyCode.Keypad0);
            if (k >= KeyCode.F1 && k <= KeyCode.F12) return Key.F1 + (k - KeyCode.F1);
            switch (k)
            {
                case KeyCode.Space: return Key.Space; case KeyCode.Return: return Key.Enter; case KeyCode.KeypadEnter: return Key.KeypadEnter;
                case KeyCode.Escape: return Key.Escape; case KeyCode.Backspace: return Key.Backspace; case KeyCode.Tab: return Key.Tab;
                case KeyCode.LeftArrow: return Key.Left; case KeyCode.RightArrow: return Key.Right; case KeyCode.UpArrow: return Key.Up; case KeyCode.DownArrow: return Key.Down;
                case KeyCode.Plus: case KeyCode.KeypadPlus: return Key.Plus; case KeyCode.Minus: case KeyCode.KeypadMinus: return Key.Minus;
            }
            return Key.Unknown;
        }

        void KeyPress(Key k)
        {
            if (k == Key.F11) { ToggleFullscreen(); return; }
            if (k == Key.F3) { showFps = !showFps; return; }
            if (cur == null) return;
            if (cur.Modal != null)
            {
                if (k == Key.Enter || k == Key.KeypadEnter) cur.Modal.Enter();
                else if (k == Key.Backspace) cur.Modal.Back();
                else if (k == Key.Escape) cur.Modal.Cancel?.Invoke();
                return;
            }
            if (k == Key.M) { Sfx.ToggleMute(); return; }
            if (k == Key.N) { Toast(Sfx.NextTrack()); return; }
            if (k == Key.Escape) { if (!cur.Escape()) { if (cur is Menu) Quit(); else Go(new Menu()); } return; }
            cur.KeyDown(k);
        }

        static bool OnMusic(float x, float y) => cur != null && cur.Chrome && x > 1425 && x < 1515 && y > 0 && y < 104;
        static bool OnMute(float x, float y) => cur != null && cur.Chrome && x > 1516 && x < 1600 && y > 0 && y < 104;
        void MoveTo(float x, float y)
        {
            MX = x; MY = y; if (cur == null) return;
            if (cur.Modal != null) { cur.Modal.Move(MX, MY); return; }
            cur.Ui.Move(MX, MY); cur.MouseMove(MX, MY);
        }
        void Down()
        {
            if (cur == null || pending != null) return;
            if (cur.Modal != null) { cur.Modal.MDown(MX, MY); return; }
            if (OnMute(MX, MY)) { Sfx.ToggleMute(); return; }
            if (OnMusic(MX, MY)) { Toast(Sfx.NextTrack()); Sfx.Play(S.Chip); return; }
            if (!cur.Ui.Down(MX, MY)) cur.MouseDown(MX, MY);
        }
        void Up()
        {
            if (cur == null || pending != null) return;
            if (cur.Modal != null) { cur.Modal.MUp(MX, MY); return; }
            if (!cur.Ui.Up(MX, MY)) cur.MouseUp(MX, MY);
        }

        // ------------------------------------------------------------------ Frame
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            autoT += dt;
            Sfx.Update();

            // Sichtbereich: 16:9-Designraum, bei anderem Seitenverhaeltnis wird der Rand erweitert (wie im Original)
            float aspect = Math.Max(.2f, (float)Screen.width / Math.Max(1, Screen.height));
            if (aspect >= VW / VH) { cam.orthographicSize = VH / 2; float hw = VH / 2 * aspect; VX0 = 800 - hw; VX1 = 800 + hw; VY0 = 0; VY1 = VH; }
            else { float hh = VW / 2 / aspect; cam.orthographicSize = hh; VX0 = 0; VX1 = VW; VY0 = 450 - hh; VY1 = 450 + hh; }

            // Maus
            Vector2 sp = haveMouse ? new Vector2(guiMouse.x, Screen.height - guiMouse.y) : new Vector2(-1000, -1000);
#if ENABLE_LEGACY_INPUT_MANAGER
            try { sp = Input.mousePosition; } catch { }
#endif
            var wp = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, 10));
            float mx = wp.x + 800, my = 450 - wp.y;
            if (Math.Abs(mx - MX) > .01f || Math.Abs(my - MY) > .01f) MoveTo(mx, my);
            while (events.Count > 0)
            {
                var ev = events.Dequeue();
                try
                {
                    switch (ev.kind)
                    {
                        case 1: MoveTo(mx, my); Down(); break;
                        case 2: MoveTo(mx, my); Up(); break;
                        case 3: if (cur != null && cur.Modal == null) cur.Wheel(ev.val); break;
                        case 4: KeyPress(ev.key); break;
                        case 5: if (cur?.Modal != null) cur.Modal.Char(ev.ch); break;
                    }
                }
                catch (Exception e) { Debug.LogException(e); }
            }

            // Szenenwechsel mit Ueberblendung
            if (pending != null) { fade += dt / .16f; if (fade >= 1) { fade = 1; try { cur?.Leave(); } catch (Exception e) { Debug.LogException(e); } cur = pending; pending = null; cur.Enter(); } }
            else if (fade > 0) fade = Math.Max(0, fade - dt / .3f);
            shake = Math.Max(0, shake - dt * 40);

            canvas.Begin(); Die3D.FrameBegin?.Invoke();
            try
            {
                cur.BaseUpdate(dt);
                cur.BaseDraw(canvas);
                if (cur.Chrome) DrawChrome(canvas);
            }
            catch (Exception e) { Debug.LogException(e); canvas.Begin(); }
            if (flash > 0) { var fp = Gfx.Fill(flashCol.A(flash)); fp.Additive = true; fp.Glow = 1.2f; canvas.DrawRect(VX0 - 10, VY0 - 10, VX1 - VX0 + 20, VY1 - VY0 + 20, fp); flash = Math.Max(0, flash - dt * 1.8f); }
            if (toastT > 0)
            {
                toastT -= dt; float a = Ease.Clamp(Math.Min(toastT, .3f) / .3f); var r = Gfx.Ctr(800, 840 - (1 - a) * 20, Gfx.TW(toast, 26) + 60, 52);
                Gfx.Rect(canvas, r, 26, C.Panel.A(.92f * a)); Gfx.Stroke(canvas, r, 26, C.Cyan.A(a), 2); Gfx.Text(canvas, toast, r.MidX, r.MidY, 26, Col.White.A(a));
            }
            if (showFps) { fpsAcc += dt; fpsN++; if (fpsAcc > .5f) { fps = fpsN / fpsAcc; fpsAcc = 0; fpsN = 0; } Gfx.Text(canvas, $"{fps:0} FPS  ·  {canvas.VertexCount} Vertices", VX1 - 10, VY1 - 20, 20, C.Green, Al.R); }
            if (fade > 0) canvas.DrawRect(VX0 - 100, VY0 - 100, VX1 - VX0 + 200, VY1 - VY0 + 200, Gfx.Fill(Col.Black.A(Ease.InCubic(fade))));
            canvas.Upload(mesh); Die3D.FrameEnd?.Invoke();

            float sx = shake > 0 ? (float)(Rng.Shared.NextDouble() - .5) * shake : 0, sy = shake > 0 ? (float)(Rng.Shared.NextDouble() - .5) * shake : 0;
            canvasGo.transform.localPosition = new Vector3(sx, -sy, 0);
            UpdateBackdrop();

            if (shotPath != null && shotAt >= 0 && !shotDone && autoT >= shotAt) { shotDone = true; ScreenCapture.CaptureScreenshot(shotPath); Invoke(nameof(QuitLater), .5f); }

            bool hot = cur.Modal != null ? cur.Modal.AnyHover : cur.Ui.Hot || cur.WantsHand || OnMute(MX, MY) || OnMusic(MX, MY);
            if (hot != handOn && handCursor != null) { handOn = hot; Cursor.SetCursor(hot ? handCursor : null, hot ? new Vector2(9, 2) : Vector2.zero, CursorMode.Auto); }
        }
        void QuitLater() => Quit();

        void UpdateBackdrop()
        {
            float x0 = VX0 - 60, x1 = VX1 + 60, y0 = VY0 - 60, y1 = VY1 + 60;
            var v = new[] { new Vector3(x0 - 800, 450 - y0), new Vector3(x1 - 800, 450 - y0), new Vector3(x1 - 800, 450 - y1), new Vector3(x0 - 800, 450 - y1) };
            var uv = new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
            bgMesh.Clear(); bgMesh.vertices = v; bgMesh.uv = uv; bgMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; bgMesh.bounds = new Bounds(Vector3.zero, new Vector3(1e5f, 1e5f, 10));
            bgMat.SetFloat("_T", cur != null ? cur.Time + 37 : 0);
            Vector4 V(Col c) => new Vector4(c.Red / 255f, c.Green / 255f, c.Blue / 255f, 1);
            bgMat.SetVector("_A1", V(cur?.Acc1 ?? C.Cyan)); bgMat.SetVector("_A2", V(cur?.Acc2 ?? C.Pink));
            bgMat.SetVector("_View", new Vector4(VX0, VY0, VX1, VY1));
        }

        static void DrawChrome(Canvas2D c)
        {
            {
                bool h = OnMusic(MX, MY), on = Sfx.Track >= 0 && !Sfx.Muted; var r = Gfx.Ctr(1480, 50, 52, 52);
                Gfx.Rect(c, r, 26, C.Panel.A(h ? .95f : .7f)); Gfx.Stroke(c, r, 26, (on ? C.Pink : C.Dim).A(h ? 1 : .7f), 2);
                var col = on ? Col.White : C.Dim; c.DrawCircle(1474, 61, 5.5f, Gfx.Fill(col)); c.DrawCircle(1489, 58, 5.5f, Gfx.Fill(col));
                c.DrawLine(1479, 60, 1479, 38, Gfx.Line(col, 3)); c.DrawLine(1494, 57, 1494, 35, Gfx.Line(col, 3)); c.DrawLine(1479, 38, 1494, 35, Gfx.Line(col, 4));
                if (Sfx.Track < 0) c.DrawLine(1462, 66, 1498, 34, Gfx.Line(C.Red, 3));
                else Gfx.Text(c, (Sfx.Track + 1).ToString(), 1480, 74, 15, on ? C.Pink.Light(.4f) : C.Dim);
            }
            {
                bool h = OnMute(MX, MY); var r = Gfx.Ctr(1550, 50, 52, 52);
                Gfx.Rect(c, r, 26, C.Panel.A(h ? .95f : .7f)); Gfx.Stroke(c, r, 26, (Sfx.Muted ? C.Dim : C.Cyan).A(h ? 1 : .7f), 2);
                var col = Sfx.Muted ? C.Dim : Col.White; using var p = new Path2D(); p.MoveTo(1537, 44); p.LineTo(1544, 44); p.LineTo(1553, 37); p.LineTo(1553, 63); p.LineTo(1544, 56); p.LineTo(1537, 56); p.Close(); c.DrawPath(p, Gfx.Fill(col));
                if (Sfx.Muted) { c.DrawLine(1560, 43, 1570, 57, Gfx.Line(C.Red, 3)); c.DrawLine(1570, 43, 1560, 57, Gfx.Line(C.Red, 3)); }
                else { c.DrawArc(Gfx.Ctr(1556, 50, 14, 18), -50, 100, false, Gfx.Line(col, 2.5f)); c.DrawArc(Gfx.Ctr(1557, 50, 26, 30), -50, 100, false, Gfx.Line(col.A(.7f), 2.5f)); }
            }
        }

        /// <summary>Erzeugt einen weissen Hand-Mauszeiger mit dunklem Rand (fuer klickbare Elemente).</summary>
        static Texture2D MakeHandCursor()
        {
            try
            {
                string[] art = {
                    "......XX..........",
                    ".....X..X.........",
                    ".....X..X.........",
                    ".....X..X.........",
                    ".....X..XXX.......",
                    ".....X..X..XXX....",
                    ".....X..X..X..XX..",
                    ".XX..X..X..X..X.X.",
                    "X..X.X........X..X",
                    "X...XX...........X",
                    ".X...X...........X",
                    "..X..............X",
                    "..X.............X.",
                    "...X............X.",
                    "...X...........X..",
                    "....X..........X..",
                    "....X.........X...",
                    ".....XXXXXXXXXX...",
                };
                int w = 32, h = 32; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[w * h];
                for (int y = 0; y < art.Length; y++)
                {
                    // Innenflaeche je Zeile fuellen
                    var row = art[y]; int first = row.IndexOf('X'), last = row.LastIndexOf('X');
                    for (int x = 0; x < row.Length; x++)
                    {
                        Color32 c = new Color32(0, 0, 0, 0);
                        if (row[x] == 'X') c = new Color32(20, 10, 30, 255);
                        else if (first >= 0 && x > first && x < last && y > 0) c = new Color32(255, 255, 255, 255);
                        px[(h - 1 - y) * w + x + 1] = c;
                    }
                }
                tex.SetPixels32(px); tex.Apply(); return tex;
            }
            catch { return null; }
        }
    }
}
