// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.LiveEntity;

// Typed views on the flag-gated payloads of the Live Entity family (#26).
//
// Layouts as KDIS decodes them (src/PDU/Live_Entity/*.cpp); open-dis does not
// decode these PDUs, and there is no SISO test vector. So, as for the
// Minefield Data per-mine block, the payload bytes stay what a PDU writes back,
// and a view is null whenever the bytes do not add up to exactly what the flags
// announce, or a flag bit the layout does not define is set.
//
// The compressed numbers are fixed point. Where the binary point sits, KDIS
// says, the standard does not state — its own choice it calls "an educated
// guess". They are given here as the raw integers on the wire; scaling them is
// the reader's call, not something to guess on their behalf.

/// <summary>Relative world coordinates: a reference point and a delta from it, raw fixed point.</summary>
public readonly record struct LeRelativeWorldCoordinates(ushort ReferencePoint, short DeltaX, short DeltaY, short DeltaZ)
{
    internal const int WireLength = 8;
    internal static LeRelativeWorldCoordinates Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt16(), r.ReadInt16(), r.ReadInt16(), r.ReadInt16());
    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt16(ReferencePoint); w.WriteInt16(DeltaX); w.WriteInt16(DeltaY); w.WriteInt16(DeltaZ);
    }
}

/// <summary>A 16-bit-per-axis vector, raw fixed point.</summary>
public readonly record struct LeVector16(short X, short Y, short Z)
{
    internal static LeVector16 Unmarshal(ref DisWireReader r) => new(r.ReadInt16(), r.ReadInt16(), r.ReadInt16());
    internal void Marshal(ref DisWireWriter w) { w.WriteInt16(X); w.WriteInt16(Y); w.WriteInt16(Z); }
}

/// <summary>An 8-bit-per-axis vector, raw fixed point.</summary>
public readonly record struct LeVector8(sbyte X, sbyte Y, sbyte Z)
{
    internal static LeVector8 Unmarshal(ref DisWireReader r) => new(r.ReadSByte(), r.ReadSByte(), r.ReadSByte());
    internal void Marshal(ref DisWireWriter w) { w.WriteSByte(X); w.WriteSByte(Y); w.WriteSByte(Z); }
}

/// <summary>Euler angles at 8 bits each, raw fixed point.</summary>
public readonly record struct LeEulerAngles(sbyte Psi, sbyte Theta, sbyte Phi)
{
    internal static LeEulerAngles Unmarshal(ref DisWireReader r) => new(r.ReadSByte(), r.ReadSByte(), r.ReadSByte());
    internal void Marshal(ref DisWireWriter w) { w.WriteSByte(Psi); w.WriteSByte(Theta); w.WriteSByte(Phi); }
}

/// <summary>Position error, horizontal and vertical, raw fixed point.</summary>
public readonly record struct LePositionError(short Horizontal, short Vertical);

/// <summary>Orientation error, azimuth, elevation and rotation, raw fixed point.</summary>
public readonly record struct LeOrientationError(short Azimuth, short Elevation, short Rotation);

/// <summary>Dead reckoning: algorithm, linear acceleration, angular velocity (raw fixed point).</summary>
public readonly record struct LeDeadReckoning(byte Algorithm, LeVector8 LinearAcceleration, LeVector8 AngularVelocity);

/// <summary>The optional fields of a TSPI PDU, after its flag byte.</summary>
public sealed record TspiFields(
    LeRelativeWorldCoordinates Location,
    LeVector16? LinearVelocity = null,
    LeEulerAngles? Orientation = null,
    LePositionError? PositionError = null,
    LeOrientationError? OrientationError = null,
    LeDeadReckoning? DeadReckoning = null,
    short? MeasuredSpeed = null,
    IReadOnlyList<byte>? SystemSpecificData = null);

/// <summary>The optional fields of an Appearance PDU, after its flag bytes.</summary>
public sealed record LeAppearanceFields(
    byte? ForceId = null,
    EntityType? EntityType = null,
    EntityType? AlternateEntityType = null,
    EntityMarking? Marking = null,
    uint? Capabilities = null,
    uint? VisualAppearance = null,
    uint? InfraredAppearance = null,
    uint? ElectromagneticAppearance = null,
    uint? AudioAppearance = null);

/// <summary>The munition descriptor as Live Entity sends it: the type always, the rest when flagged.</summary>
public readonly record struct LeMunitionDescriptor(
    EntityType MunitionType, ushort? Warhead, ushort? Fuse, ushort? Quantity, ushort? Rate);

