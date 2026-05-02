// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Enumerations;

/// <summary>
/// Aggregate State code (byte). IEEE 1278.1 §5.2.1.
/// </summary>
public enum AggregateState
{
    /// <summary>Other / unspecified.</summary>
    Other = 0,
    /// <summary>Aggregated — members represented as a single entity.</summary>
    Aggregated = 1,
    /// <summary>Disaggregated — members represented individually.</summary>
    Disaggregated = 2,
    /// <summary>Fully disaggregated — members plus all sub-aggregates broken out.</summary>
    FullyDisaggregated = 3,
    /// <summary>Pseudo-disaggregated — hybrid representation.</summary>
    PseudoDisaggregated = 4,
    /// <summary>Partially disaggregated.</summary>
    PartiallyDisaggregated = 5,
}

/// <summary>
/// Transfer Ownership transfer type (byte). IEEE 1278.1 §5.2.35.
/// </summary>
public enum TransferType
{
    /// <summary>Other / unspecified.</summary>
    Other = 0,
    /// <summary>Controller — full simulation ownership moves.</summary>
    Controller = 1,
    /// <summary>Automatic resignation — sender gives up without a handover partner.</summary>
    AutomaticResignation = 2,
    /// <summary>Manual resignation — same, but explicitly requested.</summary>
    ManualResignation = 3,
    /// <summary>Cancel transfer.</summary>
    CancelTransfer = 4,
}
