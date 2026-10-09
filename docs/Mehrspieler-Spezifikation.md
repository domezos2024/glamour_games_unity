# Mehrspieler (2–4 Spieler, Bluetooth): Regeln, Matrix, Messziele, Review-Checkliste

Stand: 08.10.2026, in `main` (Windows PR #11). Gilt für Android und Windows gleichermaßen; identisch im Schwester-Repo [glamour_games_unity_android](https://github.com/domezos2024/glamour_games_unity_android).

## 1. Spielregeln für 2, 3 und 4 Menschen (1.2)

Allgemein: Sitz 0 = Host, Sitze 1–3 = Gäste in Beitrittsreihenfolge. Der Host ist alleinige Autorität (Zufall, Regeln, Reihenfolge).
Unterbrechung: Verliert ein Sitz die Verbindung, **pausiert** das Spiel für alle (Overlay „Verbindung unterbrochen“) bis zu 45 s (`SessionConfig.ReconnectWindow`). Kehrt der Spieler zurück (gleicher Sitz per `resumeToken`), läuft das Spiel mit Snapshot weiter. Danach: Abbruch der Partie mit Meldung. Es gibt keine KI-Übernahme und keine stillen Auto-Aktionen. Host weg = Abbruch.

| Spiel | Spieler | Sitz-/Zugreihenfolge | Sieg | Besonderheiten |
|---|---|---|---|---|
| Memory | 2–4 | reihum nach Sitznummer, Treffer = nochmal | meiste Paare, Gleichstand = geteilt | Karten mischt der Host |
| Kniffel | 2–4 | reihum, 3 Würfe pro Zug | höchste Gesamtpunktzahl | Würfel würfelt der Host; Bonus ab 63 |
| Snake | 2–4 | gleichzeitig | letzte überlebende Schlange | Host rechnet Ticks, Futter-Spawn hostseitig |
| Blackjack | 2–4 gegen die Bank | reihum nach Sitznummer, Bank zuletzt | Hand näher an 21 als die Bank | Bankkarte verdeckt, nur Host kennt sie; Guthaben je Sitz |
| Poker (Texas Hold'em) | 2–4 | Button wandert reihum, Heads-up: Button = Small Blind | letzter mit Chips | Hole Cards privat, Side-Pots, steigende Blinds |
| TicTacToe, Vier gewinnt, Schiffe, Nim, Slot | genau 2 | Host/Gast | je Spiel | Relay-Modus, nur 2 Sitze |

Abbruch/Austritt: Verlässt ein Spieler bewusst die Partie, wird sie für alle beendet (Meldung „Ein Spieler hat die Partie verlassen“); Kick ist nur in der Lobby möglich.

## 2. Kompatibilitätsmatrix (1.3)

Legende: `S` = Simulation/Mehrprozess über TCP (echter Session-/Link-/Szenencode, kein Bluetooth), `G` = echte Geräte, `–` = nicht getestet.

| Verbindung | 2 Teilnehmer | 3 Teilnehmer | 4 Teilnehmer |
|---|---|---|---|
| Android ↔ Android | S (Protokoll), G offen | S, G offen | S, G offen |
| Android ↔ Windows | G (08.10.2026: PC als Host, Pixel 10 als Gast, Lobby und Poker-Start); weitere Spiele offen | - | - |
| Windows ↔ Windows | S (Vorschau-Prozesse über TCP), G offen | S (Poker: Host + 3 Gäste = 4 Sitze), G offen | S, G offen |

Stand der Prüfungen: 251 automatische Prüfungen (Netzkern, Transportvertrag, Poker-Regeln) (Konsolentests), Mehrprozess-Läufe Memory (3), Kniffel (3), Snake (4), Blackjack (3), Poker (3 und 4) mit identischem öffentlichem Zustand. Echte Bluetooth-Läufe mit 3–4 Geräten stehen aus.

## 3. Messziele (1.4)

| Größe | Ziel | Messung |
|---|---|---|
| Verbindungszeit (Beitritt bis Lobby) | ≤ 10 s | `SessionStats.ConnectSeconds` |
| Reconnect-Erfolg innerhalb des Fensters (45 s) | ≥ 95 % | `Resumes` / (`Resumes` + `ResumeFailures`) |
| Erkennung eines toten Peers | ≤ 7 s (`PeerTimeout`) | `Timeouts` |
| Paketverlust / Wiederholungen | < 2 % Wiederholungen | `Resends` / `Sent` |
| Desyncs (Snapshot nötig) | 0 pro Partie ohne Verbindungsverlust | `Desyncs`, `SnapshotRequests` |
| Host-FPS mit 4 Teilnehmern | ≥ 50 FPS im Mittel (Pixel 10), nie < 30 | Profiler / adb `dumpsys gfxinfo` |
| Speicher | kein Wachstum > 50 MB über 30 Min | Profiler |

Werte der echten Messung sind noch einzutragen (siehe `ChatGPT-Verbesserungen.md`, Abschnitt 6 und 7.4).

## 4. Layout-Regeln für 2/3/4 Sitze (Querformat, Designraum 1600×900)

- Keine festen Zwei-Platz-Koordinaten: Positionen je Spielerzahl aus Tabellen/Formeln (`Layouts`, `PXi(i)`, `tableN`, `boxN`).
- Poker: 2 Plätze unten (340/545, 1060/545); 3: zusätzlich oben Mitte (700/215); 4: unten links/rechts und oben rechts/links.
- Blackjack: 2 Plätze links/rechts (420/1180), 3 gleichmäßig (290/800/1310), 4 gleichmäßig (230/610/990/1370); Panelbreite 440 (≤ 3) bzw. 360 (4).
- Namen kürzen (8–14 Zeichen) statt Schrift zu verkleinern; Farbe immer zusammen mit Text (Name, „online/wartet“).
- Aktionsleisten (Würfeln, Karte, Halten, Fold/Call/Raise) bleiben an festen unteren Positionen und werden nie von Statusanzeigen verdeckt; der Netzstatus je Spieler liegt in einem schmalen Streifen am oberen Rand (y 3–23).
- Mindestschriftgröße 14 (Status), 16 (Verlauf), 20+ für Spielinformationen.

## 5. Review-Checkliste (7.5)

Vor jedem Merge von Mehrspieler-Code abhaken:

- [ ] Keine festen Sitzanzahlen (`< 3`, `% 3`, Zwei-Platz-Arrays); Schleifen laufen über `pl.Length`/`Link.Count`.
- [ ] Keine globale Ein-Peer-Wahrheit (`PeerName` nur im Relay-Modus für Altspiele).
- [ ] Jede Eingabe wird vom Host validiert (Sitz, Phase, Zug, Wertebereich); Fehlbedienung wird still verworfen.
- [ ] Keine Geheimdaten im Broadcast (Hole Cards, verdeckte Bankkarte, Zufalls-Seed/Deck); Privates nur per `EmitTo`.
- [ ] Zufall nur auf dem Host; Gäste bekommen Ergebnisse.
- [ ] Jeder Zustand ist snapshotfähig (`NetSnapshot`/`NetApplySnapshot`), Snapshots enthalten nichts, was der Sitz nicht sehen darf.
- [ ] Animationen halten keine fachliche Wahrheit (Zustand zuerst, Animation folgt).
- [ ] Host überspringt eigene Deltas (`if (Link.IsHost) return;`), keine doppelte Anwendung.
- [ ] Kein Zustandswechsel im Frame während `Link.Frozen`.
- [ ] Mehrprozess-Test (3–4 Prozesse) und `Tools/Tests` laufen grün.
