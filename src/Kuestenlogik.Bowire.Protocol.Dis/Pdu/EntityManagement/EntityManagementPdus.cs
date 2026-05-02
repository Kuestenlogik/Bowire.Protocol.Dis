// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.EntityManagement;

// --- PDU: Aggregate State (33) -----------------------------------------------

/// <summary>
/// Aggregate State PDU (type 33, family 7). Describes a group of
/// simulated entities represented as a single aggregate — forces
/// above the platform level that can be modelled coarsely to save
/// bandwidth. IEEE 1278.1 §5.3.9.1.
/// </summary>
/// <remarks>
/// <para>
/// Layout (132 bytes fixed + body blob + 4 bytes variable datum
/// count + variable datums):
/// </para>
/// <list type="bullet">
///   <item>0   : 12 — <see cref="PduHeader"/></item>
///   <item>12  :  6 — <see cref="AggregateId"/></item>
///   <item>18  :  1 — <see cref="ForceId"/></item>
///   <item>19  :  1 — <see cref="AggregateState"/></item>
///   <item>20  :  8 — <see cref="AggregateType"/></item>
///   <item>28  :  4 — <see cref="Formation"/></item>
///   <item>32  : 32 — <see cref="AggregateMarking"/></item>
///   <item>64  : 12 — <see cref="Dimensions"/></item>
///   <item>76  : 12 — <see cref="Orientation"/></item>
///   <item>88  : 24 — <see cref="CenterOfMass"/> (ECEF)</item>
///   <item>112 : 12 — <see cref="Velocity"/></item>
///   <item>124 :  2 — number of DIS aggregates</item>
///   <item>126 :  2 — number of DIS entities</item>
///   <item>128 :  2 — number of silent aggregate systems</item>
///   <item>130 :  2 — number of silent entity systems</item>
///   <item>132 :  N — aggregate ids, entity ids, silent systems, variable datums</item>
/// </list>
/// <para>
/// Every variable section after the fixed header is typed: member-id
/// lists round-trip as <see cref="EntityId"/> collections, silent
/// aggregate and entity systems as <see cref="EntityType"/>
/// collections, and trailing variable datum records as typed
/// <see cref="VariableDatum"/>s. The encoder re-emits the 32-bit
/// alignment padding between the id lists and the silent-system
/// lists as required by §5.3.9.1.
/// </para>
/// </remarks>
public sealed record AggregateStatePdu(
    PduHeader Header,
    EntityId AggregateId,
    ForceId ForceId,
    AggregateState AggregateState,
    AggregateType AggregateType,
    uint Formation,
    AggregateMarking AggregateMarking,
    Vector3Float Dimensions,
    EulerAngles Orientation,
    Vector3Double CenterOfMass,
    Vector3Float Velocity,
    IReadOnlyList<EntityId> AggregateIds,
    IReadOnlyList<EntityId> EntityIds,
    IReadOnlyList<EntityType> SilentAggregateSystems,
    IReadOnlyList<EntityType> SilentEntitySystems,
    IReadOnlyList<VariableDatum> VariableDatums)
{
    /// <summary>Fixed wire length before the typed sections start.</summary>
    public const int MinimumWireLength = 132;

    /// <summary>Total wire length including every typed section.</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            sum += (AggregateIds.Count + EntityIds.Count) * EntityId.WireLength;
            // 32-bit (4-byte) alignment padding between id-lists and silent-systems.
            sum = ((sum + 3) / 4) * 4;
            sum += (SilentAggregateSystems.Count + SilentEntitySystems.Count) * EntityType.WireLength;
            sum += sizeof(uint); // numberOfVariableDatumRecords
            foreach (var datum in VariableDatums) sum += datum.WireLength;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.AggregateState,
            ProtocolFamily = DisProtocolFamily.EntityManagement,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        AggregateId.Marshal(ref w);
        w.WriteByte((byte)ForceId);
        w.WriteByte((byte)AggregateState);
        AggregateType.Marshal(ref w);
        w.WriteUInt32(Formation);
        AggregateMarking.Marshal(ref w);
        Dimensions.Marshal(ref w);
        Orientation.Marshal(ref w);
        CenterOfMass.Marshal(ref w);
        Velocity.Marshal(ref w);
        w.WriteUInt16((ushort)AggregateIds.Count);
        w.WriteUInt16((ushort)EntityIds.Count);
        w.WriteUInt16((ushort)SilentAggregateSystems.Count);
        w.WriteUInt16((ushort)SilentEntitySystems.Count);

        foreach (var id in AggregateIds) id.Marshal(ref w);
        foreach (var id in EntityIds) id.Marshal(ref w);

        // 32-bit alignment pad between id lists and the silent-systems list.
        var idBytes = (AggregateIds.Count + EntityIds.Count) * EntityId.WireLength;
        var pad = ((idBytes + 3) / 4) * 4 - idBytes;
        if (pad > 0) w.WritePadding(pad);

        foreach (var type in SilentAggregateSystems) type.Marshal(ref w);
        foreach (var type in SilentEntitySystems) type.Marshal(ref w);

        w.WriteUInt32((uint)VariableDatums.Count);
        foreach (var datum in VariableDatums) datum.Marshal(ref w);
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
    public static AggregateStatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var aggregateId = EntityId.Unmarshal(ref r);
        var forceId = (ForceId)r.ReadByte();
        var aggregateState = (AggregateState)r.ReadByte();
        var aggregateType = Records.AggregateType.Unmarshal(ref r);
        var formation = r.ReadUInt32();
        var aggregateMarking = AggregateMarking.Unmarshal(ref r);
        var dimensions = Vector3Float.Unmarshal(ref r);
        var orientation = EulerAngles.Unmarshal(ref r);
        var centerOfMass = Vector3Double.Unmarshal(ref r);
        var velocity = Vector3Float.Unmarshal(ref r);
        var numAggregates = r.ReadUInt16();
        var numEntities = r.ReadUInt16();
        var numSilentAggregates = r.ReadUInt16();
        var numSilentEntities = r.ReadUInt16();

        var aggregateIds = new List<EntityId>(numAggregates);
        for (var i = 0; i < numAggregates; i++) aggregateIds.Add(EntityId.Unmarshal(ref r));
        var entityIds = new List<EntityId>(numEntities);
        for (var i = 0; i < numEntities; i++) entityIds.Add(EntityId.Unmarshal(ref r));

        var idBytes = (numAggregates + numEntities) * EntityId.WireLength;
        var pad = ((idBytes + 3) / 4) * 4 - idBytes;
        if (pad > 0) r.SkipPadding(pad);

        var silentAggregates = new List<EntityType>(numSilentAggregates);
        for (var i = 0; i < numSilentAggregates; i++) silentAggregates.Add(EntityType.Unmarshal(ref r));
        var silentEntities = new List<EntityType>(numSilentEntities);
        for (var i = 0; i < numSilentEntities; i++) silentEntities.Add(EntityType.Unmarshal(ref r));

        var numDatums = r.ReadUInt32();
        var datums = new List<VariableDatum>((int)numDatums);
        for (var i = 0; i < numDatums; i++) datums.Add(VariableDatum.Unmarshal(ref r));

        return new AggregateStatePdu(
            header, aggregateId, forceId, aggregateState, aggregateType, formation,
            aggregateMarking, dimensions, orientation, centerOfMass, velocity,
            aggregateIds, entityIds, silentAggregates, silentEntities, datums);
    }
}

