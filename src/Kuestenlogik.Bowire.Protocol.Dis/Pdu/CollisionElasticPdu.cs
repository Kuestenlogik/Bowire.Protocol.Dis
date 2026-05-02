// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Collision-Elastic PDU (type 40, family 1, V7 addition). Richer
/// collision report than <see cref="CollisionPdu"/> — includes a
/// full 3x3 symmetric intermediate-result matrix (the "XX, XY, XZ,
/// YY, YZ, ZZ" components of the impulse tensor), surface normal,
/// and restitution coefficient so the receiver can compute a
/// physically accurate reaction without further negotiation.
/// IEEE 1278.1-2012 §5.3.3.3.
/// </summary>
/// <remarks>
/// <para>
/// Layout (100 bytes):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="IssuingEntityId"/></item>
///   <item>18 :  6 — <see cref="CollidingEntityId"/></item>
///   <item>24 :  6 — <see cref="EventId"/></item>
///   <item>30 :  2 — reserved padding</item>
///   <item>32 : 12 — <see cref="ContactVelocity"/></item>
///   <item>44 :  4 — <see cref="Mass"/></item>
///   <item>48 : 12 — <see cref="LocationOfImpact"/></item>
///   <item>60 : 24 — 6 float32 intermediate-result components</item>
///   <item>84 : 12 — <see cref="UnitSurfaceNormal"/></item>
///   <item>96 :  4 — <see cref="CoefficientOfRestitution"/></item>
/// </list>
/// </remarks>
public sealed record CollisionElasticPdu(
    PduHeader Header,
    EntityId IssuingEntityId,
    EntityId CollidingEntityId,
    EventId EventId,
    Vector3Float ContactVelocity,
    float Mass,
    Vector3Float LocationOfImpact,
    float IntermediateResultXX,
    float IntermediateResultXY,
    float IntermediateResultXZ,
    float IntermediateResultYY,
    float IntermediateResultYZ,
    float IntermediateResultZZ,
    Vector3Float UnitSurfaceNormal,
    float CoefficientOfRestitution)
{
    /// <summary>Wire length in bytes.</summary>
    public const int WireLength = 100;

    /// <summary>Serialise into <paramref name="destination"/>; returns bytes written.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.CollisionElastic,
            ProtocolFamily = DisProtocolFamily.EntityInformation,
            Length = WireLength,
        };
        header.Marshal(ref w);

        IssuingEntityId.Marshal(ref w);
        CollidingEntityId.Marshal(ref w);
        EventId.Marshal(ref w);
        w.WriteUInt16(0); // padding

        ContactVelocity.Marshal(ref w);
        w.WriteSingle(Mass);
        LocationOfImpact.Marshal(ref w);

        w.WriteSingle(IntermediateResultXX);
        w.WriteSingle(IntermediateResultXY);
        w.WriteSingle(IntermediateResultXZ);
        w.WriteSingle(IntermediateResultYY);
        w.WriteSingle(IntermediateResultYZ);
        w.WriteSingle(IntermediateResultZZ);

        UnitSurfaceNormal.Marshal(ref w);
        w.WriteSingle(CoefficientOfRestitution);

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
    public static CollisionElasticPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var issuing = EntityId.Unmarshal(ref r);
        var colliding = EntityId.Unmarshal(ref r);
        var evt = Records.EventId.Unmarshal(ref r);
        r.SkipPadding(2);

        var contactVelocity = Vector3Float.Unmarshal(ref r);
        var mass = r.ReadSingle();
        var location = Vector3Float.Unmarshal(ref r);

        var xx = r.ReadSingle();
        var xy = r.ReadSingle();
        var xz = r.ReadSingle();
        var yy = r.ReadSingle();
        var yz = r.ReadSingle();
        var zz = r.ReadSingle();

        var normal = Vector3Float.Unmarshal(ref r);
        var restitution = r.ReadSingle();

        return new CollisionElasticPdu(
            header, issuing, colliding, evt,
            contactVelocity, mass, location,
            xx, xy, xz, yy, yz, zz,
            normal, restitution);
    }
}