/// <summary>The fields of an LE Fire PDU, after its flag byte.</summary>
/// <remarks>
/// A munition or event id sent without its site and application (flag F1 / F3
/// clear) shares them with the firing entity; they are filled in from it.
/// </remarks>
public sealed record LeFireFields(
    LiveEntityId? TargetId,
    LiveEntityId? MunitionId,
    LiveEntityId EventId,
    LeRelativeWorldCoordinates? Location,
    LeMunitionDescriptor Munition,
    LeVector16 Velocity,
    ushort Range);

/// <summary>The fields of an LE Detonation PDU, after its flag bytes.</summary>
/// <remarks>
/// The location is either in world coordinates (flag F6 clear) or relative to
/// the target entity (F6 set) — exactly one of the two is set.
/// </remarks>
public sealed record LeDetonationFields(
    LiveEntityId? TargetId,
    LiveEntityId? MunitionId,
    LiveEntityId? EventId,
    LeRelativeWorldCoordinates? WorldLocation,
    LeVector16 Velocity,
    LeEulerAngles? MunitionOrientation,
    LeMunitionDescriptor Munition,
    LeVector16? EntityLocation,
    byte DetonationResult);

/// <summary>
/// Reads and writes the flag-gated payloads of the Live Entity PDUs (#26).
/// Every read returns null rather than throwing when the payload does not read
/// cleanly — the PDU's raw payload is always there to fall back on.
/// </summary>
public static class LiveEntityFields
{
    // Flag bits per KDIS. Bit 7 of the TSPI and Fire flags is reserved, as are
    // bits 2–7 of the second Appearance and Detonation flag octets.
    private const byte TspiKnown = 0x7F;
    private const byte FireKnown = 0x7F;
    private const byte Flag2Known = 0x03;

    private delegate T Reader<out T>(ref DisWireReader r);

    private static T? Read<T>(ReadOnlySpan<byte> payload, Reader<T> read) where T : class
    {
        try
        {
            var r = new DisWireReader(payload);
            var value = read(ref r);
            return r.Remaining == 0 ? value : null;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidDataException)
        {
            return null;
        }
    }

    private static InvalidDataException Undefined() => new("A flag bit the layout does not define is set.");

    private static LiveEntityId ReadId(ref DisWireReader r, bool full, LiveEntityId owner) =>
        full ? LiveEntityId.Unmarshal(ref r) : owner with { Entity = r.ReadUInt16() };

    private static void WriteId(ref DisWireWriter w, LiveEntityId id, bool full)
    {
        if (full) id.Marshal(ref w); else w.WriteUInt16(id.Entity);
    }

    // ---- TSPI ----

    /// <summary>The TSPI payload's fields, or null when it does not read cleanly.</summary>
    public static TspiFields? TryDecodeTspi(ReadOnlySpan<byte> payload) => Read(payload, static (ref DisWireReader r) =>
    {
        var f = r.ReadByte();
        if ((f & ~TspiKnown) != 0) throw Undefined();
        var location = LeRelativeWorldCoordinates.Unmarshal(ref r);
        var vel = (f & 0x01) != 0 ? LeVector16.Unmarshal(ref r) : (LeVector16?)null;
        var ori = (f & 0x02) != 0 ? LeEulerAngles.Unmarshal(ref r) : (LeEulerAngles?)null;
        var posErr = (f & 0x04) != 0 ? new LePositionError(r.ReadInt16(), r.ReadInt16()) : (LePositionError?)null;
        var oriErr = (f & 0x08) != 0 ? new LeOrientationError(r.ReadInt16(), r.ReadInt16(), r.ReadInt16()) : (LeOrientationError?)null;
        var dr = (f & 0x10) != 0
            ? new LeDeadReckoning(r.ReadByte(), LeVector8.Unmarshal(ref r), LeVector8.Unmarshal(ref r))
            : (LeDeadReckoning?)null;
        var speed = (f & 0x20) != 0 ? r.ReadInt16() : (short?)null;
        byte[]? data = null;
        if ((f & 0x40) != 0)
        {
            data = new byte[r.ReadByte()];
            for (var i = 0; i < data.Length; i++) data[i] = r.ReadByte();
        }
        return new TspiFields(location, vel, ori, posErr, oriErr, dr, speed, data);
    });

