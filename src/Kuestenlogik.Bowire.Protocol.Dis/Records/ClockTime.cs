// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// DIS Clock Time record — an 8-byte pair representing absolute or
/// relative time: integer hours since an epoch plus time past hour
/// as a 32-bit timestamp. Used by Simulation Management Start/Stop
/// PDUs and the V7 Directed Energy Fire PDU's shot-start field.
/// IEEE 1278.1 §5.2.9.
/// </summary>
/// <param name="Hour">Hours since the start of the DIS exercise epoch (or absolute Unix hour, per exercise convention).</param>
/// <param name="TimePastHour">Time past hour encoded per §5.2.31: upper 31 bits are the time fraction of 3600 s, lowest bit flags absolute (1) or relative (0).</param>
public readonly record struct ClockTime(uint Hour, uint TimePastHour)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt32(Hour);
        w.WriteUInt32(TimePastHour);
    }

    internal static ClockTime Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt32(), r.ReadUInt32());
}
