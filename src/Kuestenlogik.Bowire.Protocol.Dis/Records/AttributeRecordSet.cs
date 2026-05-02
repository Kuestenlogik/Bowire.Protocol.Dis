// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Attribute Record Set. Top-level container inside an Attribute PDU:
/// groups a batch of <see cref="StandardVariableRecord"/>s against a
/// single target <see cref="EntityId"/>. The wire format packs one
/// entity + record count + records per set; a PDU can carry any number
/// of these. IEEE 1278.1-2012 §5.3.3.5 / §6.2.12.
/// </summary>
/// <remarks>
/// <para>Layout on the wire (8 bytes fixed + records):</para>
/// <list type="bullet">
///   <item>0 : 6 — <see cref="RecordEntityId"/></item>
///   <item>6 : 2 — attribute record count</item>
///   <item>8 : N — <see cref="AttributeRecords"/> (each record uses
///         the Standard Variable Record layout from §6.2.82 and is
///         padded to 64-bit on the wire)</item>
/// </list>
/// </remarks>
public sealed record AttributeRecordSet(
    EntityId RecordEntityId,
    IReadOnlyList<StandardVariableRecord> AttributeRecords)
{
    /// <summary>Fixed wire length of the set header before records start.</summary>
    public const int FixedWireLength = 8;

    /// <summary>Wire length in bytes: 8 fixed + sum of every record's padded length.</summary>
    public int WireLength
    {
        get
        {
            var sum = FixedWireLength;
            foreach (var record in AttributeRecords) sum += record.WireLength;
            return sum;
        }
    }

    internal void Marshal(ref DisWireWriter w)
    {
        RecordEntityId.Marshal(ref w);
        w.WriteUInt16((ushort)AttributeRecords.Count);
        foreach (var record in AttributeRecords) record.Marshal(ref w);
    }

    internal static AttributeRecordSet Unmarshal(ref DisWireReader r)
    {
        var entityId = EntityId.Unmarshal(ref r);
        var count = r.ReadUInt16();
        var records = new List<StandardVariableRecord>(count);
        for (var i = 0; i < count; i++)
            records.Add(StandardVariableRecord.Unmarshal(ref r));
        return new AttributeRecordSet(entityId, records);
    }
}
