// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagement;

// --- Shared marshaling helpers used by every SimMan PDU ----------------------

internal static class SimManCodec
{
    /// <summary>
    /// Write the common SimMan prefix: header then originating +
    /// receiving entity ids. PDU callers pass the concrete header
    /// they want on the wire; the helper rewrites the length /
    /// PduType / ProtocolFamily so the caller can't get them wrong.
    /// </summary>
    internal static void WritePrefix(
        ref DisWireWriter w,
        PduHeader header,
        DisPduType pduType,
        ushort length,
        EntityId originating,
        EntityId receiving)
    {
        var rewritten = header with
        {
            PduType = pduType,
            ProtocolFamily = DisProtocolFamily.SimulationManagement,
            Length = length,
        };
        rewritten.Marshal(ref w);
        originating.Marshal(ref w);
        receiving.Marshal(ref w);
    }

    internal static void WriteDatums(
        ref DisWireWriter w,
        IReadOnlyList<FixedDatum>? fixedDatums,
        IReadOnlyList<VariableDatum>? variableDatums)
    {
        if (fixedDatums is not null)
            foreach (var d in fixedDatums) d.Marshal(ref w);
        if (variableDatums is not null)
            foreach (var d in variableDatums) d.Marshal(ref w);
    }

    internal static (List<FixedDatum> Fixed, List<VariableDatum> Variable)
        ReadDatums(ref DisWireReader r, uint fixedCount, uint variableCount)
    {
        var fixedDatums = new List<FixedDatum>((int)fixedCount);
        for (var i = 0; i < fixedCount; i++) fixedDatums.Add(FixedDatum.Unmarshal(ref r));
        var variableDatums = new List<VariableDatum>((int)variableCount);
        for (var i = 0; i < variableCount; i++) variableDatums.Add(VariableDatum.Unmarshal(ref r));
        return (fixedDatums, variableDatums);
    }

    internal static int DatumWireLength(
        IReadOnlyList<FixedDatum>? fixedDatums,
        IReadOnlyList<VariableDatum>? variableDatums)
    {
        var total = 0;
        if (fixedDatums is not null) total += fixedDatums.Count * FixedDatum.WireLength;
        if (variableDatums is not null)
            foreach (var d in variableDatums) total += d.WireLength;
        return total;
    }
}

// --- PDU: Create Entity (11) -------------------------------------------------

/// <summary>
/// Create Entity PDU (type 11, family 5). Requests a receiver to
/// create a simulated entity on behalf of the originator. The
/// receiver is expected to reply with an Acknowledge PDU carrying
/// the same <see cref="RequestId"/>. IEEE 1278.1 §5.3.6.1.
/// </summary>
public sealed record CreateEntityPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 28;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.CreateEntity, WireLength, OriginatingEntityId, ReceivingEntityId);
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
    public static CreateEntityPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        return new CreateEntityPdu(header, originating, receiving, requestId);
    }
}

// --- PDU: Remove Entity (12) -------------------------------------------------

/// <summary>
/// Remove Entity PDU (type 12, family 5). Requests the receiver to
/// remove an entity it owns. Same shape as
/// <see cref="CreateEntityPdu"/>. IEEE 1278.1 §5.3.6.2.
/// </summary>
public sealed record RemoveEntityPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 28;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.RemoveEntity, WireLength, OriginatingEntityId, ReceivingEntityId);
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
    public static RemoveEntityPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        return new RemoveEntityPdu(header, originating, receiving, requestId);
    }
}

// --- PDU: Start/Resume (13) --------------------------------------------------

/// <summary>
/// Start/Resume PDU (type 13, family 5). Signals the start or
/// resumption of a simulation exercise; includes the real-world and
/// simulation times at which the start / resume takes effect.
/// IEEE 1278.1 §5.3.6.3.
/// </summary>
public sealed record StartResumePdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    ClockTime RealWorldTime,
    ClockTime SimulationTime,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 44;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.StartResume, WireLength, OriginatingEntityId, ReceivingEntityId);
        RealWorldTime.Marshal(ref w);
        SimulationTime.Marshal(ref w);
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
    public static StartResumePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var realWorldTime = ClockTime.Unmarshal(ref r);
        var simulationTime = ClockTime.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        return new StartResumePdu(header, originating, receiving, realWorldTime, simulationTime, requestId);
    }
}

// --- PDU: Stop/Freeze (14) ---------------------------------------------------

