using System;
using System.Collections.Generic;
using System.Numerics;

namespace GlamourGames
{
    /// <summary>Aufgezeichnete Wurfbahn eines Wuerfels (Unity-Koordinaten: y oben, z in die Tiefe; Kantenlaenge 1).</summary>
    public sealed class DiceTrack
    {
        public float Dt;
        public Vector3[] Pos;
        public Quaternion[] Rot;
        /// <summary>Aufpralle (Zeit, Staerke 0..1) fuer Klack-Geraeusche.</summary>
        public readonly List<(float t, float s)> Hits = new List<(float t, float s)>();
        public float Duration => (Pos.Length - 1) * Dt;
        public void Sample(float t, out Vector3 p, out Quaternion q)
        {
            float f = Math.Clamp(t / Dt, 0, Pos.Length - 1); int i = Math.Min((int)f, Pos.Length - 2); float k = f - i;
            if (Pos.Length < 2) { p = Pos[0]; q = Rot[0]; return; }
            p = Vector3.Lerp(Pos[i], Pos[i + 1], k); q = Quaternion.Slerp(Rot[i], Rot[i + 1], k);
        }
    }

    /// <summary>
    /// Starrkoerper-Simulation eines Wuerfels auf dem Tisch: Schwerkraft, Aufprall-Impulse an den (abgerundeten) Ecken
    /// mit Restitution und Coulomb-Reibung, Roll-/Luftwiderstand. Die Bahn wird vorab berechnet und danach so verschoben
    /// und lokal umbeschriftet, dass der Wuerfel exakt im Zielfeld mit der gewuerfelten Augenzahl oben liegen bleibt
    /// (die Bewegung bleibt dadurch physikalisch unveraendert).
    /// </summary>
    public static class DicePhysics
    {
        const float H = .47f, G = 46f, Restitution = .42f, Friction = .42f, Inertia = 1f / 6f, Dt = 1f / 240f;

        /// <summary>Flaechennormale (Wuerfel-lokal, Unity-Achsen) der Augenzahl v.</summary>
        public static Vector3 FaceNormal(int v)
        {
            for (int f = 0; f < 6; f++)
                if (Die3D.FaceValues[f] == v) { var n = Die3D.Normal(f); return new Vector3(n[0], n[1], -n[2]); }
            return Vector3.UnitY;
        }

        /// <summary>Ruhelage: Augenzahl v oben, um die Hochachse um yaw gedreht.</summary>
        public static Quaternion RestRotation(int v, float yaw) => Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw) * FromTo(FaceNormal(v), Vector3.UnitY);

        static Quaternion FromTo(Vector3 a, Vector3 b)
        {
            a = Vector3.Normalize(a); b = Vector3.Normalize(b); float d = Vector3.Dot(a, b);
            if (d > .9999f) return Quaternion.Identity;
            if (d < -.9999f) { var ax = Math.Abs(a.X) < .9f ? Vector3.Cross(a, Vector3.UnitX) : Vector3.Cross(a, Vector3.UnitZ); return Quaternion.CreateFromAxisAngle(Vector3.Normalize(ax), MathF.PI); }
            var c = Vector3.Cross(a, b); return Quaternion.Normalize(new Quaternion(c, 1 + d));
        }

        static readonly Vector3[] corners = BuildCorners();
        static Vector3[] BuildCorners() { var c = new Vector3[8]; for (int i = 0; i < 8; i++) c[i] = new Vector3((i & 1) == 0 ? -H : H, (i & 2) == 0 ? -H : H, (i & 4) == 0 ? -H : H); return c; }