    /// <summary>The TSPI payload for <paramref name="fields"/>; the flags follow from which fields are set.</summary>
    public static byte[] Encode(TspiFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var data = fields.SystemSpecificData;
        var buffer = new byte[1 + 8 + 6 + 3 + 4 + 6 + 7 + 2 + 1 + (data?.Count ?? 0)];
        var w = new DisWireWriter(buffer);
        byte f = 0;
        if (fields.LinearVelocity is not null) f |= 0x01;
        if (fields.Orientation is not null) f |= 0x02;
        if (fields.PositionError is not null) f |= 0x04;
        if (fields.OrientationError is not null) f |= 0x08;
        if (fields.DeadReckoning is not null) f |= 0x10;
        if (fields.MeasuredSpeed is not null) f |= 0x20;
        if (data is not null) f |= 0x40;
        w.WriteByte(f);
        fields.Location.Marshal(ref w);
        fields.LinearVelocity?.Marshal(ref w);
        fields.Orientation?.Marshal(ref w);
        if (fields.PositionError is { } pe) { w.WriteInt16(pe.Horizontal); w.WriteInt16(pe.Vertical); }
        if (fields.OrientationError is { } oe) { w.WriteInt16(oe.Azimuth); w.WriteInt16(oe.Elevation); w.WriteInt16(oe.Rotation); }
        if (fields.DeadReckoning is { } dr)
        {
            w.WriteByte(dr.Algorithm); dr.LinearAcceleration.Marshal(ref w); dr.AngularVelocity.Marshal(ref w);
        }
        if (fields.MeasuredSpeed is { } s) w.WriteInt16(s);
        if (data is not null)
        {
            w.WriteByte(checked((byte)data.Count));
            foreach (var b in data) w.WriteByte(b);
        }
        return buffer[..w.Offset];
    }

    // ---- Appearance ----

    /// <summary>The Appearance payload's fields, or null when it does not read cleanly.</summary>
    public static LeAppearanceFields? TryDecodeAppearance(ReadOnlySpan<byte> payload) => Read(payload, static (ref DisWireReader r) =>
    {
        var f1 = r.ReadByte();
        var f2 = (f1 & 0x80) != 0 ? r.ReadByte() : (byte)0;
        if ((f2 & ~Flag2Known) != 0) throw Undefined();
        return new LeAppearanceFields(
            ForceId: (f1 & 0x01) != 0 ? r.ReadByte() : null,
            EntityType: (f1 & 0x02) != 0 ? EntityType.Unmarshal(ref r) : null,
            AlternateEntityType: (f1 & 0x04) != 0 ? EntityType.Unmarshal(ref r) : null,
            Marking: (f1 & 0x08) != 0 ? EntityMarking.Unmarshal(ref r) : null,
            Capabilities: (f1 & 0x10) != 0 ? r.ReadUInt32() : null,
            VisualAppearance: (f1 & 0x20) != 0 ? r.ReadUInt32() : null,
            InfraredAppearance: (f1 & 0x40) != 0 ? r.ReadUInt32() : null,
            ElectromagneticAppearance: (f2 & 0x01) != 0 ? r.ReadUInt32() : null,
            AudioAppearance: (f2 & 0x02) != 0 ? r.ReadUInt32() : null);
    });

    /// <summary>The Appearance payload for <paramref name="fields"/>; the flags follow from which fields are set.</summary>
    public static byte[] Encode(LeAppearanceFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var buffer = new byte[2 + 1 + 8 + 8 + 12 + (5 * 4)];
        var w = new DisWireWriter(buffer);
        byte f1 = 0, f2 = 0;
        if (fields.ForceId is not null) f1 |= 0x01;
        if (fields.EntityType is not null) f1 |= 0x02;
        if (fields.AlternateEntityType is not null) f1 |= 0x04;
        if (fields.Marking is not null) f1 |= 0x08;
        if (fields.Capabilities is not null) f1 |= 0x10;
        if (fields.VisualAppearance is not null) f1 |= 0x20;
        if (fields.InfraredAppearance is not null) f1 |= 0x40;
        if (fields.ElectromagneticAppearance is not null) f2 |= 0x01;
        if (fields.AudioAppearance is not null) f2 |= 0x02;
        if (f2 != 0) f1 |= 0x80;
        w.WriteByte(f1);
        if (f2 != 0) w.WriteByte(f2);
        if (fields.ForceId is { } force) w.WriteByte(force);
        fields.EntityType?.Marshal(ref w);
        fields.AlternateEntityType?.Marshal(ref w);
        fields.Marking?.Marshal(ref w);
        if (fields.Capabilities is { } c) w.WriteUInt32(c);
        if (fields.VisualAppearance is { } v) w.WriteUInt32(v);
        if (fields.InfraredAppearance is { } ir) w.WriteUInt32(ir);
        if (fields.ElectromagneticAppearance is { } em) w.WriteUInt32(em);
        if (fields.AudioAppearance is { } au) w.WriteUInt32(au);
        return buffer[..w.Offset];
    }

