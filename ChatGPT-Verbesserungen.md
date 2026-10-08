# Glamour Games Unity Edition für Windows – Analyse und Maßnahmenplan

Stand: 8. Oktober 2026  
Umfang: Analyse der Windows-Unity-Edition in diesem Repository; **keine Änderung am Spielcode**. Alle Befunde in diesem Dokument sind anhand der genannten Dateien im Projekt überprüfbar.

## Kurzfazit

Die Windows-Edition ist eine Unity-6-/URP-Anwendung mit 10 Spielen, lokaler Mehrspieleroption, KI und bestehendem Bluetooth-Mehrspieler zwischen PC und PC bzw. PC und Android. Die aktuelle Bluetooth-Architektur ist jedoch als **eine Verbindung zwischen zwei Geräten** implementiert. Für bis zu vier Spieler müssen der Windows-RFCOMM-Transport, die globale `Link`-Sitzung, die Lobby, das Protokoll, das Zustandsmodell und die Spielklassen gemeinsam erweitert werden.

Der engste technische Engpass liegt im Windows-Transport: `WinBt.Host()` ruft `listen(s, 1)` auf, akzeptiert genau eine Verbindung und schließt anschließend den Listener (`Assets/Glamour/Scripts/Core/Net/WinBt.cs`, Zeilen 82–96). Die gemeinsame `Link`-Klasse kennt zudem nur einen `PeerName` und beschreibt selbst ausdrücklich eine Verbindung „zweier Geräte“ (`Assets/Glamour/Scripts/Core/Net/Link.cs`, Zeilen 39–50).

## Tatsächliche Repository-Befunde

### Bestehende Stärken

- [x] Die gemeinsame Codebasis bewusst erhalten: README dokumentiert denselben Code wie die Android-Ausgabe, daher müssen Netzprotokoll und Spielregeln plattformübergreifend kompatibel bleiben (`README.md`, Abschnitt „Neu in 2.3“).
- [x] Die Windows-spezifische Bluetooth-Integration weiterverwenden: `WinBt.cs` nutzt Winsock `AF_BTH`/`BTHPROTO_RFCOMM`, registriert einen SDP-Dienst und listet gekoppelte Bluetooth-Geräte über Windows-APIs.
- [x] Die visuelle Basis schützen: Unity 6.3 LTS, URP 17.3, HDR-Neon-Grafik, 4× MSAA und bis zu 2600 Partikel sind dokumentiert (`README.md`, Abschnitte „Projekt öffnen“ und „Was ist neu“).
- [x] Vorhandene Testhilfen ausbauen: `Tools/Preview` rendert Szenen ohne Unity als PNG; `Tools/compile_check.sh` ist als Kompiliercheck vorhanden (`README.md`, Abschnitt „Technik“).

### Architekturgrenzen für vier Spieler

- [x] Windows-Host von Single-Socket auf mehrere Peer-Sockets umbauen. Aktuell schließen `Unregister(); closesocket(s); listenSock = Invalid;` den Host-Listener direkt nach dem ersten `accept()` (`WinBt.cs`, Zeilen 91–95).
- [x] `IBtTransport`/`Link` um eine Peer-Collection, eindeutige Spieler-IDs und gezieltes Senden/Broadcast erweitern. Aktuell gibt es nur `Connected`, `IsHost`, `PeerName` und eine gemeinsame Warteschlange (`Link.cs`, Zeilen 46–62, 73–122).
- [x] Das aktuelle, zeilenbasierte Pipe-Protokoll mit `HELLO`, `GO`, `MENU`, `BYE` und `G|spiel|art|…` ablösen bzw. kapseln. Es besitzt derzeit keine Paket-ID, Sitz-ID, Protokollversion im Envelope, Quittierung, Zustandsrevision oder Snapshot (`Link.cs`, Zeilen 76–119).
- [x] Die Lobby von einer 1:1-Verbindung zu einem Raum für Host plus ein bis drei Gäste umbauen. Die UI verwendet momentan konsequent „Mitspieler“ im Singular und bietet nur Eröffnen/Beitreten (`Assets/Glamour/Scripts/Core/Net/BtLobby.cs`, Zeilen 13–68).
- [x] Den Szenenbasiszustand entkoppeln: `Scene.Remote`, `PName(int)` und `NetLost()` modellieren nur einen entfernten Gegner und wechseln bei Verbindungsverlust ins Menü (`Assets/Glamour/Scripts/Core/Scene.cs`, Zeilen 37–72).

