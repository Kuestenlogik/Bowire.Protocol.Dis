// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Entity Damage Status PDU (type 69, family 2, V7). Reports the
/// current damage state of an entity in more detail than Entity
/// State's 32-bit Appearance word allows — piece-wise damage
/// descriptions, system-by-system health, repair state. Paired with
/// directed-energy or cumulative-damage scenarios.
/// IEEE 1278.1-2012 §7.3.5.
/// </summary>
/// <remarks>
/// <para>
/// Layout (28 bytes fixed, then a sequence of damage description
/// records):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="DamagedEntityId"/></item>
///   <item>18 :  2 — reserved padding</item>
///   <item>20 :  4 — reserved padding (second block)</item>
///   <item>24 :  2 — <see cref="DamageDescriptionRecords"/> count</item>
///   <item>26 :  2 — reserved padding</item>
///   <item>28 :  N — damage description records (<see cref="StandardVariableRecord"/> per §6.2.82)</item>
/// </list>
/// <para>
/// Damage description records (propulsion, structural, steering,
/// directed-energy damage) share the §6.2.82 Standard Variable
/// Record wire shape — a 32-bit record-type code selects the typed
/// body layout per SISO-REF-010. The body bytes round-trip verbatim;
/// per-record-type decoders can layer on when consumers need them.
/// </para>
/// </remarks>
public sealed record EntityDamageStatusPdu(
    PduHeader Header,
    EntityId DamagedEntityId,
    IReadOnlyList<StandardVariableRecord> DamageDescriptionRecords)
{
    /// <summary>Fixed wire length in bytes before the damage description records.</summary>
    public const int MinimumWireLength = 28;

    /// <summary>Wire length including every typed damage description record.</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var record in DamageDescriptionRecords) sum += record.WireLength;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>; returns bytes written.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var length = WireLength;
        var header = Header with
        {
            PduType = DisPduType.EntityDamageStatus,
            ProtocolFamily = DisProtocolFamily.Warfare,
            Length = (ushort)length,
        };
        header.Marshal(ref w);

        DamagedEntityId.Marshal(ref w);
        w.WriteUInt16(0);        // padding
        w.WritePadding(4);       // reserved block
        w.WriteUInt16((ushort)DamageDescriptionRecords.Count);
        w.WriteUInt16(0);        // trailing padding

        foreach (var record in DamageDescriptionRecords) record.Marshal(ref w);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut; returns a <see cref="WireLength"/>-byte array.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static EntityDamageStatusPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var damaged = EntityId.Unmarshal(ref r);
        r.SkipPadding(2);
        r.SkipPadding(4);
        var recordCount = r.ReadUInt16();
        r.SkipPadding(2);

        var records = new List<StandardVariableRecord>(recordCount);
        for (var i = 0; i < recordCount; i++)
            records.Add(StandardVariableRecord.Unmarshal(ref r));

        return new EntityDamageStatusPdu(header, damaged, records);
    }
}
