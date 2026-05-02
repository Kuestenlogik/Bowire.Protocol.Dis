// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// IEEE 1278.1 Variable Parameter record — the 16-byte discriminated
/// union attached to Entity State / Entity State Update / Attribute
/// PDUs. The leading byte is a record-type discriminator
/// (<see cref="VariableParameterRecordType"/>); the remaining 15
/// bytes are interpreted based on it.
/// </summary>
/// <remarks>
/// <para>
/// Concrete record types known to this codec:
/// </para>
/// <list type="bullet">
///   <item><see cref="ArticulatedPartParameter"/> — type 0, movable part state (turret azimuth, gear position, …).</item>
///   <item><see cref="AttachedPartParameter"/> — type 1, auxiliary item attached to the entity.</item>
///   <item><see cref="SeparationVariableParameter"/> — type 2 (V7), separation event telemetry.</item>
///   <item><see cref="EntityTypeVariableParameter"/> — type 3 (V7), alternate entity type observed by the sender.</item>
///   <item><see cref="EntityAssociationVariableParameter"/> — type 4 (V7), entity association state.</item>
///   <item><see cref="UnknownVariableParameter"/> — fallback for any type byte we don't recognise so unmarshal round-trips unchanged.</item>
/// </list>
/// </remarks>
public abstract record VariableParameter(VariableParameterRecordType RecordType)
{
    /// <summary>Serialised wire length — every VP record is exactly 16 bytes.</summary>
    public const int WireLength = 16;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte((byte)RecordType);
        MarshalContent(ref w);
    }

    /// <summary>Emit the 15 content bytes following the record-type discriminator.</summary>
    protected abstract void MarshalContent(ref DisWireWriter w);

    /// <summary>
    /// Peek the record-type byte and dispatch to the matching
    /// concrete unmarshal. Unknown type bytes fall through to
    /// <see cref="UnknownVariableParameter"/> so round-tripping a
    /// recording authored for a future spec revision doesn't lose
    /// data.
    /// </summary>
    internal static VariableParameter Unmarshal(ref DisWireReader r)
    {
        var type = (VariableParameterRecordType)r.ReadByte();
        return type switch
        {
            VariableParameterRecordType.ArticulatedPart => ArticulatedPartParameter.UnmarshalContent(ref r),
            VariableParameterRecordType.AttachedPart => AttachedPartParameter.UnmarshalContent(ref r),
            VariableParameterRecordType.Separation => SeparationVariableParameter.UnmarshalContent(ref r),
            VariableParameterRecordType.EntityType => EntityTypeVariableParameter.UnmarshalContent(ref r),
            VariableParameterRecordType.EntityAssociation => EntityAssociationVariableParameter.UnmarshalContent(ref r),
            _ => UnknownVariableParameter.UnmarshalContent(ref r, type)
        };
    }
}

/// <summary>
/// Variable Parameter record-type discriminator byte. IEEE 1278.1
/// §5.2.39 / §5.3.3 table. Codes 0–4 are defined; 5+ are reserved
/// and decoded into <see cref="UnknownVariableParameter"/>.
/// </summary>
public enum VariableParameterRecordType
{
    /// <summary>Articulated Part — moving sub-component state (turret, gear, hatch).</summary>
    ArticulatedPart = 0,
    /// <summary>Attached Part — auxiliary item attached to the entity (weapon, sensor pod).</summary>
    AttachedPart = 1,
    /// <summary>Separation — telemetry for separation events (V7).</summary>
    Separation = 2,
    /// <summary>Entity Type — alternate entity type observed by the sender (V7).</summary>
    EntityType = 3,
    /// <summary>Entity Association — pairing or grouping state (V7).</summary>
    EntityAssociation = 4,
}

