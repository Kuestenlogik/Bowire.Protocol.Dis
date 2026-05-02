// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Attribute PDU (type 71, family 1, V7). Carries change-only
/// attribute updates for entities using the DIS Attribute Record
/// Sets mechanism — the V7 answer to the fixed-field structure of
/// Entity State. A single Attribute PDU can report attribute changes
/// for many entities. IEEE 1278.1-2012 §5.3.3.5.
/// </summary>
/// <remarks>
/// <para>
/// Layout (minimum 36 bytes, then a sequence of Attribute Record
/// Sets):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  4 — <see cref="OriginatingSimulationAddress"/></item>
///   <item>16 :  4 — reserved padding</item>
///   <item>20 :  4 — reserved padding (second block)</item>
///   <item>24 :  1 — <see cref="AttributeRecordPduType"/></item>
///   <item>25 :  1 — <see cref="AttributeRecordProtocolVersion"/></item>
///   <item>26 :  2 — reserved padding</item>
///   <item>28 :  4 — <see cref="MasterAttributeRecordType"/></item>
///   <item>32 :  1 — <see cref="ActionCode"/></item>
///   <item>33 :  1 — reserved padding</item>
///   <item>34 :  2 — <see cref="AttributeRecordSets"/> count</item>
///   <item>36 : N — attribute record sets (<see cref="AttributeRecordSet"/> per §6.2.12)</item>
/// </list>
/// <para>
/// Attribute record sets are typed all the way down: each set carries
/// a target <see cref="EntityId"/> plus a list of
/// <see cref="StandardVariableRecord"/>s (§6.2.82). Per-record-type
/// content shapes are decided by the <see cref="StandardVariableRecord.RecordType"/>
/// code per SISO-REF-010.
/// </para>
/// </remarks>
public sealed record AttributePdu(
    PduHeader Header,
    SimulationAddress OriginatingSimulationAddress,
    byte AttributeRecordPduType,
    byte AttributeRecordProtocolVersion,
    uint MasterAttributeRecordType,
    byte ActionCode,
    IReadOnlyList<AttributeRecordSet> AttributeRecordSets)
{
    /// <summary>Wire length with no attribute record sets attached.</summary>
    public const int MinimumWireLength = 36;

    /// <summary>Wire length including every typed record set.</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var set in AttributeRecordSets) sum += set.WireLength;
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
            PduType = DisPduType.Attribute,
            ProtocolFamily = DisProtocolFamily.EntityInformation,
            Length = (ushort)length,
        };
        header.Marshal(ref w);

        OriginatingSimulationAddress.Marshal(ref w);
        w.WritePadding(4);
        w.WritePadding(4);

        w.WriteByte(AttributeRecordPduType);
        w.WriteByte(AttributeRecordProtocolVersion);
        w.WriteUInt16(0); // padding
        w.WriteUInt32(MasterAttributeRecordType);

        w.WriteByte(ActionCode);
        w.WriteByte(0); // padding
        w.WriteUInt16((ushort)AttributeRecordSets.Count);

        foreach (var set in AttributeRecordSets) set.Marshal(ref w);

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
    public static AttributePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var origin = SimulationAddress.Unmarshal(ref r);
        r.SkipPadding(4);
        r.SkipPadding(4);

        var recordPduType = r.ReadByte();
        var recordProtocolVersion = r.ReadByte();
        r.SkipPadding(2);
        var masterRecordType = r.ReadUInt32();

        var actionCode = r.ReadByte();
        r.SkipPadding(1);
        var recordSetCount = r.ReadUInt16();

        var sets = new List<AttributeRecordSet>(recordSetCount);
        for (var i = 0; i < recordSetCount; i++)
            sets.Add(AttributeRecordSet.Unmarshal(ref r));

        return new AttributePdu(
            header, origin, recordPduType, recordProtocolVersion,
            masterRecordType, actionCode, sets);
    }
}
