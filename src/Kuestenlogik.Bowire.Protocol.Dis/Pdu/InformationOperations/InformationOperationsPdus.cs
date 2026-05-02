// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.InformationOperations;

// --- PDU: IO Action (81) -----------------------------------------------------

/// <summary>
/// Information Operations Action PDU (type 81, family 13, V7).
/// Describes an Information Operations event — cyber attack,
/// electromagnetic warfare action, psychological operations effect.
/// New in IEEE 1278.1-2012. §7.3.13.1.
/// </summary>
/// <remarks>
/// IO record sets are exposed as typed
/// <see cref="StandardVariableRecord"/>s (§6.2.82); per-record-type
/// shapes come from the SISO-REF-010 IO record-type enumeration.
/// </remarks>
public sealed record InformationOperationsActionPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    ushort IoWarfareType,
    ushort IoSimulationSource,
    ushort IoActionType,
    ushort IoActionPhase,
    uint IoActionParameter1,
    uint IoActionParameter2,
    EntityId IoAttackerId,
    EntityId IoPrimaryTargetId,
    IReadOnlyList<StandardVariableRecord> IoRecordSets)
{
    /// <summary>Fixed wire length before the record-sets blob.</summary>
    public const int MinimumWireLength = 60;

    /// <summary>Total wire length including every IO record (each padded to 64-bit).</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var record in IoRecordSets) sum += record.WireLength;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.InformationOperationsAction,
            ProtocolFamily = DisProtocolFamily.InformationOperations,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        w.WriteUInt32(RequestId);
        w.WriteUInt16(IoWarfareType);
        w.WriteUInt16(IoSimulationSource);
        w.WriteUInt16(IoActionType);
        w.WriteUInt16(IoActionPhase);
        w.WriteUInt32(IoActionParameter1);
        w.WriteUInt32(IoActionParameter2);
        IoAttackerId.Marshal(ref w);
        IoPrimaryTargetId.Marshal(ref w);
        w.WriteUInt16((ushort)IoRecordSets.Count);
        w.WriteUInt16(0); // padding
        foreach (var record in IoRecordSets) record.Marshal(ref w);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static InformationOperationsActionPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        var warfareType = r.ReadUInt16();
        var simSource = r.ReadUInt16();
        var actionType = r.ReadUInt16();
        var actionPhase = r.ReadUInt16();
        var actionParam1 = r.ReadUInt32();
        var actionParam2 = r.ReadUInt32();
        var attackerId = EntityId.Unmarshal(ref r);
        var primaryTargetId = EntityId.Unmarshal(ref r);
        var numRecordSets = r.ReadUInt16();
        r.SkipPadding(2);
        var records = new List<StandardVariableRecord>(numRecordSets);
        for (var i = 0; i < numRecordSets; i++)
            records.Add(StandardVariableRecord.Unmarshal(ref r));
        return new InformationOperationsActionPdu(
            header, originating, receiving, requestId,
            warfareType, simSource, actionType, actionPhase,
            actionParam1, actionParam2, attackerId, primaryTargetId,
            records);
    }
}

// --- PDU: IO Report (82) -----------------------------------------------------

/// <summary>
/// Information Operations Report PDU (type 82, family 13, V7).
/// Reports the outcome of an IO action — success/failure, observed
/// effects, diagnostic data. IEEE 1278.1-2012 §7.3.13.2.
/// </summary>
/// <remarks>
/// IO record sets exposed as typed <see cref="StandardVariableRecord"/>s
/// (§6.2.82) — same wire layout as the Action PDU.
/// </remarks>
public sealed record InformationOperationsReportPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    byte IoSimulationSource,
    ushort IoReportType,
    EntityId IoAttackerId,
    EntityId IoPrimaryTargetId,
    IReadOnlyList<StandardVariableRecord> IoRecordSets)
{
    /// <summary>Fixed wire length before the record-sets blob.</summary>
    public const int MinimumWireLength = 48;

    /// <summary>Total wire length including every IO record (each padded to 64-bit).</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var record in IoRecordSets) sum += record.WireLength;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.InformationOperationsReport,
            ProtocolFamily = DisProtocolFamily.InformationOperations,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        OriginatingEntityId.Marshal(ref w);
        w.WriteByte(IoSimulationSource);
        w.WritePadding(1);
        w.WriteUInt16(IoReportType);
        w.WriteUInt32(0); // padding
        IoAttackerId.Marshal(ref w);
        IoPrimaryTargetId.Marshal(ref w);
        w.WriteUInt16(0); // padding
        w.WriteUInt16((ushort)IoRecordSets.Count);
        foreach (var record in IoRecordSets) record.Marshal(ref w);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static InformationOperationsReportPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var simSource = r.ReadByte();
        r.SkipPadding(1);
        var reportType = r.ReadUInt16();
        r.SkipPadding(4);
        var attackerId = EntityId.Unmarshal(ref r);
        var primaryTargetId = EntityId.Unmarshal(ref r);
        r.SkipPadding(2);
        var numRecordSets = r.ReadUInt16();
        var records = new List<StandardVariableRecord>(numRecordSets);
        for (var i = 0; i < numRecordSets; i++)
            records.Add(StandardVariableRecord.Unmarshal(ref r));
        return new InformationOperationsReportPdu(
            header, originating, simSource, reportType,
            attackerId, primaryTargetId, records);
    }
}
