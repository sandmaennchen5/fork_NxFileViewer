# Feedback, Umfragen und Nutzungsstatistik

Einrichtung und Betrieb des NxFileViewer-Webdienstes auf ALL-INKL.
Diese Datei ist die gemeinsame Anleitung für Installation, Konfiguration und Verwaltung.
Änderungen am Webdienst stehen separat im [Web-Changelog](CHANGELOG.md).

## Inhalt

- [Einrichtung auf ALL-INKL](#einrichtung-auf-all-inkl)
- [Programmeinstellungen](#programmeinstellungen)
- [Verwaltung](#verwaltung)
- [Admin-Sicherheit](#admin-sicherheit)
- [Support-ID-Übersicht](#support-id-übersicht)
- [Support-ID und Datenschutz](#support-id-und-datenschutz)
- [Nutzungsstatistik](#nutzungsstatistik)
- [Grafiken und Badges](#grafiken-und-badges)
- [Grafiktexte ändern](#grafiktexte-ändern)
- [Neue Datenbank initialisieren](#neue-datenbank-initialisieren)
- [Updates, Sicherungen und Fehlerbehebung](#updates-sicherungen-und-fehlerbehebung)
- [Lokale Tests](#lokale-tests)

## Einrichtung auf ALL-INKL

### 1. Subdomain und Verzeichnisse

Die HTTPS-Subdomain **nxfile.sandmaennchen5.de** zeigt ausschließlich auf
`/nxfile.sandmaennchen5.de/public/`.

```text
/nxfile.sandmaennchen5.de/
├── public/    ← öffentliches Ziel der Subdomain
│   ├── .htaccess
│   ├── admin/
│   └── … öffentliche PHP-Dateien
└── store/     ← außerhalb aller öffentlichen Webverzeichnisse
    ├── config.php
    ├── github-app.pem
    └── … private PHP-Bibliotheken und Grafiktexte
```

Der lokale Ordner `tools/feedback/` hat bereits diese Struktur.
Lade den Inhalt von `store/` zuerst und anschließend den Inhalt von `public/`
einschließlich der versteckten `.htaccess` und `admin/` hoch.
Diese Anleitung und `tests/` müssen nicht auf den Webspace.

Benötigt werden PHP 8.1+, HTTPS, cURL, OpenSSL, zlib und PDO MySQL.
Für SQLite sowie SQLite-Exporte ist zusätzlich PDO SQLite erforderlich.
PHP muss in `store/` schreiben und HTTPS-Verbindungen zu GitHub aufbauen können.
Für private Logs sollte `post_max_size` mindestens 8 MiB betragen.

### 2. GitHub App

Erstelle auf GitHub eine GitHub App mit folgenden Einstellungen:

- Repository-Berechtigung **Issues: Read and write**.
- Installation nur auf `sandmaennchen5/fork_NxFileViewer`.
- Benutzerautorisierung über den Webablauf aktivieren; Webhooks deaktivieren.
- Callback-URL: `https://nxfile.sandmaennchen5.de/report.php`.
- Client Secret und privaten App-Schlüssel erzeugen.
- Privaten Schlüssel als `store/github-app.pem` ablegen.

### 3. Private Konfiguration

Verwende `store/config.example.php` als Vorlage für `store/config.php`.
Bei einer bestehenden Installation die ausgefüllte Datei beibehalten und nur ergänzen.

| Einstellung | Wert / Bedeutung |
| --- | --- |
| `client_id`, `client_secret` | GitHub-App-Zugangsdaten |
| `installation_id`, `repository_id` | GitHub-Installation und Repository |
| `callback_url` | Exakte HTTPS-Adresse von `report.php` |
| `private_key` | Privater Pfad zu `github-app.pem` |
| `poll_secret` | Dauerhafter zufälliger Schlüssel |
| `support_secret` | Optional eigener Support-Schlüssel; sonst wird `poll_secret` verwendet |
| `poll_admin_password_hash` | Startpasswort-Hash für den ersten Benutzer `admin` |
| `admin_password_hash` | Optionaler Startpasswort-Hash mit Vorrang vor dem bisherigen Umfrage-Hash |
| `statistics_days` | Aktivitätszeitraum; Standard 30, z. B. 60, 90 oder 120; erlaubt: 1–3650 |
| `database_maintenance` | Normalerweise `false`; `true` sperrt Datenbankzugriffe bei Wartungsarbeiten |

Schlüssel und Passwort-Hash lokal mit PHP erzeugen:

```sh
php -r "echo bin2hex(random_bytes(32)), PHP_EOL;"
php -r "echo password_hash('DEIN-LANGES-ADMIN-PASSWORT', PASSWORD_DEFAULT), PHP_EOL;"
```

Den zufälligen Schlüssel dauerhaft sichern und unverändert lassen:
Ein Wechsel verändert Support-IDs und die Zuordnung von Stimmen und Statistik.

Für eine neue MariaDB-Installation im KAS eine Datenbank anlegen und eintragen:

```php
'database_driver' => 'mariadb',
'database_maintenance' => false,
'mariadb' => [
    'host' => 'localhost',
    'port' => 3306,
    'database' => 'DEINE_DATENBANK',
    'user' => 'DEIN_DB_BENUTZER',
    'password' => 'DEIN_ECHTES_KENNWORT',
],
'statistics_days' => 120,
```

Tabellen werden automatisch mit InnoDB und utf8mb4 angelegt.
Erforderlich sind CREATE, INDEX, SELECT, INSERT, UPDATE und DELETE.
Für große Log-Importe sollte `max_allowed_packet` mindestens 16 MiB betragen.

Bestehende SQLite-Installationen können `database_driver => 'sqlite'` und den
`database`-Dateipfad weiterverwenden. Den Wechsel beschreibt
[Von SQLite zu MariaDB wechseln](#von-sqlite-zu-mariadb-wechseln).

### 4. Funktion prüfen

Öffne [die Verwaltung](https://nxfile.sandmaennchen5.de/admin/) und melde dich an.
Prüfe je eine Fehlermeldung mit und ohne GitHub, eine Umfrage sowie
[die öffentliche Statistik](https://nxfile.sandmaennchen5.de/statistik).

## Programmeinstellungen

Die Datei `NxFileViewerFork.settings.json` liegt neben der Programmdatei.
Endpunkte bei geschlossenem Programm auf oberster Ebene einstellen:

```json
"reportEndpoint": "https://nxfile.sandmaennchen5.de/report.php",
"pollEndpoint": "https://nxfile.sandmaennchen5.de/umfragen"
```

Die Statistik verwendet `usage.php` im selben Verzeichnis wie `report.php`.
Ein leerer `reportEndpoint` öffnet stattdessen das vorausgefüllte GitHub-Formular.

Ohne GitHub kann ein freiwilliger Anzeigename angegeben werden; das Programm
speichert ihn als `reportDisplayName`. Erlaubt sind 1–40 Buchstaben, Ziffern,
Leerzeichen, Punkte, Unterstriche und Bindestriche. Der öffentliche GitHub-Autor
ist dabei der App-Bot; der gewählte Name steht im Issue-Text.

Mit GitHub ist der autorisierte Benutzer der Issue-Autor. Die App zeigt den
Wartezustand und anschließend den bestätigten Login an. Die Autorisierung erfolgt
für jede Meldung; der Dienst behält das Benutzertoken nicht.
Den vorherigen Browser-Anmeldestatus kann das Programm nicht erkennen.

## Verwaltung

Alle Bereiche verwenden dieselbe Anmeldung:

| Adresse | Funktion |
| --- | --- |
| [/admin/](https://nxfile.sandmaennchen5.de/admin/) | Einstieg und Anmeldung |
| [/admin/umfragen](https://nxfile.sandmaennchen5.de/admin/umfragen) | Umfragen erstellen, schließen oder löschen |
| [/admin/support](https://nxfile.sandmaennchen5.de/admin/support) | Support suchen, GitHub-Status filtern, private Logs herunterladen/löschen |
| [/admin/support/ids](https://nxfile.sandmaennchen5.de/admin/support/ids) | Vergebene Support-IDs und private Statistikzuordnung |
| [/admin/statistik](https://nxfile.sandmaennchen5.de/admin/statistik) | Interne Nutzungsübersicht |
| [/umfragen](https://nxfile.sandmaennchen5.de/umfragen) | Öffentliche Umfragen |
| [/statistik](https://nxfile.sandmaennchen5.de/statistik) | Öffentliche aggregierte Statistik |

### Umfragen

Unter **/admin/umfragen** gibt es zwei Antwortarten:

- **Auswahl:** 2–20 aktive Antworten, pro Kennung eine Stimme.
- **Freitext:** eine private Antwort mit maximal 2000 Zeichen, etwa für Funktionswünsche.
  Antwortlisten leer lassen. Nur im geschützten Adminbereich werden die Texte
  angezeigt; öffentliche Ergebnisse und SVG-Bilder enthalten nur die Anzahl.

**Umfrage bearbeiten** ändert Frage, Übersetzung und vorhandene Antworttexte.
Zusätzliche Auswahlen unten zeilenweise ergänzen. **Weiter auswählbar** abschalten
entfernt eine Antwort aus der Auswahl; die Antwort-ID und bisherigen Stimmen bleiben
in den Ergebnissen mit „Entfernt“ erhalten. Die Auswahl kann wieder aktiviert werden.
Mindestens zwei und höchstens 20 aktive Auswahlen sind erforderlich. Nur Texte ändern,
deren Bedeutung erhalten bleibt, damit bereits abgegebene Stimmen sinnvoll bleiben.
Die Antwortart wird bei der Erstellung festgelegt und beim Bearbeiten nicht geändert.

Drei separate Schalter steuern die App:

| Einstellung | Wirkung |
| --- | --- |
| Umfrage in der App zur Teilnahme anzeigen | Neue Antworten in der App erlauben |
| Ergebnisse in der App anzeigen | Ergebnisse auch unabhängig von der Teilnahme anzeigen |
| Beim App-Start erinnern | Unbeantwortete, offene Umfragen beim Start melden |

Ohne Start-Hinweis ist eine Umfrage optional: Sie bleibt unter **Info → Umfragen**
erreichbar. Beide Anzeige-Schalter ausblenden entfernt sie vollständig aus der App;
die öffentliche Webseite bleibt davon unabhängig. Ausgeschaltete App-Ergebnisse
werden schon im API-Datensatz maskiert, nicht nur optisch versteckt. Für geschlossene
Umfragen zählt nur die Ergebnisanzeige. **Umfrage geschlossen** kann bei der
Bearbeitung gesetzt oder aufgehoben werden.

**Nicht teilnehmen** speichert die Entscheidung zur Teilnehmerkennung auf dem
Server. Diese Umfrage erscheint danach nicht mehr als unbeantwortet beim App-Start.
**Doch teilnehmen** hebt die Entscheidung wieder auf. Nichtteilnahme zählt nicht als
Stimme. Bereits gespeicherte Stimmen oder Freitext-Antworten werden dadurch nicht
entfernt. Die App verwendet ihre feste Kennung; Browser verwenden eine separate
Sitzungskennung. Die Sprachauswahl verändert keine dieser Kennungen.

Die App enthält keinen Webübersicht-Button mehr. Auswahltexte verwenden dynamische
Themefarben und bleiben im Dark Mode lesbar. **Info → Fehler / Funktionswunsch melden**
öffnet das gemeinsame Formular für Fehlerberichte und Featurewünsche.

**Umfrage löschen** verlangt eine Bestätigung und entfernt die Umfrage samt
Antwortoptionen, Stimmen, Freitext-Antworten, Nichtteilnahmen und Übersetzungen.

Ergebnislinks und SVG, beispielsweise für Umfrage 1:

- `https://nxfile.sandmaennchen5.de/umfragen?poll=1&results=1`
- `https://nxfile.sandmaennchen5.de/umfragen?poll=1&image=1`

Die öffentliche Ergebnisseite aktualisiert sich alle 30 Sekunden. Gelöschte Umfragen
liefern HTTP 404; externe Bild-Caches können ältere Ergebnisse zeigen.

### Deutsch und Englisch

Öffentliche Startseite, Umfragen, Statistikübersicht und der gesamte Adminbereich
bieten **Deutsch / English**. Die Auswahl wird als Cookie `NXFILELANG` für sechs
Monate gespeichert. Ohne Auswahl wird die bevorzugte unterstützte Browsersprache
verwendet, ansonsten Deutsch. Die Auswahl ändert keine Teilnehmerkennung und keine
abgegebene Stimme. Direkte Links können `?lang=de` oder `?lang=en` enthalten; bei
vorhandenen Parametern `&lang=en` ergänzen.

Bei Auswahl-Umfragen eine **deutsche Frage und 2 bis 20 deutsche Antworten**
eingeben. Für Freitext nur die deutsche und gegebenenfalls englische Frage eingeben. Optional dazu eine englische Frage und genau dieselbe Anzahl englischer
Antworten in derselben Reihenfolge eingeben. Beide englischen Felder müssen gemeinsam
ausgefüllt werden oder gemeinsam leer bleiben. Beide Sprachen verwenden dieselben
Umfrage- und Antwort-IDs sowie denselben Stimmenstand. Ohne englische Übersetzung
bleibt die deutsche Fassung sichtbar; Nutzereingaben werden nicht automatisch übersetzt.

Bestehende Umfragen haben weiterhin ihre bisherigen Texte. Unter **Umfrage bearbeiten** können berechtigte Benutzer die englische Fassung ergänzen
oder ändern. Das leert keine Stimmen und ändert keine Antwort-ID. Die englische Frage und Antworttexte leeren entfernt nur die Übersetzung. Löschen einer Umfrage entfernt auch deren
Übersetzungen. Berechtigung: **Umfragen erstellen / schließen**; SHOW darf nur ansehen.

Die App fragt Umfragen entsprechend ihrer gewählten Sprache ab. Die öffentliche
Webübersicht sowie Ergebnislinks und SVG-Ergebnisbilder können ebenfalls über `lang`
Deutsch oder Englisch anzeigen. Beispiel:

```text
https://nxfile.sandmaennchen5.de/umfragen?lang=en
https://nxfile.sandmaennchen5.de/umfragen?poll=1&results=1&lang=en
https://nxfile.sandmaennchen5.de/umfragen?poll=1&image=1&lang=en
https://nxfile.sandmaennchen5.de/admin/?lang=en
```

Feste Web-Oberflächentexte liegen in `store/web-translations.php`, die Sprachauswahl
in `store/web-language.php`. Anpassbare Statistik-/Badge-/Diagrammtexte bleiben in
`store/statistics-texts.json` für Deutsch; Englisch verwendet
`store/statistics-texts.en.json`. Meldungsinhalte, GitHub-Texte, Benutzernamen und
Support-IDs werden unverändert angezeigt und nicht als Oberflächentexte übersetzt.

Für dieses Update **zuerst die privaten Dateien aus `store/`, danach `public/`
inklusive `.htaccess` hochladen**. Neu erforderlich: `web-language.php`,
`web-translations.php`, `poll-translations.php`, `statistics-texts.en.json` und
`public/index.php`. Die alte `public/index.html` entfernen. Vorhandene private
`config.php` und individuell angepasste deutsche Statistiktexte erhalten; vorher
Datenbank sichern. `database.php` legt die beiden Übersetzungstabellen automatisch
für die neue leere Datenbank an. Eine Übernahme alter Testdaten findet nicht statt.
Die App neu erstellen / aktualisieren, damit sie die Umfragesprache mitsendet.

### Dauerhaftes Meldungsarchiv und Warteschlange

**/admin/meldungen** zeigt alle neu über den Webdienst eingegangenen Meldungen:
Titel, vollständige übermittelte Beschreibung einschließlich gewähltem öffentlichen
Log-Auszug, Support-ID, Name, Eingang, Übertragungsstatus und später die GitHub-Nummer.
Dieser Inhalt liegt in der eigenen Datenbank und wird auch nach erfolgreicher
GitHub-Veröffentlichung erhalten. Das Archiv benötigt keinen GitHub-Aufruf.
Änderungen oder Löschungen auf GitHub ändern den gespeicherten Originalbericht nicht.
Ältere Issues, deren Inhalt bisher nicht lokal gespeichert wurde, können bei einem
GitHub-Ausfall nicht nachträglich rekonstruiert werden.

| Status | Bedeutung |
| --- | --- |
| Wartet auf GitHub-Anmeldung | Inhalt ist gespeichert; Autorisierung steht aus |
| GitHub-Anmeldung erforderlich | Autorisierung fehlt, ist abgebrochen oder abgelaufen |
| Wartet auf Übertragung | Ein sicher wiederholbarer Versuch wird automatisch geplant |
| Übertragung wird vorbereitet / Wird gesendet | Ein Worker hat die Meldung exklusiv übernommen |
| Erfolgreich übertragen | GitHub-Nummer bestätigt; Original bleibt lokal lesbar |
| Erstellung unklar – prüfen | Timeout, Serverfehler oder Abbruch nach möglichem Senden; kein blindes automatisches Nachsenden |
| Übertragung fehlgeschlagen | GitHub lehnt die Meldung ab oder zwölf sichere Versuche sind fehlgeschlagen |

Die App bestätigt bei einem GitHub-Ausfall die Speicherung beim Betreiber mit einer
kurzen Meldungskennung. Diese kann im Archiv gesucht werden. Bei einem erneuten
Sendeversuch derselben unveränderten Meldung im geöffneten Fenster verhindert eine
stabile Anfragekennung doppelte Archiveinträge, etwa nach verlorener HTTP-Antwort.
Das gilt erst mit der aktualisierten App; ältere Clients erhalten ebenfalls die
Warteschlangen-Antwort, können sie aber noch als unbestätigte Erstellung anzeigen.
Wenn der eigene Webdienst nicht erreichbar ist, kann er nichts speichern; die App
behauptet dann keine erfolgreiche Annahme.

SHOW darf das Archiv ansehen. Für manuelles Senden / erneutes Versuchen und Zuordnen
ist das neue Einzelrecht **Meldungen übertragen / erneut versuchen** erforderlich.
Bei bereits vorhandenen Admin-Konten dieses Recht unter `/admin/users` aktivieren;
neu angelegte ADMIN-Vorlagen enthalten es. Private Logs erfordern weiterhin separat
**Private Logs herunterladen** bzw. **Private Logs löschen**. Sie sind bereits bei
wartenden Meldungen verfügbar und werden nicht nach 24 Stunden verworfen.

Nach unklarer Erstellung auf GitHub nachsehen. Ein vorhandenes Issue kann anhand
seiner Nummer zugeordnet werden; der Dienst prüft die verborgene Meldungskennung im
Issue-Text. Bewusstes erneutes Senden verlangt eine ausdrückliche Bestätigung des
möglichen Duplikats. Erfolgreiche Meldungen werden nie automatisch erneut gesendet.
Sichere Wiederholungen erfolgen mit steigenden Wartezeiten von einer Minute bis zu
einer Stunde, höchstens zwölf Versuche. Parallele Worker übernehmen dieselbe Meldung
nicht doppelt. Nach abgebrochenem Senden bleibt sie zur Prüfung stehen.

Bei GitHub-Anmeldung wird erst nach erfolgreicher Autorisierung im Namen des
Benutzers gesendet. Dafür wird der kurzlebige GitHub-Zugang verschlüsselt gespeichert,
nach Erfolg oder spätestens Ablauf entfernt. Fehlende / abgelaufene Autorisierung
wird nicht durch Veröffentlichung als Bot ersetzt. Bereits gespeicherte Inhalte
bleiben unabhängig davon für den Support lesbar. Bei unklarer Erstellung kann eine
manuelle Wiederholung nur mit noch gültiger GitHub-Autorisierung erfolgen; sonst
zunächst das möglicherweise bereits vorhandene Issue prüfen / zuordnen.

### Automatische Übertragung auf ALL-INKL einrichten

1. Aktuelle private Dateien aus `store/` zuerst, danach `public/` inklusive
   `.htaccess`, `admin/` und `admin-reports.php` hochladen. Die vorhandene private
   `config.php` erhalten; vorher Datenbank und Konfiguration sichern. Die Tabelle
   `feedback_queue` wird in der neuen leeren Datenbank automatisch angelegt (SQLite / MariaDB).
2. Ein separates zufälliges Geheimnis erzeugen, z. B. lokal mit
   `php -r "echo bin2hex(random_bytes(32)), PHP_EOL;"`. In der privaten
   `store/config.php` ergänzen:

   ```php
   'queue_cron_secret' => 'HIER_DAS_ZUFAELLIGE_GEHEIMNIS_EINTRAGEN',
   ```

3. In **KAS → Tools → Cronjobs → Cronjob anlegen**:
   - Protokoll/Pfad: `https://nxfile.sandmaennchen5.de/process-feedback.php`
   - Intervall: z. B. alle fünf Minuten.
   - Zusätzliche Einstellungen → **HTTP Benutzer**: `queue`.
   - **HTTP Passwort**: genau das private `queue_cron_secret`.
   - Speichern. Zugangsdaten niemals als URL-Parameter eintragen.
4. Der erfolgreiche Cron-Aufruf liefert JSON mit `ok: true` und den bearbeiteten
   Zuständen. Ohne Zugangsdaten liefert die URL HTTP 401. Der Endpoint akzeptiert
   ausschließlich HTTPS. Eine eigene `queue_cron_secret`-Angabe ist für diesen
   HTTP-Cronjob erforderlich; ohne sie bleibt er gesperrt.

Die Felder und die tarifabhängige Verfügbarkeit von Cronjobs beschreibt auch die
[ALL-INKL-Anleitung](https://all-inkl.com/en/support/tutorials/kas/tools/cronjobs/setup_479.html).
Die HTTP-Anmeldung wird vom Skript geprüft; kein öffentlicher Queue-Schlüssel in
GitHub, README oder Statistikgrafiken. Das Admin-Passwort dient nicht als Cron-Passwort.

Alternativ mit privatem SSH-/CLI-Zugang:

```sh
php /absoluter/pfad/nxfile.sandmaennchen5.de/store/process-feedback.php
```

Ohne eingerichteten Cronjob versucht der Dienst die erste Übertragung sofort;
weitere geplante Versuche benötigen den Cronjob oder die manuelle Admin-Aktion.
Das Cron-Skript verarbeitet pro Aufruf höchstens zehn Meldungen und speichert keine
unverschlüsselten OAuth-Zugänge. Den privaten Schlüssel wie bei 2FA stabil halten
und zusammen mit der Datenbank sichern. Backups/Exporte enthalten jetzt auch die
privaten vollständigen Meldungsinhalte.

### Support und private Logs

Die Suche findet Support-ID, interne Kennung, aktuellen bzw. gemeldeten Namen
und Issue-Nummer. Angezeigt werden bis zu 200 Treffer **nach** dem Filtern.
Alte Issues ohne erfasste Kennung werden nicht rückwirkend zugeordnet.

GitHub-Titel, offen/geschlossen, Schließungsgrund und Labels werden angezeigt.
Status-, Grund- und Label-Filter sind mit der Suche kombinierbar.
**Duplikat** findet den GitHub-Grund `duplicate` oder das gleichnamige Label.

Der GitHub-Abgleich erfolgt beim Öffnen der Übersicht höchstens alle fünf Minuten.
Jeder Abgleich liest alle Issues einschließlich geschlossener; Pull Requests
werden ausgelassen. Erst nach einem vollständig erfolgreichen Abruf werden
fehlende, zuvor gespeicherte oder lokal zugeordnete Issues einzeln geprüft.
HTTP 410 mit „This issue was deleted“ wird als **Gelöscht** angezeigt.
HTTP 404 oder ein anderer 410-Hinweis wird als **Nicht verfügbar** angezeigt. Dieser Status ist separat filterbar. Bei Ausfällen bleibt der letzte Stand mit Zeitangabe und Warnung
sichtbar. `Unbekannt` bedeutet, dass noch kein Status gespeichert ist.
„Gelöscht“ und „Nicht verfügbar“ sind getrennt filterbar. Eine fehlende Listenposition
allein wird nicht als Löschbestätigung behandelt. Bereits bestätigte Löschungen
bleiben gespeichert; erscheint das Issue wieder in der Liste, wird sein neuer Status übernommen.
Bei API-Fehlern oder unvollständigen Abrufen werden keine Issues als fehlend markiert.
Wieder verfügbare Issues erhalten beim nächsten erfolgreichen Abgleich ihren aktuellen
Status zurück. Support-Zuordnung und private Logs bleiben erhalten und können separat
verwaltet werden. „Unbekannt“ bedeutet weiterhin, dass noch kein Status erfasst wurde.
Statusänderungen selbst erfolgen auf GitHub. Die vorhandene App-Berechtigung genügt.

Im Meldedialog sind zwei Log-Optionen unabhängig auswählbar:

| Option | Veröffentlichung / Speicherung |
| --- | --- |
| Bereinigter Log-Auszug | Bearbeitbarer Text im öffentlichen GitHub-Issue |
| Original-Sitzungslog | Unveränderte Momentaufnahme, maximal 5 MiB, komprimiert nur in der privaten Datenbank |

Im öffentlichen Auszug werden erkannte Pfade, Dateinamen sowie lokale Benutzer-,
Rechner- und Domänennamen vor der Vorschau und erneut vor dem Senden ersetzt.
Das lokale Original bleibt unverändert. Freitext und Vorschau vor Veröffentlichung
prüfen: Die Bereinigung kann nicht jede persönliche Angabe erkennen.

Private Originale können persönliche Angaben enthalten. Sie sind ausschließlich
nach Admin-Anmeldung unter **Original herunterladen** abrufbar.
**Privates Log löschen…** öffnet eine Bestätigungsseite und entfernt nur dieses
Log aus der aktiven Datenbank. Issue und Support-Zuordnung bleiben bestehen.
Vorhandene Sicherungen und öffentliche Log-Auszüge bleiben ebenfalls bestehen.
Nicht fertig zugeordnete Uploads werden nach 24 Stunden beim nächsten Zugriff entfernt.

## Benutzer, Rechte und 2FA

Beim ersten Aufruf nach diesem Update wird der bisherige Passwort-Hash einmalig
als Benutzer **admin** mit allen Rechten übernommen. Anmeldung: Benutzername
`admin`, bisheriges Admin-Passwort. Danach liegen Benutzer und Passwort-Hashes in
der Datenbank; eine Änderung des Startpassworts in `config.php` ändert bestehende
Konten nicht. Alte Sitzungen müssen sich neu anmelden. Die Tabelle `admin_users`
wird automatisch angelegt, auch beim Upgrade einer vorhandenen MariaDB.

Unter **/admin/users** können berechtigte Benutzer Konten anlegen, bearbeiten und
löschen. Rechte werden einzeln über Checkboxen vergeben. Die Vorlagen sind:

| Vorlage | Rechte |
| --- | --- |
| SHOW | Umfragen, Support/Support-IDs und Statistik ansehen |
| SHOW+UMFRAGE | SHOW plus Umfragen erstellen und schließen |
| ADMIN | Alle Rechte einschließlich Löschen, privater Logs und Benutzerverwaltung |

Umfragen löschen, private Logs herunterladen, private Logs löschen und Benutzer
verwalten sind separate Rechte. Wer Benutzer verwalten darf, kann alle Rechte
vergeben. Änderungen erfordern das eigene Passwort, bei aktivierter 2FA zusätzlich
einen aktuellen Code, wenn die letzte Bestätigung mehr als fünf Minuten zurückliegt.
Der letzte Benutzer mit Benutzerverwaltung kann nicht gelöscht oder herabgestuft
werden; das eigene Konto kann nicht gelöscht werden. Neue Passwörter müssen 12 bis
72 Bytes lang sein. Namen: 3–64 Zeichen aus a–z, 0–9, Punkt, Unterstrich und Bindestrich.
Geänderte Konten, Passwörter, Rechte und 2FA-Resets machen bestehende Sitzungen ungültig.

Unter **/admin/account** kann jeder Benutzer optional 2FA einrichten. In einer
Authenticator-App den angezeigten Schlüssel als zeitbasiertes Konto eingeben
(6 Stellen, 30 Sekunden; z. B. Konto „NxFileViewer Admin / Benutzername“). Es wird
kein externer QR-Dienst verwendet. Erst ein passender Code aktiviert 2FA.
Bei gesetzter **2FA-Pflicht** muss der Benutzer unmittelbar nach der Passwortanmeldung
2FA einrichten, bevor er Verwaltungsdaten sehen kann. Die Einrichtung / zweite
Anmeldestufe läuft nach fünf Minuten ab; maximal fünf Codeversuche sind möglich.
Auch diese Versuche unterliegen den allgemeinen Anmeldelimits.

Nach der Einrichtung erscheinen acht Wiederherstellungscodes **ein einziges Mal**.
Sicher offline aufbewahren: Jeder ersetzt einmal den Authenticator-Code. Neue Codes
machen die alten ungültig. Verpflichtende 2FA kann der Benutzer nicht deaktivieren.
Ein Administrator kann 2FA eines Kontos zurücksetzen; eine bestehende Pflicht bleibt
erhalten und erzwingt die erneute Einrichtung. Bei Verlust sämtlicher Codes und
fehlendem weiteren Administrator über eine private SSH-Konsole zurücksetzen:

```sh
cd /pfad/zum/store
php admin-recover.php admin --reset-2fa
```

Das setzt nur 2FA zurück, erhält Passwort und Pflicht und beendet alle Sitzungen
des Kontos. Der Befehl funktioniert ausschließlich über CLI; niemals nach `public/`
verschieben. Ohne SSH einen anderen Administrator um den Reset bitten.

TOTP-Schlüssel werden mit AES-256-GCM verschlüsselt gespeichert,
Wiederherstellungscodes nur als HMAC. Der private Schlüssel kommt aus
`admin_totp_key`, ersatzweise `support_secret`, ersatzweise `poll_secret`.
**Den verwendeten Schlüssel unverändert privat sichern und mit der Datenbank sichern.**
Nach einer Änderung sind vorhandene 2FA-Schlüssel unlesbar und müssen zurückgesetzt
und neu eingerichtet werden. Systemzeit des Servers und Authenticators synchron halten.

### Kleine öffentliche Startseite

`public/index.php` zeigt nur das Bild aus `.github/social-preview.png` und Links
zu GitHub, GitLab und Forgejo. Das Bild liegt für den Webserver als Kopie in
`public/assets/social-preview.png`. `.htaccess` setzt `index.php` als Startseite.
Die Domain zeigt weiterhin auf `public/`; `store/` bleibt außerhalb des Webroots.

### Dateien für das Benutzer-Update hochladen

Zuerst die privaten Dateien `store/admin-accounts.php`, `store/admin-totp.php`,
`store/admin-recover.php` und `store/database.php`
hochladen; `store/admin-security.php` bleibt ebenfalls erforderlich. Die bestehende
private `store/config.php` nicht durch die Beispieldatei ersetzen.
Danach die aktualisierten Dateien aus `public/` einschließlich `.htaccess`,
`admin/index.php`, `admin-users.php`, `admin-account.php`, `index.php` und `assets/`
hochladen. Vorher Datenbank und private Konfiguration sichern. Benutzer und 2FA liegen in der privaten Datenbank; den Verschlüsselungsschlüssel
zusammen mit Datenbanksicherungen aufbewahren. Die Startseite benötigt keine Datenbank.

## Admin-Sicherheit

Der Adminbereich verwendet eine eigene Sitzung (Cookie NXFILEADMIN, Pfad /admin),
getrennt von den öffentlichen Umfragen. Cookies sind Secure, HttpOnly und SameSite=Lax;
PHP akzeptiert nur serverseitig angelegte Sitzungs-IDs. Nach Login und Logout werden
Sitzungs-ID und CSRF-Token erneuert.

Standardmäßig endet die Anmeldung nach **20 Minuten ohne Admin-Anfrage** oder
spätestens **acht Stunden nach dem Login**, auch bei fortlaufender Nutzung.
Öffentliche Seitenaufrufe verlängern diese Admin-Sitzung nicht. Vorhandene alte
Anmeldungen müssen nach dem Update neu erfolgen. Alle Admin-Unterseiten einschließlich
Original-Log-Downloads und Support-ID-Liste durchlaufen diese Prüfung.

Anmeldeversuche werden serverseitig in der bestehenden Tabelle admin_login_rate
reserviert: maximal **10 pro IP und Stunde** sowie **50 über alle IPs je
15-Minuten-Zeitfenster**. Eine bereits durch ihr IP-Limit gesperrte Quelle verbraucht
keine weiteren globalen Versuche. Die Zählung ist transaktional und greift auch bei
parallelen Anfragen oder neuen Browser-Sitzungen. Bei Überschreitung antwortet der
Dienst mit HTTP 429 und Retry-After. Nach Ablauf des jeweiligen Zeitfensters kann
erneut angemeldet werden. Das globale Limit kann während eines verteilten Angriffs
auch den Betreiber zeitweise von einer Neuanmeldung abhalten; bestehende gültige
Sitzungen werden dadurch nicht gesperrt. Dies ersetzt keinen Schutz des Hosters
gegen Überlastungsangriffe.

Optional in der privaten store/config.php ergänzen; ohne Einträge gelten diese Werte:

```php
'admin_security' => [
    'idle_timeout_seconds' => 1200,
    'absolute_timeout_seconds' => 28800,
    'login_ip_limit' => 10,
    'login_ip_window_seconds' => 3600,
    'login_global_limit' => 50,
    'login_global_window_seconds' => 900,
],
```

Zeitangaben sind Sekunden, Grenzen positive ganze Zahlen. Ungültige Werte werden
abgewiesen. Die absolute Sitzungsdauer darf nicht kürzer als die Inaktivitätsdauer sein.
Die Tabellen werden beim Start mit einer leeren Datenbank angelegt.

Die public/.htaccess leitet HTTP mit Status **308** auf
https://nxfile.sandmaennchen5.de um und sendet bei HTTPS **HSTS für ein Jahr**.
Bei einer anderen Domain das feste Umleitungsziel in .htaccess anpassen.
HSTS gilt für diesen Host, ohne includeSubDomains und ohne Preload-Anmeldung.
Benötigt werden ein gültiges TLS-Zertifikat, Apache mod_rewrite sowie mod_headers
für den allgemeinen HSTS-Header. Der PHP-Adminbereich prüft HTTPS zusätzlich selbst
und sendet dort ebenfalls HSTS. X-Forwarded-Proto wird nicht als Nachweis akzeptiert.
Bei einem vorgeschalteten Proxy muss daher auch die Verbindung zum Webspace HTTPS
verwenden; eine reine HTTP-Verbindung zum Ursprung wird abgewiesen.

Nach dem Hochladen prüfen: HTTP leitet auf HTTPS, HTTPS-Antworten enthalten
Strict-Transport-Security: max-age=31536000, und Login/Logout funktionieren.
Browser merken sich HSTS: HTTPS und Zertifikat müssen danach dauerhaft verfügbar bleiben.

## Support-ID-Übersicht

Unter **/admin/support/ids** sind ausschließlich nach Admin-Anmeldung sichtbar:

- Anzahl registrierter Support-IDs (jede ID einmal, unabhängig von Issues).
- Davon IDs mit mindestens einer Statistikmeldung.
- Davon IDs mit einer Meldung im statistics_days-Zeitraum.
- Davon IDs ohne bisher gespeicherte Statistikmeldung.
- Liste mit Name, erstem/letztem Kontakt, Issue-Anzahl, letzter Statistikmeldung
  und zuletzt gemeldeter Umgebung; Suche, Statistikfilter und 100 IDs pro Seite.

Diese Liste und Zuordnungszahlen sind **nicht öffentlich**. Öffentliche Grafiken
enthalten weiterhin ausschließlich zusammengefasste Nutzungszahlen ohne IDs/Namen.
„Keine Meldung“ bedeutet nicht „Statistik deaktiviert“: Offline-Zeiten und längere
Nichtnutzung sind ebenfalls möglich. Die Einstellung selbst wird nicht gemeldet.

Eine Windows-Installation erhält weiterhin dieselbe abgeleitete Kennung; es wird
nicht bei jedem Start eine neue Support-ID erzeugt. Die Übersicht erfasst nur
serverseitig registrierte IDs, keine garantierte Gesamtzahl aller Installationen.
Neue Statistikmeldungen legen bei Bedarf das dazugehörige Support-Profil an,
auch ohne vorherigen Abruf der öffentlichen ID im Infofenster. Frühere reine
Statistikkennungen lassen sich aus ihrem internen Hash nicht nachträglich in die
öffentliche Support-ID zurückrechnen; sie werden separat als noch nicht zugeordnet
gezählt. Bei der nächsten Statistikmeldung ergänzt der Dienst die Zuordnung.

### Dateien für dieses Admin-Update hochladen

Zuerst die neuen privaten Bibliotheken:

- store/admin-security.php
- store/support-overview.php

Danach gemeinsam aktualisieren:

- public/admin/index.php
- public/.htaccess
- public/support.php
- public/support-ids.php (neu)
- public/usage.php

Vorhandene config.php und Schlüssel beibehalten. config.example.php zeigt die
optionalen neuen Grenzwerte. Die Standardwerte funktionieren ohne Config-Änderung.

## Support-ID und Datenschutz

NxFileViewer liest unter Windows `MachineGuid` aus der 64-Bit-Registry und bildet
lokal SHA-256 aus einem festen App-Präfix und dem normalisierten Wert.
Die ursprüngliche Windows-Kennung wird weder übertragen noch gespeichert oder geloggt.

Der Dienst berechnet mit dem privaten Schlüssel getrennte HMACs für die interne
Kennung und eine öffentliche Support-ID, z. B. `NX-1234-5678-9ABC-DEF0-1234`.
Die öffentliche ID steht in Info/Meldedialog und im Issue-Text **mit und ohne GitHub**.
Sie lässt sich kopieren und für die GitHub-Suche verwenden.
Das Programm speichert nur die öffentliche ID und ihren Endpunkt zwischen.

Dieselbe Windows-Installation erhält dieselbe ID, auch für mehrere Benutzer,
nach Programmupdates oder nach Löschen der Programmeinstellungen.
Neuinstallationen können eine andere, geklonte Systeme dieselbe Kennung haben.
Für zukünftige Linux-/macOS-Clients ist noch ein entsprechender Kennungsanbieter nötig.

Private Support-Profile verknüpfen interne Kennung, Support-ID, Namen und Issues.
Bei GitHub-Meldungen wird der bestätigte Login erfasst.
Die Statistik ist deshalb technisch **pseudonym** und intern mit Support verknüpfbar.
Die öffentliche Support-ID legt die Windows-Kennung nicht offen; absolute
Anonymität oder Schutz vor dem Prüfen einer bereits bekannten Ausgangskennung
wird dadurch nicht garantiert.

Der Dienst begrenzt Meldungen auf drei Versuche je Quell-IP und 30 insgesamt
pro Zeitstunde, Stimmversuche auf 30 und Admin-Anmeldungen standardmäßig auf zehn je Quell-IP und Stunde sowie
50 über alle IPs innerhalb eines 15-Minuten-Zeitfensters. Für diese Begrenzungen werden IP-Adressen mit Schlüssel gehasht.
ALL-INKL-Zugriffslogs können zusätzlich IP-Adressen und Callback-URLs enthalten.
Abgelaufene Autorisierungsentwürfe werden beim nächsten Dienstaufruf entfernt.

Das lokale Startlog enthält Programm-/Build- und Assembly-Version, Betriebssystem,
Plattform, Prozess-/OS-Architektur, Oberfläche, .NET-Auslieferung, Runtime und RID.
Das aktuelle WPF-Programm ist eine Windows-Anwendung.

## Nutzungsstatistik

**Einstellungen → Programm → Anonyme Nutzungsstatistik senden** ist standardmäßig
aktiviert. Ohne bestätigte Auswahl erklärt ein Startfenster Zweck und Daten.
Erst nach der Entscheidung wird gegebenenfalls gesendet.
**Aktiviert lassen**, **Deaktivieren** und Schließen/Esc speichern die Entscheidung;
Schließen/Esc lässt die Statistik aktiviert. Ein sichtbarer Countdown schließt das Fenster nach zehn Sekunden ebenfalls mit aktivierter Statistik. Nur **Deaktivieren** schaltet sie aus. Bereits gespeicherte Entscheidungen bleiben erhalten.

Die Auswahl steht als `SendAnonymousUsageStatistics` und
`UsageStatisticsChoiceConfirmed` in der Programmeinstellungsdatei.
Ein bisher automatisch gespeichertes `true` ohne Bestätigung löst die Frage einmalig aus.

Über HTTPS werden abgeleitete Kennung, Version/Build, Betriebssystem/Plattform,
Prozess-/OS-Architektur, WPF/Avalonia, .NET-Auslieferung, Runtime und RID gesendet.
Anzeigename, ursprüngliche Windows-Kennung, Pfade, Dateien und Logs sind kein Teil
der Statistikmeldung. Der Zweck ist die Planung von Updates, Kompatibilität und Tests.

Es gibt höchstens einen Startversuch je UTC-Tag; nach einem Buildwechsel sofort
beim nächsten Start. Auch fehlgeschlagene Versuche zählen zum Tageslimit.
Der lokale Status liegt unter
`%LOCALAPPDATA%/NxFileViewerFork/usage-statistics.json`; eine Lockdatei verhindert
doppelte Versuche gleichzeitig gestarteter Instanzen desselben Benutzerprofils.
Der Dienst hat zehn Sekunden Timeout; ein Ausfall verhindert den Programmstart nicht.

Der Server speichert je interner Kennung den letzten Stand und zusätzliche Tagesstände.
Jede Kennung zählt im eingestellten Aktivitätsfenster einmal mit ihrer letzten Umgebung.
Abschalten beendet neue Meldungen; vorhandene Daten bleiben gespeichert und fallen
nach Ablauf des Fensters ohne neue Meldung aus der aktuellen Auswertung.

`statistics_days` in `store/config.php` steuert Admin-Auswertung, Badges, Balken
und Verläufe gemeinsam. Standard: 30 Tage; beispielsweise 120 einstellen.
Ein Verlauf zeigt höchstens so viele UTC-Tagespunkte wie eingestellt und tatsächlich
verfügbar. Pro Punkt wird der letzte bekannte Stand im damaligen Aktivitätsfenster gezählt.
Bis zu sechs Kategorien plus Sammellinie werden dargestellt.

Die Historie beginnt mit ihrer Erfassung. Aus einem früher gespeicherten letzten
Stand lässt sich kein vollständiger früherer Verlauf rekonstruieren.
Öffentlich erscheinen nur Gruppenzahlen, keine Kennungen, Namen oder detaillierten
Systembeschreibungen.

## Grafiken und Badges

Alle folgenden Pfade beginnen mit `https://nxfile.sandmaennchen5.de`.
Die Kategorien sind `versionen`, `betriebssysteme`, `architekturen`,
`oberflaechen` und `dotnet`.

| Darstellung | Beispielpfad |
| --- | --- |
| Balken | `/statistik/architekturen.svg` |
| Verlauf einer Kategorie | `/statistik/verlauf/architekturen.svg` |
| Installationsverlauf | `/statistik/verlauf/installationen.svg` |
| Ausführliches Installations-Badge | `/statistik/badges/installationen.svg` |
| Kurzes „Installs“-Badge | `/statistik/badges/installs.svg` |
| Einzelwert, z. B. x64 | `/statistik/badges/architekturen.svg?wert=x64` |
| Kombi-Badge, z. B. x64: 1 · x86: 2 | `/statistik/badges/architekturen.svg?kombiniert=1` |
| Anzahl unterschiedlicher Kategorien | `/statistik/badges/architekturen.svg` |

„Installs“ und das ausführliche Installations-Badge zählen dieselben **aktiven**
Installationen im eingestellten Zeitraum, nicht alle jemals gesehenen Systeme.
Kombi-Badges enthalten alle vorhandenen Gruppen und werden bei vielen Gruppen breit.
Die öffentliche Übersicht bietet passende Links einschließlich kodierter Einzelwerte.

Beispiel zum Einbetten in eine GitHub-README:

```markdown
[![Installs](https://nxfile.sandmaennchen5.de/statistik/badges/installs.svg)](https://nxfile.sandmaennchen5.de/statistik)
![Architekturen](https://nxfile.sandmaennchen5.de/statistik/badges/architekturen.svg?kombiniert=1)
![Versionen](https://nxfile.sandmaennchen5.de/statistik/versionen.svg)
```

Die Übersicht aktualisiert sich alle fünf Minuten. Grafiken werden beim Abruf
mit aktuellen Daten erzeugt; GitHub und andere Bild-Caches können verzögert aktualisieren.
Alle Zeitraumbeschriftungen verwenden `statistics_days`; der frühere feste
„30 Tage“-Text in der Balken-Unterzeile ist korrigiert.

## Grafiktexte ändern

Bearbeite **`store/statistics-texts.json`** als UTF-8. Nur Werte ändern und
Schlüssel/JSON-Struktur erhalten. Die Datei enthält keine Zugangsdaten.

| Schlüssel | Inhalt |
| --- | --- |
| `app_name` | Programmname in Grafiktiteln |
| `chart_titles` | Kategorienamen und Überschriften |
| `labels` | Optionale Umbenennungen, z. B. `"x64": "64-Bit"` |
| `badges` | Installations-, Einzel- und Kombi-Badge-Texte, Trenner, leere Ergebnisse |
| `bars` | Titel, Beschreibung, Unterzeile, Fußzeile und Leerhinweis |
| `trends` | Verlaufstexte, Sammelkategorie und Leerhinweis |

Die vorhandenen Vorlagen zeigen die jeweils verfügbaren Platzhalter:
`{app}`, `{title}`, `{days}`, `{total}`, `{updated}`, `{start}`, `{label}`, `{count}`.
Texte werden für SVG/HTML maskiert. Einzelwert-URLs verwenden weiterhin den
Originalwert, z. B. `?wert=x64`, auch bei umbenannter Beschriftung.
Ungültiges JSON oder fehlende erforderliche Schlüssel können die Darstellung verhindern.

## Neue Datenbank initialisieren

Dieses Feedbacksystem startet mit einer **neuen leeren Datenbank**. Es gibt keine
Versions-Upgrades, ALTER-TABLE-Migrationen oder Werkzeuge zur Übernahme früherer
Testdaten. Die Tabellen werden beim ersten Zugriff automatisch angelegt.

Für MariaDB im KAS eine leere Datenbank anlegen, die Zugangsdaten in der privaten
`store/config.php` eintragen und `database_driver` auf `mariadb` setzen. Für SQLite
auf `sqlite` stellen und einen neuen Dateipfad außerhalb von `public/` verwenden.
Vorhandene Testtabellen werden nicht automatisch geändert oder gelöscht.

Vor dem Start mit der neuen Datenbank die bisherigen Testdaten bei Bedarf sichern.
Die privaten Schlüssel und GitHub-App-Einstellungen können erhalten bleiben.
Beim ersten Admin-Aufruf entsteht Benutzer `admin` mit dem konfigurierten
Startpasswort. Weitere Benutzer und Umfragen anschließend neu anlegen.
Alte Serverdateien `store/migrate-to-mariadb.php` und `store/export-mariadb.php`
werden nicht mehr benötigt und können aus dem privaten Testbestand entfernt werden.

## Updates, Sicherungen und Fehlerbehebung

Bei Updates zuerst die aktuellen privaten Bibliotheken aus `store/`, danach
alle Dateien aus `public/` einschließlich `.htaccess` und `admin/` hochladen.
Zusammengehörige Dateien gemeinsam aktualisieren; beispielsweise benötigt
`report.php` auch `store/github.php`.

**Vorhandene `config.php`, Schlüssel, Datenbanken und eigene Grafiktexte nicht
mit Vorlagen überschreiben.** Benötigte neue Konfigurationseinträge gezielt ergänzen.

| Speicherort | Inhalt / Sicherung |
| --- | --- |
| `store/config.php`, `github-app.pem` | Zugangsdaten und dauerhafte Schlüssel; privat sichern |
| MariaDB oder `store/feedback.sqlite` | Support-Zuordnung, Stimmen, Statistik, Tageshistorie und private Log-BLOBs |
| `store/statistics-texts.json` | Eigene Grafiktexte; vor Updates sichern |
| `store/github-issues-cache.json` und `.lock` | Automatischer Statuscache; keine Tokens oder Original-Logs |

Private Konfiguration, PEM-Schlüssel, SQLite-Dateien, SQL-Exporte und GitHub-Cache
werden durch `store/.gitignore` aus Git ausgeschlossen. `store/` darf trotzdem
niemals öffentlich erreichbar sein.

| Problem | Prüfen |
| --- | --- |
| /umfragen oder /statistik liefert 404 | Subdomain-Ziel, vollständiger Upload und versteckte `.htaccess` |
| Dienst liefert 503 | Wartungsmodus, Datenbankzugang, PHP-Erweiterungen und vollständige private Bibliotheken |
| Grafik nach Textänderung ausgefallen | Gültiges JSON und erhaltene Schlüssel in `statistics-texts.json` |
| GitHub-Status unbekannt/veraltet | Zeit/Warnung im Adminbereich, App-Zugang, Schreibrechte für Cache; fünf Minuten Abgleichintervall |
| Bild zeigt älteren Zeitraum | Aktuelle Dateien hochladen, `statistics_days` prüfen und externen Bild-Cache berücksichtigen |

## Lokale Tests

PHP mit PDO SQLite sowie Python verwenden. Die Tests arbeiten mit temporären
Daten; die HTTP-Tests verwenden einen lokalen Testserver und senden nichts an GitHub.

Vom Repository-Hauptverzeichnis:

```sh
php tools/feedback/tests/support-admin-test.php tools/feedback
python tools/feedback/tests/support-admin-http-test.py PFAD/ZU/php.exe
python tools/feedback/tests/statistics-graphics-test.py PFAD/ZU/php.exe
```

Zusätzliche Admin-Tests (temporäre SQLite-Daten, keine produktive Konfiguration):

```sh
php tools/feedback/tests/admin-security-test.php tools/feedback
php tools/feedback/tests/support-overview-test.php tools/feedback
python tools/feedback/tests/admin-security-http-test.py PFAD/ZU/php.exe
```

Der HTTP-Test simuliert TLS ausschließlich in seinem lokalen Test-Router; die
Apache-Umleitung auf dem echten Webspace wird dadurch nicht getestet.
Optional kann admin-mariadb-test.py mit PHP-Pfad und Pfad einer vorhandenen
portablen MariaDB-Laufzeit ausgeführt werden. Es startet eine temporäre Datenbank
nur auf 127.0.0.1 und verändert keine vorhandenen Datenbanken oder Dienste.

Benutzer- und 2FA-Tests (zusätzlich PHP OpenSSL aktivieren):

```sh
php tests/admin-accounts-test.php
```

Der HTTP-Test enthält auch SHOW-Sperren bei direkten POSTs, verpflichtende 2FA,
Sitzungswiderruf, einmalige Wiederherstellungscodes und den Schutz des letzten Admins.

Warteschlangen-Tests mit temporären Daten und simulierten GitHub-Ausfällen:

```sh
php tests/feedback-queue-test.php
python tests/feedback-http-test.py /pfad/zu/php
```

Der PHP-Test benötigt PDO SQLite und OpenSSL; derselbe Test läuft im isolierten
MariaDB-Test ebenfalls. Der HTTP-Test ersetzt GitHub ausschließlich in seiner
privaten temporären Kopie und prüft Archiv, Berechtigungen, OAuth-Ausfälle,
Idempotenz, private Original-Logs und den geschützten Cron-Aufruf.

Zweisprachige Umfragen testen:

```sh
php tests/poll-translations-test.php
```

Der HTTP-Test prüft zusätzlich die Sprachauswahl, öffentliche und Admin-Oberflächen,
beide Textfassungen, fehlende Übersetzungen, gleiche Stimmen, Übersetzungsbearbeitung,
SVG-Ergebnisse und sprachabhängige Statistik-Badges.

Umfragebearbeitung und Teilnahmezustände testen:

```sh
php tests/polls-test.php
```

Geprüft werden Stimmenerhalt beim Entfernen, neue Antworten, zurückgewiesene
ungültige Bearbeitungen, App-Sichtbarkeit und Ergebnis-Maskierung, optionale
Erinnerungen, Nichtteilnahme und Rücknahme sowie private Freitext-Antworten.
Der HTTP-Test prüft dieselben Abläufe mit Berechtigungen und Ausgabemaskierung.
Die App-Tests prüfen außerdem die Erinnerungslogik und Themewechsel der Auswahltexte.
