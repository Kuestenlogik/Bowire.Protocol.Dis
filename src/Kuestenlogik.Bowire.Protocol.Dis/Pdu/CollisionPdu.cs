// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Collision PDU (type 4, family 1). Sent when two entities collide
/// to report the event with enough physical information for each
/// side to model the impact. IEEE 1278.1 §5.3.3.2.
/// </summary>
/// <remarks>
/// <para>
/// Layout (60 bytes):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="IssuingEntityId"/></item>
///   <item>18 :  6 — <see cref="CollidingEntityId"/></item>
///   <item>24 :  6 — <see cref="EventId"/></item>
///   <item>30 :  1 — <see cref="CollisionType"/></item>
///   <item>31 :  1 — reserved padding</item>
///   <item>32 : 12 — <see cref="Velocity"/></item>
///   <item>44 :  4 — <see cref="Mass"/> (kg)</item>
///   <item>48 : 12 — <see cref="Location"/> in issuing-entity body coords</item>
/// </list>
/// </remarks>
public sealed record CollisionPdu(
    PduHeader Header,
    EntityId IssuingEntityId,
    EntityId CollidingEntityId,
    EventId EventId,
    CollisionType CollisionType,
    Vector3Float Velocity,
    float Mass,
    Vector3Float Location)
{
    /// <summary>Wire length in bytes — fixed, no variable records.</summary>
    public const int WireLength = 60;

    /// <summary>Serialise into <paramref name="destination"/>; returns bytes written.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.Collision,
            ProtocolFamily = DisProtocolFamily.EntityInformation,
            Length = WireLength,
        };
        header.Marshal(ref w);

        IssuingEntityId.Marshal(ref w);
        CollidingEntityId.Marshal(ref w);
        EventId.Marshal(ref w);

        w.WriteByte((byte)CollisionType);
        w.WriteByte(0); // reserved padding

        Velocity.Marshal(ref w);
        w.WriteSingle(Mass);
        Location.Marshal(ref w);

        return w.Offset;
    }

    /// <summary>Allocation-included shortcut; returns a <see cref="WireLength"/>-byte array.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static CollisionPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var issuing = EntityId.Unmarshal(ref r);
        var colliding = EntityId.Unmarshal(ref r);
        var evt = Records.EventId.Unmarshal(ref r);
        var type = (CollisionType)r.ReadByte();
        r.SkipPadding(1);
        var velocity = Vector3Float.Unmarshal(ref r);
        var mass = r.ReadSingle();
        var location = Vector3Float.Unmarshal(ref r);
        return new CollisionPdu(header, issuing, colliding, evt, type, velocity, mass, location);
    }
}

/// <summary>
/// Collision type code (byte). IEEE 1278.1 §5.2.11 — picks the
/// physical model the receiver should apply.
/// </summary>
public enum CollisionType
{
    /// <summary>Inelastic — energy absorbed (e.g. crumple collision).</summary>
    Inelastic = 0,
    /// <summary>Elastic — energy conserved (e.g. armored clash).</summary>
    Elastic = 1,
}
