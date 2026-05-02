// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.SyntheticEnvironment;

// --- PDU: Environmental Process (41) -----------------------------------------

/// <summary>
/// Environmental Process PDU (type 41, family 9). Reports an
/// environmental process (weather front, plume dispersion, smoke
/// cloud propagation, ...) with a stream of environment records
/// describing the evolving state. IEEE 1278.1 §5.3.11.1.
/// </summary>
/// <remarks>
/// Environment records use the §6.2.54 Environment Record shape,
/// which is the same 8-byte-header + padded-body pattern as §6.2.82
/// <see cref="StandardVariableRecord"/>. Per-record-type bodies come
/// from the SISO-REF-010 environment-record-type enumeration; this
/// codec round-trips the content bytes verbatim.
/// </remarks>
public sealed record EnvironmentalProcessPdu(
    PduHeader Header,
    EntityId EnvironmentalProcessId,
    EntityType EnvironmentType,
    byte ModelType,
    byte EnvironmentStatus,
    ushort SequenceNumber,
    IReadOnlyList<StandardVariableRecord> EnvironmentRecords)
{
    /// <summary>Fixed wire length before the environment-records list.</summary>
    public const int MinimumWireLength = 32;

    /// <summary>Total wire length including every typed environment record.</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var record in EnvironmentRecords) sum += record.WireLength;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.EnvironmentalProcess,
            ProtocolFamily = DisProtocolFamily.SyntheticEnvironment,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        EnvironmentalProcessId.Marshal(ref w);
        EnvironmentType.Marshal(ref w);
        w.WriteByte(ModelType);
        w.WriteByte(EnvironmentStatus);
        w.WriteUInt16((ushort)EnvironmentRecords.Count);
        w.WriteUInt16(SequenceNumber);
        foreach (var record in EnvironmentRecords) record.Marshal(ref w);
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
    public static EnvironmentalProcessPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var processId = EntityId.Unmarshal(ref r);
        var envType = EntityType.Unmarshal(ref r);
        var modelType = r.ReadByte();
        var envStatus = r.ReadByte();
        var numRecords = r.ReadUInt16();
        var seqNumber = r.ReadUInt16();
        var records = new List<StandardVariableRecord>(numRecords);
        for (var i = 0; i < numRecords; i++)
            records.Add(StandardVariableRecord.Unmarshal(ref r));
        return new EnvironmentalProcessPdu(
            header, processId, envType, modelType, envStatus,
            seqNumber, records);
    }
}

// --- PDU: Gridded Data (42) --------------------------------------------------

/// <summary>
/// Gridded Data PDU (type 42, family 9). Carries environmental
/// variables sampled on a 3D grid — wind fields, temperature fields,
/// pressure, obscurants — broken into a numbered sequence of PDUs
/// so large grids fit under MTU. IEEE 1278.1 §5.3.11.2.
/// </summary>
/// <remarks>
/// Grid-axis descriptors are exposed as typed
/// <see cref="GridAxisDescriptor"/> records (§6.2.41); each one
/// carries the axis range, sample count, data-representation code and
/// the raw per-sample values. Per-value decoding is up to the caller
/// since the sample shape depends on
/// <see cref="GridAxisDescriptor.DataRepresentation"/>.
/// </remarks>
public sealed record GriddedDataPdu(
    PduHeader Header,
    SimulationAddress EnvironmentalSimulationApplicationId,
    ushort FieldNumber,
    ushort PduNumber,
    ushort PduTotal,
    ushort CoordinateSystem,
    byte ConstantGrid,
    EntityType EnvironmentType,
    EulerAngles Orientation,
    ulong SampleTime,
    uint TotalValues,
    byte VectorDimension,
    IReadOnlyList<GridAxisDescriptor> Axes)
{
    /// <summary>Fixed wire length before the grid-axis descriptors.</summary>
    public const int MinimumWireLength = 62;

    /// <summary>Total wire length including every typed axis.</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var axis in Axes) sum += axis.WireLength;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.GriddedData,
            ProtocolFamily = DisProtocolFamily.SyntheticEnvironment,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        EnvironmentalSimulationApplicationId.Marshal(ref w);
        w.WriteUInt16(FieldNumber);
        w.WriteUInt16(PduNumber);
        w.WriteUInt16(PduTotal);
        w.WriteUInt16(CoordinateSystem);
        w.WriteByte((byte)Axes.Count);
        w.WriteByte(ConstantGrid);
        EnvironmentType.Marshal(ref w);
        Orientation.Marshal(ref w);
        w.WriteUInt64(SampleTime);
        w.WriteUInt32(TotalValues);
        w.WriteByte(VectorDimension);
        w.WriteByte(0); // padding
        w.WriteUInt16(0); // padding
        foreach (var axis in Axes) axis.Marshal(ref w);
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
    public static GriddedDataPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var simAppId = SimulationAddress.Unmarshal(ref r);
        var fieldNumber = r.ReadUInt16();
        var pduNumber = r.ReadUInt16();
        var pduTotal = r.ReadUInt16();
        var coordinateSystem = r.ReadUInt16();
        var numGridAxes = r.ReadByte();
        var constantGrid = r.ReadByte();
        var envType = EntityType.Unmarshal(ref r);
        var orientation = EulerAngles.Unmarshal(ref r);
        var sampleTime = r.ReadUInt64();
        var totalValues = r.ReadUInt32();
        var vectorDimension = r.ReadByte();
        r.SkipPadding(3);
        var axes = new List<GridAxisDescriptor>(numGridAxes);
        for (var i = 0; i < numGridAxes; i++) axes.Add(GridAxisDescriptor.Unmarshal(ref r));
        return new GriddedDataPdu(
            header, simAppId, fieldNumber, pduNumber, pduTotal, coordinateSystem,
            constantGrid, envType, orientation, sampleTime,
            totalValues, vectorDimension, axes);
    }
}

