using System;
using System.Collections.Generic;

namespace GlamourGames
{
    /// <summary>
    /// Gemeinsamer 2D-Physik-Kern im Designraum (Pixel, y nach unten). Jede Szene waehlt einen Massstab (Pixel je Meter)
    /// passend zur echten Objektgroesse; daraus folgt die echte Erdbeschleunigung. Integration: semi-implizites Euler
    /// mit festen Teilschritten (stabil auch bei Rucklern).
    /// </summary>
    public static class Phys
    {
        public const float G = 9.81f, Step = 1f / 240f;
        /// <summary>Erdbeschleunigung in Pixel/s² fuer den Massstab pxPerM.</summary>
        public static float Gpx(float pxPerM) => G * pxPerM;
        /// <summary>Teilschritte fuer dt (je hoechstens 1/240 s, gesamt hoechstens 0,1 s).</summary>
        public static int Sub(float dt, out float h) { dt = Math.Clamp(dt, 0, .1f); int n = Math.Max(1, (int)MathF.Ceiling(dt / Step)); h = dt / n; return n; }
        /// <summary>Anfangsgeschwindigkeit fuer einen Wurf von (x0,y0) nach (x1,y1) in der Zeit T unter Schwerkraft g.</summary>
        public static (float vx, float vy) Launch(float x0, float y0, float x1, float y1, float T, float g) => ((x1 - x0) / T, (y1 - y0) / T - .5f * g * T);
        /// <summary>Startgeschwindigkeit, damit ein Koerper nach Flugzeit tf (reibungsfrei) und anschliessendem Rutschen mit Verzoegerung decel genau die Strecke d zuruecklegt.</summary>
        public static float SlideSpeed(float d, float tf, float decel) => decel * (-tf + MathF.Sqrt(tf * tf + 2 * d / decel));
        /// <summary>Hoehe eines aus h0 losgelassenen Koerpers zur Zeit t mit Aufprall-Restitution e (geschlossen geloest, bildratenunabhaengig); hops = Aufpralle bis t.</summary>
        public static float Drop(float t, float h0, float g, float e, out int hops)
        {
            hops = 0; float tf = MathF.Sqrt(2 * h0 / g); if (t < tf) return h0 - .5f * g * t * t;
            t -= tf; hops = 1; float v = g * tf * e;
            while (v > 40) { float d = 2 * v / g; if (t < d) return v * t - .5f * g * t * t; t -= d; hops++; v *= e; }
            return 0;
        }
        /// <summary>Auslenkung einer gedaempften Schwingung (Frequenz freq Hz, Daempfungsgrad zeta) nach einem Stoss mit Geschwindigkeit v0.</summary>
        public static float Ring(float v0, float t, float freq, float zeta)
        {
            if (t < 0) return 0; float w = 2 * MathF.PI * freq, wd = w * MathF.Sqrt(Math.Max(1e-4f, 1 - zeta * zeta));
            return v0 / wd * MathF.Exp(-zeta * w * t) * MathF.Sin(wd * t);
        }
    }

    /// <summary>Werkstoffe fuer Aufprallgeraeusche.</summary>
    public enum Mat { Plastic, Wood, Metal, Clay, Paper, Dice }