    // ---- munition descriptor, shared by Fire and Detonation ----

    private static LeMunitionDescriptor ReadMunition(ref DisWireReader r, byte f) =>
        ReadMunitionTail(EntityType.Unmarshal(ref r), ref r, f);

    private static LeMunitionDescriptor ReadMunitionTail(EntityType type, ref DisWireReader r, byte f)
    {
        ushort? warhead = null, fuse = null, quantity = null, rate = null;
        if ((f & 0x10) != 0) { warhead = r.ReadUInt16(); fuse = r.ReadUInt16(); }
        if ((f & 0x20) != 0) { quantity = r.ReadUInt16(); rate = r.ReadUInt16(); }
        return new LeMunitionDescriptor(type, warhead, fuse, quantity, rate);
    }

    private static byte MunitionFlags(LeMunitionDescriptor m)
    {
        if ((m.Warhead is null) != (m.Fuse is null) || (m.Quantity is null) != (m.Rate is null))
            throw new ArgumentException("Warhead and fuse, and quantity and rate, are sent in pairs.", nameof(m));
        return (byte)((m.Warhead is not null ? 0x10 : 0) | (m.Quantity is not null ? 0x20 : 0));
    }

    private static void WriteMunition(ref DisWireWriter w, LeMunitionDescriptor m)
    {
        m.MunitionType.Marshal(ref w);
        if (m.Warhead is { } wh) { w.WriteUInt16(wh); w.WriteUInt16(m.Fuse!.Value); }
        if (m.Quantity is { } q) { w.WriteUInt16(q); w.WriteUInt16(m.Rate!.Value); }
    }

    private static bool SameSiteApp(LiveEntityId id, LiveEntityId owner) =>
        id.Site == owner.Site && id.Application == owner.Application;

    // ---- Fire ----

    /// <summary>The LE Fire payload's fields, or null when it does not read cleanly.</summary>
    /// <param name="payload">The bytes after the firing entity's id.</param>
    /// <param name="firingEntity">The PDU's own id; ids sent without site and application take them from it.</param>
    public static LeFireFields? TryDecodeFire(ReadOnlySpan<byte> payload, LiveEntityId firingEntity) => Read(payload, (ref DisWireReader r) =>
    {
        var f = r.ReadByte();
        if ((f & ~FireKnown) != 0) throw Undefined();
        var target = (f & 0x01) != 0 ? LiveEntityId.Unmarshal(ref r) : (LiveEntityId?)null;
        var munitionId = (f & 0x04) != 0 ? ReadId(ref r, (f & 0x02) != 0, firingEntity) : (LiveEntityId?)null;
        var eventId = ReadId(ref r, (f & 0x08) != 0, firingEntity);
        var location = (f & 0x40) != 0 ? LeRelativeWorldCoordinates.Unmarshal(ref r) : (LeRelativeWorldCoordinates?)null;
        var munition = ReadMunition(ref r, f);
        var velocity = LeVector16.Unmarshal(ref r);
        var range = r.ReadUInt16();
        return new LeFireFields(target, munitionId, eventId, location, munition, velocity, range);
    });

