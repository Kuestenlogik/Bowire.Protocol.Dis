// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Entity State Update PDU (type 67, family 1). Lightweight sibling
/// of <see cref="EntityStatePdu"/> — carries only the kinematic state
/// (velocity, location, orientation, appearance) of an entity
/// already known to the receiver from an earlier full Entity State.
/// Used for high-rate updates between heartbeats to keep bandwidth
/// low. IEEE 1278.1 §5.3.3.4.
/// </summary>
/// <remarks>
/// <para>
/// Layout (72 bytes minimum, +16 bytes per variable parameter):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="EntityId"/></item>
///   <item>18 :  1 — reserved padding</item>
///   <item>19 :  1 — number of variable parameter records</item>
///   <item>20 : 12 — <see cref="LinearVelocity"/></item>
///   <item>32 : 24 — <see cref="Location"/> (ECEF)</item>
///   <item>56 : 12 — <see cref="Orientation"/></item>
///   <item>68 :  4 — <see cref="Appearance"/></item>
///   <item>72 : N×16 — <see cref="VariableParameters"/></item>
/// </list>
/// </remarks>
public sealed record EntityStateUpdatePdu(
    PduHeader Header,
    EntityId EntityId,
    Vector3Float LinearVelocity,
    Vector3Double Location,
    EulerAngles Orientation,
    uint Appearance,
    IReadOnlyList<VariableParameter>? VariableParameters = null)
{
    /// <summary>Wire length with no variable parameter records.</summary>
    public const int MinimumWireLength = 72;

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
            PduType = DisPduType.EntityStateUpdate,
            ProtocolFamily = DisProtocolFamily.EntityInformation,
            Length = (ushort)length,
        };
        header.Marshal(ref w);

        EntityId.Marshal(ref w);
        w.WriteByte(0); // reserved padding

        var variableParams = VariableParameters ?? Array.Empty<VariableParameter>();
        w.WriteByte((byte)variableParams.Count);

        LinearVelocity.Marshal(ref w);
        Location.Marshal(ref w);
        Orientation.Marshal(ref w);
        w.WriteUInt32(Appearance);

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
    public static EntityStateUpdatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var entityId = EntityId.Unmarshal(ref r);
        r.SkipPadding(1);
        var variableParamCount = r.ReadByte();
        var linearVelocity = Vector3Float.Unmarshal(ref r);
        var location = Vector3Double.Unmarshal(ref r);
        var orientation = EulerAngles.Unmarshal(ref r);
        var appearance = r.ReadUInt32();

        List<VariableParameter>? variableParams = null;
        if (variableParamCount > 0)
        {
            variableParams = new List<VariableParameter>(variableParamCount);
            for (var i = 0; i < variableParamCount; i++)
            {
                variableParams.Add(VariableParameter.Unmarshal(ref r));
            }
        }

        return new EntityStateUpdatePdu(
            header, entityId, linearVelocity, location, orientation, appearance, variableParams);
    }
}
