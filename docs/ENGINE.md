# Glamour Engine für Unity – Entwickler-Referenz

Die Unity-Version von Glamour Games nutzt **keine** Unity-Szenen-Objekte pro Spiel. Stattdessen läuft eine eigene,
schlanke Sofortmodus-Engine (`Assets/Glamour/Scripts/Core`) auf Unity 6.3 LTS + URP:

* **Ein Mesh, ein Draw-Call pro Frame.** Jede Zeichenoperation (`Canvas2D.DrawCircle`, `Gfx.Text`, …) hängt ein Quad
  oder Polygon an. Der Shader `Glamour/Shape` rendert alle Formen über **Signed-Distance-Funktionen**: gestochen scharf
  in jeder Auflösung (bis 4K), kantengeglättet, weichgezeichnet (Glow) und mit **HDR-Leuchtkraft**.
* **URP-Postprocessing** (64-Bit-HDR, Bloom mit Linsenschmutz, Tonemapping, Farbkorrektur, Vignette, bei Siegen
  Screen-Space-Lens-Flare) macht aus HDR-Werten > 1 echtes Neon-Leuchten.
* **3D-Objekte** (Würfel, Münze, Chips, Schiffe: `URP/DiceRig.cs`; Pokal: `URP/RewardRig.cs`) rendert je eine eigene Kamera
  in eine Render-Textur, die der Canvas einblendet (Shape-Typen `DICE`, `TRAY`, `HERO`).
* **Hintergrund-Shader** `Glamour/Backdrop`: Nebel mit Domain-Warping, drei Sternschichten, Polarlicht, Vignette.
* **SDF-Schriftatlas** (Selawik, Selawik Bold, PT Serif Bold Italic, Noto Sans Symbols 2) – Text kann leuchten,
  Umrisse und Farbverläufe haben.
* **Designraum 1600 × 900**, y nach unten, wie im Original. Andere Seitenverhältnisse erweitern den Rand
  (`App.VX0..VX1`, `App.VY0..VY1`).

## Spiel-Szene schreiben

```csharp
namespace GlamourGames
{
    public class MeinSpiel : Scene
    {
        public override string Title => "Mein Spiel";
        public override Col Acc1 => C.Cyan; public override Col Acc2 => C.Pink;   // Akzentfarben (auch Hintergrund)
        public override string OppKey => "mein";                                      // aktiviert die Gegnerwahl

        public override void Enter()
        {
            base.Enter();                                  // fügt den "< Menü"-Button hinzu
            Ui.Add(new Button(40, 780, 260, 62, "Punkte zurück", C.Purple, NewMatch, 22));
            Opponents.AddSwitch(this, OppKey, 40, 690, 260, 70, NewMatch);   // "Gegner: ..." im Spiel ändern
            Opponents.Pick(this, OppKey, o => NewMatch());                   // Dialog beim Start
        }
        public override void Update(float dt) { /* Logik, dt in Sekunden */ }
        public override void Draw(Canvas2D c) { /* zeichnen */ }
        public override void MouseUp(float x, float y) { }
        public override void KeyDown(Key k) { }
    }
}
```

Registrierung: `Games/Menu.cs` → `Registry.All`.

### Wichtige Regeln

* **Kein `using UnityEngine;` in Spieldateien.** Alles Nötige liefert die Engine. Zufall: `Rng.Shared`
  (`System.Random`), `Rng.F()`, `Rng.I(n)`, `Rng.Shuffle(list)`.
* C# 9 (Unity): keine file-scoped namespaces, kein `Random.Shared`, kein `Enum.GetValues<T>()`,
  keine `record`-Typen mit `init` außerhalb der Engine nötig, `MathF` ist verfügbar.
* Spieler 2 heißt immer `PName(1)` (liefert „Computer“, wenn der Computer spielt). Spieler 1: `PName(0)`.
* Computerzüge **immer** über `CpuThink(Opponents.ThinkTime(Opp), () => ...)` auslösen: zeigt „Computer denkt nach“
  und wirkt natürlich. Während `CpuThinking` oder wenn der Computer am Zug ist, Mauseingaben ignorieren.
* Schwere KI-Berechnungen (z. B. Minimax) dürfen den Frame nicht blockieren (< ~30 ms) oder laufen per
  `System.Threading.Tasks.Task.Run` und werden in `Update` abgeholt – **niemals** Unity-API im Hintergrundthread.

## API-Überblick