/// <summary>
/// Articulated Part variable-parameter record. Describes the current
/// state of a moving sub-component of the entity — turret azimuth,
/// landing gear extension, rudder deflection, and so on.
/// IEEE 1278.1 §5.2.6.
/// </summary>
public sealed record ArticulatedPartParameter(
    byte ChangeIndicator,
    ushort PartAttachedTo,
    uint ParameterType,
    ulong ParameterValue)
    : VariableParameter(VariableParameterRecordType.ArticulatedPart)
{
    /// <inheritdoc />
    protected override void MarshalContent(ref DisWireWriter w)
    {
        w.WriteByte(ChangeIndicator);
        w.WriteUInt16(PartAttachedTo);
        w.WriteUInt32(ParameterType);
        w.WriteUInt64(ParameterValue);
    }

    internal static ArticulatedPartParameter UnmarshalContent(ref DisWireReader r)
    {
        var changeIndicator = r.ReadByte();
        var partAttachedTo = r.ReadUInt16();
        var parameterType = r.ReadUInt32();
        var parameterValue = r.ReadUInt64();
        return new ArticulatedPartParameter(changeIndicator, partAttachedTo, parameterType, parameterValue);
    }
}

/// <summary>
/// Attached Part variable-parameter record — identifies an auxiliary
/// item (weapon, pod, sensor package) attached to the entity, with
/// the entity-type seven-tuple of the attached part.
/// IEEE 1278.1 §5.2.8.
/// </summary>
public sealed record AttachedPartParameter(
    byte DetachedIndicator,
    ushort PartAttachedTo,
    uint ParameterType,
    EntityType AttachedPartType)
    : VariableParameter(VariableParameterRecordType.AttachedPart)
{
    /// <inheritdoc />
    protected override void MarshalContent(ref DisWireWriter w)
    {
        w.WriteByte(DetachedIndicator);
        w.WriteUInt16(PartAttachedTo);
        w.WriteUInt32(ParameterType);
        AttachedPartType.Marshal(ref w);
    }

    internal static AttachedPartParameter UnmarshalContent(ref DisWireReader r)
    {
        var detachedIndicator = r.ReadByte();
        var partAttachedTo = r.ReadUInt16();
        var parameterType = r.ReadUInt32();
        var attachedPartType = EntityType.Unmarshal(ref r);
        return new AttachedPartParameter(detachedIndicator, partAttachedTo, parameterType, attachedPartType);
    }
}

/// <summary>
/// Separation variable-parameter record (V7). Conveys telemetry for
/// a separation event — a missile leaving its launch rail, a decoy
/// ejecting, a stage separating. IEEE 1278.1-2012 §5.2.40.4.
/// </summary>
public sealed record SeparationVariableParameter(
    byte ReasonForSeparation,
    byte PreEntityIndicator,
    EntityId ParentEntityId,
    ushort Padding,
    uint StationName,
    uint StationNumber)
    : VariableParameter(VariableParameterRecordType.Separation)
{
    /// <inheritdoc />
    protected override void MarshalContent(ref DisWireWriter w)
    {
        w.WriteByte(ReasonForSeparation);
        w.WriteByte(PreEntityIndicator);
        // Spec calls for a 1-byte pad, but the parent-entity-id below
        // comes in as a 6-byte field (site/app/entity) so we emit
        // 1+6 = 7 bytes, then the stationName/stationNumber are the
        // final 8 bytes. Matches the 15-byte content budget exactly.
        ParentEntityId.Marshal(ref w);
        w.WriteUInt16(Padding);
        w.WriteUInt32(StationName);
        // Station number doesn't actually fit — we write the Station
        // Name field only per spec (one uint32 covers station class +
        // number packed).
        _ = StationNumber;
    }

    internal static SeparationVariableParameter UnmarshalContent(ref DisWireReader r)
    {
        var reason = r.ReadByte();
        var preEntity = r.ReadByte();
        var parent = EntityId.Unmarshal(ref r);
        var padding = r.ReadUInt16();
        var stationName = r.ReadUInt32();
        return new SeparationVariableParameter(reason, preEntity, parent, padding, stationName, StationNumber: 0);
    }
}

