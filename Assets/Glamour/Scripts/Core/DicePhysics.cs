using System;
using System.Collections.Generic;
using System.Linq;
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
        // Wuerfel gegen Wuerfel: abgerundeter Wuerfel als Kugel (Mittenabstand beim Beruehren ~1.05 Kanten), Stoss mit Reibung erzeugt Drall
        const float DieGap = 1.05f, DieRest = .35f, DieFric = .3f, WallX = 3.8f, WallBack = 4.2f, WallZ = 2.1f, Arrange = .32f;

        /// <summary>Einzelwurf (Kompatibilitaet): ein Wuerfel ohne Partner.</summary>
        public static DiceTrack Throw(Random rnd, Vector3 target, int value, float delay = 0) => ThrowAll(rnd, new[] { target }, new[] { value })[0];

        /// <summary>
        /// Gemeinsamer Wurf aus einer Hand: alle Wuerfel fliegen gleichzeitig, stossen in der Luft und auf dem Tisch
        /// aneinander und rollen aus. Danach werden sie den Zielplaetzen in Rollreihenfolge (x) zugeordnet; eine gemeinsame
        /// Abbildung (Verschiebung + Streckung in x) bringt die Gruppe zu den Plaetzen, der kleine Rest je Wuerfel wird ueber
        /// die Bahn eingeblendet. Die gewuerfelte Augenzahl entsteht durch lokale Umbeschriftung.
        /// power (0.6..1.6) = Wurfkraft, spin = Drall (-1..1), aim = seitliche Richtung (-1..1) - z.B. aus einer Wischgeste.
        /// obstacles = liegende (gehaltene) Wuerfel, an denen die geworfenen abprallen.
        /// </summary>
        public static DiceTrack[] ThrowAll(Random rnd, Vector3[] targets, int[] values, float power = 1, float spin = 0, float aim = 0, Vector3[] obstacles = null)
        {
            int n = targets.Length; float F() => (float)rnd.NextDouble();
            var p = new Vector3[n]; var v = new Vector3[n]; var w = new Vector3[n]; var q = new Quaternion[n];
            var pos = new List<Vector3>[n]; var rot = new List<Quaternion>[n]; var tr = new DiceTrack[n]; var lastHit = new float[n];
            power = Math.Clamp(power, .6f, 1.6f); spin = Math.Clamp(spin, -1, 1); aim = Math.Clamp(aim, -1, 1);
            for (int i = 0; i < n; i++)
            {
                // Hand: Wuerfel liegen dicht beieinander (kleiner Haufen), alle mit aehnlicher Wurfrichtung
                float k = i - (n - 1) / 2f;
                p[i] = new Vector3(-3.2f + (i % 2) * .5f + F() * .2f, 1.5f + (i / 2) * .55f + F() * .2f, k * .55f + (F() - .5f) * .15f);
                v[i] = new Vector3((3.9f + F() * 1.8f) * power, (1f + F() * 1.3f) * MathF.Sqrt(power), (F() - .5f) * 1.2f + aim * 1.6f);
                w[i] = new Vector3((F() - .5f) * 26 * power, (F() - .5f) * 14, (F() - .5f) * 26 * power - spin * 18);
                q[i] = Quaternion.Normalize(new Quaternion(F() - .5f, F() - .5f, F() - .5f, F() + .2f));
                pos[i] = new List<Vector3>(); rot[i] = new List<Quaternion>(); tr[i] = new DiceTrack { Dt = Dt }; lastHit[i] = -1;
            }
            var still = new int[n]; var done = new bool[n];
            for (int step = 0; step < 240 * 5; step++)
            {
                float t = step * Dt;
                for (int i = 0; i < n; i++)
                {
                    if (done[i]) continue;
                    v[i].Y -= G * Dt; p[i] += v[i] * Dt;
                    var wq = new Quaternion(w[i] * Dt * .5f, 0); q[i] = Quaternion.Normalize(q[i] + wq * q[i]);
                    bool contact = PlaneContact(ref p[i], ref v[i], ref w[i], q[i], Vector3.UnitY, 0, t, tr[i], ref lastHit[i]);
                    // Rand der Wuerfelschale: Wuerfel prallen an den Seiten ab
                    PlaneContact(ref p[i], ref v[i], ref w[i], q[i], -Vector3.UnitX, -WallX, t, tr[i], ref lastHit[i]);
                    PlaneContact(ref p[i], ref v[i], ref w[i], q[i], Vector3.UnitX, -WallBack, t, tr[i], ref lastHit[i]);
                    PlaneContact(ref p[i], ref v[i], ref w[i], q[i], -Vector3.UnitZ, -WallZ, t, tr[i], ref lastHit[i]);
                    PlaneContact(ref p[i], ref v[i], ref w[i], q[i], Vector3.UnitZ, -WallZ, t, tr[i], ref lastHit[i]);
                    if (contact) { float slow = v[i].Length() < .5f && w[i].Length() < 2.5f ? 5f : .9f; w[i] *= 1 - slow * Dt; v[i].X *= 1 - slow * .6f * Dt; v[i].Z *= 1 - slow * .6f * Dt; }
                    if (contact && v[i].Length() < .15f && w[i].Length() < .6f) { if (++still[i] > 24) { done[i] = true; v[i] = w[i] = Vector3.Zero; } } else still[i] = 0;
                }
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) DieDie(i, j, p, v, w, t, tr, lastHit, done);
                if (obstacles != null) for (int i = 0; i < n; i++) foreach (var ob in obstacles) DieFixed(ref p[i], ref v[i], ref w[i], ob, t, tr[i], ref lastHit[i]);
                for (int i = 0; i < n; i++) { pos[i].Add(p[i]); rot[i].Add(q[i]); }
                if (done.All(d => d)) break;
            }
            // Zuordnung: Wuerfel in Rollreihenfolge (x) zu den Zielplaetzen in x-Reihenfolge
            var simOrder = Enumerable.Range(0, n).OrderBy(i => p[i].X).ToArray(); var slotOrder = Enumerable.Range(0, n).OrderBy(i => targets[i].X).ToArray();
            var slotOf = new int[n]; for (int r = 0; r < n; r++) slotOf[simOrder[r]] = slotOrder[r];
            // Nach dem Ausrollen schiebt die Hand die Wuerfel kurz in die Reihe (gleitend, ohne Umdrehen): Wurf bleibt unverzerrt
            var result = new DiceTrack[n]; int arr = (int)(Arrange / Dt); var flat = new Quaternion[n]; var upv = new int[n];
            for (int i = 0; i < n; i++)
            {
                var P = pos[i]; var Q = rot[i];
                var qEnd = Q[Q.Count - 1]; int upVal = 1; float best = -2;
                for (int val = 1; val <= 6; val++) { float d = Vector3.Transform(FaceNormal(val), qEnd).Y; if (d > best) { best = d; upVal = val; } }
                var qFlat = Quaternion.Normalize(FromTo(Vector3.Transform(FaceNormal(upVal), qEnd), Vector3.UnitY) * qEnd);
                var pEnd = P[P.Count - 1]; pEnd.Y = .5f; int blend = (int)(.18f / Dt);
                for (int k = 1; k <= blend; k++) { float e = k / (float)blend; e = e * e * (3 - 2 * e); Q.Add(Quaternion.Slerp(qEnd, qFlat, e)); P.Add(Vector3.Lerp(P[P.Count - 1], pEnd, e)); }
                flat[i] = qFlat; upv[i] = upVal;
            }
            // Aufreihen gemeinsam: Sollbahn je Wuerfel, Abstaende werden pro Schritt eingehalten (kein Durchdringen)
            var cur = new Vector3[n]; for (int i = 0; i < n; i++) cur[i] = pos[i][pos[i].Count - 1];
            var start = (Vector3[])cur.Clone();
            for (int k = 1; k <= arr; k++)
            {
                float e = k / (float)arr; e = e * e * (3 - 2 * e);
                for (int i = 0; i < n; i++) { var tg = targets[slotOf[i]]; cur[i] = Vector3.Lerp(start[i], new Vector3(tg.X, .5f, tg.Z), e); }
                if (k < arr)
                    for (int it = 0; it < 4; it++)
                        for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++)
                        {
                            var d = cur[j] - cur[i]; d.Y = 0; float dist = d.Length(); if (dist >= DieGap || dist < 1e-4f) continue;
                            var push = d / dist * (DieGap - dist) * .5f; cur[i] -= push; cur[j] += push;
                        }
                for (int i = 0; i < n; i++) { pos[i].Add(cur[i]); rot[i].Add(flat[i]); }
            }
            for (int i = 0; i < n; i++)
            {
                int slot = slotOf[i]; var relabel = FromTo(FaceNormal(values[slot]), FaceNormal(upv[i]));
                var o = tr[i]; o.Pos = pos[i].ToArray(); o.Rot = new Quaternion[rot[i].Count];
                for (int k = 0; k < o.Rot.Length; k++) o.Rot[k] = Quaternion.Normalize(rot[i][k] * relabel);
                result[slot] = o;
            }
            return result;
        }

        /// <summary>Kontakte der Wuerfelecken mit einer Ebene {x : x·nrm = d0} (nrm zeigt in den freien Raum): Restitution, Coulomb-Reibung, Ruhekontakt ohne Rueckprall.</summary>
        static bool PlaneContact(ref Vector3 p, ref Vector3 v, ref Vector3 w, Quaternion q, Vector3 nrm, float d0, float t, DiceTrack tr, ref float lastHit)
        {
            float dmin = 0; for (int k = 0; k < 8; k++) dmin = Math.Min(dmin, Vector3.Dot(p + Vector3.Transform(corners[k], q), nrm) - d0);
            p -= nrm * dmin;
            var rs = new List<Vector3>(4);
            for (int k = 0; k < 8; k++) { var r0 = Vector3.Transform(corners[k], q); if (Vector3.Dot(p + r0, nrm) - d0 < .012f) rs.Add(r0); }
            bool contact = rs.Count > 0;
            for (int it = 0; it < 6 && contact; it++)
                foreach (var r in rs)
                {
                    var vp = v + Vector3.Cross(w, r); float vn = Vector3.Dot(vp, nrm);
                    if (vn >= 0) continue;
                    var rn = Vector3.Cross(r, nrm);
                    float e = -vn < 1.5f || it > 0 ? 0 : Restitution;
                    float jn = -(1 + e) * vn / (1 + Vector3.Dot(rn, rn) / Inertia);
                    v += nrm * jn; w += Vector3.Cross(r, nrm * jn) / Inertia;
                    if (it == 0 && -vn > 1.6f && t - lastHit > .06f) { tr.Hits.Add((t, Math.Min(1, -vn / 9f))); lastHit = t; }
                    var vp2 = v + Vector3.Cross(w, r); var vt = vp2 - nrm * Vector3.Dot(vp2, nrm); float vtl = vt.Length();
                    if (vtl > 1e-4f)
                    {
                        var tdir = vt / vtl; var rt = Vector3.Cross(r, tdir);
                        float jt = Math.Min(vtl / (1 + Vector3.Dot(rt, rt) / Inertia), Friction * jn);
                        v -= tdir * jt; w -= Vector3.Cross(r, tdir * jt) / Inertia;
                    }
                }
            return contact;
        }

        /// <summary>Stoss gegen einen liegenden (gehaltenen) Wuerfel: unbeweglich, Restitution und Reibung.</summary>
        static void DieFixed(ref Vector3 p, ref Vector3 v, ref Vector3 w, Vector3 ob, float t, DiceTrack tr, ref float lastHit)
        {
            var d = p - ob; float dist = d.Length(); if (dist >= DieGap || dist < 1e-4f) return;
            var nrm = d / dist; p += nrm * (DieGap - dist); var r = -nrm * (DieGap / 2);
            var vp = v + Vector3.Cross(w, r); float vn = Vector3.Dot(vp, nrm); if (vn >= 0) return;
            float jn = -(1 + (vn < -1 ? DieRest : 0)) * vn; v += nrm * jn;
            if (-vn > 1.4f && t - lastHit > .06f) { tr.Hits.Add((t, Math.Min(1, -vn / 9f) * .8f)); lastHit = t; }
            var vt = vp - nrm * vn; float vtl = vt.Length();
            if (vtl > 1e-4f) { var td = vt / vtl; var c = Vector3.Cross(r, td); float jt = Math.Min(vtl / (1 + Vector3.Dot(c, c) / Inertia), DieFric * jn); v -= td * jt; w -= Vector3.Cross(r, td * jt) / Inertia; }
        }

        /// <summary>Stoss zweier Wuerfel (gleiche Masse): Normalimpuls mit Restitution, Reibungsimpuls am Beruehrpunkt -> Drall.</summary>
        static void DieDie(int i, int j, Vector3[] p, Vector3[] v, Vector3[] w, float t, DiceTrack[] tr, float[] lastHit, bool[] done)
        {
            var d = p[j] - p[i]; float dist = d.Length(); if (dist >= DieGap || dist < 1e-4f) return;
            var nrm = d / dist; float pen = DieGap - dist;
            // ruhende Wuerfel werden angestossen wieder beweglich
            if (done[i] || done[j]) { done[i] = done[j] = false; }
            p[i] -= nrm * pen * .5f; p[j] += nrm * pen * .5f;
            var ri = nrm * (DieGap / 2); var rj = -ri;
            var vrel = v[j] + Vector3.Cross(w[j], rj) - v[i] - Vector3.Cross(w[i], ri); float vn = Vector3.Dot(vrel, nrm);
            if (vn >= 0) return;
            float jn = -(1 + (vn < -1 ? DieRest : 0)) * vn / 2;
            v[i] -= nrm * jn; v[j] += nrm * jn;
            if (-vn > 1.4f && t - lastHit[i] > .06f) { float s = Math.Min(1, -vn / 9f) * .8f; tr[i].Hits.Add((t, s)); lastHit[i] = t; }
            var vt = vrel - nrm * vn; float vtl = vt.Length();
            if (vtl > 1e-4f)
            {
                var tdir = vt / vtl; var ci = Vector3.Cross(ri, tdir); var cj = Vector3.Cross(rj, tdir);
                float jt = Math.Min(vtl / (2 + (Vector3.Dot(ci, ci) + Vector3.Dot(cj, cj)) / Inertia), DieFric * jn);
                v[i] += tdir * jt; v[j] -= tdir * jt; w[i] += Vector3.Cross(ri, tdir * jt) / Inertia; w[j] -= Vector3.Cross(rj, tdir * jt) / Inertia;
            }
        }
    }
}
