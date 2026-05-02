// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagement;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagementReliability;

// --- Shared helpers ----------------------------------------------------------

internal static class SimManRCodec
{
    /// <summary>
    /// Write the Reliability family's common prefix: header + two
    /// entity ids + reliability service byte + 3 bytes padding.
    /// </summary>
    internal static void WritePrefix(
        ref DisWireWriter w,
        PduHeader header,
        DisPduType pduType,
        ushort length,
        EntityId originating,
        EntityId receiving,
        RequiredReliabilityService reliability)
    {
        var rewritten = header with
        {
            PduType = pduType,
            ProtocolFamily = DisProtocolFamily.SimulationManagementWithReliability,
            Length = length,
        };
        rewritten.Marshal(ref w);
        originating.Marshal(ref w);
        receiving.Marshal(ref w);
        w.WriteByte((byte)reliability);
        w.WritePadding(3);
    }

    internal static (PduHeader Header, EntityId Originating, EntityId Receiving, RequiredReliabilityService Reliability)
        ReadPrefix(ref DisWireReader r)
    {
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var reliability = (RequiredReliabilityService)r.ReadByte();
        r.SkipPadding(3);
        return (header, originating, receiving, reliability);
    }
}

// --- PDU: Create Entity-R (51) -----------------------------------------------

/// <summary>
/// Create Entity-R PDU (type 51, family 10). Reliable-transport
/// variant of <see cref="CreateEntityPdu"/>. IEEE 1278.1 §5.3.12.1.
/// </summary>
public sealed record CreateEntityRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    RequiredReliabilityService Reliability,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 32;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.CreateEntityR, WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
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
    public static CreateEntityRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        return new CreateEntityRPdu(header, originating, receiving, reliability, requestId);
    }
}

// --- PDU: Remove Entity-R (52) -----------------------------------------------

/// <summary>
/// Remove Entity-R PDU (type 52, family 10). IEEE 1278.1 §5.3.12.2.
/// </summary>
public sealed record RemoveEntityRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    RequiredReliabilityService Reliability,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 32;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.RemoveEntityR, WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
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
    public static RemoveEntityRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        return new RemoveEntityRPdu(header, originating, receiving, reliability, requestId);
    }
}

// --- PDU: Start/Resume-R (53) ------------------------------------------------

/// <summary>
/// Start/Resume-R PDU (type 53, family 10). IEEE 1278.1 §5.3.12.3.
/// </summary>
public sealed record StartResumeRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    ClockTime RealWorldTime,
    ClockTime SimulationTime,
    RequiredReliabilityService Reliability,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 48;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        // Start/Resume-R keeps the two clock times BEFORE the
        // reliability byte on the wire so field positions stay
        // predictable despite the added byte — see §5.3.12.3.
        var header = Header with
        {
            PduType = DisPduType.StartResumeR,
            ProtocolFamily = DisProtocolFamily.SimulationManagementWithReliability,
            Length = WireLength,
        };
        header.Marshal(ref w);
        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        RealWorldTime.Marshal(ref w);
        SimulationTime.Marshal(ref w);
        w.WriteByte((byte)Reliability);
        w.WritePadding(3);
        w.WriteUInt32(RequestId);
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
    public static StartResumeRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var realWorld = ClockTime.Unmarshal(ref r);
        var simTime = ClockTime.Unmarshal(ref r);
        var reliability = (RequiredReliabilityService)r.ReadByte();
        r.SkipPadding(3);
        var requestId = r.ReadUInt32();
        return new StartResumeRPdu(header, originating, receiving, realWorld, simTime, reliability, requestId);
    }
}

// --- PDU: Stop/Freeze-R (54) -------------------------------------------------

/// <summary>
/// Stop/Freeze-R PDU (type 54, family 10). IEEE 1278.1 §5.3.12.4.
/// </summary>
public sealed record StopFreezeRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    ClockTime RealWorldTime,
    StopFreezeReason Reason,
    byte FrozenBehavior,
    RequiredReliabilityService Reliability,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 40;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.StopFreezeR,
            ProtocolFamily = DisProtocolFamily.SimulationManagementWithReliability,
            Length = WireLength,
        };
        header.Marshal(ref w);
        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        RealWorldTime.Marshal(ref w);
        w.WriteByte((byte)Reason);
        w.WriteByte(FrozenBehavior);
        w.WriteByte((byte)Reliability);
        w.WritePadding(1);
        w.WriteUInt32(RequestId);
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
    public static StopFreezeRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var realWorld = ClockTime.Unmarshal(ref r);
        var reason = (StopFreezeReason)r.ReadByte();
        var frozenBehavior = r.ReadByte();
        var reliability = (RequiredReliabilityService)r.ReadByte();
        r.SkipPadding(1);
        var requestId = r.ReadUInt32();
        return new StopFreezeRPdu(header, originating, receiving, realWorld, reason, frozenBehavior, reliability, requestId);
    }
}

