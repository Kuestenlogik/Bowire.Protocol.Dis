// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.Minefield;

/// <summary>
/// The requesting-simulator field that Minefield Query, Data and Response
/// NACK share, and that changed size between versions (#58).
/// </summary>
/// <remarks>
/// Up to IEEE 1278.1a-1998 it is a Requesting Entity ID — site, application,
/// entity, 6 bytes (KDIS and open-dis dis6 both read it so). IEEE
/// 1278.1-2012 made it a Simulation Identifier — site, application, 4 bytes
/// (open-dis dis7). The plugin read 4 bytes for every version, so a V6
/// query was off by two bytes from there on.
/// </remarks>
internal static class MinefieldRequester
{
    /// <summary>Extra bytes a pre-2012 PDU carries for the entity number.</summary>
    internal static int ExtraBytes(DisProtocolVersion version) =>
        version >= DisProtocolVersion.Ieee1278_1_2012 ? 0 : sizeof(ushort);

    internal static void Marshal(ref DisWireWriter w, DisProtocolVersion version, SimulationAddress simulation, ushort entity)
    {
        simulation.Marshal(ref w);
        if (ExtraBytes(version) > 0) w.WriteUInt16(entity);
    }

    internal static (SimulationAddress Simulation, ushort Entity) Unmarshal(ref DisWireReader r, DisProtocolVersion version)
    {
        var simulation = SimulationAddress.Unmarshal(ref r);
        var entity = ExtraBytes(version) > 0 ? r.ReadUInt16() : (ushort)0;
        return (simulation, entity);
    }
}

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
    /// <summary>Fixed wire length before the typed perimeter / sensor lists (IEEE 1278.1-2012 layout).</summary>
    public const int MinimumWireLength = 38;

    /// <summary>
    /// Entity number of the requesting entity. Only a pre-2012 (V6) PDU carries
    /// it — there the requester is an entity, site:application:entity; from
    /// IEEE 1278.1-2012 on it is a simulation and this stays 0 (#58).
    /// </summary>
    public ushort RequestingEntity { get; init; }

    /// <summary>Fixed wire length for this PDU's protocol version (<see cref="MinimumWireLength"/> is the 2012 one).</summary>
    public int FixedWireLength => MinimumWireLength + MinefieldRequester.ExtraBytes(Header.ProtocolVersion);

    /// <summary>Total wire length including perimeter points and sensor types.</summary>
    public int WireLength =>
        FixedWireLength
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
        MinefieldRequester.Marshal(ref w, Header.ProtocolVersion, RequestingSimulationId, RequestingEntity);
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
        var (simId, requestingEntity) = MinefieldRequester.Unmarshal(ref r, header.ProtocolVersion);
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
            dataFilter, requestedMineType, perimeter, sensors)
        { RequestingEntity = requestingEntity };
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
/// The body contains, in order: sensor types (always present, then
/// padded to the next 32-bit boundary of the PDU), mine locations (always
/// present), then a variable block of per-mine arrays — ground burial
/// depths, orientations, emplacement times, mine entity numbers,
/// fusing, paint schemes and more, most of them gated by a DataFilter
/// bit.
/// </para>
/// <para>
/// The plugin used to read the sensor types from the end of the PDU
/// instead. That round-tripped with itself and with nothing else: any
/// Data PDU with a sensor type from another implementation decoded
/// wrong mine locations. The order here is the one both KDIS and
/// open-dis (generated from the SISO XML) use.
/// </para>
/// <para>
/// <see cref="MineLocations"/> and <see cref="SensorTypes"/> are typed.
/// The per-mine block is kept as <see cref="OptionalFieldsBlob"/> and
/// round-trips verbatim; <see cref="Mines"/> reads it as typed records
/// where it can (see <see cref="MinefieldMineFields"/> for the source of
/// that layout and when it declines).
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
    /// <summary>Fixed wire length before the sensor types start (IEEE 1278.1-2012 layout).</summary>
    public const int MinimumWireLength = 42;

    /// <summary>
    /// The per-mine fields of <see cref="OptionalFieldsBlob"/> as typed
    /// records, one per mine location — or null when the bytes do not read
    /// cleanly under <see cref="DataFilter"/> (#25). The blob stays what is
    /// written back, so a PDU round-trips byte for byte either way.
    /// </summary>
    public IReadOnlyList<MinefieldMine>? Mines =>
        MinefieldMineFields.TryDecode(OptionalFieldsBlob, DataFilter, MineLocations.Count, SensorTypes.Count);

    /// <summary>
    /// Entity number of the requesting entity. Only a pre-2012 (V6) PDU carries
    /// it — there the requester is an entity, site:application:entity; from
    /// IEEE 1278.1-2012 on it is a simulation and this stays 0 (#58).
    /// </summary>
    public ushort RequestingEntity { get; init; }

    /// <summary>Fixed wire length for this PDU's protocol version (<see cref="MinimumWireLength"/> is the 2012 one).</summary>
    public int FixedWireLength => MinimumWireLength + MinefieldRequester.ExtraBytes(Header.ProtocolVersion);

    /// <summary>Total wire length including sensor types, their padding, mine locations, and opaque optional fields.</summary>
    public int WireLength =>
        FixedWireLength
        + SensorTypesWireLength(FixedWireLength, SensorTypes.Count)
        + (MineLocations.Count * Vector3Float.WireLength)
        + OptionalFieldsBlob.Length;

    /// <summary>
    /// Sensor types are 16 bits each; the mine locations after them start on
    /// a 32-bit boundary of the PDU. With the 2012 fixed part (42 bytes) that
    /// is two bytes of padding after an even count, with the V6 one (44
    /// bytes) after an odd count.
    /// </summary>
    private static int SensorTypesWireLength(int fixedLength, int count)
    {
        var end = fixedLength + (count * sizeof(ushort));
        return (count * sizeof(ushort)) + ((4 - (end % 4)) % 4);
    }

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
        MinefieldRequester.Marshal(ref w, Header.ProtocolVersion, RequestingSimulationId, RequestingEntity);
        w.WriteUInt16(MinefieldSequenceNumber);
        w.WriteByte(RequestId);
        w.WriteByte(PduSequenceNumber);
        w.WriteByte(NumberOfPdus);
        w.WriteByte((byte)MineLocations.Count);
        w.WriteByte((byte)SensorTypes.Count);
        w.WriteByte(0); // padding
        w.WriteUInt32(DataFilter);
        MineType.Marshal(ref w);
        foreach (var sensor in SensorTypes) w.WriteUInt16(sensor);
        for (var i = SensorTypes.Count * sizeof(ushort); i < SensorTypesWireLength(FixedWireLength, SensorTypes.Count); i++)
            w.WriteByte(0); // padding to 32 bits
        foreach (var loc in MineLocations) loc.Marshal(ref w);
        w.WriteBytes(OptionalFieldsBlob);
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
        var (simId, requestingEntity) = MinefieldRequester.Unmarshal(ref r, header.ProtocolVersion);
        var seqNumber = r.ReadUInt16();
        var requestId = r.ReadByte();
        var pduSeq = r.ReadByte();
        var numPdus = r.ReadByte();
        var numMines = r.ReadByte();
        var numSensors = r.ReadByte();
        r.SkipPadding(1);
        var dataFilter = r.ReadUInt32();
        var mineType = EntityType.Unmarshal(ref r);

        var sensors = new List<ushort>(numSensors);
        for (var i = 0; i < numSensors; i++) sensors.Add(r.ReadUInt16());
        var fixedLength = MinimumWireLength + MinefieldRequester.ExtraBytes(header.ProtocolVersion);
        r.SkipPadding(SensorTypesWireLength(fixedLength, numSensors) - (numSensors * sizeof(ushort)));

        var mineLocations = new List<Vector3Float>(numMines);
        for (var i = 0; i < numMines; i++) mineLocations.Add(Vector3Float.Unmarshal(ref r));

        // Everything after the mine locations is the per-mine block.
        var fixedBytes = fixedLength + SensorTypesWireLength(fixedLength, numSensors) + (numMines * Vector3Float.WireLength);
        var optionalBytes = Math.Max(0, header.Length - fixedBytes);
        var optional = optionalBytes > 0 ? r.ReadBytes(optionalBytes).ToArray() : [];

        return new MinefieldDataPdu(
            header, minefieldId, simId, seqNumber, requestId, pduSeq,
            numPdus, dataFilter, mineType, mineLocations, sensors, optional)
        { RequestingEntity = requestingEntity };
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
/// The sequence numbers follow the count directly. The plugin used to put
/// two padding bytes in between, which none of KDIS, open-dis dis6 or
/// open-dis dis7 has (#58).
/// </remarks>
public sealed record MinefieldResponseNackPdu(
    PduHeader Header,
    EntityId MinefieldId,
    SimulationAddress RequestingSimulationId,
    byte RequestId,
    byte NumberOfMissingPdus,
    byte[] MissingPduSequenceNumbers)
{
    /// <summary>Fixed wire length before the missing-pdu list (IEEE 1278.1-2012 layout).</summary>
    public const int MinimumWireLength = 24;

    /// <summary>
    /// Entity number of the requesting entity. Only a pre-2012 (V6) PDU carries
    /// it — there the requester is an entity, site:application:entity; from
    /// IEEE 1278.1-2012 on it is a simulation and this stays 0 (#58).
    /// </summary>
    public ushort RequestingEntity { get; init; }

    /// <summary>Fixed wire length for this PDU's protocol version (<see cref="MinimumWireLength"/> is the 2012 one).</summary>
    public int FixedWireLength => MinimumWireLength + MinefieldRequester.ExtraBytes(Header.ProtocolVersion);

    /// <summary>Total wire length including the missing-pdu list.</summary>
    public int WireLength => FixedWireLength + MissingPduSequenceNumbers.Length;

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
        MinefieldRequester.Marshal(ref w, Header.ProtocolVersion, RequestingSimulationId, RequestingEntity);
        w.WriteByte(RequestId);
        w.WriteByte(NumberOfMissingPdus);
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
        var (simId, requestingEntity) = MinefieldRequester.Unmarshal(ref r, header.ProtocolVersion);
        var requestId = r.ReadByte();
        var numMissing = r.ReadByte();
        var missing = numMissing > 0 ? r.ReadBytes(numMissing).ToArray() : [];
        return new MinefieldResponseNackPdu(
            header, minefieldId, simId, requestId, numMissing, missing)
        { RequestingEntity = requestingEntity };
    }
}
