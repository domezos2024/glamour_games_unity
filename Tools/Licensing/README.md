# Glamour Games: kostenlose Testzeit und PayPal-Abos

Stand: 31.10.2026. Direkter Vertrieb als Windows-EXE und Android-APK.

## Gewuenschter Ablauf

1. Erster Online-Start: sieben Tage Vollzugriff automatisch aktivieren.
2. KEINE Registrierung bei PayPal und KEIN Zahlungsmittel fuer diese Testzeit.
3. Nach sieben Tagen ohne bestaetigte Zahlung Spiele sperren.
4. Erst dann freiwillig ein PayPal-Abo abschliessen: Windows 2,99 EUR, Android 1,99 EUR alle 30 Tage.
5. Ohne Abo-Abschluss findet keine Abbuchung statt. Ein abgeschlossenes Abo verlaengert sich bis zur Kuendigung.

Die kostenlose App-Testzeit ist vom PayPal-Abo getrennt. Die verwendeten PayPal-Tarife haben deshalb
keine eigene Testphase: ein Kauf nach Ablauf der App-Testzeit bezahlt sofort den ersten 30-Tage-Zeitraum.

## Aktive PayPal-Tarife

| Plattform | Preis / 30 Tage | Plan-ID |
| --- | --- | --- |
| Windows | 2,99 EUR | P-5FV55168KL4254910NLSSYNA |
| Android | 1,99 EUR | P-86J656498A592441KNLSSYNI |

Die zuvor erstellten PayPal-Tarife P-11945203YW512243YNLSSOJI und P-95M997722V571262LNLSSOYY
mit eingebauter PayPal-Testphase wurden deaktiviert. Die aelteren Entwuerfe des anderen Kontos
P-40694520KC280964BNLSSF5A / P-0L7267558H2712417NLSSHJI werden ebenfalls nicht verwendet.
Bestehende fremde Produkte, insbesondere TinyURLs snote, wurden nicht veraendert.

## Hosting und Zugangsdaten

sFTP-Server com, Verzeichnis /dw.com/glamour_licensing.
API: https://domezos-ware.com/glamour_licensing/index.php
Readiness: https://domezos-ware.com/glamour_licensing/health.php

Die Live-REST-App heisst Glamour Games Licensing. Nur die Abo-Funktion ist fuer diese Integration
erforderlich; unnoetige Auszahlungs-/Streitfall-/Payment-Link-Funktionen wurden abgeschaltet.
Client-ID/Secret und Signaturschluessel liegen ausschliesslich in _private/config.php und
_private/signing-key.php. Die SQLite-Datenbank und Migrationssicherung liegen ebenfalls in _private.
Require all denied blockiert den gesamten privaten Ordner; konkrete Webabrufe der Konfiguration,
des Schluessels und der Datenbank wurden mit HTTP 403 geprueft.
Lokale temporaere Geheimnisdateien wurden nach der Uebertragung entfernt. Keine Secrets ins Git!

Der oeffentliche RSA-Pruefschluessel und die API-Adresse stehen in
Assets/Resources/Glamour/LicenseSettings.json, identisch in beiden Projekten.

## Ablauf und Grenzen der Sperre

* Nutzerwahl: ohne sichere Online-Pruefung sperren.
* Jeder Neustart, Fokuswechsel oder Standby verwirft die bisherige Freigabe.
* Eine signierte Freigabe gilt maximal 120 Sekunden und niemals ueber den kostenlosen/bezahlten Zeitraum hinaus.
  Nach 45 Sekunden erneut pruefen; bei Prueffehler sofort sperren.
* Der Ablauf verwendet Serverzeit und eine monotone Laufzeituhr, keinen manipulierbaren lokalen Kalender.
  Ein Sicherheitsabzug am Ablauf und die gesamte HTTP-Laufzeit verhindern eine Verlaengerung durch Rundung/Latenz.
* Keine Freigabe wird auf Platte gespeichert. Ohne Internet beim Start bleibt der Zugang gesperrt.
  Ein bereits laufendes Spiel kann bei Verbindungsverlust bis zur naechsten Pruefung bzw. hoechstens
  bis zum Ende der aktuellen Freigabe weiterlaufen.
* Die Testzeit wird pro Geraet und Plattform in device_trials gespeichert. Die App hasht die stabile
  Betriebssystemkennung mit Plattform/Praefix, bevor sie diese an den Lizenzserver sendet.
  Es werden weder Hardware-Rohkennung noch PayPal-Daten fuer den Test angefordert.
* Neue lokale Zugangsschluessel oder geloeschte App-Einstellungen setzen denselben Geraetetest nicht zurueck.
  Die Kennung ist pseudonymisiert, nicht als vollkommen anonym zu bezeichnen.