        /// <summary>Simuliert einen Wurf und liefert die korrigierte Bahn (Endlage: target, value oben).</summary>
        public static DiceTrack Throw(Random rnd, Vector3 target, int value, float delay = 0)
        {
            var p = new Vector3(-2.6f - (float)rnd.NextDouble() * 1.2f, 1.3f + (float)rnd.NextDouble() * .9f, (float)(rnd.NextDouble() - .5) * .8f);
            var v = new Vector3(3.6f + (float)rnd.NextDouble() * 2f, 1f + (float)rnd.NextDouble() * 1.5f, (float)(rnd.NextDouble() - .5) * 1.4f);
            var w = new Vector3((float)(rnd.NextDouble() - .5) * 26, (float)(rnd.NextDouble() - .5) * 14, (float)(rnd.NextDouble() - .5) * 26);
            var q = Quaternion.Normalize(new Quaternion((float)rnd.NextDouble() - .5f, (float)rnd.NextDouble() - .5f, (float)rnd.NextDouble() - .5f, (float)rnd.NextDouble() + .2f));
            var pos = new List<Vector3>(); var rot = new List<Quaternion>(); var tr = new DiceTrack { Dt = Dt };
            int still = 0; float lastHit = -1;
            for (int d = 0; d < (int)(delay / Dt); d++) { pos.Add(p); rot.Add(q); }
            for (int step = 0; step < 240 * 4; step++)
            {
                float t = (pos.Count) * Dt;
                v.Y -= G * Dt;
                p += v * Dt;
                var wq = new Quaternion(w * Dt * .5f, 0); q = Quaternion.Normalize(q + wq * q);
                bool contact = false;
                // Kontaktpunkte sammeln (alle Ecken nahe am Tisch), einmal herausschieben, dann sequentielle Impulse
                float dmin = 0; for (int k = 0; k < 8; k++) dmin = Math.Min(dmin, (p + Vector3.Transform(corners[k], q)).Y);
                p.Y -= dmin;
                var rs = new List<Vector3>(4);
                for (int k = 0; k < 8; k++) { var r0 = Vector3.Transform(corners[k], q); if (p.Y + r0.Y < .012f) rs.Add(r0); }
                contact = rs.Count > 0;
                for (int it = 0; it < 6 && contact; it++)
                    foreach (var r in rs)
                    {
                        var vp = v + Vector3.Cross(w, r);
                        if (vp.Y >= 0) continue;
                        var n = Vector3.UnitY; var rn = Vector3.Cross(r, n);
                        float e = -vp.Y < 1.5f || it > 0 ? 0 : Restitution; // Ruhekontakt ohne Rueckprall
                        float jn = -(1 + e) * vp.Y / (1 + Vector3.Dot(rn, rn) / Inertia);
                        v += n * jn; w += Vector3.Cross(r, n * jn) / Inertia;
                        if (it == 0 && -vp.Y > 1.6f && t - lastHit > .06f) { tr.Hits.Add((t, Math.Min(1, -vp.Y / 9f))); lastHit = t; }
                        var vp2 = v + Vector3.Cross(w, r); var vt = vp2 - n * vp2.Y; float vtl = vt.Length();
                        if (vtl > 1e-4f)
                        {
                            var tdir = vt / vtl; var rt = Vector3.Cross(r, tdir);
                            float jt = Math.Min(vtl / (1 + Vector3.Dot(rt, rt) / Inertia), Friction * jn);
                            v -= tdir * jt; w -= Vector3.Cross(r, tdir * jt) / Inertia;
                        }
                    }
                if (contact) { float slow = v.Length() < .5f && w.Length() < 2.5f ? 5f : .9f; w *= 1 - slow * Dt; v.X *= 1 - slow * .6f * Dt; v.Z *= 1 - slow * .6f * Dt; }
                pos.Add(p); rot.Add(q);
                if (contact && v.Length() < .15f && w.Length() < .6f) { if (++still > 24) break; } else still = 0;
            }
            // Ausrichten: naechste Flaeche exakt nach oben, sanft ueber 0.18 s
            var qEnd = rot[rot.Count - 1]; int upVal = 1; float best = -2;
            for (int val = 1; val <= 6; val++) { float d = Vector3.Transform(FaceNormal(val), qEnd).Y; if (d > best) { best = d; upVal = val; } }
            var qFlat = Quaternion.Normalize(FromTo(Vector3.Transform(FaceNormal(upVal), qEnd), Vector3.UnitY) * qEnd);
            var pEnd = pos[pos.Count - 1]; pEnd.Y = .5f;
            int blend = (int)(.18f / Dt);
            for (int k = 1; k <= blend; k++) { float e = k / (float)blend; e = e * e * (3 - 2 * e); rot.Add(Quaternion.Slerp(qEnd, qFlat, e)); pos.Add(Vector3.Lerp(pos[pos.Count - 1], pEnd, e)); }
            // Lokale Umbeschriftung: Flaeche 'value' an die Stelle der oben liegenden Flaeche drehen
            var relabel = FromTo(FaceNormal(value), FaceNormal(upVal));
            var shift = target - pEnd; shift.Y = 0;
            tr.Pos = new Vector3[pos.Count]; tr.Rot = new Quaternion[rot.Count];
            for (int k = 0; k < pos.Count; k++) { tr.Pos[k] = pos[k] + shift; tr.Rot[k] = Quaternion.Normalize(rot[k] * relabel); }
            return tr;
        }
    }
}
