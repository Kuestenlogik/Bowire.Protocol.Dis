// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Enumerations;

/// <summary>
/// Required Reliability Service code (byte) — present on every PDU
/// in the Simulation Management with Reliability family. Tells the
/// transport layer whether delivery must be acknowledged or may be
/// fire-and-forget. IEEE 1278.1 §5.2.27.
/// </summary>
public enum RequiredReliabilityService
{
    /// <summary>Acknowledged — transport must guarantee delivery.</summary>
    Acknowledged = 0,
    /// <summary>Unacknowledged — best-effort delivery is acceptable.</summary>
    Unacknowledged = 1,
}