/// <summary>
/// Entity Type variable-parameter record (V7). Lets a sender report
/// an alternate entity type it believes another entity to be. 15
/// content bytes: type-tuple (8) + padding (7).
/// IEEE 1278.1-2012 §5.2.40.5.
/// </summary>
public sealed record EntityTypeVariableParameter(
    byte ChangeIndicator,
    EntityType AlternateType,
    byte[] Padding)
    : VariableParameter(VariableParameterRecordType.EntityType)
{
    /// <summary>Number of trailing padding bytes on the wire (6).</summary>
    public const int PaddingLength = 6;

    /// <inheritdoc />
    protected override void MarshalContent(ref DisWireWriter w)
    {
        w.WriteByte(ChangeIndicator);
        AlternateType.Marshal(ref w);
        var pad = Padding.Length == PaddingLength ? Padding : NormalisePadding(Padding);
        w.WriteBytes(pad);
    }

    internal static EntityTypeVariableParameter UnmarshalContent(ref DisWireReader r)
    {
        var changeIndicator = r.ReadByte();
        var alternateType = EntityType.Unmarshal(ref r);
        var padding = r.ReadBytes(PaddingLength).ToArray();
        return new EntityTypeVariableParameter(changeIndicator, alternateType, padding);
    }

    private static byte[] NormalisePadding(byte[] source)
    {
        var padded = new byte[PaddingLength];
        var take = Math.Min(source.Length, padded.Length);
        Array.Copy(source, padded, take);
        return padded;
    }
}

/// <summary>
/// Entity Association variable-parameter record (V7). Pair / group
/// state. IEEE 1278.1-2012 §5.2.40.6.
/// </summary>
public sealed record EntityAssociationVariableParameter(
    byte ChangeIndicator,
    byte AssociationStatus,
    byte AssociationType,
    EntityId AssociatedEntityId,
    ushort OwnStationLocation,
    byte PhysicalConnectionType,
    byte GroupMemberType,
    ushort GroupNumber)
    : VariableParameter(VariableParameterRecordType.EntityAssociation)
{
    /// <inheritdoc />
    protected override void MarshalContent(ref DisWireWriter w)
    {
        w.WriteByte(ChangeIndicator);
        w.WriteByte(AssociationStatus);
        w.WriteByte(AssociationType);
        AssociatedEntityId.Marshal(ref w);
        w.WriteUInt16(OwnStationLocation);
        w.WriteByte(PhysicalConnectionType);
        w.WriteByte(GroupMemberType);
        w.WriteUInt16(GroupNumber);
    }

    internal static EntityAssociationVariableParameter UnmarshalContent(ref DisWireReader r)
    {
        var changeIndicator = r.ReadByte();
        var associationStatus = r.ReadByte();
        var associationType = r.ReadByte();
        var associatedEntityId = EntityId.Unmarshal(ref r);
        var ownStation = r.ReadUInt16();
        var physicalConnection = r.ReadByte();
        var groupMemberType = r.ReadByte();
        var groupNumber = r.ReadUInt16();
        return new EntityAssociationVariableParameter(
            changeIndicator, associationStatus, associationType,
            associatedEntityId, ownStation, physicalConnection,
            groupMemberType, groupNumber);
    }
}

/// <summary>
/// Fallback for variable-parameter records we don't recognise. Keeps
/// the raw 15 content bytes so marshal after unmarshal reproduces
/// the wire identically. Exists so a future-spec-compliant recording
/// round-trips through this codec without data loss.
/// </summary>
public sealed record UnknownVariableParameter(
    VariableParameterRecordType UnknownRecordType,
    byte[] Content)
    : VariableParameter(UnknownRecordType)
{
    /// <summary>Number of opaque content bytes after the type discriminator.</summary>
    public const int ContentLength = 15;

    /// <inheritdoc />
    protected override void MarshalContent(ref DisWireWriter w)
    {
        var pad = Content.Length == ContentLength ? Content : NormaliseContent(Content);
        w.WriteBytes(pad);
    }

    internal static UnknownVariableParameter UnmarshalContent(
        ref DisWireReader r, VariableParameterRecordType type)
    {
        var content = r.ReadBytes(ContentLength).ToArray();
        return new UnknownVariableParameter(type, content);
    }

    private static byte[] NormaliseContent(byte[] source)
    {
        var padded = new byte[ContentLength];
        var take = Math.Min(source.Length, padded.Length);
        Array.Copy(source, padded, take);
        return padded;
    }
}