// --- PDU: Acknowledge-R (55) -------------------------------------------------

/// <summary>
/// Acknowledge-R PDU (type 55, family 10). Reliable-transport
/// Acknowledge — no reliability-service field since acknowledge is
/// the reliability mechanism itself. IEEE 1278.1 §5.3.12.5.
/// </summary>
public sealed record AcknowledgeRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    AcknowledgeFlag AcknowledgeFlag,
    ResponseFlag ResponseFlag,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 32;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.AcknowledgeR,
            ProtocolFamily = DisProtocolFamily.SimulationManagementWithReliability,
            Length = WireLength,
        };
        header.Marshal(ref w);
        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        w.WriteUInt16((ushort)AcknowledgeFlag);
        w.WriteUInt16((ushort)ResponseFlag);
        w.WriteUInt32(RequestId);
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
    public static AcknowledgeRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var ackFlag = (AcknowledgeFlag)r.ReadUInt16();
        var responseFlag = (ResponseFlag)r.ReadUInt16();
        var requestId = r.ReadUInt32();
        return new AcknowledgeRPdu(header, originating, receiving, ackFlag, responseFlag, requestId);
    }
}

// --- PDU: Action Request-R (56) ----------------------------------------------

/// <summary>
/// Action Request-R PDU (type 56, family 10). IEEE 1278.1 §5.3.12.6.
/// </summary>
public sealed record ActionRequestRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    RequiredReliabilityService Reliability,
    uint RequestId,
    uint ActionId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 44;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength => MinimumWireLength + DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.ActionRequestR, (ushort)WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
        w.WriteUInt32(ActionId);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static ActionRequestRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        var actionId = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = ReadDatums(ref r, fixedCount, variableCount);
        return new ActionRequestRPdu(header, originating, receiving, reliability, requestId, actionId, fixedDatums, variableDatums);
    }

    internal static int DatumWireLength(IReadOnlyList<FixedDatum>? fd, IReadOnlyList<VariableDatum>? vd)
    {
        var total = 0;
        if (fd is not null) total += fd.Count * FixedDatum.WireLength;
        if (vd is not null) foreach (var d in vd) total += d.WireLength;
        return total;
    }

    internal static void WriteDatums(ref DisWireWriter w, IReadOnlyList<FixedDatum>? fd, IReadOnlyList<VariableDatum>? vd)
    {
        if (fd is not null) foreach (var d in fd) d.Marshal(ref w);
        if (vd is not null) foreach (var d in vd) d.Marshal(ref w);
    }

    internal static (List<FixedDatum> Fixed, List<VariableDatum> Variable)
        ReadDatums(ref DisWireReader r, uint fixedCount, uint variableCount)
    {
        var fd = new List<FixedDatum>((int)fixedCount);
        for (var i = 0; i < fixedCount; i++) fd.Add(FixedDatum.Unmarshal(ref r));
        var vd = new List<VariableDatum>((int)variableCount);
        for (var i = 0; i < variableCount; i++) vd.Add(VariableDatum.Unmarshal(ref r));
        return (fd, vd);
    }
}

// --- PDU: Action Response-R (57) ---------------------------------------------

/// <summary>
/// Action Response-R PDU (type 57, family 10). IEEE 1278.1 §5.3.12.7.
/// No reliability service byte — responses reuse the request's
/// transport guarantee.
/// </summary>
public sealed record ActionResponseRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    uint RequestStatus,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength => MinimumWireLength + ActionRequestRPdu.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ActionResponseR,
            ProtocolFamily = DisProtocolFamily.SimulationManagementWithReliability,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);
        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        w.WriteUInt32(RequestId);
        w.WriteUInt32(RequestStatus);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        ActionRequestRPdu.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static ActionResponseRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        var requestStatus = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = ActionRequestRPdu.ReadDatums(ref r, fixedCount, variableCount);
        return new ActionResponseRPdu(header, originating, receiving, requestId, requestStatus, fixedDatums, variableDatums);
    }
}

// --- PDU: Data Query-R (58) --------------------------------------------------

