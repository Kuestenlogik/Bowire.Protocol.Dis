# Kuestenlogik.Bowire.Protocol.Dis

Bowire protocol plugin for **IEEE 1278.1 Distributed Interactive
Simulation (DIS)**.

Two paths into a DIS multicast group:

- **Live discovery + entity-scoped streaming** — point Bowire at a
  `dis://group:port` URL and the workbench probes the group, surfaces
  every active entity as a service, and streams typed PDU envelopes
  (filtered to that entity, or to the whole exercise feed) into the
  workbench.
- **Mock replay** — `DisMockEmitter` plugs into Bowire's mock server
  so a captured PDU sequence can be re-broadcast on UDP at the original
  cadence. Use it for offline testing without a live exercise.

## URL shapes

```
dis://239.1.2.3:3000          # standard multicast group + DIS port
dis://multicast               # shortcut for 239.1.2.3 (default port)
239.1.2.3:3000                # bare host:port also accepted
```

The default group is `239.1.2.3` and the default port is `3000`
(IEEE 1278 convention). The probe duration during discovery is
configurable via the plugin's `probeDuration` setting (defaults to 3
seconds — long enough to catch a heartbeat from every active entity,
short enough that the workbench feels responsive).

## Live discovery

Standalone:

```bash
bowire --url dis://239.1.2.3:3000
```

Embedded:

```csharp
app.MapBowire(options =>
{
    options.ServerUrls.Add("dis://239.1.2.3:3000");
});
```

`DiscoverAsync` joins the multicast group for the configured probe
window, observes every EntityState PDU it sees, and surfaces:

- one synthetic **`Exercise`** service whose `monitor` stream yields
  every PDU on the group (raw exercise feed)
- one service per discovered entity, named after its `EntityId`
  (`<site>:<application>:<entity>`) plus marking + entity-type info,
  whose `monitor` stream yields only PDUs from that entity

Subscribing to a service opens a UDP socket on the same group and
yields one JSON envelope per matched PDU. The envelope decodes the
PDU header (kind, family, exercise id, timestamp) plus EntityState
specifics (position, orientation, velocity, marking string, force id)
when applicable; other PDU kinds surface header + raw bytes so they
can still be hex-dumped in the workbench.

## Mock replay

`DisMockEmitter` plugs into Bowire's mock server via
`IBowireMockEmitter`. Recordings tagged `protocol: "dis"` get
re-broadcast on the configured multicast group at the original
cadence:

| Metadata key (first DIS step) | Purpose | Default |
|-------------------------------|---------|---------|
| `multicast-group`             | UDP multicast destination | `239.1.2.3` |
| `port`                        | UDP port | `3000` |
| `ttl`                         | Multicast TTL — `1` keeps it on the local subnet | `1` |

Each step carries:

- `protocol: "dis"`
- `responseBinary` — base64 of the raw PDU bytes (IEEE 1278 wire format)
- `capturedAt` — millisecond timestamp used for emission pacing
- optional `metadata` (only on the first DIS step — applied to the
  whole DIS sub-sequence)

Use `bowire mock --recording my-exercise.bowire-recording.json`
to start replay. `--loop` re-emits the sequence on repeat.

## Sample

A runnable end-to-end sample lives under [`samples/Kuestenlogik.Bowire.Protocol.Dis.Sample`](samples/Kuestenlogik.Bowire.Protocol.Dis.Sample) — an ASP.NET Core host that replays the bundled `convoy.bowire-recording.json` capture as a live multicast stream so the DIS tab has something deterministic to subscribe to without a running exercise. Loops on EOF so the feed never goes quiet.

```bash
dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sample
```

Then point Bowire's DIS tab at the multicast group + port the replayer logs at startup (defaults to `dis://239.1.2.3:3000`).

## Install

```bash
dotnet tool install -g Kuestenlogik.Bowire.Tool
bowire plugin install Kuestenlogik.Bowire.Protocol.Dis
```

## Local development

This plugin lives in its own repo so the external-plugin distribution
pipeline gets exercised end-to-end. For local dev against an
unpublished core Bowire:

1. `cd ../Bowire && dotnet pack -c Release` — produces `.nupkg`s for
   `Kuestenlogik.Bowire` and `Kuestenlogik.Bowire.Mock` under `artifacts/packages/`.
2. This repo's [`nuget.config`](nuget.config) adds that directory as
   a package source, so `dotnet restore && dotnet build && dotnet test`
   here picks up the freshly-packed contract packages.
3. Smoke-test end-to-end by dropping the built plugin DLL into
   `~/.bowire/plugins/Kuestenlogik.Bowire.Protocol.Dis/` (or wherever
   `bowire --plugin-dir` points) and running either
   `bowire --url dis://239.1.2.3:3000` for live discovery or
   `bowire mock --recording dis-sample.json` for replay.

Once Bowire's core packages are on nuget.org, the local feed in
`nuget.config` becomes redundant — consumers resolve the dependencies
directly and this repo becomes a pure NuGet package.

## Status

- **Live discovery + entity-scoped PDU streaming**: shipped.
  EntityState PDUs decode markings, entity types, force ids; other PDU
  kinds surface header + raw bytes for hex-dumping.
- **Mock replay**: shipped (raw byte replay only — the emitter doesn't
  re-decode; it ships the captured bytes verbatim).
- **Composition with the [UDP plugin](https://github.com/Kuestenlogik/Bowire.Protocol.Udp)**:
  run both at once for typed (`dis`) plus raw-bytes (`udp`) views of
  the same group.

## License

Apache-2.0 — see [LICENSE](LICENSE).