### Konkrete Spielbefunde

| Spiel | Befund | Nötige Erweiterung |
| --- | --- | --- |
| Kniffel | Namen, Farben, Spalten und Summen werden mehrfach über `p < 2` geführt (`Games/Kniffel.cs`, Zeilen 257–280). | Dynamische Spieler-/Scoreboard-Liste, 2–4 Sitze, zyklische Turns und hostautoritatives Eintragen. |
| Blackjack | Zwei Hände, zwei Einsatz-Button-Paare und zwei feste Positionen: `Hand[] pl`, `bm/bp[2]`, `PX` (`Games/Blackjack.cs`, Zeilen 18–33, 58, 199). | 2–4 Spieler plus Dealer, dynamische Hand- und UI-Container, hostautoritatives Deck/Dealer. |
| Poker | Bluetooth ist fest als Host, Gast und Computer dokumentiert; Spiellogik und Rendering nutzen viele Schleifen `< 3` (`Games/Poker.cs`, Zeilen 372–376, 439, 534, 608, 649). | 2–4 menschliche Sitze, dynamische Blinds/Button/Side-Pots, private Hole-Card-Nachrichten. |
| Memory | `score` ist ein `int[2]`; Kommentar nennt Bluetooth-Gast „Platz 2“ (`Games/Memory.cs`, Zeilen 22, 30, 55). | Dynamische Punkte-/Spielerliste, aktiver Sitz, hostvalidierte Kartenwahl. |
| Snake | Host simuliert, Gast meldet Richtungswechsel; nur die zweite Schlange/deren Queue wird behandelt (`Games/Snake.cs`, Zeilen 17–42, 86). | Bis zu vier Schlangen und Eingabepuffer, Host-Ticks, Voll-Snapshots bei Join/Reconnect. |

### Grafik, UI/UX, Codequalität und Tests

- [x] Große Spielklassen schrittweise in Regelwerk/Zustand, Netzwerkadapter, Eingabe und Rendering trennen: `Slot.cs` (847 Zeilen), `Poker.cs` (723), `Kniffel.cs` (507), `Battleship.cs` (500) und `Snake.cs` (366) bündeln heute mehrere Verantwortlichkeiten.
- [x] 2/3/4-Spieler-Layouts aus festen 1600×900-Koordinaten ableiten, ohne die Desktop-4K-Schärfe zu verlieren. Die Szenenbasis nennt selbst diesen Designraum (`Scene.cs`, Zeile 7); zusätzliche Namens- und Statusanzeigen brauchen definierte responsive Bereiche.
- [ ] Performance auf Windows messen, bevor Effekte erweitert werden: Das Projekt kombiniert HDR, Bloom, GPU-SDF-Rendering, 3D-Rigs und große Partikelmengen (`README.md`).
- [x] Testlücke schließen: Es wurden keine Dateien mit Test/Test-Namensmuster im Repository gefunden. Der vorhandene Preview-/TCP-Ansatz ist der geeignete Startpunkt für deterministische Protokoll- und Mehr-Peer-Tests.

## Zielbild: Windows-Host für bis zu vier Bluetooth-Spieler

- [x] Einen PC oder ein anderes Gerät als **Host** definieren; der Host hält bis zu drei gleichzeitige Gastverbindungen und bleibt während der Partie autoritativ.
- [x] Eine Sitzung als `SessionId`, `PlayerId`, `SeatId`, Roster und klaren Zustand modellieren: `Discovering`, `Lobby`, `Starting`, `InGame`, `Reconnecting`, `Finished`.
- [x] Pro Peer Reader/Writer, Verbindungsstatus, ausgehende Warteschlange und kontrolliertes Ressourcen-Cleanup einführen. Thread-Ereignisse nur auf dem Unity-Hauptthread in die Spiellogik überführen.
- [x] Versioniertes Envelope verwenden, z. B. `{protocolVersion, sessionId, messageId, senderId, seat, type, sequence, ack, stateRevision, payload}`.
- [x] Der Host validiert ausschließlich Eingabeabsichten der Gäste und verteilt autoritative Deltas bzw. Snapshots. Gäste berechnen niemals konkurrierende Spielwahrheiten.
- [x] Bei Poker private Karten nur an den zugehörigen Sitz senden; öffentliche Ereignisse, Board und Einsätze separat broadcasten.

