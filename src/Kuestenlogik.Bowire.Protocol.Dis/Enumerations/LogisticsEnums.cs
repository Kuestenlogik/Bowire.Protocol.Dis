// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Enumerations;

/// <summary>
/// Service Type Requested (byte) — ships with Service Request PDU.
/// IEEE 1278.1 §5.2.28.
/// </summary>
public enum ServiceTypeRequested
{
    /// <summary>Other / not specified.</summary>
    Other = 0,
    /// <summary>Resupply.</summary>
    Resupply = 1,
    /// <summary>Repair.</summary>
    Repair = 2,
}

/// <summary>
/// Repair Complete "Repair" field (16-bit enumeration). Identifies
/// which repair was performed. Subset — the full SISO-REF-010 list
/// has hundreds of repair codes across every entity family.
/// IEEE 1278.1 §5.2.22.
/// </summary>
public enum RepairCode
{
    /// <summary>No specific repair.</summary>
    NoRepair = 0,
    /// <summary>Full mechanical overhaul.</summary>
    AllMechanicalRepairsPerformed = 1,
    /// <summary>Full electrical overhaul.</summary>
    AllElectricalRepairsPerformed = 2,
}

/// <summary>
/// Repair Response result (byte). Tells the requester whether a
/// repair attempt succeeded. IEEE 1278.1 §5.2.23.
/// </summary>
public enum RepairResult
{
    /// <summary>Other / unspecified outcome.</summary>
    Other = 0,
    /// <summary>Repair succeeded.</summary>
    RepairEnded = 1,
    /// <summary>Invalid repair request.</summary>
    InvalidRepair = 2,
    /// <summary>Repair unsupported by the repairing entity.</summary>
    RepairNotSupported = 3,
}