    /// <summary>
    /// Aufprallgeraeusch nach Werkstoff und Staerke: Lautstaerke waechst mit der Aufprallgeschwindigkeit (gesaettigt),
    /// harte Stoesse klingen etwas heller; schwere/grosse Koerper tiefer. Zu leise Stoesse werden verschluckt.
    /// </summary>
    public static class Impact
    {
        static float lastT; static int burst;
        /// <summary>strength = Aufprallgeschwindigkeit relativ zu einem "kraeftigen" Stoss (1 = kraeftig), size = Groessenfaktor (1 normal, &gt;1 tiefer).</summary>
        public static void Play(Mat m, float strength, float size = 1)
        {
            if (strength < .06f) return;
            // Begrenzung: hoechstens ~12 Klacks in kurzer Folge (viele Koerper gleichzeitig)
            float now = UnityEngine.Time.realtimeSinceStartup; if (now - lastT < .015f) { if (++burst > 3) return; } else burst = 0; lastT = now;
            float s = Math.Clamp(strength, 0, 1.4f), vol = Math.Min(1, .15f + .85f * MathF.Sqrt(s)), pitch = (1 + .18f * (s - .5f)) / MathF.Sqrt(Math.Max(.3f, size));
            switch (m)
            {
                case Mat.Plastic: Sfx.Play(S.Drop, vol * .55f, pitch * 1.25f); break;
                case Mat.Wood: Sfx.Play(S.Drop, vol * .5f, pitch * .85f); Sfx.Play(S.Tick, vol * .25f, pitch * .7f); break;
                case Mat.Metal: Sfx.Play(S.Coin, vol * .5f, pitch * 1.1f); if (size > 1.5f) Sfx.Play(S.Gong, vol * .45f, pitch * 1.2f); break;
                case Mat.Clay: Sfx.Play(S.Chip, vol * .7f, pitch); break;
                case Mat.Paper: Sfx.Play(S.Deal, vol * .35f, pitch * 1.1f); break;
                case Mat.Dice: Sfx.Play(S.Dice, vol * .45f, pitch * 1.15f); break;
            }
        }
    }

    /// <summary>
    /// Starrkoerper (Masse 1) als Kreisscheibe (R &gt; 0) oder Rechteck (Hw/Hh). Stoesse an Boden und Waenden mit
    /// Restitution und Coulomb-Reibung am Kontaktpunkt (koppelt Translation und Drehung: Aufkanten, Abrollen, Ausrutschen),
    /// quadratischer Luftwiderstand.
    /// </summary>
    public class Body
    {
        public float X, Y, Vx, Vy, A, W, Hw, Hh, R, E = .3f, Mu = .4f, Drag, Gravity;
        /// <summary>Boden (y) und Waende (x); NaN = nicht vorhanden.</summary>
        public float Floor = float.NaN, WallL = float.NaN, WallR = float.NaN;
        /// <summary>Staerkster Aufprall (Normalgeschwindigkeit px/s) im letzten Update - fuer Klack-Geraeusche.</summary>
        public float Impact;
        /// <summary>Wie lange der Koerper schon praktisch ruht (s).</summary>
        public float Still;
        public object Tag;
        /// <summary>Schraege Flaechen (Rampen) als Strecken; Kontakt nur innerhalb ihrer x-Spanne.</summary>
        readonly List<(float nx, float ny, float d, float x0, float x1)> ramps = new List<(float, float, float, float, float)>();
        /// <summary>Rampe von (x0,y0) nach (x1,y1), begehbar von oben.</summary>
        public Body Ramp(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0, l = MathF.Sqrt(dx * dx + dy * dy), nx = dy / l, ny = -dx / l; if (ny > 0) { nx = -nx; ny = -ny; }
            ramps.Add((nx, ny, nx * x0 + ny * y0, Math.Min(x0, x1), Math.Max(x0, x1))); return this;
        }
        float InvI => R > 0 ? 2 / (R * R) : 3 / Math.Max(1, Hw * Hw + Hh * Hh);
        public bool Resting => Still > .25f;

        public void Update(float dt)
        {
            Impact = 0; int n = Phys.Sub(dt, out float h);
            for (int i = 0; i < n; i++)
            {
                Vy += Gravity * h;
                if (Drag > 0) { float v = MathF.Sqrt(Vx * Vx + Vy * Vy), k = 1 / (1 + Drag * v * h); Vx *= k; Vy *= k; W *= 1 / (1 + Drag * MathF.Abs(W) * h * 40); }
                X += Vx * h; Y += Vy * h; A += W * h;
                if (!float.IsNaN(Floor)) Contacts(0, -1, -Floor);
                if (!float.IsNaN(WallL)) Contacts(1, 0, WallL);
                if (!float.IsNaN(WallR)) Contacts(-1, 0, -WallR);
                foreach (var r in ramps) Contacts(r.nx, r.ny, r.d, r.x0, r.x1, (R > 0 ? R : Math.Max(Hw, Hh)) + 30);
            }
            Still = Vx * Vx + Vy * Vy < 400 && MathF.Abs(W) < .3f ? Still + dt : 0;
        }