* Ein gepatchter Client kann Kennungen faelschen oder die Sperre entfernen. Eine absolute Garantie gegen
  ein kontrolliertes Betriebssystem ist nicht moeglich. Das MIT-lizenzierte Repo und alte freie Builds
  lassen sich nicht nachtraeglich sperren. Der Schutz gilt fuer unveraenderte neue Vertriebs-Builds.
* Nach dem Test reichen ACTIVE oder next_billing_time nicht: nur abgeschlossene, nicht erstattete
  Zahlungen mit passendem Plan, Konto, Betrag und EUR gewaehren bis zu 30 Tage.
* Kuendigung beendet die Verlaengerung; bereits bezahlte Restzeit bleibt nutzbar.
  Ein Neuabschluss nach Kuendigung nutzt ebenfalls einen Tarif ohne weitere PayPal-Testphase.
* Der Zugangsschluessel stellt einen vorhandenen bezahlten Zugang wieder her. Es gibt noch kein Limit
  gleichzeitig verwendeter Geraete; nicht als exklusive Ein-Geraet-Lizenz bewerben.
* Ohne Zugang werden Spielstart und Simulation blockiert; Bluetooth-Sitzungen werden getrennt.

## Pruefungen

php Tools/Licensing/policy-tests.php
dotnet run --project Tools/Licensing/LeaseTests/LeaseTests.csproj -c Release
dotnet build Tools/Licensing/CompileCheck/CompileCheck.csproj -c Release
dotnet build Tools/Licensing/CompileCheck/CompileCheck.csproj -c Release -p:PlatformSymbols=UNITY_ANDROID
powershell -File Tools/Licensing/Integration-Check.ps1

24 Zahlungs-/Testzeitpruefungen und 22 Client-Lizenzpruefungen bestanden.
Die Live-Integration bestaetigt einen signierten trial-Zugang OHNE Zahlungsdaten, Signatur, Nonce,
kurze Freigabe und unveraendertes Ablaufdatum beim Wechsel des lokalen Zugangsschluessels.
Die bestehenden Mehrspieler-Tests bestanden ueber httpListenerV2: 251 Pruefungen, 0 Fehler.
Readiness bestaetigt Serverfaehigkeiten, PayPal-OAuth, aktive Tarife ohne PayPal-Testphase und Signierung.

Die korrigierten Player wurden erfolgreich gebaut:
* Windows: Builds/LicenseTest/GlamourGames.exe samt zugehoerigem Datenordner.
* Android: Builds/Android/GlamourGamesUnityAndroid-licensing.apk, mit vorhandenem Release-Schluessel signiert.
* APK-Signatur mit apksigner verifiziert; android.permission.INTERNET im fertigen APK bestaetigt.
* Der Windows-Runtime-Test oeffnete nach automatischer Online-Aktivierung direkt Menu, ohne PayPal oder Zahlungsmittel.
  Der Screenshot-Aufruf im Batchmodus lieferte kein Bild; die Freischaltung ist im Runtime-Log als Scene.Enter: enter Menu belegt.
* Androids Internetberechtigung wird in ProjectBootstrap und ProjectSettings dauerhaft gesetzt.
* Die veralteten Android-Caches Library/Bee und Library/BuildPlayerData wurden vor dem erfolgreichen Build
  reversibel nach Temp/LicensingCacheBackup verschoben.
* Die bestehenden unabhaengigen Android-Aenderungen wurden erhalten; Windows-Assets, die nur durch
  Unity neu serialisiert worden waren und vorher sauber waren, wurden auf ihren Ausgangsstand zurueckgesetzt.

Build-Players.ps1 baut beide Player und liest die vorhandenen Android-Signierdaten, ohne sie auszugeben.
Ein vorhandener alter Keystore-Pfad nach dem Ordnerumzug wird aufgeloest; bei genau einer Keystore-Datei
im Signierordner kann diese verwendet werden. Zugangsdaten stehen nicht in Skripten oder Kommandozeilen.

Offen: Das Pixel 10 wird derzeit nicht per USB/WLAN-ADB erkannt. Installation und echter Android-Geraetetest
konnten deshalb noch nicht ausgefuehrt werden. ADB-Abfragen lieferten eine leere Geraeteliste.
Kein kostenpflichtiges Kundenabo und keine echte Kundenzahlung wurden durch den Agenten abgeschlossen;
ein vollstaendiger Test des bezahlten Kunden-Checkouts bleibt ausstehend.
Die Browsersteuerung wurde nach dem physischen Escape des Nutzers nicht fortgesetzt; die Korrektur
und Bereitstellung erfolgten ueber httpListenerV2 und direkte Server-APIs.

Die gemeinsamen Lizenzdateien, Test-/Serverquellen und der oeffentliche Pruefschluessel werden in beiden
Repos gepflegt. Unabhaengige vorhandene Android-Aenderungen bleiben erhalten.
