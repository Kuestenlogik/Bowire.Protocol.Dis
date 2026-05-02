// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Enumerations;

/// <summary>
/// Stop/Freeze PDU reason code. IEEE 1278.1 §5.2.30. Subset — the
/// wire field is a byte, so any value round-trips even if not named
/// here.
/// </summary>
public enum StopFreezeReason
{
    /// <summary>Other / not specified.</summary>
    Other = 0,
    /// <summary>Recess — temporary break, exercise continues later.</summary>
    Recess = 1,
    /// <summary>Termination — exercise ending permanently.</summary>
    Termination = 2,
    /// <summary>System failure — exercise can't continue without intervention.</summary>
    SystemFailure = 3,
    /// <summary>Security violation — exercise stopped for security reasons.</summary>
    SecurityViolation = 4,
    /// <summary>Entity reconstitution — entities being rebuilt / reset.</summary>
    EntityReconstitution = 5,
    /// <summary>Stop for reset.</summary>
    StopForReset = 6,
    /// <summary>Stop for restart.</summary>
    StopForRestart = 7,
    /// <summary>Abort training — return to tactical operations.</summary>
    AbortTrainingReturnToTacticalOperations = 8,
}

/// <summary>
/// Acknowledge PDU AcknowledgeFlag (16-bit enumeration). Identifies
/// the PDU type being acknowledged. IEEE 1278.1 §5.2.5.
/// </summary>
public enum AcknowledgeFlag
{
    /// <summary>Placeholder — no acknowledgement target set.</summary>
    Unspecified = 0,
    /// <summary>Acknowledging a Create Entity PDU.</summary>
    CreateEntity = 1,
    /// <summary>Acknowledging a Remove Entity PDU.</summary>
    RemoveEntity = 2,
    /// <summary>Acknowledging a Start/Resume PDU.</summary>
    StartResume = 3,
    /// <summary>Acknowledging a Stop/Freeze PDU.</summary>
    StopFreeze = 4,
    /// <summary>Acknowledging a Transfer Ownership PDU.</summary>
    TransferOwnership = 5,
}

/// <summary>
/// Acknowledge PDU ResponseFlag (16-bit enumeration). Indicates the
/// responding simulator's ability to comply with the acknowledged
/// request. IEEE 1278.1 §5.2.26.
/// </summary>
public enum ResponseFlag
{
    /// <summary>Other / unspecified.</summary>
    Other = 0,
    /// <summary>Able to comply — request will be honoured.</summary>
    AbleToComply = 1,
    /// <summary>Unable to comply — request can't be honoured.</summary>
    UnableToComply = 2,
    /// <summary>Pending operation — comply once a dependency resolves.</summary>
    PendingOperation = 3,
}
