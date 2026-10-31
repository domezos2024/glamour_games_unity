# Glamour Games Windows 3.0.0

Zehn Spiele mit Computergegnern, Neon-Grafik, 3D-Physik und Bluetooth-Mehrspieler.

**7 Tage automatisch kostenlos testen - ohne PayPal-Anmeldung und ohne Zahlungsmittel.**
Danach optional **2,99 EUR je 30 Tage** per PayPal-Abo. Ohne ausdruecklichen Abo-Abschluss
wird nichts abgebucht. Nach dem Test sind die Spiele ohne bezahlte Lizenz gesperrt.
Internet ist fuer Start und regelmaessige Lizenzpruefungen erforderlich.

## Download

* [Windows 3.0.0: Installer und portable ZIP](https://github.com/domezos2024/glamour_games_unity/releases/tag/v3.0.0)
* [Android 3.0.0: signierte ARM64-APK](https://github.com/domezos2024/glamour_games_unity_android/releases/tag/v3.0.0)
* [Testzeit, Abo, Wiederherstellung und Datenschutz](docs/ABONNEMENT.md)
* [Aenderungen in 3.0.0](CHANGELOG.md)

Voraussetzungen: Windows 10/11 (64 Bit, DirectX 11/12) bzw. Android 8.0+ (ARM64).
Android wird direkt als APK angeboten; die neue Version ist nicht in Google Play.
Pruefsummen stehen in SHA256SUMS.txt beim jeweiligen GitHub-Release.

## Die zehn Spiele

Memory, Tic Tac Toe, Vier Gewinnt, Schiffe Versenken, Snake, Kniffel, Nim,
Buch der Pharaonen, Black Jack und Poker. Jedes Spiel bietet Computergegner in drei Staerken.
Poker, Memory, Kniffel, Snake und Black Jack unterstuetzen zwei bis vier Bluetooth-Spieler;
die anderen Mehrspieler-Modi bleiben bei zwei Personen. Gespielt wird ausschliesslich mit
Spielpunkten - keine Echtgeldwetten, keine kaufbaren Chips und keine Geldgewinne.

## Testen und bauen

Unity 6000.3.25f1 / URP 17.3. Lizenzserver und oeffentlicher Pruefschluessel sind konfiguriert;
private PayPal-Zugangsdaten und Signaturschluessel gehoeren niemals ins Repository.

```powershell
php Tools/Licensing/policy-tests.php
dotnet run --project Tools/Licensing/LeaseTests/LeaseTests.csproj -c Release
dotnet run --project Tools/Tests/GlamourTests.csproj -c Release
```

Release-Build: `Tools/Release/Build-Release.ps1`. Android verwendet die vorhandenen
Signierdaten ausserhalb des Repositories. Danach erstellt Tools/Release/Package-Release.ps1 den Windows-Installer mit Inno Setup 6 sowie ZIP, APK-Pruefung und SHA256-Pruefsummen.

Verifiziert: 24 Testzeit-/Zahlungstests, 22 Client-Lizenztests und 251 Mehrspielerpruefungen.
Der Windows-Runtime-Test startet in der kostenlosen Testzeit ohne Zahlungsdaten direkt im Menue.
Ein aktueller physischer Android-Geraetetest und ein vollstaendiger bezahlter Checkout stehen aus.

## Lizenz

Neue proprietaere Bestandteile und kommerzielle Ausgabe ab 3.0.0: [kommerzielle Lizenz](LICENSE).
Fruehere MIT-Rechte bleiben bestehen: [MIT-Hinweis](legal/MIT-legacy.txt).
Fremdkomponenten behalten ihre eigenen Lizenzen: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

[Engine-Dokumentation](docs/ENGINE.md) · [Bluetooth-Spezifikation](docs/Mehrspieler-Spezifikation.md) ·
[Betreiber-Dokumentation](Tools/Licensing/README.md) · [historische README](docs/ARCHIVE-README-pre-3.0.md)
