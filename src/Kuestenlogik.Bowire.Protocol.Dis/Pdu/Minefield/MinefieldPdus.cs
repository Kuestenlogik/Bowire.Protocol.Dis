// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.Minefield;

// --- PDU: Minefield State (37) -----------------------------------------------

/// <summary>
/// Minefield State PDU (type 37, family 8). Broadcast by a minefield
/// simulator to advertise the presence, perimeter, and mine-type mix
/// of a minefield. IEEE 1278.1 §5.3.10.1.
/// </summary>
/// <remarks>
/// <para>
/// Layout (72 bytes fixed + perimeter points + mine types blob):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="MinefieldId"/></item>
///   <item>18 :  2 — <see cref="MinefieldSequence"/></item>
///   <item>20 :  1 — <see cref="ForceId"/></item>
///   <item>21 :  1 — <see cref="PerimeterPoints"/> count</item>
///   <item>22 :  8 — <see cref="MinefieldType"/></item>
///   <item>30 :  2 — <see cref="MineTypes"/> count</item>
///   <item>32 : 24 — <see cref="MinefieldLocation"/> (ECEF)</item>
///   <item>56 : 12 — <see cref="MinefieldOrientation"/></item>
///   <item>68 :  2 — <see cref="AppearanceCode"/></item>
///   <item>70 :  2 — <see cref="ProtocolMode"/></item>
///   <item>72 :  N — perimeter points (<see cref="Vector2Float"/>) + mine types (<see cref="EntityType"/>)</item>
/// </list>
/// <para>
/// Perimeter points and the mine-type list are both fully typed:
/// perimeter points round-trip as 2D <see cref="Vector2Float"/>s (§6.2.97)
/// and mine types as standard <see cref="EntityType"/> seven-tuples.
/// </para>
/// </remarks>
public sealed record MinefieldStatePdu(
    PduHeader Header,
    EntityId MinefieldId,
    ushort MinefieldSequence,
    ForceId ForceId,
    EntityType MinefieldType,
    Vector3Double MinefieldLocation,
    EulerAngles MinefieldOrientation,
    ushort AppearanceCode,
    ushort ProtocolMode,
    IReadOnlyList<Vector2Float> PerimeterPoints,
    IReadOnlyList<EntityType> MineTypes)
{
    /// <summary>Fixed wire length before the typed lists start.</summary>
    public const int MinimumWireLength = 72;

    /// <summary>Total wire length including perimeter points and mine types.</summary>
    public int WireLength =>
        MinimumWireLength
        + (PerimeterPoints.Count * Vector2Float.WireLength)
        + (MineTypes.Count * EntityType.WireLength);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.MinefieldState,
            ProtocolFamily = DisProtocolFamily.Minefield,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        MinefieldId.Marshal(ref w);
        w.WriteUInt16(MinefieldSequence);
        w.WriteByte((byte)ForceId);
        w.WriteByte((byte)PerimeterPoints.Count);
        MinefieldType.Marshal(ref w);
        w.WriteUInt16((ushort)MineTypes.Count);
        MinefieldLocation.Marshal(ref w);
        MinefieldOrientation.Marshal(ref w);
        w.WriteUInt16(AppearanceCode);
        w.WriteUInt16(ProtocolMode);
        foreach (var pt in PerimeterPoints) pt.Marshal(ref w);
        foreach (var type in MineTypes) type.Marshal(ref w);
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
    public static MinefieldStatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var minefieldId = EntityId.Unmarshal(ref r);
        var seq = r.ReadUInt16();
        var force = (ForceId)r.ReadByte();
        var numPerimeter = r.ReadByte();
        var minefieldType = EntityType.Unmarshal(ref r);
        var numMineTypes = r.ReadUInt16();
        var location = Vector3Double.Unmarshal(ref r);
        var orientation = EulerAngles.Unmarshal(ref r);
        var appearance = r.ReadUInt16();
        var protocolMode = r.ReadUInt16();
        var perimeter = new List<Vector2Float>(numPerimeter);
        for (var i = 0; i < numPerimeter; i++) perimeter.Add(Vector2Float.Unmarshal(ref r));
        var mineTypes = new List<EntityType>(numMineTypes);
        for (var i = 0; i < numMineTypes; i++) mineTypes.Add(EntityType.Unmarshal(ref r));
        return new MinefieldStatePdu(
            header, minefieldId, seq, force, minefieldType,
            location, orientation, appearance, protocolMode, perimeter, mineTypes);
    }
}

// --- PDU: Minefield Query (38) -----------------------------------------------