/// <summary>
/// Stop/Freeze PDU (type 14, family 5). Signals the halting or
/// freezing of a simulation exercise. IEEE 1278.1 §5.3.6.4.
/// </summary>
public sealed record StopFreezePdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    ClockTime RealWorldTime,
    StopFreezeReason Reason,
    byte FrozenBehavior,
    uint RequestId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 40;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.StopFreeze, WireLength, OriginatingEntityId, ReceivingEntityId);
        RealWorldTime.Marshal(ref w);
        w.WriteByte((byte)Reason);
        w.WriteByte(FrozenBehavior);
        w.WriteUInt16(0); // padding
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
    public static StopFreezePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var realWorldTime = ClockTime.Unmarshal(ref r);
        var reason = (StopFreezeReason)r.ReadByte();
        var frozenBehavior = r.ReadByte();
        r.SkipPadding(2);
        var requestId = r.ReadUInt32();
        return new StopFreezePdu(header, originating, receiving, realWorldTime, reason, frozenBehavior, requestId);
    }
}

// --- PDU: Acknowledge (15) ---------------------------------------------------

/// <summary>
/// Acknowledge PDU (type 15, family 5). Replies to a Create Entity,
/// Remove Entity, Start/Resume, Stop/Freeze, or Transfer Ownership
/// request with ability-to-comply status. IEEE 1278.1 §5.3.6.5.
/// </summary>
public sealed record AcknowledgePdu(
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
        SimManCodec.WritePrefix(ref w, Header, DisPduType.Acknowledge, WireLength, OriginatingEntityId, ReceivingEntityId);
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
    public static AcknowledgePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var ackFlag = (AcknowledgeFlag)r.ReadUInt16();
        var responseFlag = (ResponseFlag)r.ReadUInt16();
        var requestId = r.ReadUInt32();
        return new AcknowledgePdu(header, originating, receiving, ackFlag, responseFlag, requestId);
    }
}

// --- PDU: Action Request (16) ------------------------------------------------

/// <summary>
/// Action Request PDU (type 16, family 5). Requests the receiver
/// to perform an action identified by <see cref="ActionId"/>, with
/// optional fixed / variable datum payload. IEEE 1278.1 §5.3.6.6.
/// </summary>
public sealed record ActionRequestPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    uint ActionId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength =>
        MinimumWireLength + SimManCodec.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.ActionRequest, (ushort)WireLength, OriginatingEntityId, ReceivingEntityId);
        w.WriteUInt32(RequestId);
        w.WriteUInt32(ActionId);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        SimManCodec.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static ActionRequestPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        var actionId = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = SimManCodec.ReadDatums(ref r, fixedCount, variableCount);
        return new ActionRequestPdu(header, originating, receiving, requestId, actionId, fixedDatums, variableDatums);
    }
}

// --- PDU: Action Response (17) -----------------------------------------------

/// <summary>
/// Action Response PDU (type 17, family 5). Reply to an Action
/// Request. <see cref="RequestStatus"/> reports execution status;
/// datum payload typically echoes or augments the request's data.
/// IEEE 1278.1 §5.3.6.7.
/// </summary>
public sealed record ActionResponsePdu(
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
    public int WireLength =>
        MinimumWireLength + SimManCodec.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.ActionResponse, (ushort)WireLength, OriginatingEntityId, ReceivingEntityId);
        w.WriteUInt32(RequestId);
        w.WriteUInt32(RequestStatus);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        SimManCodec.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static ActionResponsePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        var requestStatus = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = SimManCodec.ReadDatums(ref r, fixedCount, variableCount);
        return new ActionResponsePdu(header, originating, receiving, requestId, requestStatus, fixedDatums, variableDatums);
    }
}

// --- PDU: Data Query (18) ----------------------------------------------------

/// <summary>
/// Data Query PDU (type 18, family 5). Requests specific datum
/// values from the receiver. The payload is datum <i>ids</i>, not
/// datum values — one uint32 per fixed id, one uint32 per variable
/// id. IEEE 1278.1 §5.3.6.8.
/// </summary>
public sealed record DataQueryPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    uint TimeInterval,
    IReadOnlyList<uint>? FixedDatumIds = null,
    IReadOnlyList<uint>? VariableDatumIds = null)
{
    /// <summary>Fixed wire length before the datum-id lists.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datum ids (4 bytes each).</summary>
    public int WireLength =>
        MinimumWireLength
        + ((FixedDatumIds?.Count ?? 0) * 4)
        + ((VariableDatumIds?.Count ?? 0) * 4);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.DataQuery, (ushort)WireLength, OriginatingEntityId, ReceivingEntityId);
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
    public static DataQueryPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        var timeInterval = r.ReadUInt32();
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var fixedIds = new List<uint>((int)fixedCount);
        for (var i = 0; i < fixedCount; i++) fixedIds.Add(r.ReadUInt32());
        var variableIds = new List<uint>((int)variableCount);
        for (var i = 0; i < variableCount; i++) variableIds.Add(r.ReadUInt32());
        return new DataQueryPdu(header, originating, receiving, requestId, timeInterval, fixedIds, variableIds);
    }
}