## Nummerierter Umsetzungsplan

### 1. Technische Vorarbeit und Kompatibilität

- [x] 1.1 Vor jeder Änderung den Git-Status prüfen und nur Dateien ändern, die zur Vier-Spieler-Erweiterung gehören.
- [x] 1.2 Einen plattformneutralen Netzwerkvertrag in der gemeinsamen Codebasis festlegen, damit Windows und Android dieselbe Protokollversion sprechen.
- [x] 1.3 Eine Testmatrix Windows↔Windows, Windows↔Android und Android↔Android für 2/3/4 Teilnehmer erstellen.
- [ ] 1.4 Vor dem Funktionsversprechen drei gleichzeitige Gastverbindungen mit mehreren Bluetooth-Adaptern/Windows-Versionen verifizieren; RFCOMM-Kapazitäten können geräte- und treiberabhängig sein.

### 2. Windows-RFCOMM-Mehr-Peer-Transport

- [x] 2.1 `WinBt` so umbauen, dass der Listener bis zur Raumkapazität offen bleibt und jeden akzeptierten Socket als eigenen Peer verwaltet.
- [x] 2.2 Pro Peer paralleles Lesen, geordnetes Schreiben, Fehlerbehandlung, Heartbeat und sauberes `closesocket` beim Entfernen implementieren.
- [x] 2.3 Die SDP-Registrierung erst entfernen, wenn der Raum voll ist oder der Host die Lobby beendet – nicht nach dem ersten Join.
- [x] 2.4 Die Windows-Geräteliste und die Lobby um Teilnehmerliste, Kapazität, Ready-Status und eindeutige Fehlermeldungen ergänzen.
- [ ] 2.5 Windows-Ressourcen und Hintergrundthreads mit Last- und Abbruchtests prüfen, insbesondere bei wiederholtem Host/Join/Stop.

### 3. Sitzungs- und Protokollschicht

- [x] 3.1 `Link` in Transport, Protokollcodec, `SessionManager`, Roster und Szenenadapter zerlegen.
- [x] 3.2 Stabile Spiel-ID statt Registry-Index übertragen; `GO` verwendet derzeit den Registry-Index als Vertrag (`Link.cs`, Zeile 87 und 107).
- [x] 3.3 Nachrichten für Hello/Welcome/Join/Roster/Ready/Start/Intent/Delta/Snapshot/Ack/Ping/Pong/Disconnect/Resume/Error implementieren.
- [x] 3.4 Client-Sequenzen und Host-Revisionen führen; doppelte, verspätete, falsche oder nicht erlaubte Aktionen idempotent verwerfen und mit dem aktuellen Zustand beantworten.
- [x] 3.5 Vollständige Snapshots beim Beitritt, Reconnect oder Revisionskonflikt übertragen; danach kompakte Deltas senden.

### 4. Reconnect, Fehlerfälle und Windows-Nutzerführung

- [x] 4.1 Heartbeats und einen sichtbaren Reconnect-Zustand pro Teilnehmer einführen; nicht mehr jedes `NetLost()` direkt mit Rückkehr ins Menü behandeln.
- [x] 4.2 Kurzzeitige Trennung mit konfigurierbarem Zeitfenster behandeln; `sessionId`, `playerId` und kurzlebiges `resumeToken` für sicheres Wiederverbinden nutzen.
- [x] 4.3 Für jedes Spiel eine explizite Abwesenheitsregel definieren: pausieren (empfohlen für Kniffel, Memory und Poker), aussetzen oder bewusster Abbruch.
- [x] 4.4 Host-Verlust zunächst als sauberer Spielabbruch mit Erklärung behandeln. Eine Host-Migration erst nach stabiler Snapshot-Replikation als separate Ausbaustufe umsetzen.
- [x] 4.5 Windows-UI für Sitzfarben plus Text/Symbole, aktive Runde, Verbindung und Fehler gestalten; reine Farbkennzeichnung vermeiden.

### 5. Spiele nacheinander migrieren

