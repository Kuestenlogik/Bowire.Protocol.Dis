# DIS-Sandbox: Design

Ticket: [#67](https://github.com/Kuestenlogik/Bowire.Protocol.Dis/issues/67), Milestone v1.2. Folgeticket im Plugin: [#68](https://github.com/Kuestenlogik/Bowire.Protocol.Dis/issues/68).

## Zweck

Ein Testprogramm, mit dem man DIS in Bowire von Hand antreibt. Das vorhandene Sample spielt nur eine feste Aufnahme ab. Mit der Sandbox legt man Objekte auf einer Karte an, bewegt sie und ändert ihren Zustand, und sieht in Bowires DIS-Tab, was davon ankommt. Nebenbei prüft sie Bowires Decoder gegen einen unabhängigen Encoder.

Bowire ist dabei der Empfänger. Die Sandbox sendet nur.

## Entscheidungen

| Frage | Entscheidung | Grund |
|---|---|---|
| Ort | `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox` | Eigenständiges Sample, wird in nichts eingebettet. |
| UI-Stack | Blazor Web App, Interactive Server | Der übliche Stack. UDP kann nur der Server senden, WebAssembly hat keine Sockets. |
| Karte | MapLibre GL JS über JS-Interop | Wie Terracle. |
| Kodieren | OpenDisNet (NuGet, MIT, DIS v7) | Fremder Encoder: ein Fehler, den Encoder und Decoder teilen, bleibt nicht unsichtbar. Das Sample referenziert das Plugin nicht. |
| Anzahl Objekte | Beliebig viele | Testet Bowires Entity-Liste und mehrere Spuren. |
| Key-Value-Paare | Comment-PDU (Typ 22), ein Variable Datum Record pro Paar | Die Comment-PDU ist für beliebige Nachrichten gedacht. Die Data-PDU antwortet auf Data Query bzw. Set Data. |
| Datum als Text in Bowire | Nein | SISO-REF-010 nennt für Datum-IDs keinen Datentyp. Eine Darstellung als Text wäre geraten. #68 zeigt nur die SISO-Namen der IDs. |

## Aufbau

```
Browser (MapLibre)  ──JS-Interop──►  Blazor-Komponenten  ──►  EntityStore  ──Events──►  DisPublisher  ──UDP──►  Bowire
```

### `EntityStore` (Singleton)

Hält die Objekte: Entity-Nummer, Name, Typ-Voreinstellung, Force, Lat/Lon/Höhe, Kurs, Geschwindigkeit und eine geordnete Liste von Key-Value-Paaren. Operationen: `Add`, `Move`, `Update`, `SetPairs`, `Remove`. Jede Operation löst ein `Changed`-Event mit Objekt und Art der Änderung aus. Der Store kennt weder Karte noch DIS.

Entity-Nummern zählen ab 1 hoch. Eine gelöschte Nummer wird in derselben Sitzung nicht neu vergeben, sonst hielte Bowire ein neues Objekt für das alte.

Der Store ist threadsicher (ein Lock), weil Blazor-Circuits und der Heartbeat-Timer gleichzeitig zugreifen.

### `DisPublisher` (Hosted Service)

Hört auf `Changed` und sendet sofort:

| Änderung | PDUs |
|---|---|
| Angelegt, verschoben, bearbeitet | `EntityState` |
| Key-Value-Paare geändert | `Comment` mit allen Paaren des Objekts |
| Gelöscht | `EntityState` mit Deactivated-Bit, danach nichts mehr |

Zusätzlich sendet ein Heartbeat alle `HeartbeatSeconds` (Default 5) für jedes Objekt einen `EntityState` und, wenn es Paare hat, eine `Comment`-PDU. Ein Empfänger, der spät dazukommt, hat so nach einem Heartbeat den vollständigen Stand.

Kodiert wird mit OpenDisNet (`DisSerializer.Serialize`), gesendet mit einem `UdpClient` an das konfigurierte Ziel.

### `GeoConversion` (statisch)

- Lat/Lon/Höhe (WGS84) in geozentrische Koordinaten und zurück.
- Kurs, Nick- und Rollwinkel im lokalen Nord-Ost-Unten-System in die DIS-Euler-Winkel ψ/θ/φ (IEEE 1278.1, Anhang zu Koordinatensystemen) und zurück.
- Eine Geschwindigkeit über Grund (Betrag und Kurs) in einen geozentrischen Geschwindigkeitsvektor.

Reine Mathematik ohne Abhängigkeiten.

### UI

Eine Seite: Karte links, Seitenleiste rechts, Statuszeile oben.

- **Karte** (`wwwroot/sandbox-map.js`, dünn): Basemap-Umschalter, ein Marker pro Objekt (Farbe nach Force, Beschriftung mit Name), Drag, Modus *Maus folgen*. Meldet an Blazor nur zwei Dinge: „Klick bei Lat/Lon“ und „Objekt N nach Lat/Lon bewegt“. Blazor schickt Marker-Änderungen zurück.
- **Seitenleiste:** Objektliste (Auswahl per Klick in Liste oder auf Marker) und Editor für das ausgewählte Objekt: Name (mit Anzeige der 11 Zeichen, die als Marking gesendet werden), Typ, Force, Höhe, Key-Value-Tabelle (hinzufügen, ändern, entfernen), Knopf *Maus folgen*, Knopf *Löschen*.
- **Statuszeile:** Ziel, Anzahl gesendeter PDUs, letzter Fehler.

Ein Objekt anlegen: Knopf *Neues Objekt*, dann Klick auf die Karte. Es bekommt einen Default-Namen (`Objekt 3`), den gewählten Typ und Force *eigene Kräfte*.

**Maus folgen:** Das ausgewählte Objekt folgt dem Cursor. Ein Klick setzt es ab, Esc bricht ab und setzt es ab, wo es gerade ist. Während des Folgens sendet die Sandbox höchstens 10 `EntityState` pro Sekunde. Kurs und Geschwindigkeit werden aus den letzten beiden Positionen berechnet. Beim Absetzen geht ein letzter `EntityState` mit Geschwindigkeit 0 und Dead Reckoning *statisch* raus.

**Drag:** setzt um, ohne Geschwindigkeit. Ein `EntityState` beim Loslassen, keiner währenddessen.

### Basemaps

Aus `Terracle.Map/Services/BasemapOptions.cs`, ohne API-Key:

| Id | Art | Quelle |
|---|---|---|
| `dark` (Default) | Style | `https://tiles.openfreemap.org/styles/dark` |
| `positron` | Style | `https://tiles.openfreemap.org/styles/positron` |
| `liberty` | Style | `https://tiles.openfreemap.org/styles/liberty` |
| `osm` | Raster | `https://tile.openstreetmap.org/{z}/{x}/{y}.png` |
| `satellite` | Raster | Esri World Imagery |
| `topo` | Raster | Esri World Topo Map |
| `blank` | Farbe | keine Quelle; Rückfall, wenn nichts lädt |

Die Liste steht in `appsettings.json` und lässt sich dort ergänzen. Attributionen werden wie in Terracle mitgeführt.

## Abbildung auf DIS v7

Codes nach SISO-REF-010 v37 (2026-05-25).

### `EntityState`-PDU (Typ 1)

| Feld | Wert |
|---|---|
| Header | Protocol Version 7, Exercise-ID aus der Konfiguration, Timestamp relativ |
| Entity-ID | Site und Application aus der Konfiguration, Entity = Nummer des Objekts |
| Force-ID | 1 eigene, 2 gegnerische, 3 neutrale Kräfte |
| Entity-Typ | aus der Voreinstellung, siehe unten |
| Linear Velocity | geozentrisch; im Stand und nach Drag 0 |
| Location | geozentrisch aus Lat/Lon/Höhe |
| Orientation | ψ/θ/φ aus Kurs, Nick 0, Roll 0 |
| Appearance | 0; beim Löschen Bit 23 (*State*: deaktiviert) gesetzt. Bit 23 ist für Land-, Luft-, See- und Lebensformen gleich. |
| Dead Reckoning | 1 *Static* im Stand, 2 *DRM (FPW)* während Maus folgen |
| Marking | Character Set 1 (ASCII), Name auf 11 Zeichen gekürzt, Nicht-ASCII durch `?` ersetzt |
| Capabilities | 0 |
| Articulation Parameters | keine |

Entity-Typen (Kind.Domain.Country.Category.Subcategory, Country 78 = Deutschland):

| Voreinstellung | Code | SISO |
|---|---|---|
| Kampfpanzer | 1.1.78.1.0 | Platform, Land, Tank |
| LKW | 1.1.78.7.0 | Platform, Land, Large Wheeled Utility Vehicle |
| Hubschrauber | 1.2.78.21.0 | Platform, Air, Utility Helicopter |
| Flugzeug | 1.2.78.4.0 | Platform, Air, Cargo/Tanker |
| Schiff | 1.3.78.50.0 | Platform, Surface, Frigate (including Corvette) |
| Person | 3.1.78.11.0 | Life form, Land, Army |

Höhe: Land- und Lebensform-Typen werden auf 0 m über dem Ellipsoid gesetzt, Luft-Typen bekommen per Default 500 m. Die Höhe ist im Editor änderbar.

### `Comment`-PDU (Typ 22)

| Feld | Wert |
|---|---|
| Originating Entity | Entity-ID des Objekts |
| Receiving Entity | `0xFFFF:0xFFFF:0xFFFF` (alle) |
| Fixed Datum Records | keine |
| Variable Datum Records | einer pro Paar |

Jeder Variable Datum Record:

- **Datum-ID:** `CommentDatumId` aus der Konfiguration, Default `900000`. In SISO-REF-010 v37 ist im Bereich 900000–999999 keine ID vergeben. Einen Bereich für frei definierte IDs gibt es nicht; die README sagt deshalb, dass das eine Konvention der Sandbox ist und kein Standard.
- **Wert:** `key=value` als UTF-8.
- **Länge:** in Bits (Bytes × 8); der Wert wird auf ein Vielfaches von 64 Bit mit Nullbytes aufgefüllt.

Ein gelöschtes Paar fehlt in der nächsten Comment-PDU. Hat ein Objekt keine Paare mehr, geht einmal eine Comment-PDU ohne Records raus, damit der Empfänger den Wechsel sieht. Danach sendet der Heartbeat für dieses Objekt keine Comment-PDU mehr.

## Konfiguration

`appsettings.json`, Abschnitt `Sandbox`:

| Schlüssel | Default |
|---|---|
| `Target` | `239.1.2.3:3000` (wie das Replay-Sample) |
| `Ttl` | 1 |
| `ExerciseId` | 1 |
| `SiteId` | 1 |
| `ApplicationId` | 3001 |
| `HeartbeatSeconds` | 5 |
| `FollowMaxUpdatesPerSecond` | 10 |
| `CommentDatumId` | 900000 |
| `InitialView` | Lat 53.55, Lon 9.99, Zoom 11 (Hamburg) |
| `Basemaps` | Liste wie oben |

Die Sandbox prüft die Konfiguration beim Start (gültiger Endpunkt, IDs im Bereich 0–65535 bzw. 0–255 für Exercise, Heartbeat > 0) und bricht mit einer klaren Meldung ab, wenn etwas nicht stimmt.

## Fehlerbehandlung

- **Senden schlägt fehl** (Netzwerkkarte weg, Multicast nicht erlaubt): loggen, Zähler und letzter Fehler in der Statuszeile, weitermachen. Weder UI noch Heartbeat halten an.
- **Kodieren schlägt fehl:** das betroffene Objekt wird in der Liste markiert, die übrigen senden weiter.
- **Zu große Paare:** Der Editor zeigt die Länge der künftigen Comment-PDU und lehnt eine Änderung ab, die sie über 1400 Byte brächte. So passt jede PDU in ein UDP-Paket ohne Fragmentierung.
- **Basemap lädt nicht:** MapLibre zeigt leere Kacheln, alles andere läuft weiter. `blank` ist der Rückfall.
- **Circuit bricht ab:** Store und Publisher leben im Server, Objekte und Heartbeat bleiben. Nach dem Wiederverbinden zeigt die Seite den aktuellen Stand. Ein Objekt, das gerade der Maus folgte, bleibt an der letzten Position stehen und wird auf Geschwindigkeit 0 gesetzt.

## Tests

Neues Testprojekt `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests` (xUnit v3 unter Microsoft.Testing.Platform, wie die übrigen Tests im Repo). Es referenziert die Sandbox und das Plugin.

- **`GeoConversion`:** Referenzpunkte (0°/0°, Nordpol, Hamburg) gegen bekannte geozentrische Werte auf mm; Hin- und Rückrechnung; Kurs 0°, 90° und 180° an zwei Orten gegen die erwarteten Euler-Winkel.
- **`EntityStore`:** Anlegen, Verschieben, Bearbeiten, Paare setzen und Löschen lösen die richtigen Events aus; gelöschte Nummern werden nicht neu vergeben.
- **Gegen den Decoder des Plugins** (der eigentliche Zweck): Der Publisher kodiert mit OpenDisNet, die Bytes gehen durch `DisPduDecoder`. Geprüft wird Feld für Feld: Entity-ID, Force, Entity-Typ, Position nach Rückrechnung auf cm, Orientierung, Geschwindigkeit, Dead Reckoning, Marking, Deactivated-Bit, sowie Datum-ID, Länge in Bits und Bytes jedes Variable Datum Records. Auch die leere Comment-PDU.
- **Publisher:** mit einer Fake-Senke statt UDP. Welche PDUs bei welcher Änderung rausgehen, dass der Heartbeat alle Objekte erfasst, und dass Maus folgen auf 10 pro Sekunde gedrosselt wird (mit einer steuerbaren Uhr).

Kein automatischer UI-Test der Karte. Drag und Maus folgen werden von Hand mit Bowires DIS-Tab geprüft; die README beschreibt die Schritte.

## README

Im Stil des Replay-Samples: starten (`dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox`), in Bowire `dis://239.1.2.3:3000` abonnieren, Objekt anlegen, bewegen, Paare setzen, löschen, und was man dabei jeweils in Bowire sehen sollte. Dazu ein Absatz zur Datum-ID-Konvention und zu den Basemaps (Online-Dienste, ohne Netz bleibt nur `blank`).

## Nicht Teil davon

- Empfang von DIS in der Sandbox.
- Speichern der Objekte über einen Neustart.
- Offline-Karten, Tile-Proxy, MIL-STD-2525-Symbole.
- UI für Exercise-, Site- und Application-ID (nur Konfiguration).
- Änderungen am Plugin; die Namen der Datum-IDs kommen mit #68.
