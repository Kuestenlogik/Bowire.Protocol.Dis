// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Munition Descriptor record (16 bytes). Ships with Fire and
/// Detonation PDUs to describe the ordnance: munition type
/// seven-tuple, warhead code, fuse code, quantity, and fire rate.
/// IEEE 1278.1-2012 §5.2.19 renames this from the V6 "Burst
/// Descriptor" record; the wire format is identical so one type
/// serves both versions.
/// </summary>
/// <param name="MunitionType">Entity-type seven-tuple of the munition.</param>
/// <param name="Warhead">Warhead code per SISO-REF-010 (e.g. 1000 = high explosive).</param>
/// <param name="Fuse">Fuse code per SISO-REF-010 (e.g. 1000 = point detonating).</param>
/// <param name="Quantity">Number of rounds in this fire event (burst length for automatic weapons).</param>
/// <param name="Rate">Rate of fire in rounds per minute — 0 when not applicable.</param>
public readonly record struct MunitionDescriptor(
    EntityType MunitionType,
    ushort Warhead,
    ushort Fuse,
    ushort Quantity,
    ushort Rate)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 16;

    internal void Marshal(ref DisWireWriter w)
    {
        MunitionType.Marshal(ref w);
        w.WriteUInt16(Warhead);
        w.WriteUInt16(Fuse);
        w.WriteUInt16(Quantity);
        w.WriteUInt16(Rate);
    }

    internal static MunitionDescriptor Unmarshal(ref DisWireReader r)
    {
        var type = EntityType.Unmarshal(ref r);
        var warhead = r.ReadUInt16();
        var fuse = r.ReadUInt16();
        var quantity = r.ReadUInt16();
        var rate = r.ReadUInt16();
        return new MunitionDescriptor(type, warhead, fuse, quantity, rate);
    }
}
