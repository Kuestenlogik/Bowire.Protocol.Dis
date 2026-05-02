# End-to-end smoke test

Verifies the whole chain: **pack Bowire → install plugin → run mock → see PDUs on UDP multicast**. Takes about 90 seconds the first time (most of it NuGet restore), <10 s on repeat runs.

## Prerequisites

- Windows / Linux / macOS with the .NET SDK matching
  [`global.json`](global.json).
- Bowire main repo checked out as a sibling directory
  (`../Bowire`).
- `bowire` CLI installed as a global tool — from the main repo:
  ```bash
  dotnet tool install --global --add-source ./artifacts/packages bowire
  ```
  Or build-and-run without installing:
  `dotnet run --project ../Bowire/src/Kuestenlogik.Bowire.Tool` plus the
  subcommand.

## Steps

### 1. Pack Bowire core + this plugin

```bash
# in ../Bowire
dotnet pack -c Release

# in this repo
dotnet pack -c Release
```

Produces `.nupkg` files under each repo's `artifacts/packages/`.

### 2. Install the DIS plugin

```bash
bowire plugin install Kuestenlogik.Bowire.Protocol.Dis \
    --source ./artifacts/packages \
    --source ../Bowire/artifacts/packages
```

Verify it landed:

```bash
bowire plugin list --verbose
```

Expected: `Kuestenlogik.Bowire.Protocol.Dis` listed with version `0.9.4`, the
plugin DLL visible in `~/.bowire/plugins/Kuestenlogik.Bowire.Protocol.Dis/`.

### 3. Start the multicast listener

In terminal **A**:

```powershell
pwsh ./tools/Receive-DisMulticast.ps1
```

Blocks on the socket. Any packets the mock broadcasts show up here.

### 4. Run the mock with the sample recording

In terminal **B**:

```bash
bowire mock --recording ./samples/convoy.bowire-recording.json --port 0
```

`--port 0` lets the OS pick an HTTP port for the normal mock surface
(we don't hit it for DIS — traffic leaves via the emitter's UDP socket).

### 5. Observe

Terminal **A** should print three lines like

```
#1   144  bytes  EntityState   06 01 01 01 00 00 00 00 00 90 00 00 00 01 00 01...
#2   144  bytes  EntityState   06 01 01 01 00 00 00 00 00 90 00 00 00 01 00 01...
#3   144  bytes  EntityState   06 01 01 01 00 00 00 00 00 90 00 00 00 01 00 01...
```

- Length 144 is the canonical Entity State PDU size.
- `06 01 01 01` is the DIS header: protocol version 6, exercise id 1,
  PDU type 1 (Entity State), protocol family 1 (Entity Information).

Ctrl+C in both terminals to tear down.

## What this confirms

| Component                         | Exercised |
|-----------------------------------|-----------|
| `dotnet pack` of core Bowire     | yes       |
| `dotnet pack` of external plugin  | yes       |
| `bowire plugin install` flow     | yes       |
| Plugin ALC load at `bowire mock` startup | yes |
| `IBowireMockEmitter.CanEmit` + `StartAsync` wiring | yes |
| DIS plugin emitter → UDP multicast | yes       |
| Recording → wire bytes parity     | yes       |

## Regenerating the sample

`samples/convoy.bowire-recording.json` is committed, but reproducible
from [`tests/.../SampleFixtureGenerator.cs`](tests/Kuestenlogik.Bowire.Protocol.Dis.Tests/SampleFixtureGenerator.cs).
Un-skip the `[Fact]`, run `dotnet test --filter SampleFixtureGenerator`,
re-skip, commit the regenerated JSON.
