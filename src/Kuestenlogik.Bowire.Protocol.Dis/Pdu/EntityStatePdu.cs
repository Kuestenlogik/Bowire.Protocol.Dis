// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Entity State PDU (type 1, family 1). The workhorse of DIS — one
/// of these is emitted per simulated entity on every update tick, at
/// a rate driven by movement, orientation change, or the five-second
/// heartbeat. IEEE 1278.1 §5.3.3.
/// </summary>
/// <remarks>
/// <para>
/// Variable parameters (articulated parts, attached parts, V7
/// separation / entity-type / entity-association records) ride in
/// <see cref="VariableParameters"/>. Each record adds 16 bytes to
/// the wire length; PDUs without any stay at the minimum 144.
/// </para>
/// <para>
/// Layout (144 bytes, minimum):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="EntityId"/></item>
///   <item>18 :  1 — <see cref="Force"/></item>
///   <item>19 :  1 — Number of variable parameter records</item>
///   <item>20 :  8 — <see cref="EntityType"/></item>
///   <item>28 :  8 — <see cref="AlternativeEntityType"/></item>
///   <item>36 : 12 — <see cref="LinearVelocity"/></item>
///   <item>48 : 24 — <see cref="Location"/> (ECEF)</item>
///   <item>72 : 12 — <see cref="Orientation"/></item>
///   <item>84 :  4 — <see cref="Appearance"/></item>
///   <item>88 :  1 — <see cref="DeadReckoning"/>.Algorithm</item>
///   <item>89 : 15 — DR other parameters (reserved / quaternion in V7)</item>
///   <item>104: 12 — DR linear acceleration</item>
///   <item>116: 12 — DR angular velocity</item>
///   <item>128: 12 — <see cref="Marking"/></item>
///   <item>140:  4 — <see cref="Capabilities"/></item>
/// </list>
/// </remarks>
public sealed record EntityStatePdu(
    PduHeader Header,
    EntityId EntityId,
    ForceId Force,
    EntityType EntityType,
    EntityType AlternativeEntityType,
    Vector3Float LinearVelocity,
    Vector3Double Location,
    EulerAngles Orientation,
    uint Appearance,
    DeadReckoningParameters DeadReckoning,
    EntityMarking Marking,
    uint Capabilities,
    IReadOnlyList<VariableParameter>? VariableParameters = null)
{
    /// <summary>
    /// Canonical Entity State PDU length, in bytes, with no variable
    /// parameter records attached. Matches §5.3.3's minimum.
    /// </summary>
    public const int MinimumWireLength = 144;

    /// <summary>
    /// Compute the full wire length including any variable
    /// parameter records. Each VP record is a fixed 16 bytes.
    /// </summary>
    public int WireLength =>
        MinimumWireLength + ((VariableParameters?.Count ?? 0) * VariableParameter.WireLength);

    /// <summary>
    /// Serialise the PDU into <paramref name="destination"/>. Returns
    /// the actual number of bytes written — equal to
    /// <see cref="WireLength"/>. Throws
    /// <see cref="IndexOutOfRangeException"/> when the buffer is too
    /// small.
    /// </summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var length = WireLength;

        // Header.Length covers the whole PDU including variable
        // parameters. Also forces PduType / ProtocolFamily to match
        // what this PDU represents so callers who built the header
        // generically still produce correct wire bytes.
        var header = Header with
        {
            PduType = DisPduType.EntityState,
            ProtocolFamily = DisProtocolFamily.EntityInformation,
            Length = (ushort)length,
        };
        header.Marshal(ref w);

        EntityId.Marshal(ref w);
        w.WriteByte((byte)Force);

        var variableParams = VariableParameters ?? Array.Empty<VariableParameter>();
        w.WriteByte((byte)variableParams.Count);

        EntityType.Marshal(ref w);
        AlternativeEntityType.Marshal(ref w);
        LinearVelocity.Marshal(ref w);
        Location.Marshal(ref w);
        Orientation.Marshal(ref w);
        w.WriteUInt32(Appearance);

        DeadReckoning.Marshal(ref w);
        Marking.Marshal(ref w);
        w.WriteUInt32(Capabilities);

        foreach (var vp in variableParams) vp.Marshal(ref w);

        return w.Offset;
    }

    /// <summary>
    /// Shortcut overload that allocates a buffer sized to
    /// <see cref="WireLength"/> and marshals into it. Fine for tests
    /// and ad-hoc fixture generation; hot paths should reuse a buffer
    /// via the span overload instead.
    /// </summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>
    /// Parse an Entity State PDU off the wire. Expects
    /// <paramref name="source"/> to start with the 12-byte header —
    /// the caller is responsible for slicing to PDU boundaries when
    /// multiple PDUs arrive back-to-back in a single datagram.
    /// </summary>
    public static EntityStatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var entityId = EntityId.Unmarshal(ref r);
        var force = (ForceId)r.ReadByte();
        var variableParamCount = r.ReadByte();
        var entityType = Records.EntityType.Unmarshal(ref r);
        var altEntityType = Records.EntityType.Unmarshal(ref r);
        var linearVelocity = Vector3Float.Unmarshal(ref r);
        var location = Vector3Double.Unmarshal(ref r);
        var orientation = EulerAngles.Unmarshal(ref r);
        var appearance = r.ReadUInt32();
        var deadReckoning = DeadReckoningParameters.Unmarshal(ref r);
        var marking = EntityMarking.Unmarshal(ref r);
        var capabilities = r.ReadUInt32();

        List<VariableParameter>? variableParams = null;
        if (variableParamCount > 0)
        {
            variableParams = new List<VariableParameter>(variableParamCount);
            for (var i = 0; i < variableParamCount; i++)
            {
                variableParams.Add(VariableParameter.Unmarshal(ref r));
            }
        }

        return new EntityStatePdu(
            header,
            entityId,
            force,
            entityType,
            altEntityType,
            linearVelocity,
            location,
            orientation,
            appearance,
            deadReckoning,
            marking,
            capabilities,
            variableParams);
    }
}

