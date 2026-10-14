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
/// <remarks>
/// Values are SISO-REF-010 v37, enumeration uid 4 ("DIS-PDU Type"), and a
/// test pins every one of them. Nine were wrong until #57 — Collision-Elastic
/// sat on 40 next to Minefield Response NACK with a comment claiming the
/// family byte told them apart. It does not: a PDU type id is unique across
/// families. Marshal and Unmarshal shared the wrong numbers, so every
/// round-trip test passed while the wire said something else.
/// </remarks>
public enum DisPduType
{
    /// <summary>Not specified.</summary>
    Other = 0,

    // ---- Family 1: Entity Information / Interaction ----
    /// <summary>Entity State PDU.</summary>
    EntityState = 1,
    /// <summary>Collision PDU.</summary>
    Collision = 4,
    /// <summary>Collision Elastic PDU.</summary>
    CollisionElastic = 66,
    /// <summary>Entity State Update PDU.</summary>
    EntityStateUpdate = 67,
    /// <summary>Attribute PDU.</summary>
    Attribute = 72,

    // ---- Family 2: Warfare ----
    /// <summary>Fire PDU.</summary>
    Fire = 2,
    /// <summary>Detonation PDU.</summary>
    Detonation = 3,
    /// <summary>Directed Energy Fire PDU.</summary>
    DirectedEnergyFire = 68,
    /// <summary>Entity Damage Status PDU.</summary>
    EntityDamageStatus = 69,

    // ---- Family 3: Logistics ----
    /// <summary>Service Request PDU.</summary>
    ServiceRequest = 5,
    /// <summary>Resupply Offer PDU.</summary>
    ResupplyOffer = 6,
    /// <summary>Resupply Received PDU.</summary>
    ResupplyReceived = 7,
    /// <summary>Resupply Cancel PDU.</summary>
    ResupplyCancel = 8,
    /// <summary>Repair Complete PDU.</summary>
    RepairComplete = 9,
    /// <summary>Repair Response PDU.</summary>
    RepairResponse = 10,

    // ---- Family 5: Simulation Management ----
    /// <summary>Create Entity PDU.</summary>
    CreateEntity = 11,
    /// <summary>Remove Entity PDU.</summary>
    RemoveEntity = 12,
    /// <summary>Start Resume PDU.</summary>
    StartResume = 13,
    /// <summary>Stop Freeze PDU.</summary>
    StopFreeze = 14,
    /// <summary>Acknowledge PDU.</summary>
    Acknowledge = 15,
    /// <summary>Action Request PDU.</summary>
    ActionRequest = 16,
    /// <summary>Action Response PDU.</summary>
    ActionResponse = 17,
    /// <summary>Data Query PDU.</summary>
    DataQuery = 18,
    /// <summary>Set Data PDU.</summary>
    SetData = 19,
    /// <summary>Data PDU.</summary>
    Data = 20,
    /// <summary>Event Report PDU.</summary>
    EventReport = 21,
    /// <summary>Comment PDU.</summary>
    Comment = 22,

    // ---- Family 6: Distributed Emission Regeneration ----
    /// <summary>Electromagnetic Emission PDU.</summary>
    ElectromagneticEmission = 23,
    /// <summary>Designator PDU.</summary>
    Designator = 24,
    /// <summary>Underwater Acoustic PDU.</summary>
    UnderwaterAcoustic = 29,
    /// <summary>Supplemental Emission Entity State PDU.</summary>
    SupplementalEmissionEntityState = 30,

    // ---- Family 4: Radio Communications ----
    /// <summary>Transmitter PDU.</summary>
    Transmitter = 25,
    /// <summary>Signal PDU.</summary>
    Signal = 26,
    /// <summary>Receiver PDU.</summary>
    Receiver = 27,
    /// <summary>Intercom Signal PDU.</summary>
    IntercomSignal = 31,
    /// <summary>Intercom Control PDU.</summary>
    IntercomControl = 32,

    // ---- Family 7: Entity Management ----
    /// <summary>Aggregate State PDU.</summary>
    AggregateState = 33,
    /// <summary>Is Group Of PDU.</summary>
    IsGroupOf = 34,
    /// <summary>Transfer Ownership PDU.</summary>
    TransferOwnership = 35,
    /// <summary>Is Part Of PDU.</summary>
    IsPartOf = 36,

    // ---- Family 8: Minefield ----
    /// <summary>Minefield State PDU.</summary>
    MinefieldState = 37,
    /// <summary>Minefield Query PDU.</summary>
    MinefieldQuery = 38,
    /// <summary>Minefield Data PDU.</summary>
    MinefieldData = 39,
    /// <summary>Minefield Response Nack PDU.</summary>
    MinefieldResponseNack = 40,

    // ---- Family 9: Synthetic Environment ----
    /// <summary>Environmental Process PDU.</summary>
    EnvironmentalProcess = 41,
    /// <summary>Gridded Data PDU.</summary>
    GriddedData = 42,
    /// <summary>Point Object State PDU.</summary>
    PointObjectState = 43,
    /// <summary>Linear Object State PDU.</summary>
    LinearObjectState = 44,
    /// <summary>Areal Object State PDU.</summary>
    ArealObjectState = 45,

    // ---- Family 10: Simulation Management with Reliability ----
    /// <summary>Create Entity-R PDU.</summary>
    CreateEntityR = 51,
    /// <summary>Remove Entity-R PDU.</summary>
    RemoveEntityR = 52,
    /// <summary>Start Resume-R PDU.</summary>
    StartResumeR = 53,
    /// <summary>Stop Freeze-R PDU.</summary>
    StopFreezeR = 54,
    /// <summary>Acknowledge-R PDU.</summary>
    AcknowledgeR = 55,
    /// <summary>Action Request-R PDU.</summary>
    ActionRequestR = 56,
    /// <summary>Action Response-R PDU.</summary>
    ActionResponseR = 57,
    /// <summary>Data Query-R PDU.</summary>
    DataQueryR = 58,
    /// <summary>Set Data-R PDU.</summary>
    SetDataR = 59,
    /// <summary>Data-R PDU.</summary>
    DataR = 60,
    /// <summary>Event Report-R PDU.</summary>
    EventReportR = 61,
    /// <summary>Comment-R PDU.</summary>
    CommentR = 62,
    /// <summary>Record-R PDU.</summary>
    RecordR = 63,
    /// <summary>Set Record-R PDU.</summary>
    SetRecordR = 64,
    /// <summary>Record Query-R PDU.</summary>
    RecordQueryR = 65,

    // ---- Family 11: Live Entity ----
    /// <summary>Time Space Position Information PDU.</summary>
    TimeSpacePositionInformation = 46,
    /// <summary>Appearance PDU.</summary>
    Appearance = 47,
    /// <summary>Articulated Parts PDU.</summary>
    ArticulatedParts = 48,
    /// <summary>Live Entity Fire PDU.</summary>
    LiveEntityFire = 49,
    /// <summary>Live Entity Detonation PDU.</summary>
    LiveEntityDetonation = 50,

    // ---- Family 13: Information Operations (V7) ----
    /// <summary>Information Operations Action PDU.</summary>
    InformationOperationsAction = 70,
    /// <summary>Information Operations Report PDU.</summary>
    InformationOperationsReport = 71,
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