    /// <summary>The LE Fire payload for <paramref name="fields"/>. Ids that share the firing entity's site and application are sent short.</summary>
    public static byte[] Encode(LeFireFields fields, LiveEntityId firingEntity)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var buffer = new byte[1 + 4 + 4 + 4 + 8 + 16 + 6 + 2];
        var w = new DisWireWriter(buffer);
        var f = MunitionFlags(fields.Munition);
        if (fields.TargetId is not null) f |= 0x01;
        if (fields.MunitionId is { } mid) { f |= 0x04; if (!SameSiteApp(mid, firingEntity)) f |= 0x02; }
        if (!SameSiteApp(fields.EventId, firingEntity)) f |= 0x08;
        if (fields.Location is not null) f |= 0x40;
        w.WriteByte(f);
        fields.TargetId?.Marshal(ref w);
        if (fields.MunitionId is { } m) WriteId(ref w, m, (f & 0x02) != 0);
        WriteId(ref w, fields.EventId, (f & 0x08) != 0);
        fields.Location?.Marshal(ref w);
        WriteMunition(ref w, fields.Munition);
        fields.Velocity.Marshal(ref w);
        w.WriteUInt16(fields.Range);
        return buffer[..w.Offset];
    }

    // ---- Detonation ----

    /// <summary>The LE Detonation payload's fields, or null when it does not read cleanly.</summary>
    /// <param name="payload">The bytes after the firing entity's id.</param>
    /// <param name="firingEntity">The PDU's own id; ids sent without site and application take them from it.</param>
    public static LeDetonationFields? TryDecodeDetonation(ReadOnlySpan<byte> payload, LiveEntityId firingEntity) => Read(payload, (ref DisWireReader r) =>
    {
        var f1 = r.ReadByte();
        var f2 = (f1 & 0x80) != 0 ? r.ReadByte() : (byte)0;
        if ((f2 & ~Flag2Known) != 0) throw Undefined();
        var target = (f1 & 0x01) != 0 ? LiveEntityId.Unmarshal(ref r) : (LiveEntityId?)null;
        var munitionId = (f1 & 0x04) != 0 ? ReadId(ref r, (f1 & 0x02) != 0, firingEntity) : (LiveEntityId?)null;
        var eventId = (f2 & 0x02) != 0 ? ReadId(ref r, (f1 & 0x08) != 0, firingEntity) : (LiveEntityId?)null;
        var entityCoordinates = (f1 & 0x40) != 0;
        var world = !entityCoordinates ? LeRelativeWorldCoordinates.Unmarshal(ref r) : (LeRelativeWorldCoordinates?)null;
        var velocity = LeVector16.Unmarshal(ref r);
        var orientation = (f2 & 0x01) != 0 ? LeEulerAngles.Unmarshal(ref r) : (LeEulerAngles?)null;
        var munition = ReadMunition(ref r, f1);
        var entityLocation = entityCoordinates ? LeVector16.Unmarshal(ref r) : (LeVector16?)null;
        var result = r.ReadByte();
        return new LeDetonationFields(target, munitionId, eventId, world, velocity, orientation, munition, entityLocation, result);
    });

    /// <summary>The LE Detonation payload for <paramref name="fields"/>.</summary>
    /// <exception cref="ArgumentException">Not exactly one of the two locations is set.</exception>
    public static byte[] Encode(LeDetonationFields fields, LiveEntityId firingEntity)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if ((fields.WorldLocation is null) == (fields.EntityLocation is null))
            throw new ArgumentException("A detonation has either a world or an entity location.", nameof(fields));
        var buffer = new byte[2 + 4 + 4 + 4 + 8 + 6 + 3 + 16 + 6 + 1];
        var w = new DisWireWriter(buffer);
        var f1 = MunitionFlags(fields.Munition);
        byte f2 = 0;
        if (fields.TargetId is not null) f1 |= 0x01;
        if (fields.MunitionId is { } mid) { f1 |= 0x04; if (!SameSiteApp(mid, firingEntity)) f1 |= 0x02; }
        if (fields.EventId is { } eid) { f2 |= 0x02; if (!SameSiteApp(eid, firingEntity)) f1 |= 0x08; }
        if (fields.EntityLocation is not null) f1 |= 0x40;
        if (fields.MunitionOrientation is not null) f2 |= 0x01;
        if (f2 != 0) f1 |= 0x80;
        w.WriteByte(f1);
        if (f2 != 0) w.WriteByte(f2);
        fields.TargetId?.Marshal(ref w);
        if (fields.MunitionId is { } m) WriteId(ref w, m, (f1 & 0x02) != 0);
        if (fields.EventId is { } e) WriteId(ref w, e, (f1 & 0x08) != 0);
        fields.WorldLocation?.Marshal(ref w);
        fields.Velocity.Marshal(ref w);
        fields.MunitionOrientation?.Marshal(ref w);
        WriteMunition(ref w, fields.Munition);
        fields.EntityLocation?.Marshal(ref w);
        w.WriteByte(fields.DetonationResult);
        return buffer[..w.Offset];
    }
}