/// <summary>
/// Minefield Query PDU (type 38, family 8). Requests mine data for
/// a specific minefield from the originating simulator. The query
/// can be constrained by a perimeter polygon, a sensor-type filter,
/// and a data-filter bitmap. IEEE 1278.1 §5.3.10.2.
/// </summary>
public sealed record MinefieldQueryPdu(
    PduHeader Header,
    EntityId MinefieldId,
    SimulationAddress RequestingSimulationId,
    byte RequestId,
    uint DataFilter,
    EntityType RequestedMineType,
    IReadOnlyList<Vector2Float> PerimeterPoints,
    IReadOnlyList<ushort> SensorTypes)
{
    /// <summary>Fixed wire length before the typed perimeter / sensor lists.</summary>
    public const int MinimumWireLength = 38;

    /// <summary>Total wire length including perimeter points and sensor types.</summary>
    public int WireLength =>
        MinimumWireLength
        + (PerimeterPoints.Count * Vector2Float.WireLength)
        + (SensorTypes.Count * sizeof(ushort));

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.MinefieldQuery,
            ProtocolFamily = DisProtocolFamily.Minefield,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        MinefieldId.Marshal(ref w);
        RequestingSimulationId.Marshal(ref w);
        w.WriteByte(RequestId);
        w.WriteByte((byte)PerimeterPoints.Count);
        w.WriteByte(0); // padding
        w.WriteByte((byte)SensorTypes.Count);
        w.WriteUInt32(DataFilter);
        RequestedMineType.Marshal(ref w);
        foreach (var pt in PerimeterPoints) pt.Marshal(ref w);
        foreach (var sensor in SensorTypes) w.WriteUInt16(sensor);
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
    public static MinefieldQueryPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var minefieldId = EntityId.Unmarshal(ref r);
        var simId = SimulationAddress.Unmarshal(ref r);
        var requestId = r.ReadByte();
        var numPerimeter = r.ReadByte();
        r.SkipPadding(1);
        var numSensors = r.ReadByte();
        var dataFilter = r.ReadUInt32();
        var requestedMineType = EntityType.Unmarshal(ref r);
        var perimeter = new List<Vector2Float>(numPerimeter);
        for (var i = 0; i < numPerimeter; i++) perimeter.Add(Vector2Float.Unmarshal(ref r));
        var sensors = new List<ushort>(numSensors);
        for (var i = 0; i < numSensors; i++) sensors.Add(r.ReadUInt16());
        return new MinefieldQueryPdu(
            header, minefieldId, simId, requestId,
            dataFilter, requestedMineType, perimeter, sensors);
    }
}

// --- PDU: Minefield Data (39) ------------------------------------------------

