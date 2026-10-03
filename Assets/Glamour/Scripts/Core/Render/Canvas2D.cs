using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlamourGames
{
    /// <summary>Gradient fuer eine Paint (Koordinaten im aktuellen Objektraum, wie Skia-Shader).</summary>
    public sealed class Grad
    {
        public int Mode; // 1 = linear, 2 = radial
        public float X0, Y0, X1, Y1; public Col A, B;
        public static Grad Linear(float x0, float y0, float x1, float y1, Col a, Col b) => new Grad { Mode = 1, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, A = a, B = b };
        public static Grad Radial(float cx, float cy, float r, Col a, Col b) => new Grad { Mode = 2, X0 = cx, Y0 = cy, X1 = r, A = a, B = b };
    }

    /// <summary>Zeichenparameter (entspricht SKPaint im Original).</summary>
    public sealed class Paint
    {
        public Col Color = Col.White;
        public bool Stroke;
        public float StrokeWidth = 1;
        /// <summary>Gauss-Weichzeichnung (Sigma in Objekt-Einheiten), 0 = scharf mit Kantenglättung.</summary>
        public float Blur;
        /// <summary>Additives Mischen (Skia BlendMode.Plus) - ideal fuer Licht/Partikel.</summary>
        public bool Additive;
        /// <summary>HDR-Leuchtkraft: Werte &gt; 1 werden vom Bloom erfasst.</summary>
        public float Glow = 1;
        public Grad Shader;
        public Paint Reset() { Color = Col.White; Stroke = false; StrokeWidth = 1; Blur = 0; Additive = false; Glow = 1; Shader = null; return this; }
    }

    /// <summary>2D-Affintransformation: x' = A*x + C*y + Tx, y' = B*x + D*y + Ty.</summary>
    public struct Aff
    {
        public float A, B, C, D, Tx, Ty;
        public static readonly Aff Identity = new Aff { A = 1, D = 1 };
        public Aff(float a, float b, float c, float d, float tx, float ty) { A = a; B = b; C = c; D = d; Tx = tx; Ty = ty; }
        public static Aff operator *(Aff m, Aff n) => new Aff(
            m.A * n.A + m.C * n.B, m.B * n.A + m.D * n.B,
            m.A * n.C + m.C * n.D, m.B * n.C + m.D * n.D,
            m.A * n.Tx + m.C * n.Ty + m.Tx, m.B * n.Tx + m.D * n.Ty + m.Ty);
        public void Map(float x, float y, out float ox, out float oy) { ox = A * x + C * y + Tx; oy = B * x + D * y + Ty; }
        public float ScaleX => MathF.Sqrt(A * A + B * B);
        public float ScaleY => MathF.Sqrt(C * C + D * D);
        /// <summary>Affine Abbildung des Einheitsquadrats auf drei Punkte (0,0)->p0, (1,0)->p1, (0,1)->p3.</summary>
        public static Aff FromPoints(Pt p0, Pt p1, Pt p3) => new Aff(p1.X - p0.X, p1.Y - p0.Y, p3.X - p0.X, p3.Y - p0.Y, p0.X, p0.Y);
    }

    /// <summary>Pfad aus Linien und Kurven (wird beim Zeichnen in Polygone zerlegt).</summary>
    public sealed class Path2D : IDisposable
    {
        internal readonly List<List<Vector2>> Contours = new List<List<Vector2>>();
        internal readonly List<bool> Closed = new List<bool>();
        List<Vector2> cur;
        Vector2 last;
        public bool EvenOdd;
        public void Reset() { Contours.Clear(); Closed.Clear(); cur = null; }
        public void Dispose() { }
        public void MoveTo(float x, float y) { cur = new List<Vector2> { new Vector2(x, y) }; Contours.Add(cur); Closed.Add(false); last = new Vector2(x, y); }
        public void MoveTo(Pt p) => MoveTo(p.X, p.Y);
        public void LineTo(float x, float y) { if (cur == null) { MoveTo(x, y); return; } cur.Add(new Vector2(x, y)); last = new Vector2(x, y); }
        public void LineTo(Pt p) => LineTo(p.X, p.Y);
        public void QuadTo(float cx, float cy, float x, float y)
        {
            if (cur == null) MoveTo(last.x, last.y);
            var p0 = last; int n = Segs(p0, new Vector2(x, y), 14);
            for (int i = 1; i <= n; i++) { float t = i / (float)n, u = 1 - t; LineTo(u * u * p0.x + 2 * u * t * cx + t * t * x, u * u * p0.y + 2 * u * t * cy + t * t * y); }
        }
        public void CubicTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            if (cur == null) MoveTo(last.x, last.y);
            var p0 = last; int n = Segs(p0, new Vector2(x, y), 20);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1 - t;
                LineTo(u * u * u * p0.x + 3 * u * u * t * c1x + 3 * u * t * t * c2x + t * t * t * x, u * u * u * p0.y + 3 * u * u * t * c1y + 3 * u * t * t * c2y + t * t * t * y);
            }
        }
        static int Segs(Vector2 a, Vector2 b, int max) => Math.Max(6, Math.Min(max, (int)((a - b).magnitude / 4)));
        public void Close() { if (cur != null) { Closed[Closed.Count - 1] = true; last = cur[0]; } cur = null; }
        public void AddCircle(float cx, float cy, float r) => AddOval(cx - r, cy - r, cx + r, cy + r);
        public void AddOval(float l, float t, float r, float b)
        {
            float cx = (l + r) / 2, cy = (t + b) / 2, rx = (r - l) / 2, ry = (b - t) / 2; int n = Math.Max(24, Math.Min(96, (int)(Math.Max(rx, ry) * .8f)));
            MoveTo(cx + rx, cy); for (int i = 1; i < n; i++) { float a = i * MathF.PI * 2 / n; LineTo(cx + MathF.Cos(a) * rx, cy + MathF.Sin(a) * ry); }
            Close();
        }
        public void AddRect(Box r) { MoveTo(r.Left, r.Top); LineTo(r.Right, r.Top); LineTo(r.Right, r.Bottom); LineTo(r.Left, r.Bottom); Close(); }
        public void AddRoundRect(Box r, float rad)
        {
            rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2);
            if (rad <= .01f) { AddRect(r); return; }
            int n = 8; bool first = true;
            void Corner(float cx, float cy, float a0)
            {
                for (int i = 0; i <= n; i++) { float a = a0 + i * MathF.PI / 2 / n, x = cx + MathF.Cos(a) * rad, y = cy + MathF.Sin(a) * rad; if (first) { MoveTo(x, y); first = false; } else LineTo(x, y); }
            }
            Corner(r.Right - rad, r.Top + rad, -MathF.PI / 2); Corner(r.Right - rad, r.Bottom - rad, 0); Corner(r.Left + rad, r.Bottom - rad, MathF.PI / 2); Corner(r.Left + rad, r.Top + rad, MathF.PI);
            Close();
        }
        /// <summary>Kreisbogen im Oval r, Winkel in Grad (0 = rechts, positiv = im Uhrzeigersinn).</summary>
        public void AddArc(Box r, float startDeg, float sweepDeg)
        {
            float cx = r.MidX, cy = r.MidY, rx = r.Width / 2, ry = r.Height / 2;
            int n = Math.Max(4, (int)(Math.Abs(sweepDeg) / 360f * Math.Max(24, Math.Min(96, Math.Max(rx, ry) * .8f))));
            for (int i = 0; i <= n; i++)
            {
                float a = (startDeg + sweepDeg * i / n) * MathF.PI / 180f, x = cx + MathF.Cos(a) * rx, y = cy + MathF.Sin(a) * ry;
                if (i == 0) MoveTo(x, y); else LineTo(x, y);
            }
        }
        public void ArcTo(Box r, float startDeg, float sweepDeg)
        {
            float cx = r.MidX, cy = r.MidY, rx = r.Width / 2, ry = r.Height / 2; int n = Math.Max(4, (int)(Math.Abs(sweepDeg) / 8));
            for (int i = 0; i <= n; i++) { float a = (startDeg + sweepDeg * i / n) * MathF.PI / 180f; LineTo(cx + MathF.Cos(a) * rx, cy + MathF.Sin(a) * ry); }
        }
    }

    /// <summary>Bildausschnitt im Bild-Atlas.</summary>
    public sealed class Img
    {
        public string Name; public float U0, V0, U1, V1; public int Width, Height;
    }

    /// <summary>
    /// Sofortmodus-Zeichenflaeche im 1600x900-Designraum. Alle Formen werden pro Frame in EIN Mesh
    /// geschrieben und von einem Shader mit Signed-Distance-Funktionen pixelgenau, kantengeglaettet und
    /// mit HDR-Leuchtkraft gerendert. Die Reihenfolge der Aufrufe ist die Zeichenreihenfolge.
    /// </summary>
    public sealed class Canvas2D
    {
        public const int SOLID = 0, RRECT = 1, ELLIPSE = 2, RADIAL = 3, BALL = 4, IMAGE = 5, GLYPH = 6, STRIP = 7, PERFORATED = 8, ARC = 9, DICE = 10;

        struct State { public Aff M; public Vector4 Clip; public float ClipR; public float Alpha; }
        static readonly Vector4 NoClip = new Vector4(-1e5f, -1e5f, 1e5f, 1e5f);

        Aff m = Aff.Identity; Vector4 clip = NoClip; float clipR; float alpha = 1;
        readonly Stack<State> stack = new Stack<State>();

        readonly List<Vector3> pos = new List<Vector3>(16384);
        readonly List<Color32> col = new List<Color32>(16384);
        readonly List<Vector4>[] uv = new List<Vector4>[8];
        readonly List<Vector4> tan = new List<Vector4>(16384);
        readonly List<int> idx = new List<int>(32768);

        public Canvas2D() { for (int i = 0; i < 8; i++) uv[i] = new List<Vector4>(16384); }

        public int VertexCount => pos.Count;
        public Aff Matrix { get => m; set => m = value; }
        public float Alpha => alpha;

        public void Begin()
        {
            pos.Clear(); col.Clear(); tan.Clear(); idx.Clear(); for (int i = 0; i < 8; i++) uv[i].Clear();
            stack.Clear(); m = Aff.Identity; clip = NoClip; clipR = 0; alpha = 1;
        }

        public void Upload(Mesh mesh)
        {
            mesh.Clear();
            if (pos.Count == 0) return;
            mesh.SetVertices(pos); mesh.SetColors(col);
            for (int i = 0; i < 8; i++) mesh.SetUVs(i, uv[i]);
            mesh.SetTangents(tan);
            mesh.SetIndices(idx, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e5f, 1e5f, 100));
        }

        // ---------------------------------------------------------------- Zustand
        public int Save() { stack.Push(new State { M = m, Clip = clip, ClipR = clipR, Alpha = alpha }); return stack.Count; }
        /// <summary>Wie Skia SaveLayer mit Alpha: alles Folgende wird bis Restore abgeschwaecht.</summary>
        public int SaveLayer(float layerAlpha) { Save(); alpha *= Mathf.Clamp01(layerAlpha); return stack.Count; }
        public int SaveLayer(Paint p) => SaveLayer(p == null ? 1 : p.Color.Alpha / 255f);
        public void Restore() { if (stack.Count == 0) return; var s = stack.Pop(); m = s.M; clip = s.Clip; clipR = s.ClipR; alpha = s.Alpha; }
        public void Translate(float x, float y) => m = m * new Aff(1, 0, 0, 1, x, y);
        public void Scale(float s) => Scale(s, s);
        public void Scale(float sx, float sy) => m = m * new Aff(sx, 0, 0, sy, 0, 0);
        public void Scale(float sx, float sy, float px, float py) { Translate(px, py); Scale(sx, sy); Translate(-px, -py); }
        public void RotateRadians(float r) { float c = MathF.Cos(r), s = MathF.Sin(r); m = m * new Aff(c, s, -s, c, 0, 0); }
        public void RotateDegrees(float d) => RotateRadians(d * MathF.PI / 180f);
        public void RotateDegrees(float d, float px, float py) { Translate(px, py); RotateDegrees(d); Translate(-px, -py); }
        public void Concat(Aff a) => m = m * a;

        /// <summary>Begrenzt alles Folgende auf ein (abgerundetes) Rechteck. Bei Drehung wird die Huelle verwendet.</summary>
        public void ClipRoundRect(Box r, float rad)
        {
            m.Map(r.Left, r.Top, out var x0, out var y0); m.Map(r.Right, r.Top, out var x1, out var y1);
            m.Map(r.Right, r.Bottom, out var x2, out var y2); m.Map(r.Left, r.Bottom, out var x3, out var y3);
            var c = new Vector4(Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)), Math.Min(Math.Min(y0, y1), Math.Min(y2, y3)), Math.Max(Math.Max(x0, x1), Math.Max(x2, x3)), Math.Max(Math.Max(y0, y1), Math.Max(y2, y3)));
            float sc = Math.Min(m.ScaleX, m.ScaleY);
            // Schnittmenge mit bestehendem Clip
            clip = new Vector4(Math.Max(clip.x, c.x), Math.Max(clip.y, c.y), Math.Min(clip.z, c.z), Math.Min(clip.w, c.w));
            clipR = Math.Max(clipR, rad * sc);
        }
        public void ClipRect(Box r) => ClipRoundRect(r, 0);
        /// <summary>Elliptischer/kreisfoermiger Clip (z.B. Muenze): abgerundetes Rechteck mit maximalem Radius.</summary>
        public void ClipOval(Box r) => ClipRoundRect(r, Math.Min(r.Width, r.Height) / 2);

        // ---------------------------------------------------------------- Primitive
        public void DrawRect(float x, float y, float w, float h, Paint p) => RRect(x + w / 2, y + h / 2, w / 2, h / 2, 0, p);
        public void DrawRect(Box r, Paint p) => RRect(r.MidX, r.MidY, r.Width / 2, r.Height / 2, 0, p);
        public void DrawRoundRect(Box r, float rx, float ry, Paint p) => RRect(r.MidX, r.MidY, r.Width / 2, r.Height / 2, Math.Min(rx, ry), p);
        public void DrawCircle(float x, float y, float r, Paint p) { if (r > 0) RRect(x, y, r, r, r, p); }
        public void DrawOval(float cx, float cy, float rx, float ry, Paint p)
        {
            if (rx <= 0 || ry <= 0) return;
            if (Math.Abs(rx - ry) < .01f) { RRect(cx, cy, rx, rx, rx, p); return; }
            Shape(ELLIPSE, cx, cy, 1, 0, rx, ry, 0, p, Vector4.zero, Vector4.zero);
        }
        public void DrawOval(Box r, Paint p) => DrawOval(r.MidX, r.MidY, r.Width / 2, r.Height / 2, p);

        public void DrawLine(float x0, float y0, float x1, float y1, Paint p)
        {
            float dx = x1 - x0, dy = y1 - y0, len = MathF.Sqrt(dx * dx + dy * dy), w = Math.Max(.01f, p.StrokeWidth);
            float ax = len > 1e-4f ? dx / len : 1, ay = len > 1e-4f ? dy / len : 0;
            // Linie = gedrehte Kapsel mit runden Enden (StrokeCap.Round)
            var fill = Tmp(p); fill.Stroke = false;
            Shape(RRECT, (x0 + x1) / 2, (y0 + y1) / 2, ax, ay, len / 2 + w / 2, w / 2, w / 2, fill, Vector4.zero, Vector4.zero);
        }
        public void DrawLine(Pt a, Pt b, Paint p) => DrawLine(a.X, a.Y, b.X, b.Y, p);

        /// <summary>Kreisbogen (Grad, 0 = rechts, im Uhrzeigersinn). useCenter=true zeichnet ein Tortenstueck.</summary>
        public void DrawArc(Box oval, float startDeg, float sweepDeg, bool useCenter, Paint p)
        {
            if (sweepDeg < 0) { startDeg += sweepDeg; sweepDeg = -sweepDeg; }
            if (sweepDeg >= 360) { DrawOval(oval, p); return; }
            float rx = oval.Width / 2, ry = oval.Height / 2;
            if (useCenter || !p.Stroke)
            {
                using var path = new Path2D(); path.MoveTo(oval.MidX, oval.MidY); path.ArcTo(oval, startDeg, sweepDeg); path.Close(); DrawPath(path, p); return;
            }
            Save(); Translate(oval.MidX, oval.MidY);
            float r = rx;
            if (Math.Abs(rx - ry) > .01f) { Scale(1, ry / rx); }
            Shape(ARC, 0, 0, 1, 0, r, r, 0, p, new Vector4(startDeg, sweepDeg, 0, 0), Vector4.zero);
            Restore();
        }

        public void DrawPath(Path2D path, Paint p)
        {
            if (p.Stroke)
            {
                for (int i = 0; i < path.Contours.Count; i++) StrokePoly(path.Contours[i], path.Closed[i], p);
                return;
            }
            if (path.EvenOdd && path.Contours.Count > 1) { FillEvenOdd(path, p); return; }
            foreach (var c in path.Contours) FillPoly(c, p);
        }

        /// <summary>Bild aus dem Atlas in ein Zielrechteck, optional mit abgerundeten Ecken (rad).</summary>
        public void DrawImage(Img img, Box dst, Paint p, float rad = 0)
        {
            if (img == null) return;
            Shape(IMAGE, dst.MidX, dst.MidY, 1, 0, dst.Width / 2, dst.Height / 2, rad, p, Vector4.zero, Vector4.zero, new Vector4(img.U0, img.V0, img.U1, img.V1));
        }

        /// <summary>Ausschnitt (u0,v0,u1,v1) der 3D-Wuerfel-Textur (vormultipliziertes Alpha) in ein Zielrechteck.</summary>
        public void DrawDice(Box dst, Vector4 uv, Paint p) => Shape(DICE, dst.MidX, dst.MidY, 1, 0, dst.Width / 2, dst.Height / 2, 0, p, Vector4.zero, Vector4.zero, uv);

        /// <summary>Rechteck mit regelmaessigem Lochraster (Vier-Gewinnt-Brett). Loecher: cols x rows ab gridLeft/gridTop.</summary>
        public void DrawPerforated(Box r, float rad, float gridLeft, float gridTop, float cellW, float cellH, int cols, int rows, float holeR, Paint p)
        {
            var extra = new Vector4(cellW, cellH, holeR, cols + rows * 1000);
            var t = new Vector4(gridLeft - r.MidX, gridTop - r.MidY, 0, 0);
            Shape(PERFORATED, r.MidX, r.MidY, 1, 0, r.Width / 2, r.Height / 2, rad, p, extra, t);
        }

        /// <summary>Weicher radialer Verlauf (Farbe -&gt; transparent), z.B. fuer Lichthoefe.</summary>
        public void DrawRadial(float x, float y, float r, Paint p) { if (r > 0) Shape(RADIAL, x, y, 1, 0, r, r, 0, p, Vector4.zero, Vector4.zero); }
        /// <summary>Beleuchtete Kugel (Spielsteine, Schlange, Kugeln).</summary>
        public void DrawBall(float x, float y, float r, Paint p) { if (r > 0) Shape(BALL, x, y, 1, 0, r, r, r, p, Vector4.zero, Vector4.zero); }

        // ---------------------------------------------------------------- Interna
        static readonly Paint tmpPaint = new Paint();
        static Paint Tmp(Paint p)
        {
            tmpPaint.Color = p.Color; tmpPaint.Stroke = p.Stroke; tmpPaint.StrokeWidth = p.StrokeWidth; tmpPaint.Blur = p.Blur;
            tmpPaint.Additive = p.Additive; tmpPaint.Glow = p.Glow; tmpPaint.Shader = p.Shader; return tmpPaint;
        }

        void RRect(float cx, float cy, float hw, float hh, float rad, Paint p)
        {
            if (hw <= 0 || hh <= 0) return;
            Shape(RRECT, cx, cy, 1, 0, Math.Abs(hw), Math.Abs(hh), Math.Max(0, Math.Min(rad, Math.Min(hw, hh))), p, Vector4.zero, Vector4.zero);
        }

        /// <summary>Gibt ein Quad fuer eine SDF-Form aus. (ax, ay) = lokale x-Achse (Einheitsvektor) im Objektraum.</summary>
        internal void Shape(int type, float cx, float cy, float ax, float ay, float hw, float hh, float rad, Paint p, Vector4 extra, Vector4 tng, Vector4 tex = default)
        {
            float sw = p.Stroke ? Math.Max(.01f, p.StrokeWidth) : 0, blur = Math.Max(0, p.Blur);
            float pad = blur * 3f + sw * .5f + 2f / Math.Max(.05f, Math.Min(m.ScaleX, m.ScaleY));
            if (type == IMAGE || type == DICE) pad = 0;
            if (type == ARC) pad += 0;
            float ex = hw + pad, ey = hh + pad;
            var shp = new Vector4(hw, hh, rad, sw);
            int baseV = pos.Count;
            float bx = -ay, by = ax; // senkrechte Achse (y nach unten)
            for (int k = 0; k < 4; k++)
            {
                float lx = (k == 0 || k == 3) ? -ex : ex, ly = k < 2 ? -ey : ey;
                float ox = cx + ax * lx + bx * ly, oy = cy + ay * lx + by * ly;
                Vector4 u0 = new Vector4(lx, ly, 0, 0);
                if (type == IMAGE || type == DICE) { u0.z = lx < 0 ? tex.x : tex.z; u0.w = ly < 0 ? tex.y : tex.w; }
                Vert(ox, oy, u0, shp, type, blur, p, extra, tng);
            }
            Tri(baseV);
        }

        void Tri(int b) { idx.Add(b); idx.Add(b + 1); idx.Add(b + 2); idx.Add(b); idx.Add(b + 2); idx.Add(b + 3); }

        internal void Vert(float ox, float oy, Vector4 u0, Vector4 shp, int type, float blur, Paint p, Vector4 extra, Vector4 tng)
        {
            m.Map(ox, oy, out var dx, out var dy);
            pos.Add(new Vector3(dx - 800f, 450f - dy, 0));
            var c = p.Shader != null ? p.Shader.A : p.Color;
            float a = alpha * (p.Shader != null ? p.Color.Alpha / 255f : 1f);
            col.Add(new Color32(c.Red, c.Green, c.Blue, (byte)Mathf.Clamp(c.Alpha * a + .5f, 0, 255)));
            uv[0].Add(u0); uv[1].Add(shp);
            uv[2].Add(new Vector4(type, blur, p.Glow, p.Additive ? 1 : 0));
            uv[3].Add(extra);
            uv[4].Add(clip);
            if (p.Shader != null)
            {
                var b = p.Shader.B; uv[5].Add(new Vector4(b.Red / 255f, b.Green / 255f, b.Blue / 255f, b.Alpha / 255f * a));
                uv[6].Add(new Vector4(p.Shader.X0, p.Shader.Y0, p.Shader.X1, p.Shader.Y1));
                uv[7].Add(new Vector4(ox, oy, clipR, p.Shader.Mode));
            }
            else { uv[5].Add(Vector4.zero); uv[6].Add(Vector4.zero); uv[7].Add(new Vector4(ox, oy, clipR, 0)); }
            tan.Add(tng);
        }

        // ------------------------------------------------ Polygone
        static readonly List<Vector2> work = new List<Vector2>(256);
        static readonly List<int> ear = new List<int>(256);

        void FillPoly(List<Vector2> pts, Paint p)
        {
            int n = pts.Count; if (n < 3) return;
            if (p.Blur > 0) { /* weiche Polygone: als Kontur mit Weichzeichnung annaehern */ }
            work.Clear(); work.AddRange(pts);
            if (work.Count > 3 && (work[0] - work[work.Count - 1]).sqrMagnitude < 1e-6f) work.RemoveAt(work.Count - 1);
            n = work.Count; if (n < 3) return;
            float area = 0; for (int i = 0; i < n; i++) { var a = work[i]; var b = work[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            int baseV = pos.Count; var shp = Vector4.zero;
            for (int i = 0; i < n; i++) Vert(work[i].x, work[i].y, Vector4.zero, shp, SOLID, 0, p, Vector4.zero, Vector4.zero);
            ear.Clear(); for (int i = 0; i < n; i++) ear.Add(i);
            bool ccw = area > 0; int guard = 0;
            while (ear.Count > 3 && guard++ < n * n)
            {
                bool cut = false;
                for (int i = 0; i < ear.Count; i++)
                {
                    int i0 = ear[(i + ear.Count - 1) % ear.Count], i1 = ear[i], i2 = ear[(i + 1) % ear.Count];
                    var a = work[i0]; var b = work[i1]; var c = work[i2];
                    float cr = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (ccw ? cr <= 0 : cr >= 0) continue;
                    bool inside = false;
                    for (int k = 0; k < ear.Count && !inside; k++)
                    {
                        int j = ear[k]; if (j == i0 || j == i1 || j == i2) continue;
                        if (InTri(work[j], a, b, c)) inside = true;
                    }
                    if (inside) continue;
                    idx.Add(baseV + i0); idx.Add(baseV + i1); idx.Add(baseV + i2); ear.RemoveAt(i); cut = true; break;
                }
                if (!cut) break;
            }
            if (ear.Count >= 3) for (int i = 1; i + 1 < ear.Count; i++) { idx.Add(baseV + ear[0]); idx.Add(baseV + ear[i]); idx.Add(baseV + ear[i + 1]); }
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y), d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y), d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, posi = d1 > 0 || d2 > 0 || d3 > 0; return !(neg && posi);
        }

        /// <summary>Gerade-Ungerade-Fuellung per Scanline-Streifen (fuer Pfade mit Loechern).</summary>
        void FillEvenOdd(Path2D path, Paint p)
        {
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var c in path.Contours) foreach (var v in c) { y0 = Math.Min(y0, v.y); y1 = Math.Max(y1, v.y); }
            int bands = Math.Min(400, Math.Max(8, (int)(y1 - y0)));
            var xs = new List<float>();
            for (int bi = 0; bi < bands; bi++)
            {
                float ya = y0 + (y1 - y0) * bi / bands, yb = y0 + (y1 - y0) * (bi + 1) / bands, ym = (ya + yb) / 2;
                xs.Clear();
                foreach (var c in path.Contours)
                    for (int i = 0; i < c.Count; i++)
                    {
                        var a = c[i]; var b = c[(i + 1) % c.Count];
                        if ((a.y <= ym && b.y > ym) || (b.y <= ym && a.y > ym)) xs.Add(a.x + (ym - a.y) / (b.y - a.y) * (b.x - a.x));
                    }
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                {
                    int bv = pos.Count;
                    Vert(xs[i], ya, Vector4.zero, Vector4.zero, SOLID, 0, p, Vector4.zero, Vector4.zero);
                    Vert(xs[i + 1], ya, Vector4.zero, Vector4.zero, SOLID, 0, p, Vector4.zero, Vector4.zero);
                    Vert(xs[i + 1], yb, Vector4.zero, Vector4.zero, SOLID, 0, p, Vector4.zero, Vector4.zero);
                    Vert(xs[i], yb, Vector4.zero, Vector4.zero, SOLID, 0, p, Vector4.zero, Vector4.zero);
                    Tri(bv);
                }
            }
        }

        /// <summary>Polylinie als Streifen mit Gehrungsfugen (keine Ueberlappung bei Transparenz).</summary>
        void StrokePoly(List<Vector2> src, bool closed, Paint p)
        {
            work.Clear();
            foreach (var v in src) if (work.Count == 0 || (work[work.Count - 1] - v).sqrMagnitude > 1e-6f) work.Add(v);
            if (closed && work.Count > 2 && (work[0] - work[work.Count - 1]).sqrMagnitude < 1e-6f) work.RemoveAt(work.Count - 1);
            int n = work.Count;
            if (n < 2) { if (n == 1) DrawCircle(work[0].x, work[0].y, p.StrokeWidth / 2, Fillify(p)); return; }
            float w = Math.Max(.01f, p.StrokeWidth), blur = Math.Max(0, p.Blur);
            float e = w / 2 + blur * 3 + 2f / Math.Max(.05f, Math.Min(m.ScaleX, m.ScaleY));
            var shp = new Vector4(0, w / 2, 0, w);
            int segs = closed ? n : n - 1;
            for (int s = 0; s < segs; s++)
            {
                var a = work[s]; var b = work[(s + 1) % n];
                var dir = (b - a).normalized; var nrm = new Vector2(-dir.y, dir.x);
                Vector2 Miter(int i, Vector2 segN)
                {
                    bool hasPrev = closed || i > 0, hasNext = closed || i < n - 1;
                    if (!hasPrev || !hasNext) return segN;
                    var pv = work[(i - 1 + n) % n]; var cv = work[i]; var nv = work[(i + 1) % n];
                    var d0 = (cv - pv).normalized; var d1 = (nv - cv).normalized;
                    var n0 = new Vector2(-d0.y, d0.x); var n1 = new Vector2(-d1.y, d1.x);
                    var mt = (n0 + n1); if (mt.sqrMagnitude < 1e-6f) return segN;
                    mt.Normalize(); float k = Vector2.Dot(mt, segN); k = Math.Max(k, .35f);
                    return mt / k;
                }
                var ma = Miter(s, nrm); var mb = Miter((s + 1) % n, nrm);
                Vector2 ea = Vector2.zero, eb = Vector2.zero;
                if (!closed && s == 0) ea = -dir * (w / 2 + blur * 2);
                if (!closed && s == segs - 1) eb = dir * (w / 2 + blur * 2);
                int bv = pos.Count;
                var pa0 = a + ea - ma * e; var pb0 = b + eb - mb * e; var pb1 = b + eb + mb * e; var pa1 = a + ea + ma * e;
                Vert(pa0.x, pa0.y, new Vector4(0, -e, 0, 0), shp, STRIP, blur, p, Vector4.zero, Vector4.zero);
                Vert(pb0.x, pb0.y, new Vector4(0, -e, 0, 0), shp, STRIP, blur, p, Vector4.zero, Vector4.zero);
                Vert(pb1.x, pb1.y, new Vector4(0, e, 0, 0), shp, STRIP, blur, p, Vector4.zero, Vector4.zero);
                Vert(pa1.x, pa1.y, new Vector4(0, e, 0, 0), shp, STRIP, blur, p, Vector4.zero, Vector4.zero);
                Tri(bv);
            }
        }

        static readonly Paint fillTmp = new Paint();
        static Paint Fillify(Paint p) { fillTmp.Color = p.Color; fillTmp.Blur = p.Blur; fillTmp.Additive = p.Additive; fillTmp.Glow = p.Glow; fillTmp.Shader = p.Shader; fillTmp.Stroke = false; return fillTmp; }

        /// <summary>Gibt ein Glyph-Quad aus (von FontAtlas verwendet).</summary>
        internal void Glyph(float x0, float y0, float w, float h, float u0, float v0, float u1, float v1, float k, float spreadObj, Paint p)
        {
            float sw = p.Stroke ? Math.Max(.01f, p.StrokeWidth) : 0;
            var shp = new Vector4(w / 2, h / 2, k, sw);
            var extra = new Vector4(spreadObj, 0, 0, 0);
            int bv = pos.Count; float cx = x0 + w / 2, cy = y0 + h / 2;
            Vert(x0, y0, new Vector4(-w / 2, -h / 2, u0, v0), shp, GLYPH, p.Blur, p, extra, Vector4.zero);
            Vert(x0 + w, y0, new Vector4(w / 2, -h / 2, u1, v0), shp, GLYPH, p.Blur, p, extra, Vector4.zero);
            Vert(x0 + w, y0 + h, new Vector4(w / 2, h / 2, u1, v1), shp, GLYPH, p.Blur, p, extra, Vector4.zero);
            Vert(x0, y0 + h, new Vector4(-w / 2, h / 2, u0, v1), shp, GLYPH, p.Blur, p, extra, Vector4.zero);
            Tri(bv);
        }
    }
}