/// <summary>
/// Data Query-R PDU (type 58, family 10). IEEE 1278.1 §5.3.12.8.
/// </summary>
public sealed record DataQueryRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    RequiredReliabilityService Reliability,
    uint RequestId,
    uint TimeInterval,
    IReadOnlyList<uint>? FixedDatumIds = null,
    IReadOnlyList<uint>? VariableDatumIds = null)
{
    /// <summary>Fixed wire length before the datum-id lists.</summary>
    public const int MinimumWireLength = 44;

    /// <summary>Total wire length including datum ids.</summary>
    public int WireLength =>
        MinimumWireLength
        + ((FixedDatumIds?.Count ?? 0) * 4)
        + ((VariableDatumIds?.Count ?? 0) * 4);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.DataQueryR, (ushort)WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
        w.WriteUInt32(TimeInterval);
        w.WriteUInt32((uint)(FixedDatumIds?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatumIds?.Count ?? 0));
        if (FixedDatumIds is not null) foreach (var id in FixedDatumIds) w.WriteUInt32(id);
        if (VariableDatumIds is not null) foreach (var id in VariableDatumIds) w.WriteUInt32(id);
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
    public static DataQueryRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        var timeInterval = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var fixedIds = new List<uint>((int)fixedCount);
        for (var i = 0; i < fixedCount; i++) fixedIds.Add(r.ReadUInt32());
        var variableIds = new List<uint>((int)variableCount);
        for (var i = 0; i < variableCount; i++) variableIds.Add(r.ReadUInt32());
        return new DataQueryRPdu(header, originating, receiving, reliability, requestId, timeInterval, fixedIds, variableIds);
    }
}

// --- PDU: Set Data-R (59) ----------------------------------------------------

/// <summary>
/// Set Data-R PDU (type 59, family 10). IEEE 1278.1 §5.3.12.9.
/// </summary>
public sealed record SetDataRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    RequiredReliabilityService Reliability,
    uint RequestId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength => MinimumWireLength + ActionRequestRPdu.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.SetDataR, (ushort)WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        ActionRequestRPdu.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static SetDataRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = ActionRequestRPdu.ReadDatums(ref r, fixedCount, variableCount);
        return new SetDataRPdu(header, originating, receiving, reliability, requestId, fixedDatums, variableDatums);
    }
}

// --- PDU: Data-R (60) --------------------------------------------------------

/// <summary>
/// Data-R PDU (type 60, family 10). IEEE 1278.1 §5.3.12.10.
/// </summary>
public sealed record DataRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    RequiredReliabilityService Reliability,
    uint RequestId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength => MinimumWireLength + ActionRequestRPdu.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.DataR, (ushort)WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        ActionRequestRPdu.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static DataRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = ActionRequestRPdu.ReadDatums(ref r, fixedCount, variableCount);
        return new DataRPdu(header, originating, receiving, reliability, requestId, fixedDatums, variableDatums);
    }
}

// --- PDU: Event Report-R (61) ------------------------------------------------

/// <summary>
/// Event Report-R PDU (type 61, family 10). One-way informational;
/// no reliability-service field. IEEE 1278.1 §5.3.12.11.
/// </summary>
public sealed record EventReportRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint EventType,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength => MinimumWireLength + ActionRequestRPdu.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.EventReportR,
            ProtocolFamily = DisProtocolFamily.SimulationManagementWithReliability,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);
        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        w.WriteUInt32(EventType);
        w.WriteUInt32(0); // padding
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        ActionRequestRPdu.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static EventReportRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var eventType = r.ReadUInt32();
        r.SkipPadding(4);
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = ActionRequestRPdu.ReadDatums(ref r, fixedCount, variableCount);
        return new EventReportRPdu(header, originating, receiving, eventType, fixedDatums, variableDatums);
    }
}

// --- PDU: Comment-R (62) -----------------------------------------------------

/// <summary>
/// Comment-R PDU (type 62, family 10). Free-form commentary; no
/// reliability-service field since comments are informational.
/// IEEE 1278.1 §5.3.12.12.
/// </summary>
public sealed record CommentRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 32;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength => MinimumWireLength + ActionRequestRPdu.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.CommentR,
            ProtocolFamily = DisProtocolFamily.SimulationManagementWithReliability,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);
        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        ActionRequestRPdu.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static CommentRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = ActionRequestRPdu.ReadDatums(ref r, fixedCount, variableCount);
        return new CommentRPdu(header, originating, receiving, fixedDatums, variableDatums);
    }
}

// --- PDU: Record-R (63) ------------------------------------------------------