/// <summary>
/// Minefield Data PDU (type 39, family 8). Response to a Minefield
/// Query carrying the queried mines' positions and per-mine optional
/// attributes (orientation, fuse type, emplacement time, etc., each
/// toggled by a bit in DataFilter). IEEE 1278.1 §5.3.10.3.
/// </summary>
/// <remarks>
/// <para>
/// The body contains, in order: mine locations (always present), a
/// variable block of DataFilter-gated optional arrays (ground burial
/// depths, orientations, emplacement times, fusing, paint schemes,
/// etc. — the spec lists 12 possible arrays each gated by a single
/// DataFilter bit), then sensor types (always present).
/// </para>
/// <para>
/// <see cref="MineLocations"/> and <see cref="SensorTypes"/> are typed;
/// the DataFilter-gated middle section is kept as a
/// <see cref="OptionalFieldsBlob"/> byte array and round-trips
/// verbatim. Typed decoders for the optional arrays would need the
/// exact DataFilter bit-to-array mapping from SISO test vectors.
/// </para>
/// </remarks>
public sealed record MinefieldDataPdu(
    PduHeader Header,
    EntityId MinefieldId,
    SimulationAddress RequestingSimulationId,
    ushort MinefieldSequenceNumber,
    byte RequestId,
    byte PduSequenceNumber,
    byte NumberOfPdus,
    uint DataFilter,
    EntityType MineType,
    IReadOnlyList<Vector3Float> MineLocations,
    IReadOnlyList<ushort> SensorTypes,
    byte[] OptionalFieldsBlob)
{
    /// <summary>Fixed wire length before the mine-locations list starts.</summary>
    public const int MinimumWireLength = 42;

    /// <summary>Total wire length including mine locations, sensor types, and opaque optional fields.</summary>
    public int WireLength =>
        MinimumWireLength
        + (MineLocations.Count * Vector3Float.WireLength)
        + OptionalFieldsBlob.Length
        + (SensorTypes.Count * sizeof(ushort));

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.MinefieldData,
            ProtocolFamily = DisProtocolFamily.Minefield,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        MinefieldId.Marshal(ref w);
        RequestingSimulationId.Marshal(ref w);
        w.WriteUInt16(MinefieldSequenceNumber);
        w.WriteByte(RequestId);
        w.WriteByte(PduSequenceNumber);
        w.WriteByte(NumberOfPdus);
        w.WriteByte((byte)MineLocations.Count);
        w.WriteByte((byte)SensorTypes.Count);
        w.WriteByte(0); // padding
        w.WriteUInt32(DataFilter);
        MineType.Marshal(ref w);
        foreach (var loc in MineLocations) loc.Marshal(ref w);
        w.WriteBytes(OptionalFieldsBlob);
        foreach (var sensor in SensorTypes) w.WriteUInt16(sensor);
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
    public static MinefieldDataPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var minefieldId = EntityId.Unmarshal(ref r);
        var simId = SimulationAddress.Unmarshal(ref r);
        var seqNumber = r.ReadUInt16();
        var requestId = r.ReadByte();
        var pduSeq = r.ReadByte();
        var numPdus = r.ReadByte();
        var numMines = r.ReadByte();
        var numSensors = r.ReadByte();
        r.SkipPadding(1);
        var dataFilter = r.ReadUInt32();
        var mineType = EntityType.Unmarshal(ref r);

        var mineLocations = new List<Vector3Float>(numMines);
        for (var i = 0; i < numMines; i++) mineLocations.Add(Vector3Float.Unmarshal(ref r));

        // Remaining body after mine locations = optional-fields blob
        // + sensor-types tail. We know exactly how many sensor-type
        // bytes trail (2 bytes each) so the optional-fields blob is
        // everything between.
        var totalBody = Math.Max(0, header.Length - MinimumWireLength);
        var mineLocBytes = numMines * Vector3Float.WireLength;
        var sensorBytes = numSensors * sizeof(ushort);
        var optionalBytes = Math.Max(0, totalBody - mineLocBytes - sensorBytes);
        var optional = optionalBytes > 0 ? r.ReadBytes(optionalBytes).ToArray() : [];

        var sensors = new List<ushort>(numSensors);
        for (var i = 0; i < numSensors; i++) sensors.Add(r.ReadUInt16());

        return new MinefieldDataPdu(
            header, minefieldId, simId, seqNumber, requestId, pduSeq,
            numPdus, dataFilter, mineType, mineLocations, sensors, optional);
    }
}

// --- PDU: Minefield Response NACK (40) ---------------------------------------

/// <summary>
/// Minefield Response NACK PDU (type 40, family 8). Sent when a
/// simulator misses one or more Minefield Data PDUs in a queried
/// sequence; lists the missing PDU sequence numbers so the
/// originator can retransmit. IEEE 1278.1 §5.3.10.4.
/// </summary>
/// <remarks>
/// Same PDU type id (40) as Collision-Elastic in family 1; the
/// protocol-family byte disambiguates. Receivers must never dispatch
/// by PDU type alone — this is why Bowire's decoder tables key on
/// (family, type) pairs.
/// </remarks>
public sealed record MinefieldResponseNackPdu(
    PduHeader Header,
    EntityId MinefieldId,
    SimulationAddress RequestingSimulationId,
    byte RequestId,
    byte NumberOfMissingPdus,
    byte[] MissingPduSequenceNumbers)
{
    /// <summary>Fixed wire length before the missing-pdu list.</summary>
    public const int MinimumWireLength = 26;

    /// <summary>Total wire length including the missing-pdu list.</summary>
    public int WireLength => MinimumWireLength + MissingPduSequenceNumbers.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.MinefieldResponseNack,
            ProtocolFamily = DisProtocolFamily.Minefield,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        MinefieldId.Marshal(ref w);
        RequestingSimulationId.Marshal(ref w);
        w.WriteByte(RequestId);
        w.WriteByte(NumberOfMissingPdus);
        w.WriteUInt16(0); // padding
        w.WriteBytes(MissingPduSequenceNumbers);
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
    public static MinefieldResponseNackPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var minefieldId = EntityId.Unmarshal(ref r);
        var simId = SimulationAddress.Unmarshal(ref r);
        var requestId = r.ReadByte();
        var numMissing = r.ReadByte();
        r.SkipPadding(2);
        var missing = numMissing > 0 ? r.ReadBytes(numMissing).ToArray() : [];
        return new MinefieldResponseNackPdu(
            header, minefieldId, simId, requestId, numMissing, missing);
    }
}