// --- PDU: IsGroupOf (34) -----------------------------------------------------

/// <summary>
/// IsGroupOf PDU (type 34, family 7). Describes a set of entities
/// grouped for exercise purposes — by reference to a shared
/// geographic origin (latitude / longitude) and a category that
/// determines the grouping shape. IEEE 1278.1 §5.3.9.2.
/// </summary>
/// <remarks>
/// Grouped-entity descriptions are exposed as per-record byte arrays
/// — every record in a given PDU is the same size (determined by
/// <see cref="GroupedEntityCategory"/>), so the codec splits the
/// blob evenly across <see cref="GroupedEntityDescriptions"/>.
/// Per-category typed decoders are out of scope for now; IEEE 1278.1
/// defines per-category layouts but every category has a fixed record
/// size so the split is unambiguous.
/// </remarks>
public sealed record IsGroupOfPdu(
    PduHeader Header,
    EntityId GroupEntityId,
    byte GroupedEntityCategory,
    double Latitude,
    double Longitude,
    IReadOnlyList<byte[]> GroupedEntityDescriptions)
{
    /// <summary>Fixed wire length before the grouped-entity descriptions.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including every typed GED record.</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var ged in GroupedEntityDescriptions) sum += ged.Length;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.IsGroupOf,
            ProtocolFamily = DisProtocolFamily.EntityManagement,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        GroupEntityId.Marshal(ref w);
        w.WriteByte(GroupedEntityCategory);
        w.WriteByte((byte)GroupedEntityDescriptions.Count);
        w.WritePadding(4);
        w.WriteDouble(Latitude);
        w.WriteDouble(Longitude);
        foreach (var ged in GroupedEntityDescriptions) w.WriteBytes(ged);
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
    public static IsGroupOfPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var groupEntity = EntityId.Unmarshal(ref r);
        var category = r.ReadByte();
        var numGrouped = r.ReadByte();
        r.SkipPadding(4);
        var latitude = r.ReadDouble();
        var longitude = r.ReadDouble();
        var blobLength = Math.Max(0, header.Length - MinimumWireLength);
        var geds = new List<byte[]>(numGrouped);
        if (numGrouped > 0 && blobLength > 0)
        {
            // Per-record size is implied: every record in a given PDU
            // shares the same GED category, so uniform width.
            var perRecord = blobLength / numGrouped;
            for (var i = 0; i < numGrouped; i++)
                geds.Add(r.ReadBytes(perRecord).ToArray());
        }
        return new IsGroupOfPdu(header, groupEntity, category, latitude, longitude, geds);
    }
}

