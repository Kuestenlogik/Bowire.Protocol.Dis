// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Enumerations;

/// <summary>
/// DIS protocol version — the first byte of every PDU header.
/// Identifies which revision of IEEE 1278.1 the sender targets so
/// receivers can pick the right parsing rules.
/// </summary>
public enum DisProtocolVersion
{
    /// <summary>Not specified — reserved value, should never appear on the wire.</summary>
    Other = 0,
    /// <summary>DIS PDU version 1.0, May 1992.</summary>
    Version1May92 = 1,
    /// <summary>IEEE 1278-1993.</summary>
    Ieee1278_1993 = 2,
    /// <summary>DIS PDU version 2.0, third draft, May 1993.</summary>
    Version2Third93 = 3,
    /// <summary>DIS PDU version 2.0, fourth draft, revised, March 1994.</summary>
    Version2Fourth94 = 4,
    /// <summary>IEEE 1278.1-1995 — DIS v6, the long-time baseline.</summary>
    Ieee1278_1_1995 = 5,
    /// <summary>IEEE 1278.1a-1998 — minor amendments on top of 1995.</summary>
    Ieee1278_1A_1998 = 6,
    /// <summary>IEEE 1278.1-2012 — DIS v7, adds directed-energy PDUs and pdu_status byte.</summary>
    Ieee1278_1_2012 = 7,
}

/// <summary>
/// DIS protocol family — second byte of the header that groups
/// related PDUs. IEEE 1278.1 §5.2.29. Protocol families are how the
/// PDU Type id is disambiguated (type 11 in family 5 means "Create
/// Entity"; in family 10 it would mean something else entirely).
/// </summary>
public enum DisProtocolFamily
{
    /// <summary>Not specified.</summary>
    Other = 0,
    /// <summary>Entity Information / Interaction — Entity State, Collision, ...</summary>
    EntityInformation = 1,
    /// <summary>Warfare — Fire, Detonation.</summary>
    Warfare = 2,
    /// <summary>Logistics — Resupply, Repair.</summary>
    Logistics = 3,
    /// <summary>Radio Communications — Transmitter, Signal, Receiver.</summary>
    RadioCommunications = 4,
    /// <summary>Simulation Management — Create/Remove/Start/Stop, etc.</summary>
    SimulationManagement = 5,
    /// <summary>Distributed Emission Regeneration — Emissions, Designators.</summary>
    DistributedEmissionRegeneration = 6,
    /// <summary>Entity Management — Aggregate State, Transfer Ownership.</summary>
    EntityManagement = 7,
    /// <summary>Minefield — Minefield State, Query, Data.</summary>
    Minefield = 8,
    /// <summary>Synthetic Environment — Environmental Process, Object State.</summary>
    SyntheticEnvironment = 9,
    /// <summary>Simulation Management with Reliability — reliable-transport variants of family 5.</summary>
    SimulationManagementWithReliability = 10,
    /// <summary>Live Entity — TSPI, LE Fire, LE Detonation.</summary>
    LiveEntity = 11,
    /// <summary>Non-Real-Time — record / replay.</summary>
    NonRealTime = 12,
    /// <summary>Information Operations — IO Action, IO Report (V7).</summary>
    InformationOperations = 13,
}

/// <summary>
/// DIS PDU type — third byte of the header. Numbered per
/// IEEE 1278.1-2012 §5.3 Table 5. Values outside this enum are
/// reserved; receivers should either route by the family byte or
/// treat them as Other.
/// </summary>
public enum DisPduType
{
    /// <summary>Not specified.</summary>
    Other = 0,

    // ---- Family 1: Entity Information / Interaction ----
    EntityState = 1,
    Collision = 4,
    CollisionElastic = 40,
    EntityStateUpdate = 67,
    Attribute = 71,

    // ---- Family 2: Warfare ----
    Fire = 2,
    Detonation = 3,
    DirectedEnergyFire = 68,
    EntityDamageStatus = 69,

    // ---- Family 3: Logistics ----
    ServiceRequest = 5,
    ResupplyOffer = 6,
    ResupplyReceived = 7,
    ResupplyCancel = 8,
    RepairComplete = 9,
    RepairResponse = 10,

    // ---- Family 5: Simulation Management ----
    CreateEntity = 11,
    RemoveEntity = 12,
    StartResume = 13,
    StopFreeze = 14,
    Acknowledge = 15,
    ActionRequest = 16,
    ActionResponse = 17,
    DataQuery = 18,
    SetData = 19,
    Data = 20,
    EventReport = 21,
    Comment = 22,

    // ---- Family 6: Distributed Emission Regeneration ----
    ElectromagneticEmission = 23,
    Designator = 24,
    UnderwaterAcoustic = 29,
    SupplementalEmissionEntityState = 30,

