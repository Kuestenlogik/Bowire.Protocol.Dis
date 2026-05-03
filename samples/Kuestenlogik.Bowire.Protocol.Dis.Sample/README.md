# Sample — DIS convoy replay

A small ASP.NET Core host that replays the bundled
[`convoy.bowire-recording.json`](../convoy.bowire-recording.json) capture
as a live DIS multicast stream so the Bowire workbench's **DIS** tab has
something real to subscribe to without a running exercise.

```
convoy.bowire-recording.json ──► ConvoyReplayer ──UDP/multicast──► DIS tab
```

The recording carries three `EntityState` PDUs of an M1 tank stepping
~100 m per update. The replayer paces them by their captured
`capturedAt` offsets and **loops on EOF** so the stream never goes
silent — every run shows the same convoy events, deterministically.

## Run

1. **Start the replayer**

   ```sh
   dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sample
   ```

   On startup the host logs the multicast group + port + capture span,
   e.g.:

   ```
   ConvoyReplayer streaming 3 PDUs from "dis convoy sample" →
   udp://239.1.2.3:3000 (ttl=1, captureSpan=00:00:00.2000000, looping on EOF).
   Point Bowire's DIS tab at dis://239.1.2.3:3000 to observe.
   ```

2. **Subscribe in Bowire**

   Open the workbench at <http://localhost:5080/bowire> (or run the
   `bowire` CLI), pick the **DIS** tab, and subscribe to the multicast
   group the replayer logged — `dis://239.1.2.3:3000` by default.

3. **Watch the convoy**

   The DIS tab probes the group, surfaces the M1 entity as a service,
   and streams typed `EntityState` envelopes (position + orientation +
   marking) into the frame pane. Other PDU kinds, if any, fall through
   to the hex-dump view.

## Notes

- **Data source**: [`convoy.bowire-recording.json`](../convoy.bowire-recording.json)
  is already in the repo — no extra download. The replay is
  deterministic by design.
- **No DIS encoder dependency**: the replayer ships the captured PDU
  bytes verbatim over `System.Net.Sockets`. We don't synthesise.
- **Multicast loopback** is enabled (`MulticastLoopback=true` plus
  `IPProtectionLevel.Unrestricted`) so the workbench can receive its own
  emissions on a single machine.
- **Windows firewall** may prompt on first run — UDP multicast on port
  3000. Allow it for the sample to be visible to local subscribers.
- **Network config** comes from the recording's first DIS step's
  `metadata` (`multicast-group`, `port`, `ttl`) and falls back to
  `239.1.2.3:3000 / ttl 1` (IEEE 1278 convention) when absent.
