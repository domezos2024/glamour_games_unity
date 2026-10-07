using System;

namespace GlamourGames
{
    /// <summary>
    /// Chipstapel mit Fallphysik: kommen Chips hinzu, fallen sie als Paket aus der Hand auf den Stapel (freier Fall,
    /// Aufprall mit kleiner Restitution wie Ton-Chips auf Ton), jeder Aufprall klackt. Zeitbasiert, daher bildratenunabhaengig.
    /// </summary>
    public class ChipStack
    {
        // Chip 39 mm Durchmesser bei r = 22..24 px -> rund 1200 px/m
        const float H0 = 110, Rest = .28f; static readonly float Gc = Phys.Gpx(1200);
        int shown, from, hops; float t0 = -99;
        public void Draw(Canvas2D c, float now, float x, float y, float r, int n, Col a, Col b)
        {
            if (n < shown || n <= 0) { shown = Math.Max(0, n); from = shown; }
            if (n > shown) { from = shown; shown = n; t0 = now; hops = 0; }
            float h = Phys.Drop(now - t0, H0, Gc, Rest, out int hp);
            if (hp > hops) { Impact.Play(Mat.Clay, MathF.Pow(Rest, hops)); hops = hp; }
            if (now - t0 > 1.5f || from >= n) { Chip3D.Stack(c, x, y, r, n, a, b); return; }
            if (from > 0) Chip3D.Stack(c, x, y, r, from, a, b);
            bool odd = from % 2 == 1; Chip3D.Stack(c, x, y - from * r * .23f - h, r, n - from, odd ? b : a, odd ? a : b);
        }
    }
}