/// <summary>
/// Dead-reckoning parameters (40 bytes, §5.2.13). Tells receivers
/// how to extrapolate entity state between updates.
/// </summary>
/// <param name="Algorithm">Algorithm enum — picks the motion model.</param>
/// <param name="OtherParameters">15 reserved bytes; V7 overloads the first 3 bytes here with a quaternion flag + 2-byte padding.</param>
/// <param name="LinearAcceleration">Constant acceleration when the algorithm is second-order.</param>
/// <param name="AngularVelocity">Constant angular velocity when the algorithm is rotational.</param>
public readonly record struct DeadReckoningParameters(
    DeadReckoningAlgorithm Algorithm,
    byte[] OtherParameters,
    Vector3Float LinearAcceleration,
    Vector3Float AngularVelocity)
{
    /// <summary>Length of the reserved "other parameters" slot in bytes.</summary>
    public const int OtherParametersLength = 15;

    /// <summary>Total wire length of the dead-reckoning record, in bytes.</summary>
    public const int WireLength = 1 + OtherParametersLength + Vector3Float.WireLength + Vector3Float.WireLength;

    /// <summary>
    /// Default parameters for a static entity: algorithm <see cref="DeadReckoningAlgorithm.FPW"/>,
    /// zero acceleration and angular velocity, reserved bytes all zero.
    /// Matches the lowest-effort "just keep moving at constant velocity"
    /// dead-reckoning most scenarios ship with.
    /// </summary>
    public static DeadReckoningParameters Default => new(
        DeadReckoningAlgorithm.FPW,
        new byte[OtherParametersLength],
        Vector3Float.Zero,
        Vector3Float.Zero);

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte((byte)Algorithm);
        var padded = OtherParameters.Length == OtherParametersLength
            ? OtherParameters
            : NormaliseOtherParameters(OtherParameters);
        w.WriteBytes(padded);
        LinearAcceleration.Marshal(ref w);
        AngularVelocity.Marshal(ref w);
    }

    internal static DeadReckoningParameters Unmarshal(ref DisWireReader r)
    {
        var algorithm = (DeadReckoningAlgorithm)r.ReadByte();
        var other = r.ReadBytes(OtherParametersLength).ToArray();
        var accel = Vector3Float.Unmarshal(ref r);
        var angular = Vector3Float.Unmarshal(ref r);
        return new DeadReckoningParameters(algorithm, other, accel, angular);
    }

    private static byte[] NormaliseOtherParameters(byte[] source)
    {
        var padded = new byte[OtherParametersLength];
        var take = Math.Min(source.Length, padded.Length);
        Array.Copy(source, padded, take);
        return padded;
    }
}
