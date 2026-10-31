# Testzeit, Abo und Lizenz ab Version 3.0.0

| Plattform | Kostenlos testen | Danach optional |
| --- | --- | --- |
| Windows | 7 Tage voller Funktionsumfang | 2,99 EUR je 30 Tage |
| Android | 7 Tage voller Funktionsumfang | 1,99 EUR je 30 Tage |

Die Testzeit startet beim ersten erfolgreichen Online-Start. Dafuer sind weder
PayPal-Anmeldung noch Zahlungsmittel erforderlich. Ohne ausdruecklichen Abo-Abschluss
wird nichts abgebucht. Nach Ablauf sperrt die App die Spiele ohne bezahlte Lizenz.

Ein abgeschlossenes PayPal-Abo verlaengert sich alle 30 Tage bis zur Kuendigung.
Ein bereits bezahlter Zeitraum bleibt nach der Kuendigung nutzbar. Windows und
Android haben getrennte Tarife. Die in PayPal eingebauten Tarife enthalten keine
weitere Testphase: Der kostenlose Test findet ausschliesslich in der App statt.

## Internet und Ablauf

Internet wird beim Start, nach Standby/Fokuswechseln und regelmaessig benoetigt.
Ohne bestaetigte Online-Pruefung bleibt der Spielzugang gesperrt. Eine bestehende
signierte Freigabe gilt hoechstens 120 Sekunden und niemals ueber das Ende der
Testzeit oder des bezahlten Zeitraums hinaus. Die Pruefung verwendet Serverzeit und
eine monotone Laufzeituhr; die lokale Kalenderuhr verlaengert den Zugang nicht.

Die Testzeit wird pro pseudonymisierter Geraetekennung und Plattform gespeichert.
Eine Neuinstallation oder ein neuer lokaler Zugangsschluessel setzt denselben
Geraetetest nicht zurueck. Ein vollstaendig manipuliertes Betriebssystem oder ein
gepatchter Client lassen sich damit nicht absolut absichern.

## Wiederherstellen und Datenschutz

Den Zugangsschluessel sicher aufbewahren. Auf dem Zugangsbildschirm kann ein
vorhandener Schluessel wiederhergestellt werden. Es gibt derzeit keine Begrenzung
gleichzeitiger Geraetenutzung; die Lizenz ist nicht als exklusive Ein-Geraet-Lizenz
zu verstehen.

Der Lizenzserver unter domezos-ware.com verarbeitet pseudonymisierte
Geraetekennungen, Testbeginn, gehashte Zugangsschluessel und erforderliche
Abo-Zuordnungen. PayPal wird erst beim freiwilligen Abo-Abschluss verwendet.
Die App und der Lizenzserver speichern keine Karten- oder Bankzugangsdaten.

## Lizenz und Grenzen

Neue proprietaere Bestandteile der Version 3.0.0 und ihre kommerzielle Ausgabe:
siehe [LICENSE](../LICENSE). Frueher eingeraeumte MIT-Rechte und Fremdlizenzen
bleiben erhalten; siehe [legal/MIT-legacy.txt](../legal/MIT-legacy.txt) und
[THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).

Die neue Android-Version wurde gebaut und ihre APK-Signatur sowie Internet-
berechtigung geprueft. Ein aktueller Pixel-Geraetetest und ein vollstaendiger
bezahlter Kunden-Checkout wurden noch nicht ausgefuehrt. Keine echte Zahlung wurde
im Rahmen der automatisierten Einrichtung abgeschlossen.

Technische Betreiber-Dokumentation: [Tools/Licensing/README.md](../Tools/Licensing/README.md).