// --- PDU: Set Data (19) ------------------------------------------------------

/// <summary>
/// Set Data PDU (type 19, family 5). Asks the receiver to update
/// the listed datum values. IEEE 1278.1 §5.3.6.9.
/// </summary>
public sealed record SetDataPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength =>
        MinimumWireLength + SimManCodec.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.SetData, (ushort)WireLength, OriginatingEntityId, ReceivingEntityId);
        w.WriteUInt32(RequestId);
        w.WriteUInt32(0); // padding
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        SimManCodec.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static SetDataPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        r.SkipPadding(4);
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = SimManCodec.ReadDatums(ref r, fixedCount, variableCount);
        return new SetDataPdu(header, originating, receiving, requestId, fixedDatums, variableDatums);
    }
}

// --- PDU: Data (20) ----------------------------------------------------------

/// <summary>
/// Data PDU (type 20, family 5). Carries datum values — either an
/// unsolicited push or a reply to a Data Query. Same layout as
/// <see cref="SetDataPdu"/>. IEEE 1278.1 §5.3.6.10.
/// </summary>
public sealed record DataPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength =>
        MinimumWireLength + SimManCodec.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.Data, (ushort)WireLength, OriginatingEntityId, ReceivingEntityId);
        w.WriteUInt32(RequestId);
        w.WriteUInt32(0); // padding
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        SimManCodec.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static DataPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        r.SkipPadding(4);
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = SimManCodec.ReadDatums(ref r, fixedCount, variableCount);
        return new DataPdu(header, originating, receiving, requestId, fixedDatums, variableDatums);
    }
}

// --- PDU: Event Report (21) --------------------------------------------------

/// <summary>
/// Event Report PDU (type 21, family 5). Reports an event of
/// interest to the exercise — malfunctions, weapon-system state
/// changes, training-significant events.
/// IEEE 1278.1 §5.3.6.11.
/// </summary>
public sealed record EventReportPdu(
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
    public int WireLength =>
        MinimumWireLength + SimManCodec.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.EventReport, (ushort)WireLength, OriginatingEntityId, ReceivingEntityId);
        w.WriteUInt32(EventType);
        w.WriteUInt32(0); // padding
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        SimManCodec.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static EventReportPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var eventType = r.ReadUInt32();
        r.SkipPadding(4);
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = SimManCodec.ReadDatums(ref r, fixedCount, variableCount);
        return new EventReportPdu(header, originating, receiving, eventType, fixedDatums, variableDatums);
    }
}

// --- PDU: Comment (22) -------------------------------------------------------

/// <summary>
/// Comment PDU (type 22, family 5). Free-form commentary alongside
/// an exercise. No request id — it's informational only, not
/// expected to be acknowledged. IEEE 1278.1 §5.3.6.12.
/// </summary>
public sealed record CommentPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    IReadOnlyList<FixedDatum>? FixedDatums = null,
    IReadOnlyList<VariableDatum>? VariableDatums = null)
{
    /// <summary>Fixed wire length before the datum records.</summary>
    public const int MinimumWireLength = 32;

    /// <summary>Total wire length including datums.</summary>
    public int WireLength =>
        MinimumWireLength + SimManCodec.DatumWireLength(FixedDatums, VariableDatums);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        SimManCodec.WritePrefix(ref w, Header, DisPduType.Comment, (ushort)WireLength, OriginatingEntityId, ReceivingEntityId);
        w.WriteUInt32((uint)(FixedDatums?.Count ?? 0));
        w.WriteUInt32((uint)(VariableDatums?.Count ?? 0));
        SimManCodec.WriteDatums(ref w, FixedDatums, VariableDatums);
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
    public static CommentPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var fixedCount = r.ReadUInt32();
        var variableCount = r.ReadUInt32();
        var (fixedDatums, variableDatums) = SimManCodec.ReadDatums(ref r, fixedCount, variableCount);
        return new CommentPdu(header, originating, receiving, fixedDatums, variableDatums);
    }
}
