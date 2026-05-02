# SisoGen — SISO-REF-010 enumeration generator

One-shot generator that turns the canonical SISO-REF-010 XML into C# enums. Packaged as a `dotnet tool` so plugin authors can install and run it from any repo, not just the DIS plugin's own checkout.

## Install as a .NET tool

```bash
# Global install (available everywhere on your machine):
dotnet tool install -g Kuestenlogik.Bowire.Protocol.Dis.SisoGen

# Or as a local tool pinned to a repo:
dotnet new tool-manifest   # if you don't have a .config/dotnet-tools.json yet
dotnet tool install Kuestenlogik.Bowire.Protocol.Dis.SisoGen
```

## Run

```bash
# 1. Get SISO-REF-010.xml. Two options:
#    a) Official SISO Digital Library (login required):
#       https://www.sisostandards.org/page/ReferenceDocuments
#    b) Raw mirror maintained by the open-dis project (no login):
#       https://raw.githubusercontent.com/open-dis/opendis7-source-generator/master/xml/SISO/SISO-REF-010.xml
#
# 2. Generate enums:
siso-gen --xml path/to/SISO-REF-010.xml --out ./src/MyProject/Enums/Generated

# Local tool:
dotnet siso-gen --xml ... --out ...
```

Running the tool from inside a Bowire.Protocol.Dis checkout (i.e. when the working directory is anywhere under a folder containing `Bowire.Protocol.Dis.slnx`) auto-selects `src/Kuestenlogik.Bowire.Protocol.Dis/Enumerations/Generated` as the output. Pass `--out` explicitly to override.

Output files are committed in consumer repos. The generator is invoked only when SISO publishes a new revision — CI does not need to invoke it.

## Alternative: run from source

Checkout this repo and run without installing:

```bash
dotnet run --project tools/SisoGen -- --xml path/to/SISO-REF-010.xml
```

## Scope

Current targets (SISO uids in parentheses):

| Enum | Uid | Approx. entries | Used in |
|------|-----|-----------------|---------|
| `Country` | 29 | ~280 | `EntityType.Country`, `AggregateType.Country` |
| `EntityKind` | 7 | 10 | `EntityType.Kind` |
| `PlatformDomain` | 8 | 6 | `EntityType.Domain` (kind=1) |
| `MunitionDomain` | 14 | | `EntityType.Domain` (kind=2) |
| `WarheadType` | 60 | ~100 | `MunitionDescriptor.Warhead` |
| `FuseType` | 61 | ~100 | `MunitionDescriptor.Fuse` |
| `EmitterName` | 75 | ~2800 | `ElectromagneticEmissionSystem.EmitterName` |
| `EmitterSystemFunction` | 76 | ~80 | emission system function |
| `BeamFunction` | 78 | ~10 | EE beam function |

Enums that are already hand-typed under `src/Kuestenlogik.Bowire.Protocol.Dis/Enumerations/` (e.g. `DisPduType`, `ForceId`, `DetonationResult`, `DeadReckoningAlgorithm`, `StopFreezeReason`, `AcknowledgeFlag`, `ResponseFlag`, `ServiceTypeRequested`, `RequiredReliabilityService`) are deliberately *not* regenerated to avoid drift. Add new entries to SisoGen's `specs` array when SISO publishes an enum that's not yet covered.

## Committed output

`Enumerations/Generated/Country.g.cs` holds the full canonical Country list generated from SISO-REF-010 V35 (279 entries). Re-run the generator when SISO publishes a new revision; the output file is committed so downstream consumers don't need to run the generator themselves.
