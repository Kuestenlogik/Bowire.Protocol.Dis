# Sample — DIS sandbox

A map on which you place and move objects yourself. Every change goes out
as DIS v7 on UDP, so Bowire's **DIS** tab shows traffic you control — not
a fixed recording like the [replay sample](../Kuestenlogik.Bowire.Protocol.Dis.Sample/README.md).

```
map (MapLibre) ──► EntityStore ──► DisPublisher ──UDP/multicast──► Bowire DIS tab
```

The PDUs are encoded with [OpenDisNet](https://github.com/RejectKid/OpenDisNet),
not with this plugin's code, so the sandbox also checks Bowire's decoder
against an independent encoder.

## Run

1. **Start the sandbox**

   ```sh
   dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox
   ```

   Open <http://localhost:5185>.

2. **Subscribe in Bowire** to `dis://239.1.2.3:3000`, for example with the
   replay sample's workbench (`dotnet run --project
   samples/Kuestenlogik.Bowire.Protocol.Dis.Sample`, then `/bowire` on the
   URL it logs, by default <http://localhost:5000/bowire>). Each object
   shows up as its own source entry, e.g. `Objekt 1 (1:3001:1)` with a
   `monitor` method; *Subscribe* on it streams that object's PDUs. The
   sandbox's entities have application id 3001; the replay sample's
   convoy is `1:1:1000`.

To run two sandboxes side by side, give the second one its own application
id and port, e.g. `--Sandbox:ApplicationId=3002 --urls http://localhost:5186`.
Otherwise both number their objects from `1:3001:1` and Bowire takes them
for the same objects. The sandboxes do not receive DIS, so neither shows
the other's objects on its map.

## What to try, and what Bowire should show

| In the sandbox | In Bowire |
|---|---|
| *Neues Objekt*, click the map | an Entity State PDU, entity `1:3001:1`, marking `Objekt 1` |
| drag a marker | one Entity State with the new position, dead reckoning *Static* |
| *Maus folgen* | up to 10 Entity States a second, velocity set, dead reckoning *FPW*; a click or Esc ends with one at rest |
| rename, change type or force | an Entity State with the new marking / entity type / force |
| set key-value pairs | a Comment PDU, one variable datum per pair, value `key=value` in UTF-8 |
| clear all pairs | one Comment PDU without records |
| *Löschen* | a last Entity State with appearance bit 23 (deactivated) |
| wait | every 5 s an Entity State per object, plus a Comment where it has pairs |

## Key-value pairs: a convention, not a standard

SISO-REF-010 has no datum id for "a key of your choosing" and no range for
free use. The sandbox puts every pair under one datum id, `900000` by
default (unassigned in SISO-REF-010 v37), with the text `key=value` as the
value. Bowire shows the value as raw bytes: the datum id alone says what a
value means and how it is encoded, so a receiver that does not know the id
cannot do better.

## Configuration

`appsettings.json`, section `Sandbox`: target (`239.1.2.3:3000`), TTL,
exercise / site / application id, heartbeat, follow rate, the datum id
above, the initial view and the basemaps. Every setting can be overridden
on the command line, e.g. `--Sandbox:Target=192.168.1.255:3000`.

## Basemaps

OpenFreeMap (Dark, Positron, Liberty), OpenStreetMap, Esri World Imagery and
World Topo Map — online services without an API key, the same set as
Terracle. Without a network only *Leer* (a plain colour) works; objects and
DIS keep working either way.
