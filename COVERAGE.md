# DIS Standard Coverage

Goal: complete support for **IEEE 1278.1-1995 (DIS v6)** and **IEEE 1278.1-2012 (DIS v7)**. Every PDU type in the spec gets a handwritten .NET 10 record with marshal / unmarshal + roundtrip tests. SISO-REF-010 enumerations are code-generated from the canonical XML into the `Enumerations/Generated/` folder and committed.

Status legend:

| Symbol | Meaning |
|--------|---------|
| ✅ | Implemented + tested (marshal + unmarshal + roundtrip). |
| 🟡 | Partial — marshal only, or decoder without all records. |
| 📝 | Planned for the current phase. |
| ⏳ | Scheduled for a later phase. |

## Phase plan

| Phase | Focus | PDUs | Approx. size | Status |
|-------|-------|------|--------------|--------|
| A | Foundation: shared records, PDU base, Reader/Writer, Entity State complete. | 1 | ~1 day | ✅ |
| B | Entity Information/Interaction family. | 4 | ~1 day | ✅ |
| C | Warfare family (V6 + V7 additions). | 4 | ~1 day | ✅ |
| D | Simulation Management family. | 12 | ~2 days | ✅ |
| E | Simulation Management with Reliability family. | 15 | ~1 day (mirrors D) | ✅ |
| F | Radio Communications family. | 5 | ~1 day | ✅ |
| G | Distributed Emission Regeneration family (V6 + V7). | 4 | ~1.5 days | ✅ |
| H | Logistics family. | 6 | ~1 day | ✅ |
| I | Entity Management family. | 4 | ~1 day | ✅ |
| J | Minefield family. | 4 | ~1 day | ✅ |
| K | Synthetic Environment family. | 5 | ~1.5 days | ✅ |
| L | Live Entity family. | 5 | ~1 day | ✅ |
| M | Information Operations family (V7). | 2 | ~0.5 day | ✅ |
| N | SISO-REF-010 enumeration codegen. | — | ~1 day | ✅ |

Rough total: ~16 focused days for the full spec. Each commit lands one phase, ROADMAP + this table update together.

## Shared records

| Record | Used by | Status |
|--------|---------|--------|
| `PduHeader` (V6 / V7 variants) | every PDU | ✅ |
| `EntityId` (site, application, entity) | Entity State, Fire, Detonation, many | ✅ |
| `EventId` | Fire, Detonation, Collision | ✅ |
| `EntityType` (7-tuple) | Entity State, Aggregate State | ✅ |
| `Vector3Float` / `Vector3Double` | velocity, location, accel | ✅ |
| `EulerAngles` (psi, theta, phi) | orientation | ✅ |
| `SimulationAddress` (site, app) | SimMan headers | ✅ |
| `DeadReckoningParameters` | Entity State | ✅ |
| `EntityMarking` (charset + 11 chars) | Entity State | ✅ |
| `ClockTime` (hour, time past hour) | SimMan start/stop, DE Fire | ✅ |
| `VariableParameter` base + 5 concrete types | Entity State, Entity State Update, Detonation | ✅ |
| `MunitionDescriptor` (V7 renamed from Burst Descriptor) | Fire, Detonation | ✅ |
| `FixedDatum` / `VariableDatum` | Data / Set Data / Event Report / many SimMan + Reliability PDUs | ✅ |
| `ModulationType` (8 bytes) | Transmitter | ✅ |
| `PropulsionSystem` / `VectoringNozzleSystem` (8 bytes each) | SEES | ✅ |
| `SupplyQuantity` (12 bytes) | Service Request, Resupply Offer / Received | ✅ |
| `AggregateType` (8 bytes) | Aggregate State | ✅ |
| `AggregateMarking` (32 bytes) | Aggregate State | ✅ |
| `NamedLocationId` (4 bytes) | IsPartOf | ✅ |
| `ObjectType` (4 bytes) | Point / Linear / Areal Object State | ✅ |
| `LinearSegmentParameter` (64 bytes) | Linear Object State | ✅ |
| `UnderwaterAcousticShaft` / `UnderwaterAcousticApa` / `UnderwaterAcousticEmitterSystem` / `UnderwaterAcousticBeam` | Underwater Acoustic | ✅ |
| `TrackJamData` / `FundamentalParameterData` / `ElectromagneticEmissionBeam` / `ElectromagneticEmissionSystem` | Electromagnetic Emission | ✅ |
| `RecordSet` (16 B + records, padded) | Record-R, Set Record-R | ✅ |
| `StandardVariableRecord` (§6.2.82, 8 B header + content padded to 64-bit) | Attribute, DirectedEnergyFire, EntityDamageStatus, IO Action, IO Report, Environmental Process | ✅ |
| `AttributeRecordSet` (§6.2.12, EntityId + attribute records) | Attribute | ✅ |
| `GridAxisDescriptor` (§6.2.41, 24 B header + values padded to 64-bit) | Gridded Data | ✅ |
| `Vector2Float` (8 B: x, y) | Minefield State / Query perimeter points | ✅ |