        /// <summary>Kontakte mit der Ebene {p : p·n = d} (n zeigt in den freien Raum), optional nur fuer Kontaktpunkte mit x in [x0, x1]; mehrere Ecken werden iterativ aufgeloest.</summary>
        void Contacts(float nx, float ny, float d, float x0 = float.NegativeInfinity, float x1 = float.PositiveInfinity, float lim = float.PositiveInfinity)
        {
            int m = 0; float maxPen = 0;
            if (R > 0) { cr[0] = -nx * R; cr[1] = -ny * R; float cx = X + cr[0]; float pc = d - (X * nx + Y * ny - R); if (cx >= x0 && cx <= x1 && pc < lim) { m = 1; maxPen = pc; } }
            else
            {
                float cs = MathF.Cos(A), sn = MathF.Sin(A);
                for (int k = 0; k < 4; k++)
                {
                    float lx = (k == 0 || k == 3) ? -Hw : Hw, ly = k < 2 ? -Hh : Hh, rx = lx * cs - ly * sn, ry = lx * sn + ly * cs, pen = d - ((X + rx) * nx + (Y + ry) * ny);
                    if (pen > 0 && pen < lim && X + rx >= x0 && X + rx <= x1) { cr[m * 2] = rx; cr[m * 2 + 1] = ry; m++; maxPen = Math.Max(maxPen, pen); }
                }
            }
            if (m == 0 || maxPen <= 0) return;
            for (int it = 0; it < (m > 1 ? 6 : 1); it++) for (int k = 0; k < m; k++) Hit(cr[k * 2], cr[k * 2 + 1], nx, ny, it == 0);
            X += nx * maxPen * .8f; Y += ny * maxPen * .8f;
        }
        readonly float[] cr = new float[8];

        void Hit(float rx, float ry, float nx, float ny, bool bounce)
        {
            float vx = Vx - W * ry, vy = Vy + W * rx, vn = vx * nx + vy * ny;
            if (vn >= 0) return;
            float rn = rx * ny - ry * nx, jn = -(1 + (bounce && vn < -60 ? E : 0)) * vn / (1 + rn * rn * InvI);
            Vx += jn * nx; Vy += jn * ny; W += rn * jn * InvI; if (bounce && vn < -60) Impact = Math.Max(Impact, -vn);
            float tx = -ny, ty = nx; vx = Vx - W * ry; vy = Vy + W * rx; float vt = vx * tx + vy * ty, rt = rx * ty - ry * tx;
            float jt = Math.Clamp(-vt / (1 + rt * rt * InvI), -Mu * jn, Mu * jn);
            Vx += jt * tx; Vy += jt * ty; W += rt * jt * InvI;
        }

        /// <summary>Stoss zweier Kreisscheiben gleicher Masse (Restitution, Reibung am Beruehrpunkt koppelt die Drehung).</summary>
        public static void Collide(Body a, Body b)
        {
            if (a.R <= 0 || b.R <= 0) return;
            float dx = b.X - a.X, dy = b.Y - a.Y, d = MathF.Sqrt(dx * dx + dy * dy), sum = a.R + b.R; if (d >= sum || d < 1e-3f) return;
            float nx = dx / d, ny = dy / d, pen = sum - d; a.X -= nx * pen * .5f; a.Y -= ny * pen * .5f; b.X += nx * pen * .5f; b.Y += ny * pen * .5f;
            float rvx = (b.Vx - b.W * -ny * b.R) - (a.Vx - a.W * ny * a.R), rvy = (b.Vy + b.W * -nx * b.R) - (a.Vy + a.W * nx * a.R), vn = rvx * nx + rvy * ny;
            if (vn >= 0) return;
            float e = vn < -60 ? Math.Min(a.E, b.E) : 0, jn = -(1 + e) * vn / 2;
            a.Vx -= jn * nx; a.Vy -= jn * ny; b.Vx += jn * nx; b.Vy += jn * ny; a.Impact = Math.Max(a.Impact, -vn); b.Impact = Math.Max(b.Impact, -vn);
            float tx = -ny, ty = nx, vt = rvx * tx + rvy * ty, jt = Math.Clamp(-vt / 6, -Math.Min(a.Mu, b.Mu) * jn, Math.Min(a.Mu, b.Mu) * jn);
            a.Vx -= jt * tx; a.Vy -= jt * ty; b.Vx += jt * tx; b.Vy += jt * ty; a.W -= jt * 2 / a.R; b.W -= jt * 2 / b.R;
        }

