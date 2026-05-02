// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Detonation PDU (type 3, family 2). Sent when a munition
/// detonates (or misses) — the companion to <see cref="FirePdu"/>.
/// IEEE 1278.1 §5.3.4.2.
/// </summary>
/// <remarks>
/// <para>
/// Layout (104 bytes minimum, +16 bytes per variable parameter):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="FiringEntityId"/></item>
///   <item>18 :  6 — <see cref="TargetEntityId"/></item>
///   <item>24 :  6 — <see cref="MunitionId"/> (V7: exploding entity id)</item>
///   <item>30 :  6 — <see cref="EventId"/></item>
///   <item>36 : 12 — <see cref="Velocity"/> at detonation</item>
///   <item>48 : 24 — <see cref="LocationInWorldCoordinates"/> (ECEF)</item>
///   <item>72 : 16 — <see cref="MunitionDescriptor"/></item>
///   <item>88 : 12 — <see cref="LocationInEntityCoordinates"/> (body coords of the hit entity)</item>
///   <item>100:  1 — <see cref="DetonationResult"/></item>
///   <item>101:  1 — number of variable parameter records</item>
///   <item>102:  2 — reserved padding</item>
///   <item>104: N×16 — <see cref="VariableParameters"/></item>
/// </list>
/// </remarks>
public sealed record DetonationPdu(
    PduHeader Header,
    EntityId FiringEntityId,
    EntityId TargetEntityId,
    EntityId MunitionId,
    EventId EventId,
    Vector3Float Velocity,
    Vector3Double LocationInWorldCoordinates,
    MunitionDescriptor MunitionDescriptor,
    Vector3Float LocationInEntityCoordinates,
    DetonationResult DetonationResult,
    IReadOnlyList<VariableParameter>? VariableParameters = null)
{
    /// <summary>Wire length with no variable parameter records.</summary>
    public const int MinimumWireLength = 104;

    /// <summary>Wire length including variable parameter records.</summary>
    public int WireLength =>
        MinimumWireLength + ((VariableParameters?.Count ?? 0) * VariableParameter.WireLength);

    /// <summary>Serialise into <paramref name="destination"/>; returns bytes written.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var length = WireLength;
        var header = Header with
        {
            PduType = DisPduType.Detonation,
            ProtocolFamily = DisProtocolFamily.Warfare,
            Length = (ushort)length,
        };
        header.Marshal(ref w);

        FiringEntityId.Marshal(ref w);
        TargetEntityId.Marshal(ref w);
        MunitionId.Marshal(ref w);
        EventId.Marshal(ref w);
        Velocity.Marshal(ref w);
        LocationInWorldCoordinates.Marshal(ref w);
        MunitionDescriptor.Marshal(ref w);
        LocationInEntityCoordinates.Marshal(ref w);

        w.WriteByte((byte)DetonationResult);

        var variableParams = VariableParameters ?? Array.Empty<VariableParameter>();
        w.WriteByte((byte)variableParams.Count);
        w.WriteUInt16(0); // reserved padding

        foreach (var vp in variableParams) vp.Marshal(ref w);

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
    public static DetonationPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var firing = EntityId.Unmarshal(ref r);
        var target = EntityId.Unmarshal(ref r);
        var munitionId = EntityId.Unmarshal(ref r);
        var evt = Records.EventId.Unmarshal(ref r);
        var velocity = Vector3Float.Unmarshal(ref r);
        var worldLocation = Vector3Double.Unmarshal(ref r);
        var munitionDesc = Records.MunitionDescriptor.Unmarshal(ref r);
        var entityLocation = Vector3Float.Unmarshal(ref r);
        var result = (DetonationResult)r.ReadByte();
        var variableParamCount = r.ReadByte();
        r.SkipPadding(2);

        List<VariableParameter>? variableParams = null;
        if (variableParamCount > 0)
        {
            variableParams = new List<VariableParameter>(variableParamCount);
            for (var i = 0; i < variableParamCount; i++)
            {
                variableParams.Add(VariableParameter.Unmarshal(ref r));
            }
        }

        return new DetonationPdu(
            header, firing, target, munitionId, evt, velocity,
            worldLocation, munitionDesc, entityLocation, result, variableParams);
    }
}
