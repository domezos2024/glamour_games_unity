<div align="center">

<img src="SourceArt/icon.png" width="120" alt="Glamour Games Icon">

# Glamour Games – Unity Edition

**10 glitzernde Spiele – zu zweit an einem PC oder gegen den Computer.**
Neuauflage der [Glamour Games für Windows](https://github.com/domezos2024/glamour_games_windows) mit Unity 6.3 LTS,
HDR-Neon-Grafik und einem Computer-Gegner in drei Stärken für **jedes** Spiel.

<img src="docs/screenshots/menu.png" width="900" alt="Hauptmenü">

</div>

## Was ist neu gegenüber der Windows-Version?

| | Windows 1.8.1 (SkiaSharp) | Unity Edition 2.0 |
| --- | --- | --- |
| Rendering | Skia, Glow per Weichzeichner | GPU-Signed-Distance-Renderer, **HDR-Bloom**, Tonemapping, Vignette, chromatische Aberration, Filmkorn |
| Auflösung | 1600 × 900 skaliert | gestochen scharf bis 4K (Formen und Schriften als Distanzfelder) |
| Hintergrund | Farbverläufe, Sterne | animierter Nebel (Domain-Warping), 3 Sternschichten, Polarlicht – komplett im Shader |
| Kugeln & Steine | Verlaufsfüllung | beleuchtete 3D-Kugeln mit Glanz- und Randlicht |
| Partikel | max. 360 | bis 2600, additive HDR-Funken, Feuerwerk mit Glitzer |
| Gegner | meist nur 2 Spieler | **jedes Spiel auch gegen den Computer** (Leicht / Mittel / Schwer) |

## Die 10 Spiele und ihr Computer-Gegner

| Taste | Spiel | Computer-Gegner |
| :-: | --- | --- |
| 1 | **Memory** | merkt sich aufgedeckte Karten (Gedächtnis je nach Stärke) |
| 2 | **Tic Tac Toe** | bis zum perfekten Minimax (Schwer verliert nie) |
| 3 | **Vier Gewinnt** | Alpha-Beta-Suche mit Stellungsbewertung, rechnet im Hintergrund |
| 4 | **Schiffe Versenken** | Zufall → Jagd-/Zielmodus → Wahrscheinlichkeitskarte |
| 5 | **Snake** | Solo wie gewohnt oder Duell gegen eine Computer-Schlange |
| 6 | **Kniffel** | Halte-Entscheidungen per Erwartungswert |
| 7 | **Nim** | Nim-Summen-Strategie |
| 8 | **Buch der Pharaonen** | Solo oder Duell über 10 Runden gegen den Computer |
| 9 | **Black Jack** | Basisstrategie für Spieler 2 |
| 0 | **Poker** | Monte-Carlo-Gewinnchance, Pot-Odds, Bluffs; 2 Menschen + Computer oder 1 Mensch gegen 2 Computer |

Die Gegnerwahl erscheint beim Start jedes Spiels und lässt sich im Spiel jederzeit über den Knopf „Gegner“ ändern.

<div align="center">
<img src="docs/screenshots/gegnerwahl.jpg" width="800" alt="Gegnerwahl"><br>
<img src="docs/screenshots/montage-1.jpg" width="800" alt="Spiele 1-6"><br>
<img src="docs/screenshots/montage-2.jpg" width="800" alt="Spiele 7-0">
<br><sub>Screenshots aus der Software-Vorschau (Tools/Preview); im Unity-Player kommt zusätzlich das volle URP-Bloom hinzu.</sub>
</div>

## Projekt öffnen

1. **Unity Hub** → *Add project from disk* → diesen Ordner wählen. Empfohlen: **Unity 6.3 LTS (6000.3.x)**
   mit dem Modul *Windows Build Support*.
2. Beim ersten Öffnen richtet sich das Projekt selbst ein (`Assets/Glamour/Editor/ProjectBootstrap.cs`):
   URP-Pipeline mit HDR und 4× MSAA, Startszene `Assets/Scenes/Main.unity`, Build-Einstellungen,
   Produktname, Icon, Fenster 1600 × 900, Linear-Farbraum. Manuell: Menü **Glamour Games → Projekt einrichten**.
3. **Play** drücken – die App startet sich in jeder Szene selbst.
4. Windows-Build: Menü **Glamour Games → Windows-Build erstellen** (Ausgabe `Builds/Windows/GlamourGames.exe`)
   oder per Kommandozeile:
   `Unity -batchmode -quit -projectPath . -executeMethod GlamourGames.EditorTools.ProjectBootstrap.CiBuild`

> Tipp: Nach dem ersten Öffnen die von Unity erzeugten `.meta`-Dateien und `ProjectSettings/*.asset` committen.

## Bedienung

| Aktion | So geht's |
| --- | --- |
| Spiel starten | Kachel anklicken oder Zifferntaste 1 … 0 |
| Gegner wählen | Dialog beim Spielstart oder Knopf „Gegner“ im Spiel |
| Optionen | Zahnrad oben rechts im Menü |
| Zurück zum Menü / Beenden | `Esc` |
| Vollbild | `F11` oder Optionen |
| Musik / Effekte | Symbole oben rechts, `M` stumm, `N` nächstes Stück |
| FPS-Anzeige | `F3` |

## Technik

* **Unity 6.3 LTS**, Universal Render Pipeline 17.3, C# 9, keine Fremd-Assets.
* `Assets/Glamour/Scripts/Core` – Engine: `Canvas2D` (Sofortmodus-Renderer, ein Draw-Call pro Frame), Shader
  `Glamour/Shape` und `Glamour/Backdrop`, SDF-Schrift- und Bildatlas, Szenen/UI, Partikel, prozedurale Musik und
  Soundeffekte, Karten, 3D-Würfel, Münzwurf, Sieger-Parade, Gegnerwahl.
* `Assets/Glamour/Scripts/Games` – Menü, Optionen und die 10 Spiele inkl. KI.
* `Assets/Glamour/Scripts/URP` – Postprocessing (Bloom & Co.), zur Laufzeit erzeugt.
* `Tools/build_assets.py` – baut Schrift- und Bildatlas aus `SourceArt/`.
* `Tools/Preview` – Software-Rasterizer, der Szenen **ohne Unity** als PNG rendert (Tests, Screenshots):
  `Tools/Preview/preview.sh --scene=menu --t=2 --out=menu.png`
* `Tools/compile_check.sh` – Kompilier-Check gegen die Unity-Referenz-DLLs ohne Editor.

Entwickler-Referenz: [docs/ENGINE.md](docs/ENGINE.md)

## Lizenz

Quellcode unter der [MIT-Lizenz](LICENSE). Schriften und Grafiken: siehe [Third-Party Notices](THIRD_PARTY_NOTICES.md).

<div align="center">
<br>
<b>Glamour Games 2.0 – Unity Edition</b> – erstellt von Michael Bergfeld @ DoMeZos-Ware 2026
</div>