        /// <summary>Transformation fuer das Zeichnen: Ursprung im Schwerpunkt, gedreht.</summary>
        public void Apply(Canvas2D c) { c.Translate(X, Y); c.RotateRadians(A); }
    }

    /// <summary>
    /// Kippender Starrkoerper auf fester Unterlage (Housner-Modell, z. B. Pokal auf dem Sockel): Drehung um die jeweils
    /// belastete Bodenkante, Schwerkraft als Rueckstellmoment, Energieverlust bei jedem Kantenwechsel. Th &gt; 0 hebt die
    /// linke Seite an (Drehung im Uhrzeigersinn um die rechte Kante).
    /// </summary>
    public class Rocker
    {
        public float Th, Om; readonly float alpha, p2, keep;
        /// <summary>Winkelgeschwindigkeit, ab der der Koerper umkippen wuerde (Energieerhaltung bis zum Schwerpunkt ueber der Kante).</summary>
        public float Critical => MathF.Sqrt(2 * p2 * (1 - MathF.Cos(alpha)));
        public Rocker(float hw, float hh, float g)
        {
            alpha = MathF.Atan2(hw, hh); float r = MathF.Sqrt(hw * hw + hh * hh); p2 = 3 * g / (4 * r);
            float s = MathF.Sin(alpha); keep = Math.Max(.05f, 1 - 1.5f * s * s);
        }
        /// <summary>Winkelgeschwindigkeit (rad/s) durch einen seitlichen Stoss.</summary>
        public void Kick(float om) => Om += om;
        public bool Moving => Th != 0 || Om != 0;
        public void Update(float dt)
        {
            if (!Moving) return; int n = Phys.Sub(dt, out float h);
            for (int i = 0; i < n; i++)
            {
                float s = Th != 0 ? MathF.Sign(Th) : MathF.Sign(Om); float prev = Th;
                Om += -p2 * MathF.Sin(alpha * s - Th) * h; Th += Om * h;
                if (prev != 0 && MathF.Sign(Th) != MathF.Sign(prev)) { Th = 0; Om *= keep; }
                if (MathF.Abs(Th) > MathF.PI / 2) { Th = MathF.Sign(Th) * MathF.PI / 2; Om = 0; }
            }
            if (MathF.Abs(Th) < 1e-3f && MathF.Abs(Om) < .05f) Th = Om = 0;
        }
        /// <summary>Kippt die Zeichenflaeche um die belastete Kante eines Koerpers mit Bodenmitte (cx, bottom) und halber Breite hw.</summary>
        public void Apply(Canvas2D c, float cx, float bottom, float hw)
        {
            if (Th == 0) return; float px = cx + MathF.Sign(Th) * hw;
            c.Translate(px, bottom); c.RotateRadians(Th); c.Translate(-px, -bottom);
        }
    }

    /// <summary>
    /// Sinkendes Schiff: Wassereinbruch erhoeht die Masse, der Auftrieb waechst mit der Eintauchtiefe bis der Rumpf
    /// vollstaendig unter Wasser ist, Wasserwiderstand daempft. Einseitig geflutete Abteile erzeugen Schlagseite gegen
    /// das aufrichtende Moment. Einheiten: Rumpfhoehen und Sekunden (g bereits auf die Rumpfhoehe bezogen).
    /// </summary>
    public class Sinker
    {
        public float Draft, Vz, Roll, Wr, T; readonly float side, g, d0;
        /// <summary>Wieviel der Tauchtiefe ueber den Wasserspiegel hinaus erreicht ist (0 schwimmt .. 1 versunken).</summary>
        public float Depth => Math.Clamp((Draft - d0) / (1.25f - d0), 0, 1);
        public float Flood => 1 - MathF.Exp(-T / .9f);
        public Sinker(int side, float g = 2.4f, float d0 = .35f) { this.side = side; this.g = g; this.d0 = d0; Draft = d0; }
        public void Kick(float wr) => Wr += wr;
        public void Update(float dt)
        {
            int n = Phys.Sub(dt, out float h);
            for (int i = 0; i < n; i++)
            {
                T += h; float m = 1 + 3.2f * Flood, buoy = Math.Min(Draft, 1) / d0;
                Vz += (g * (1 - buoy / m) - 2.2f * Vz - 1.4f * Vz * MathF.Abs(Vz)) * h; Draft += Vz * h;
                Wr += (-9f * MathF.Sin(Roll) * Math.Max(.35f, 1 - Depth) - 2.4f * Wr + side * 1.8f * Flood * (1 - Depth * .4f)) * h; Roll += Wr * h;
            }
        }
    }