| Bereich | API |
| --- | --- |
| Farben | `Col` (sRGB, `new Col(r,g,b[,a])`), Palette `C.Cyan`, `C.Pink`, `C.Gold`, … ; `col.A(0.5f)`, `col.Mix(b,t)`, `col.Dark(f)`, `col.Light(f)`, `Col.FromHsv(h,s,v)`, `Col.White/Black/Clear` |
| Rechtecke | `Box` (`Left/Top/Right/Bottom/MidX/MidY/Width/Height/Contains`), `Gfx.R(x,y,w,h)`, `Gfx.Ctr(cx,cy,w,h)`, `Gfx.Inflate(r,d)` |
| Paint | `Gfx.Fill(col)`, `Gfx.Line(col,w)` → `Paint` mit `.Blur` (Sigma), `.Additive`, `.Glow` (HDR-Faktor, >1 = Bloom), `.Shader = Grad.Linear(x0,y0,x1,y1,a,b)` / `Grad.Radial(cx,cy,r,a,b)` |
| Canvas | `Save/Restore/SaveLayer(alpha)/Translate/Scale/RotateDegrees/RotateRadians/Concat(Aff)`, `ClipRect/ClipRoundRect/ClipOval` (achsparallel), `DrawRect/DrawRoundRect/DrawCircle/DrawOval/DrawLine/DrawArc/DrawPath/DrawImage/DrawRadial/DrawBall/DrawPerforated` |
| Pfade | `Path2D` (`MoveTo/LineTo/QuadTo/CubicTo/Close/AddCircle/AddOval/AddRect/AddRoundRect/AddArc`), `EvenOdd = true` für Löcher |
| Gfx | `Rect, RectGrad, RectGradDir, RectRadial, Stroke, Glow, GlowFill, Radial, Light (additiv), Ball, Shadow, Text, TextPaint, TextOutline, TextShadow, BannerText, TW, Image, Heart, Suit, Gear, Star` |
| Geometrie | `Geo.LineIn(c, box, x0,y0,x1,y1, paint)` – Linie auf Rechteck zugeschnitten |
| Karten/Würfel | `CardArt.Card/Face/Back/Ranks/SuitCol`, `Die3D.Draw/Face/RX/RY/RZ/RAxis/Mul`, `Coin3D.Draw` |
| Bilder | `Assets.Img("slot_BOOK")` → `Img` (Atlas), `Gfx.Image(c, img, box, alpha, radius)` |
| Effekte | `Fx.Burst/Confetti/Spark/Ring/Shockwave/Smoke/Flames/Explosion/Splash/Ripple/Bubbles/Petals/Throw/Cannon/Rocket/Lightning/CoinShower/CoinFountain/Glints/Streamers` |
| Metall & Licht | `Metal.Text` (Gold-/Chromschrift mit Glanzstreif), `Metal.Ribbon` (Banner), `Metal.Rays`, `Metal.Flare` (Linsenreflex), `Metal.Glint` (Sternfilter), `Metal.Bar` |
| Sieg | `Trophy3D.Draw(c, Trophy3D.Rect(cx, cy, h), spin, alpha)` (3D in URP, sonst 2D), `RewardShow` (von `Celebrate` erzeugt, `Scene.Reward`), `Lens.Kick(power)` (Bloom-/Flare-Impuls) |
| Szene | `Celebrate(col,dur,power,banner)` (Pokal ab Banner + 3 s oder power ≥ 1.4; nicht wenn der Computer gewinnt), `Result(...)` (Titel mit „gewinnt“/„Bestwert“/„Sieg“ → Sieger-Dialog mit Pokal), `NameEntry(...)`, `HighscoreList(...)`, `Pop(text,x,y,col)`, `Tm.After(sec, action)`, `Co.Start(...)`, `Modal` |
| App | `App.Go(scene)`, `App.Flash(col,a)`, `App.Shake(a)`, `App.Toast(text)`, `App.MX/MY`, `App.VX0..VY1` |
| Audio | `Sfx.Play(S.Win, vol, pitch)` |
| Speicher | `Save.Int/Str/Set/Scores/IsHigh/AddScore`, `Pl.Name(i)` |
| Gegner | `Opponent` (`Human/Easy/Medium/Hard`), `Opponents.Pick/AddSwitch/ThinkTime/Accuracy/Label`, `Scene.Opp/VsCpu/PName/CpuThink/CpuThinking/ThinkPos` |
| Feiern | `DiceParade` (`PKind.Dice/Disc/Sailor`), `CoinToss.Start(scene, first => ...)` |

### Unterschiede zum Skia-Original (Portierungshilfe)

| Original (SkiaSharp) | Unity-Engine |
| --- | --- |
| `SKCanvas c` | `Canvas2D c` |
| `SKColor`, `SKColors.White` | `Col`, `Col.White` |
| `SKRect`, `SKPoint`, `SKPath`, `SKRoundRect` | `Box`, `Pt`, `Path2D`, (Box + Radius) |
| `p.MaskFilter = Gfx.Blur(8)` | `p.Blur = 8` |
| `p.BlendMode = SKBlendMode.Plus` | `p.Additive = true` |
| `SKShader.CreateLinearGradient(...)` (2 Farben) | `p.Shader = Grad.Linear(x0,y0,x1,y1,a,b)` |
| `SKShader.CreateRadialGradient(...)` | `p.Shader = Grad.Radial(cx,cy,r,a,b)` oder `Gfx.RectRadial(...)` |
| `c.ClipRoundRect(new SKRoundRect(r, rad), ...)` | `c.ClipRoundRect(r, rad)` |
| `c.ClipPath(...)` | `c.ClipOval(box)` oder `c.ClipRoundRect(...)` |
| `c.SaveLayer(paintMitAlpha)` | `c.SaveLayer(alpha)` |
| `path.FillType = EvenOdd` | `path.EvenOdd = true` oder `c.DrawPerforated(...)` |
| `Random.Shared` | `Rng.Shared` |
| `s[..^1]`, `l[^1]` | geht (C# 8), alternativ `Substring`/`l[l.Count-1]` |
| `Silk.NET.Input.Key` | `Key` (gleiche Namen: `Key.Number1`, `Key.Space`, `Key.Left` …) |

## Assets

Bilder und Schriften werden offline zu Atlanten gepackt (`Tools/build_assets.py`, Quellen in `SourceArt/`).
Neue Grafik: Datei nach `SourceArt/` legen, Skript ausführen, über `Assets.Img("dateiname")` verwenden.
Neue Sonderzeichen: in `EXTRA` im Skript ergänzen.

## Kompilier-Check ohne Unity

```bash
Tools/compile_check.sh <name> Datei1.cs Datei2.cs   # nur diese Spiele, Rest als Platzhalter
Tools/compile_check.sh all                         # alles
```