## PDU coverage

Type ids per IEEE 1278.1-2012 §5.3 Table 5. "Ver." = version the PDU first appeared in (V6 unless noted V7).

### Family 1 — Entity Information / Interaction

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 1  | Entity State | V6 + V7 header | ✅ | Marshal + unmarshal + variable-parameter records. |
| 4  | Collision | V6 | ✅ | Full roundtrip. |
| 40 | Collision-Elastic | V7 | ✅ | Full roundtrip incl. 6-component intermediate-result matrix. |
| 67 | Entity State Update | V6/V7 | ✅ | Full roundtrip incl. variable parameters. |
| 71 | Attribute | V7 | ✅ | Full typed roundtrip incl. Attribute Record Sets (§6.2.12) and Standard Variable Records (§6.2.82). |

### Family 2 — Warfare

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 2 | Fire | V6 | ✅ | Full roundtrip incl. MunitionDescriptor. |
| 3 | Detonation | V6 | ✅ | Full roundtrip incl. variable parameters. |
| 68 | Directed Energy Fire | V7 | ✅ | Full typed roundtrip incl. DE records as `StandardVariableRecord`s (§6.2.82). |
| 69 | Entity Damage Status | V7 | ✅ | Full typed roundtrip incl. damage description records as `StandardVariableRecord`s (§6.2.82). |

### Family 3 — Logistics

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 5 | Service Request | V6 | ✅ | Typed supply-quantity list. |
| 6 | Resupply Offer | V6 | ✅ | |
| 7 | Resupply Received | V6 | ✅ | |
| 8 | Resupply Cancel | V6 | ✅ | |
| 9 | Repair Complete | V6 | ✅ | |
| 10 | Repair Response | V6 | ✅ | |

### Family 4 — Simulation Management

| ID | Name | Ver. | Status |
|----|------|------|--------|
| 11 | Create Entity | V6 | ✅ |
| 12 | Remove Entity | V6 | ✅ |
| 13 | Start / Resume | V6 | ✅ |
| 14 | Stop / Freeze | V6 | ✅ |
| 15 | Acknowledge | V6 | ✅ |
| 16 | Action Request | V6 | ✅ |
| 17 | Action Response | V6 | ✅ |
| 18 | Data Query | V6 | ✅ |
| 19 | Set Data | V6 | ✅ |
| 20 | Data | V6 | ✅ |
| 21 | Event Report | V6 | ✅ |
| 22 | Comment | V6 | ✅ |

### Family 5 — Distributed Emission Regeneration

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 23 | Electromagnetic Emission | V6 | ✅ | Full typed roundtrip incl. emitter-system / beam / fundamental-parameter / track-jam records. |
| 24 | Designator | V6 | ✅ | Full roundtrip (88 bytes) incl. ECEF spot location + dead-reckoning. |
| 29 | Underwater Acoustic | V6 | ✅ | Full typed roundtrip incl. shaft / APA / UA emitter-system / UA beam records. |
| 30 | Supplemental Emission/Entity State (SEES) | V7 | ✅ | Full roundtrip incl. typed propulsion + vectoring-nozzle lists. |

### Family 6 — Radio Communications

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 25 | Transmitter | V6 | ✅ | Full roundtrip incl. modulation parameters and antenna pattern blobs. |
| 26 | Signal | V6 | ✅ | Audio data with 4-byte-boundary padding. |
| 27 | Receiver | V6 | ✅ | |
| 31 | Intercom Signal | V6 | ✅ | |
| 32 | Intercom Control | V6 | ✅ | Full roundtrip incl. intercom-parameters blob. |