- [x] 5.1 Kniffel: dynamische Score-Spalten, 2–4 Spieler, Zugrotation und synchronisierter Würfel-/Tabellenzustand.
- [x] 5.2 Memory: dynamische Spielerpunkte, aktiver Spieler, Host entscheidet Treffer und Folgeturn.
- [x] 5.3 Snake: bis zu vier Schlangen mit eindeutigen Farben/Sitzen, Input-Queue je Spieler, hostautoritatives Simulationsmodell und regelmäßige Snapshots.
- [x] 5.4 Blackjack: dynamische Hände/Buttons/Positionen, Einsatzphase und Spielerzug für alle Sitzplätze, Deck/Dealer ausschließlich am Host.
- [x] 5.5 Poker: starre Drei-Sitz-Annahmen entfernen; 2–4 Menschen, Blinds, Dealerbutton, Side-Pots, Geheimkarten und Reconnect robust umsetzen.

### 6. Grafik, Performance und Code-Cleanup

- [ ] 6.1 Draw Calls, GPU-/CPU-Zeit, GC und Arbeitsspeicher für 2/3/4 Spieler im Windows-Build messen.
- [ ] 6.2 Karten, Chips, Würfel und Partikel für zusätzliche Plätze poolen und ein szenenbezogenes Effektbudget definieren.
- [ ] 6.3 Bestehende URP/HDR/Bloom-Effekte anhand von Profilingdaten justieren, nicht global und ungeprüft reduzieren.
- [ ] 6.4 Rendering von Regel-/Netzwerklogik abkoppeln, damit ein Snapshot vollständig ohne Unity-Objekte rekonstruiert werden kann.

### 7. Test- und Abnahmeplan

- [x] 7.1 Unit-Tests für Protokollcodec, Serialisierung, Sitzverwaltung, Zugvalidierung, Wiederholungsschutz und Snapshots schreiben.
- [x] 7.2 Poker explizit auf Datenschutz testen: Kein Gast darf Hole Cards anderer Spieler im Broadcast, Log oder Snapshot erhalten.
- [x] 7.3 Mehr-Peer-Integrationstests mit dem vorhandenen Preview-/TCP-Ansatz einrichten: Host + 1/2/3 Gäste, Latenz, Paketduplikate, falsche Reihenfolge und Trennung.
- [ ] 7.4 Windows-Build, vorhandenen Kompiliercheck und reale Bluetooth-Geräte-/Adaptertests ausführen; Ergebnisse pro Spiel und Teilnehmerzahl dokumentieren.
- [ ] 7.5 Abnahme erst erteilen, wenn alle Teilnehmer denselben öffentlichen Zustand sehen, Reconnect regelkonform funktioniert und die festgelegten Performanceziele erfüllt sind.

## Verifikationsstand (Windows, 08.10.2026)

- Gepr?ft: ?bertragung der Android-Umsetzung (Branch `feature/vier-spieler-bluetooth`, Basis `22aad3f`); Unity-Windows-Build ohne Fehler (6000.3.25f1, `WinBt.cs` kompiliert); 251 Konsolenchecks, 0 Fehler (Wire, Session, Transportvertrag MemTransport/TcpBt, Poker-Regeln inkl. Geheimkarten); Mehrprozess-Lauf Poker mit Host + 3 G?sten ?ber TCP (4 Sitze, Sitzvergabe korrekt).
- Echter Bluetooth-Lauf: Windows-PC (MB-CUBE) als Host, Pixel 10 als Gast: SDP-Dienst erscheint gr?n in der Ger?teliste, Beitritt als Sitz 1, Lobby, Spielstart Poker, beide Seiten im Spiel. Ein erster Beitrittsversuch scheiterte mit ?Lesefehler? w?hrend eines 2,9-s-Startruckers der PC-App; nicht reproduziert, weiter beobachten.
- Nicht gepr?ft (bewusst offen): 3 gleichzeitige echte Bluetooth-G?ste (nur ein Handy vorhanden), Adapter-Limit, Reconnect auf echten Ger?ten, Profiling (6.x), Ger?te-Matrix (7.4), Zerlegung der Gro?klassen.

## Nicht Teil dieses Dokuments

- [ ] Keine Behauptung einer funktionierenden Vier-Ger?te-Bluetooth-Sitzung: belegt sind Simulation, TCP-Mehrprozess und ein echter 2-Ger?te-Lauf.
