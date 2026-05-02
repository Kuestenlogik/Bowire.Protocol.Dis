// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Fire PDU (type 2, family 2). Sent when an entity fires a
/// munition. Carries the firing entity, target, munition id, world-
/// coordinate location, velocity at launch, range, and munition
/// descriptor. IEEE 1278.1 §5.3.4.1.
/// </summary>
/// <remarks>
/// <para>
/// Layout (96 bytes):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="FiringEntityId"/></item>
///   <item>18 :  6 — <see cref="TargetEntityId"/></item>
///   <item>24 :  6 — <see cref="MunitionId"/> (V7: "Munition/Expendable ID")</item>
///   <item>30 :  6 — <see cref="EventId"/></item>
///   <item>36 :  4 — <see cref="FireMissionIndex"/></item>
///   <item>40 : 24 — <see cref="LocationInWorldCoordinates"/> (ECEF)</item>
///   <item>64 : 16 — <see cref="MunitionDescriptor"/> (V6: "Burst Descriptor")</item>
///   <item>80 : 12 — <see cref="Velocity"/> at launch</item>
///   <item>92 :  4 — <see cref="Range"/> (metres, 0 if unknown)</item>
/// </list>
/// </remarks>
public sealed record FirePdu(
    PduHeader Header,
    EntityId FiringEntityId,
    EntityId TargetEntityId,
    EntityId MunitionId,
    EventId EventId,
    uint FireMissionIndex,
    Vector3Double LocationInWorldCoordinates,
    MunitionDescriptor MunitionDescriptor,
    Vector3Float Velocity,
    float Range)
{
    /// <summary>Wire length in bytes — fixed, no variable records.</summary>
    public const int WireLength = 96;

    /// <summary>Serialise into <paramref name="destination"/>; returns bytes written.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.Fire,
            ProtocolFamily = DisProtocolFamily.Warfare,
            Length = WireLength,
        };
        header.Marshal(ref w);

        FiringEntityId.Marshal(ref w);
        TargetEntityId.Marshal(ref w);
        MunitionId.Marshal(ref w);
        EventId.Marshal(ref w);
        w.WriteUInt32(FireMissionIndex);
        LocationInWorldCoordinates.Marshal(ref w);
        MunitionDescriptor.Marshal(ref w);
        Velocity.Marshal(ref w);
        w.WriteSingle(Range);

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
    public static FirePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var firing = EntityId.Unmarshal(ref r);
        var target = EntityId.Unmarshal(ref r);
        var munitionId = EntityId.Unmarshal(ref r);
        var evt = Records.EventId.Unmarshal(ref r);
        var fireMission = r.ReadUInt32();
        var location = Vector3Double.Unmarshal(ref r);
        var munitionDesc = Records.MunitionDescriptor.Unmarshal(ref r);
        var velocity = Vector3Float.Unmarshal(ref r);
        var range = r.ReadSingle();

        return new FirePdu(
            header, firing, target, munitionId, evt,
            fireMission, location, munitionDesc, velocity, range);
    }
}
