# CLAUDE.md - glamour_games_unity (Windows)

## Pfade
- Lokaler Klon auf dem PC: `E:\source\repos\glamour_games_unity`
- Schwester-Repo Android: `domezos2024/glamour_games_unity_android` (PC: `E:\AndroidStudioProjects\glamour_games_unity_android`)

## Gleichstand mit Android
- `Assets/Glamour/Scripts`, `Assets/Plugins`, `Tools/Preview`, `docs/ENGINE.md` sind in beiden Repos identisch (plattformneutral ueber `Platform.Touch` und `#if`). Aenderungen immer in beiden Repos nachziehen.
- Eigenstaendig: `Assets/Glamour/Editor/ProjectBootstrap.cs`, `Assets/Glamour/Settings`, `ProjectSettings`, Installer.

## Bauen und Testen
- `Unity.exe -batchmode -quit -projectPath <Repo> -executeMethod GlamourGames.EditorTools.ProjectBootstrap.CiBuild -buildPath Builds\Windows\GlamourGames.exe -logFile build.log`
- Ohne Unity: `bash Tools/Preview/preview.sh --scene=<0..9|menu> ...`; Mehrspieler-Test zweier Prozesse ueber TCP: `--net=host:47123` / `--net=join:47123`.
- Bluetooth-Test mit dem Handy: Pixel 10 ist mit dem PC gekoppelt (B0:D5:FB:A4:92:6E).
- Muenzwurf im Bluetooth-Spiel: der Beitretende (Gast) waehlt per Knopf und wirft (`toss|wahl|ergebnis`), der Eroeffner sieht Wahl und denselben Wurf. Namen: `Link.MyName` = Optionsname, sonst Bluetooth-Name des Geraets (sonst hiessen beide "Spieler 1").
- Beitrittsliste zeigt nur PCs/Handys mit Adress-Kurzform; gruen = Geraet bietet den Glamour-Dienst an (Android: `getUuids`, Auffrischen per `fetchUuidsWithSdp`). Veraltete gleichnamige Kopplungen (z. B. `MB_CUBE` ...7D:02) fuehren sonst zu PAGE_TIMEOUT.
- Vorschau: `--scene=lobby` zeigt die Bluetooth-Seite mit Testgeraeten; mit `--net=` wartet die Vorschau vor dem Szenenstart auf die Verbindung.