### Family 7 — Entity Management

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 33 | Aggregate State | V6 | ✅ | Full typed roundtrip incl. typed aggregate-id / entity-id lists, silent aggregate / entity system lists, and variable datum records. |
| 34 | IsGroupOf | V6 | ✅ | Full typed roundtrip; per-category GED records split into `IReadOnlyList<byte[]>` since every PDU uses a uniform record size. |
| 35 | Transfer Ownership | V6 | ✅ | Fixed part roundtrips incl. RequiredReliabilityService + TransferType; record sets as opaque blob. |
| 36 | IsPartOf | V6 | ✅ | Full typed roundtrip. |

### Family 8 — Minefield

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 37 | Minefield State | V6 | ✅ | Full typed roundtrip incl. `Vector2Float` perimeter points and `EntityType` mine types. |
| 38 | Minefield Query | V6 | ✅ | Full typed roundtrip incl. perimeter points and sensor-type list. |
| 39 | Minefield Data | V6 | ✅ | Typed mine locations + sensor types; DataFilter-gated optional per-mine arrays kept as `OptionalFieldsBlob` (bit-to-array mapping needs SISO test vectors to type safely). |
| 40 | Minefield Response NACK | V6 | ✅ | Missing-PDU list roundtrips. Same type id 40 as CollisionElastic; family byte disambiguates. |

### Family 9 — Synthetic Environment

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 41 | Environmental Process | V6 | ✅ | Full typed roundtrip incl. environment records as `StandardVariableRecord`s (§6.2.54 = §6.2.82). |
| 42 | Gridded Data | V6 | ✅ | Full typed roundtrip incl. grid-axis descriptors (`GridAxisDescriptor`, §6.2.41); per-sample decoding driven by `DataRepresentation`. |
| 43 | Point Object State | V6 | ✅ | Full typed roundtrip (88 B). |
| 44 | Linear Object State | V6 | ✅ | Full typed roundtrip incl. LinearSegmentParameter list (64 B per segment). |
| 45 | Areal Object State | V6 | ✅ | Full typed roundtrip incl. ECEF polygon vertex list. |