/// <summary>
/// Record-R PDU (type 63, family 10). Reports record-query results
/// carrying one or more <see cref="RecordSet"/> records.
/// IEEE 1278.1 §5.3.12.13.
/// </summary>
public sealed record RecordRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    RequiredReliabilityService Reliability,
    ushort EventType,
    uint ResponseSerialNumber,
    IReadOnlyList<RecordSet> RecordSets)
{
    /// <summary>Fixed wire length before the record sets.</summary>
    public const int MinimumWireLength = 48;

    /// <summary>Total wire length including record sets.</summary>
    public int WireLength
    {
        get
        {
            var total = MinimumWireLength;
            foreach (var rs in RecordSets) total += rs.WireLength;
            return total;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.RecordR, (ushort)WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
        w.WriteUInt16(EventType);
        w.WriteUInt16(0); // padding
        w.WriteUInt32(ResponseSerialNumber);
        w.WriteUInt32((uint)RecordSets.Count);
        foreach (var rs in RecordSets) rs.Marshal(ref w);
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
    public static RecordRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        var eventType = r.ReadUInt16();
        r.SkipPadding(2);
        var serial = r.ReadUInt32();
        var recordSetCount = r.ReadUInt32();
        var recordSets = new List<RecordSet>((int)recordSetCount);
        for (var i = 0; i < recordSetCount; i++) recordSets.Add(RecordSet.Unmarshal(ref r));
        return new RecordRPdu(header, originating, receiving, requestId, reliability, eventType, serial, recordSets);
    }
}

// --- PDU: Set Record-R (64) --------------------------------------------------

/// <summary>
/// Set Record-R PDU (type 64, family 10). Requests the receiver to
/// update the listed records. Carries one or more
/// <see cref="RecordSet"/> records. IEEE 1278.1 §5.3.12.14.
/// </summary>
public sealed record SetRecordRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    RequiredReliabilityService Reliability,
    IReadOnlyList<RecordSet> RecordSets)
{
    /// <summary>Fixed wire length before the record sets.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including record sets.</summary>
    public int WireLength
    {
        get
        {
            var total = MinimumWireLength;
            foreach (var rs in RecordSets) total += rs.WireLength;
            return total;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.SetRecordR, (ushort)WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
        w.WriteUInt32((uint)RecordSets.Count);
        foreach (var rs in RecordSets) rs.Marshal(ref w);
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
    public static SetRecordRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        var recordSetCount = r.ReadUInt32();
        var recordSets = new List<RecordSet>((int)recordSetCount);
        for (var i = 0; i < recordSetCount; i++) recordSets.Add(RecordSet.Unmarshal(ref r));
        return new SetRecordRPdu(header, originating, receiving, requestId, reliability, recordSets);
    }
}

// --- PDU: Record Query-R (65) ------------------------------------------------

/// <summary>
/// Record Query-R PDU (type 65, family 10). Requests specific
/// record types from the receiver. IEEE 1278.1 §5.3.12.15.
/// </summary>
public sealed record RecordQueryRPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    RequiredReliabilityService Reliability,
    ushort EventType,
    uint TimeInterval,
    IReadOnlyList<uint>? RecordIds = null)
{
    /// <summary>Fixed wire length before the record id list.</summary>
    public const int MinimumWireLength = 44;

    /// <summary>Total wire length including record ids.</summary>
    public int WireLength => MinimumWireLength + ((RecordIds?.Count ?? 0) * 4);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManRCodec.WritePrefix(ref w, Header, DisPduType.RecordQueryR, (ushort)WireLength,
            OriginatingEntityId, ReceivingEntityId, Reliability);
        w.WriteUInt32(RequestId);
        w.WriteUInt16(EventType);
        w.WriteUInt16(0); // padding
        w.WriteUInt32(TimeInterval);
        w.WriteUInt32((uint)(RecordIds?.Count ?? 0));
        if (RecordIds is not null) foreach (var id in RecordIds) w.WriteUInt32(id);
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
    public static RecordQueryRPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var (header, originating, receiving, reliability) = SimManRCodec.ReadPrefix(ref r);
        var requestId = r.ReadUInt32();
        var eventType = r.ReadUInt16();
        r.SkipPadding(2);
        var timeInterval = r.ReadUInt32();
        var recordCount = r.ReadUInt32();
        var recordIds = new List<uint>((int)recordCount);
        for (var i = 0; i < recordCount; i++) recordIds.Add(r.ReadUInt32());
        return new RecordQueryRPdu(header, originating, receiving, requestId, reliability, eventType, timeInterval, recordIds);
    }
}