// --- PDU: Point Object State (43) --------------------------------------------

/// <summary>
/// Point Object State PDU (type 43, family 9). Describes a point-
/// shaped synthetic-environment object (tree, vehicle carcass,
/// sign, single-grid-cell obstacle). IEEE 1278.1 §5.3.11.3.
/// </summary>
public sealed record PointObjectStatePdu(
    PduHeader Header,
    EntityId ObjectId,
    EntityId ReferencedObjectId,
    ushort UpdateNumber,
    ForceId ForceId,
    byte Modifications,
    ObjectType ObjectType,
    Vector3Double ObjectLocation,
    EulerAngles ObjectOrientation,
    uint SpecificObjectAppearance,
    ushort GeneralObjectAppearance,
    SimulationAddress RequesterId,
    SimulationAddress ReceivingId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 88;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.PointObjectState,
            ProtocolFamily = DisProtocolFamily.SyntheticEnvironment,
            Length = WireLength,
        };
        header.Marshal(ref w);

        ObjectId.Marshal(ref w);
        ReferencedObjectId.Marshal(ref w);
        w.WriteUInt16(UpdateNumber);
        w.WriteByte((byte)ForceId);
        w.WriteByte(Modifications);
        ObjectType.Marshal(ref w);
        ObjectLocation.Marshal(ref w);
        ObjectOrientation.Marshal(ref w);
        w.WriteUInt32(SpecificObjectAppearance);
        w.WriteUInt16(GeneralObjectAppearance);
        w.WriteUInt16(0); // padding
        RequesterId.Marshal(ref w);
        ReceivingId.Marshal(ref w);
        w.WriteUInt32(0); // trailing padding
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
    public static PointObjectStatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var objectId = EntityId.Unmarshal(ref r);
        var refObjectId = EntityId.Unmarshal(ref r);
        var updateNumber = r.ReadUInt16();
        var forceId = (ForceId)r.ReadByte();
        var modifications = r.ReadByte();
        var objectType = Records.ObjectType.Unmarshal(ref r);
        var objectLocation = Vector3Double.Unmarshal(ref r);
        var objectOrientation = EulerAngles.Unmarshal(ref r);
        var specificAppearance = r.ReadUInt32();
        var generalAppearance = r.ReadUInt16();
        r.SkipPadding(2);
        var requesterId = SimulationAddress.Unmarshal(ref r);
        var receivingId = SimulationAddress.Unmarshal(ref r);
        r.SkipPadding(4);
        return new PointObjectStatePdu(
            header, objectId, refObjectId, updateNumber, forceId, modifications,
            objectType, objectLocation, objectOrientation, specificAppearance,
            generalAppearance, requesterId, receivingId);
    }
}

// --- PDU: Linear Object State (44) -------------------------------------------