// --- PDU: Transfer Ownership (35) --------------------------------------------

/// <summary>
/// Transfer Ownership PDU (type 35, family 7). Coordinates the
/// handover of simulation responsibility for an entity from one
/// simulator to another. IEEE 1278.1 §5.3.9.3.
/// </summary>
/// <remarks>
/// Record sets (transfer-specific metadata) carried as an opaque
/// blob pending typed record-set coverage.
/// </remarks>
public sealed record TransferOwnershipPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    uint RequestId,
    RequiredReliabilityService Reliability,
    TransferType TransferType,
    EntityId TransferEntityId,
    uint NumberOfRecordSets,
    byte[] RecordSetsBlob)
{
    /// <summary>Fixed wire length before the record-sets blob.</summary>
    public const int MinimumWireLength = 40;

    /// <summary>Total wire length including the opaque blob.</summary>
    public int WireLength => MinimumWireLength + RecordSetsBlob.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.TransferOwnership,
            ProtocolFamily = DisProtocolFamily.EntityManagement,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        w.WriteUInt32(RequestId);
        w.WriteByte((byte)Reliability);
        w.WriteByte((byte)TransferType);
        TransferEntityId.Marshal(ref w);
        w.WriteUInt32(NumberOfRecordSets);
        w.WriteBytes(RecordSetsBlob);
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
    public static TransferOwnershipPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var requestId = r.ReadUInt32();
        var reliability = (RequiredReliabilityService)r.ReadByte();
        var transferType = (TransferType)r.ReadByte();
        var transferEntity = EntityId.Unmarshal(ref r);
        var recordSetCount = r.ReadUInt32();
        var blobLength = Math.Max(0, header.Length - MinimumWireLength);
        var blob = r.ReadBytes(blobLength).ToArray();
        return new TransferOwnershipPdu(
            header, originating, receiving, requestId, reliability,
            transferType, transferEntity, recordSetCount, blob);
    }
}

// --- PDU: IsPartOf (36) ------------------------------------------------------

/// <summary>
/// IsPartOf PDU (type 36, family 7). Declares that one entity is
/// physically or logically a part of another — turret attached to a
/// tank, sensor pod attached to an aircraft, etc. Transmits the
/// part's relationship nature, its location on the host, and the
/// named station slot where it sits. IEEE 1278.1 §5.3.9.4.
/// </summary>
public sealed record IsPartOfPdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    EntityId ReceivingEntityId,
    ushort RelationshipNature,
    ushort RelationshipPosition,
    Vector3Float PartLocation,
    NamedLocationId NamedLocationId,
    EntityType PartEntityType)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 52;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.IsPartOf,
            ProtocolFamily = DisProtocolFamily.EntityManagement,
            Length = WireLength,
        };
        header.Marshal(ref w);

        OriginatingEntityId.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        w.WriteUInt16(RelationshipNature);
        w.WriteUInt16(RelationshipPosition);
        PartLocation.Marshal(ref w);
        NamedLocationId.Marshal(ref w);
        PartEntityType.Marshal(ref w);
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
    public static IsPartOfPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var nature = r.ReadUInt16();
        var position = r.ReadUInt16();
        var partLocation = Vector3Float.Unmarshal(ref r);
        var namedLocation = NamedLocationId.Unmarshal(ref r);
        var partEntityType = EntityType.Unmarshal(ref r);
        return new IsPartOfPdu(
            header, originating, receiving, nature, position,
            partLocation, namedLocation, partEntityType);
    }
}