    /// <summary>Kette/Seil aus Massepunkten (positionsbasierte Dynamik): feste Gliedlaengen, Schwerkraft, Boden mit Reibung,
    /// optional Bodenreibung in der Ebene (Draufsicht) und ein Begrenzungsrechteck.</summary>
    public class Chain
    {
        public readonly float[] X, Y, Vx, Vy; readonly float[] ox, oy; public float Seg, Gravity, Floor = float.NaN, Mu = .5f, E = .2f, Friction; public bool HasBounds; public Box Bounds;
        public int Count => X.Length;
        public Chain(IList<Pt> pts, float seg)
        {
            int n = pts.Count; X = new float[n]; Y = new float[n]; Vx = new float[n]; Vy = new float[n]; ox = new float[n]; oy = new float[n]; Seg = seg;
            for (int i = 0; i < n; i++) { X[i] = pts[i].X; Y[i] = pts[i].Y; }
        }
        public void Update(float dt)
        {
            int n = Phys.Sub(dt, out float h), m = X.Length;
            for (int s = 0; s < n; s++)
            {
                for (int i = 0; i < m; i++) { ox[i] = X[i]; oy[i] = Y[i]; Vy[i] += Gravity * h; if (Friction > 0) { float k = 1 / (1 + Friction * h); Vx[i] *= k; Vy[i] *= k; } X[i] += Vx[i] * h; Y[i] += Vy[i] * h; }
                for (int it = 0; it < 6; it++)
                    for (int i = 1; i < m; i++)
                    {
                        float dx = X[i] - X[i - 1], dy = Y[i] - Y[i - 1], d = MathF.Sqrt(dx * dx + dy * dy); if (d < 1e-4f) continue;
                        float k = (d - Seg) / d * .5f; X[i - 1] += dx * k; Y[i - 1] += dy * k; X[i] -= dx * k; Y[i] -= dy * k;
                    }
                for (int i = 0; i < m; i++)
                {
                    Vx[i] = (X[i] - ox[i]) / h; Vy[i] = (Y[i] - oy[i]) / h;
                    if (!float.IsNaN(Floor) && Y[i] >= Floor) { Y[i] = Floor; if (Vy[i] > 0) Vy[i] = -Vy[i] * E; Vx[i] *= Math.Max(0, 1 - Mu * 60 * h); }
                    if (HasBounds)
                    {
                        if (X[i] < Bounds.Left) { X[i] = Bounds.Left; Vx[i] = Math.Abs(Vx[i]) * E; } else if (X[i] > Bounds.Right) { X[i] = Bounds.Right; Vx[i] = -Math.Abs(Vx[i]) * E; }
                        if (Y[i] < Bounds.Top) { Y[i] = Bounds.Top; Vy[i] = Math.Abs(Vy[i]) * E; } else if (Y[i] > Bounds.Bottom) { Y[i] = Bounds.Bottom; Vy[i] = -Math.Abs(Vy[i]) * E; }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Walzentrommel (Einheit: Symbole, Position faellt beim Drehen): Motor beschleunigt gleichmaessig auf Hoechstdrehzahl,
    /// die Bremse setzt wegabhaengig ein und verzoegert gleichmaessig, sodass die Trommel mit kleiner Restgeschwindigkeit
    /// genau ins Ziel-Rastfeld laeuft; die Rastfeder faengt sie gedaempft schwingend ab (Ueberschwingen und Zurueckfedern).
    /// </summary>
    public class Drum
    {
        public float Pos, V; public bool Moving, Settling; float target, vmax, acc, dec, vImp, settleT; int phase;
        const float VHit = 3f, Ta = .2f, DetentHz = 5.5f, DetentZeta = .32f;
        /// <summary>Drehung bis target (kleiner als Pos) in etwa duration Sekunden, davon ein Teil Bremsweg.</summary>
        public void Spin(float target, float duration)
        {
            this.target = target; float d = Pos - target, td = .42f * duration, tc = Math.Max(0, duration - Ta - td);
            vmax = Math.Max(VHit * 2, (d - VHit * td / 2) / (Ta / 2 + tc + td / 2)); acc = vmax / Ta; dec = (vmax - VHit) / Math.Max(.05f, td);
            V = 0; phase = 0; Moving = true; Settling = false;
        }
        /// <summary>Schnellstopp: neues Ziel, harte Bremse (setzt ein, sobald der Bremsweg erreicht ist).</summary>
        public void Brake(float target, float hardDec = 650)
        {
            if (!Moving) return; this.target = target; vmax = Math.Max(V, VHit * 2); dec = hardDec; if (phase == 0) phase = 1;
        }
        /// <summary>true im Moment des Einrastens (fuer Stopp-Geraeusch).</summary>
        public bool Update(float dt)
        {
            if (Settling) { settleT += dt; Pos = target - Phys.Ring(vImp, settleT, DetentHz, DetentZeta); if (settleT > 1.2f) { Settling = false; Pos = target; } return false; }
            if (!Moving) return false;
            int n = Phys.Sub(dt, out float h);
            for (int i = 0; i < n; i++)
            {
                float rem = Pos - target;
                if (phase < 2 && rem <= (V * V - VHit * VHit) / (2 * dec)) phase = 2;
                if (phase == 0) { V = Math.Min(vmax, V + acc * h); if (V >= vmax) phase = 1; }
                else if (phase == 2) V = Math.Max(VHit, V - dec * h);
                Pos -= V * h;
                if (Pos <= target) { vImp = Math.Min(V, VHit * 2); Pos = target; Moving = false; Settling = true; settleT = 0; return true; }
            }
            return false;
        }
        /// <summary>Sofort auf Ziel setzen (Abbruch).</summary>
        public void Snap(float p) { Pos = target = p; V = 0; Moving = Settling = false; }
    }

    /// <summary>
    /// Wurf mit anschliessendem Rutschen ueber eine Flaeche (Spielkarte auf Filz): reibungsfreie Flugphase mit echtem
    /// Wurfbogen (Hoehe als Lift), danach gleichmaessige Verzoegerung durch Gleitreibung mu·g; Drehung bremst ebenso.
    /// Die Startgeschwindigkeit ist so gewaehlt, dass der Koerper genau am Ziel liegen bleibt.
    /// </summary>
    public class Glide
    {
        public float X, Y, Rot, Lift, T; public bool Done; readonly float x0, y0, ux, uy, v0, dec, tf, ts, g, rot0, d;
        public Glide(float x0, float y0, float x1, float y1, float rot0, float g, float mu, float flight)
        {
            this.x0 = x0; this.y0 = y0; this.g = g; this.rot0 = rot0; tf = flight; dec = mu * g; X = x0; Y = y0; Rot = rot0;
            float dx = x1 - x0, dy = y1 - y0; d = MathF.Sqrt(dx * dx + dy * dy); ux = d > 0 ? dx / d : 0; uy = d > 0 ? dy / d : 0;
            v0 = d > 0 ? Phys.SlideSpeed(d, tf, dec) : 0; ts = v0 / dec;
        }
        public float Duration => tf + ts;
        public void Update(float dt)
        {
            if (Done) return; T += dt; float t = Math.Min(T, Duration), s;
            if (t < tf) { s = v0 * t; Lift = g * t * (tf - t) * .5f; }
            else { float u = t - tf; s = v0 * tf + v0 * u - .5f * dec * u * u; Lift = 0; }
            s = Math.Min(s, d); X = x0 + ux * s; Y = y0 + uy * s; Rot = rot0 * (1 - s / Math.Max(1, d)) * (1 - s / Math.Max(1, d));
            if (T >= Duration) { Done = true; Lift = 0; Rot = 0; }
        }
    }
}