/// <summary>
/// Linear Object State PDU (type 44, family 9). Describes a line-
/// shaped synthetic-environment object made of one or more linear
/// segments — fences, trenches, concertina wire.
/// IEEE 1278.1 §5.3.11.4.
/// </summary>
public sealed record LinearObjectStatePdu(
    PduHeader Header,
    EntityId ObjectId,
    EntityId ReferencedObjectId,
    ushort UpdateNumber,
    ForceId ForceId,
    SimulationAddress RequesterId,
    SimulationAddress ReceivingId,
    ObjectType ObjectType,
    IReadOnlyList<LinearSegmentParameter>? Segments = null)
{
    /// <summary>Fixed wire length before the segment list.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including segments.</summary>
    public int WireLength =>
        MinimumWireLength + ((Segments?.Count ?? 0) * LinearSegmentParameter.WireLength);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.LinearObjectState,
            ProtocolFamily = DisProtocolFamily.SyntheticEnvironment,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        ObjectId.Marshal(ref w);
        ReferencedObjectId.Marshal(ref w);
        w.WriteUInt16(UpdateNumber);
        w.WriteByte((byte)ForceId);
        w.WriteByte((byte)(Segments?.Count ?? 0));
        RequesterId.Marshal(ref w);
        ReceivingId.Marshal(ref w);
        ObjectType.Marshal(ref w);
        if (Segments is not null) foreach (var seg in Segments) seg.Marshal(ref w);
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
    public static LinearObjectStatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var objectId = EntityId.Unmarshal(ref r);
        var refObjectId = EntityId.Unmarshal(ref r);
        var updateNumber = r.ReadUInt16();
        var forceId = (ForceId)r.ReadByte();
        var numSegments = r.ReadByte();
        var requesterId = SimulationAddress.Unmarshal(ref r);
        var receivingId = SimulationAddress.Unmarshal(ref r);
        var objectType = Records.ObjectType.Unmarshal(ref r);
        var segments = new List<LinearSegmentParameter>(numSegments);
        for (var i = 0; i < numSegments; i++) segments.Add(LinearSegmentParameter.Unmarshal(ref r));
        return new LinearObjectStatePdu(
            header, objectId, refObjectId, updateNumber, forceId,
            requesterId, receivingId, objectType, segments);
    }
}

// --- PDU: Areal Object State (45) --------------------------------------------

/// <summary>
/// Areal Object State PDU (type 45, family 9). Describes an area-
/// shaped synthetic-environment object (minefield, smoke volume,
/// contaminated zone). Points are the polygon vertices in ECEF.
/// IEEE 1278.1 §5.3.11.5.
/// </summary>
public sealed record ArealObjectStatePdu(
    PduHeader Header,
    EntityId ObjectId,
    EntityId ReferencedObjectId,
    ushort UpdateNumber,
    ForceId ForceId,
    byte Modifications,
    ObjectType ObjectType,
    uint SpecificObjectAppearance,
    ushort GeneralObjectAppearance,
    SimulationAddress RequesterId,
    SimulationAddress ReceivingId,
    IReadOnlyList<Vector3Double>? Points = null)
{
    /// <summary>Fixed wire length before the points list.</summary>
    public const int MinimumWireLength = 48;

    /// <summary>Total wire length including polygon vertices.</summary>
    public int WireLength =>
        MinimumWireLength + ((Points?.Count ?? 0) * Vector3Double.WireLength);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ArealObjectState,
            ProtocolFamily = DisProtocolFamily.SyntheticEnvironment,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        ObjectId.Marshal(ref w);
        ReferencedObjectId.Marshal(ref w);
        w.WriteUInt16(UpdateNumber);
        w.WriteByte((byte)ForceId);
        w.WriteByte(Modifications);
        ObjectType.Marshal(ref w);
        w.WriteUInt32(SpecificObjectAppearance);
        w.WriteUInt16(GeneralObjectAppearance);
        w.WriteUInt16((ushort)(Points?.Count ?? 0));
        RequesterId.Marshal(ref w);
        ReceivingId.Marshal(ref w);
        if (Points is not null) foreach (var p in Points) p.Marshal(ref w);
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
    public static ArealObjectStatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var objectId = EntityId.Unmarshal(ref r);
        var refObjectId = EntityId.Unmarshal(ref r);
        var updateNumber = r.ReadUInt16();
        var forceId = (ForceId)r.ReadByte();
        var modifications = r.ReadByte();
        var objectType = Records.ObjectType.Unmarshal(ref r);
        var specificAppearance = r.ReadUInt32();
        var generalAppearance = r.ReadUInt16();
        var numPoints = r.ReadUInt16();
        var requesterId = SimulationAddress.Unmarshal(ref r);
        var receivingId = SimulationAddress.Unmarshal(ref r);
        var points = new List<Vector3Double>(numPoints);
        for (var i = 0; i < numPoints; i++) points.Add(Vector3Double.Unmarshal(ref r));
        return new ArealObjectStatePdu(
            header, objectId, refObjectId, updateNumber, forceId, modifications,
            objectType, specificAppearance, generalAppearance,
            requesterId, receivingId, points);
    }
}
