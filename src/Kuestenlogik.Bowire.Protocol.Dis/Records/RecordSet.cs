// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Record Set record. Ships with Record-R / Set Record-R PDUs to
/// carry a homogeneous batch of records identified by a single
/// RecordId code. Per-record payload shape is determined by the
/// RecordId (SISO-REF-010 variable-record-types table); this codec
/// keeps the records as an opaque byte span for now.
/// IEEE 1278.1 §5.3.12.13.1.
/// </summary>
/// <remarks>
/// <para>
/// Layout on the wire (16 bytes fixed + records):
/// </para>
/// <list type="bullet">
///   <item>0  : 4 — <see cref="RecordId"/></item>
///   <item>4  : 4 — <see cref="RecordSetSerialNumber"/></item>
///   <item>8  : 4 — reserved padding</item>
///   <item>12 : 2 — record length (bits) per record</item>
///   <item>14 : 2 — record count</item>
///   <item>16 : N — records (padded to 64-bit boundary on the wire)</item>
/// </list>
/// </remarks>
public sealed record RecordSet(
    uint RecordId,
    uint RecordSetSerialNumber,
    ushort RecordLengthBits,
    ushort RecordCount,
    byte[] Records)
{
    /// <summary>Fixed wire length before the records payload.</summary>
    public const int FixedWireLength = 16;

    /// <summary>
    /// Wire length in bytes: 16 fixed + records padded to the next
    /// 8-byte boundary.
    /// </summary>
    public int WireLength
    {
        get
        {
            var recordsLen = Records.Length;
            var padded = ((recordsLen + 7) / 8) * 8;
            return FixedWireLength + padded;
        }
    }

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt32(RecordId);
        w.WriteUInt32(RecordSetSerialNumber);
        w.WriteUInt32(0); // padding
        w.WriteUInt16(RecordLengthBits);
        w.WriteUInt16(RecordCount);
        w.WriteBytes(Records);
        var padded = ((Records.Length + 7) / 8) * 8;
        if (padded > Records.Length) w.WritePadding(padded - Records.Length);
    }

    internal static RecordSet Unmarshal(ref DisWireReader r)
    {
        var recordId = r.ReadUInt32();
        var serialNumber = r.ReadUInt32();
        r.SkipPadding(4);
        var lengthBits = r.ReadUInt16();
        var count = r.ReadUInt16();

        // records total size = ceil((lengthBits * count) / 8) bytes,
        // padded to 8-byte boundary.
        var recordsBytes = (int)(((uint)lengthBits * count + 7) / 8);
        var padded = ((recordsBytes + 7) / 8) * 8;
        var records = recordsBytes > 0 ? r.ReadBytes(recordsBytes).ToArray() : [];
        if (padded > recordsBytes) r.SkipPadding(padded - recordsBytes);
        return new RecordSet(recordId, serialNumber, lengthBits, count, records);
    }
}