### Family 10 — Simulation Management with Reliability

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 51 | Create Entity-R | V6 | ✅ | |
| 52 | Remove Entity-R | V6 | ✅ | |
| 53 | Start/Resume-R | V6 | ✅ | |
| 54 | Stop/Freeze-R | V6 | ✅ | |
| 55 | Acknowledge-R | V6 | ✅ | No reliability byte (ack is the mechanism). |
| 56 | Action Request-R | V6 | ✅ | |
| 57 | Action Response-R | V6 | ✅ | No reliability byte (response reuses request's transport). |
| 58 | Data Query-R | V6 | ✅ | |
| 59 | Set Data-R | V6 | ✅ | |
| 60 | Data-R | V6 | ✅ | |
| 61 | Event Report-R | V6 | ✅ | No reliability byte (informational one-way). |
| 62 | Comment-R | V6 | ✅ | No reliability byte (informational). |
| 63 | Record-R | V6 | ✅ | Full typed roundtrip incl. RecordSet list with id / serial / length-in-bits / record count. |
| 64 | Set Record-R | V6 | ✅ | Full typed roundtrip incl. RecordSet list. |
| 65 | Record Query-R | V6 | ✅ | Record id list roundtrips. |

### Family 11 — Live Entity

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 66 | TSPI (Time Space Position Information) | V6 | ✅ | Header + LiveEntityId typed; compressed bit-packed payload round-trips verbatim (flag-gated field decoding deferred — opendis7 reference impl also doesn't decode). |
| 99 | Appearance | V6 | ✅ | Header + LiveEntityId typed; compressed flag-gated payload round-trips verbatim. |
| 100 | Articulated Parts | V6 | ✅ | Full typed roundtrip with `IReadOnlyList<VariableParameter>` — articulated / attached / separation records typed. |
| 101 | LE Fire | V6 | ✅ | Header + LiveEntityId typed; compressed flag-gated payload round-trips verbatim. |
| 102 | LE Detonation | V6 | ✅ | Header + LiveEntityId typed; compressed flag-gated payload round-trips verbatim. |

### Family 12 — Non-Real-Time

| ID | Name | Ver. | Status |
|----|------|------|--------|
| 67–69 | (reserved; covered by V7 family 2 entries above) | — | — |

### Family 13 — Information Operations (V7)

| ID | Name | Ver. | Status | Notes |
|----|------|------|--------|-------|
| 81 | IO Action | V7 | ✅ | Full typed roundtrip incl. IO record sets as `StandardVariableRecord`s (§6.2.82). |
| 82 | IO Report | V7 | ✅ | Full typed roundtrip incl. IO record sets as `StandardVariableRecord`s (§6.2.82). |

## Enumerations (SISO-REF-010)

Generated by the one-shot `Tools/SisoGen/` console app. Source of truth: the canonical SISO XML (V35, 2025-04-27, mirrored at [open-dis/opendis7-source-generator](https://github.com/open-dis/opendis7-source-generator/tree/master/xml/SISO) so the generator doesn't need a SISO Digital Library login). Output committed under `Enumerations/Generated/`.

Hand-written enums (kept in `Enumerations/*.cs`, not regenerated):

| Enumeration | Entries | SISO uid |
|-------------|---------|----------|
| `DisProtocolVersion` | 8 | 3 |
| `DisProtocolFamily` | 14 | 5 |
| `DisPduType` | 72 | 4 |
| `ForceId` | 4 | 6 |
| `DetonationResult` | ~30 | 62 |
| `DeadReckoningAlgorithm` | 10 | 44 |
| `StopFreezeReason` | 9 | 67 |
| `AcknowledgeFlag` | 5 | 69 |
| `ResponseFlag` | 4 | 70 |
| `ServiceTypeRequested` | 5 | 63 |
| `RequiredReliabilityService` | 2 | 74 |
| `AggregateState`, `TransferType`, `TransmitState`, `ReceiverState`, `AntennaPatternType`, `IntercomControlType`, `RepairCode`, `RepairResult` | (per-family) | — |

Generated from SISO-REF-010 V35 (committed under `Enumerations/Generated/`):

| Enumeration | Entries | SISO uid | Consumers |
|-------------|---------|----------|-----------|
| `Country` | 279 | 29 | `EntityType.Country`, `AggregateType.Country` |
| `EntityKind` | 10 | 7 | `EntityType.Kind` |
| `PlatformDomain` | 6 | 8 | `EntityType.Domain` (kind=1) |
| `MunitionDomain` | 13 | 14 | `EntityType.Domain` (kind=2) |
| `WarheadType` | 96 | 60 | `MunitionDescriptor.Warhead` |
| `FuseType` | 107 | 61 | `MunitionDescriptor.Fuse` |
| `EmitterName` | 2042 | 75 | `ElectromagneticEmissionSystem.EmitterName` |
| `EmitterSystemFunction` | 82 | 76 | EE system function |
| `BeamFunction` | 24 | 78 | EE beam function |

Add new `EnumSpec` entries to `tools/SisoGen/Program.cs` to generate more — there are ~430 enums in the full SISO-REF-010 XML, but most are rarely referenced in on-wire PDU fields.

## Workbench integration

The plugin exposes the DIS exercise to the Bowire workbench through the standard `IBowireProtocol` surface:

| Capability | Status | Notes |
|------------|--------|-------|
| `DiscoverAsync` — parse `dis://group:port` URLs | ✅ | Falls back to `239.1.2.3:3000`. Bare `host:port` accepted. |
| `DiscoverAsync` — multicast probe for live entities | ✅ | Short listen on the group; every Entity State PDU's `EntityId` + marking becomes its own service. |
| `InvokeStreamAsync` — exercise-wide PDU feed | ✅ | Yields a JSON envelope per PDU (pdu type, exercise id, length, base64 raw bytes). |
| `InvokeStreamAsync` — entity-filtered feed | ✅ | Service names carry the `site:app:entity` triple so the stream filters to that entity's PDUs. |
| `InvokeAsync` (unary) | n/a | DIS is broadcast-only — surfaces a clear error pointing to the monitor stream. |
| `OpenChannelAsync` (duplex) | n/a | No request/reply semantics to bind to. |