    // ---- Family 4: Radio Communications ----
    Transmitter = 25,
    Signal = 26,
    Receiver = 27,
    IntercomSignal = 31,
    IntercomControl = 32,

    // ---- Family 7: Entity Management ----
    AggregateState = 33,
    IsGroupOf = 34,
    TransferOwnership = 35,
    IsPartOf = 36,

    // ---- Family 8: Minefield ----
    MinefieldState = 37,
    MinefieldQuery = 38,
    MinefieldData = 39,
    MinefieldResponseNack = 40, // same id as CollisionElastic; disambiguated by family byte

    // ---- Family 9: Synthetic Environment ----
    EnvironmentalProcess = 41,
    GriddedData = 42,
    PointObjectState = 43,
    LinearObjectState = 44,
    ArealObjectState = 45,

    // ---- Family 10: Simulation Management with Reliability ----
    CreateEntityR = 51,
    RemoveEntityR = 52,
    StartResumeR = 53,
    StopFreezeR = 54,
    AcknowledgeR = 55,
    ActionRequestR = 56,
    ActionResponseR = 57,
    DataQueryR = 58,
    SetDataR = 59,
    DataR = 60,
    EventReportR = 61,
    CommentR = 62,
    RecordR = 63,
    SetRecordR = 64,
    RecordQueryR = 65,

    // ---- Family 11: Live Entity ----
    TimeSpacePositionInformation = 66,
    Appearance = 99,
    ArticulatedParts = 100,
    LiveEntityFire = 101,
    LiveEntityDetonation = 102,

    // ---- Family 13: Information Operations (V7) ----
    InformationOperationsAction = 81,
    InformationOperationsReport = 82,
}

/// <summary>
/// DIS force id (byte) — which side a simulated entity belongs to.
/// IEEE 1278.1 §5.2.19. The wire format stores one byte; the enum's
/// backing type is <c>int</c> per .NET guidelines.
/// </summary>
public enum ForceId
{
    /// <summary>No force specified.</summary>
    Other = 0,
    /// <summary>Friendly force — own side.</summary>
    Friendly = 1,
    /// <summary>Opposing force — adversary.</summary>
    Opposing = 2,
    /// <summary>Neutral force — non-combatant.</summary>
    Neutral = 3,
    /// <summary>Friendly, exercise 2.</summary>
    Friendly2 = 4,
    /// <summary>Opposing, exercise 2.</summary>
    Opposing2 = 5,
    /// <summary>Neutral, exercise 2.</summary>
    Neutral2 = 6,
}

/// <summary>
/// Detonation result — carried on Detonation PDU. Subset of
/// SISO-REF-010 detonation-result values; additional codes (guided
/// munition hit, mine detonation, HE variants, ...) round-trip
/// through the underlying byte but aren't named here.
/// IEEE 1278.1 §5.2.15.
/// </summary>
public enum DetonationResult
{
    /// <summary>Other / not specified.</summary>
    Other = 0,
    /// <summary>Entity impact — munition struck the target entity.</summary>
    EntityImpact = 1,
    /// <summary>Entity proximate detonation — close enough to affect the target.</summary>
    EntityProximateDetonation = 2,
    /// <summary>Ground impact.</summary>
    GroundImpact = 3,
    /// <summary>Ground proximate detonation.</summary>
    GroundProximateDetonation = 4,
    /// <summary>Detonation — generic.</summary>
    Detonation = 5,
    /// <summary>No detonation — munition failed.</summary>
    NoDetonation = 6,
    /// <summary>Miss — no impact.</summary>
    Miss = 8,
}

/// <summary>
/// Dead-reckoning algorithm. IEEE 1278.1 §5.2.13 Table. Indicates
/// how a receiver should extrapolate position between Entity State
/// updates.
/// </summary>
public enum DeadReckoningAlgorithm
{
    /// <summary>Other / not specified.</summary>
    Other = 0,
    /// <summary>Static — position doesn't change between updates.</summary>
    Static = 1,
    /// <summary>DRM(F,P,W) — first-order linear, constant velocity, world coords.</summary>
    FPW = 2,
    /// <summary>DRM(R,P,W) — first-order rotational, constant velocity, world coords.</summary>
    RPW = 3,
    /// <summary>DRM(R,V,W) — second-order rotational, constant accel, world coords.</summary>
    RVW = 4,
    /// <summary>DRM(F,V,W) — second-order linear, constant accel, world coords.</summary>
    FVW = 5,
    /// <summary>DRM(F,P,B) — first-order linear, constant velocity, body coords.</summary>
    FPB = 6,
    /// <summary>DRM(R,P,B) — first-order rotational, constant velocity, body coords.</summary>
    RPB = 7,
    /// <summary>DRM(R,V,B) — second-order rotational, constant accel, body coords.</summary>
    RVB = 8,
    /// <summary>DRM(F,V,B) — second-order linear, constant accel, body coords.</summary>
    FVB = 9,
}
