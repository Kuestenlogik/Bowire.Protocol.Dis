// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Byte-layout + roundtrip coverage for Entity State PDU. Each
/// field position is pinned against the IEEE 1278.1 §5.3.3 table so
/// a refactor can't silently move a byte.
/// </summary>
public sealed class EntityStatePduTests
{
    private static EntityStatePdu BuildSample() => new(
        Header: PduHeader.ForV6(
            exerciseId: 42,
            pduType: DisPduType.EntityState,
            family: DisProtocolFamily.EntityInformation,
            length: EntityStatePdu.MinimumWireLength),
        EntityId: new EntityId(0x0102, 0x0304, 0x0506),
        Force: ForceId.Friendly,
        EntityType: new EntityType(1, 1, 225, 1, 1, 0, 0),
        AlternativeEntityType: new EntityType(1, 1, 225, 1, 1, 0, 0),
        LinearVelocity: new Vector3Float(1f, 2f, 3f),
        Location: new Vector3Double(3765000.5, 661000.25, 5108000.125),
        Orientation: new EulerAngles(0.1f, 0.2f, 0.3f),
        Appearance: 0xDEADBEEF,
        DeadReckoning: DeadReckoningParameters.Default,
        Marking: EntityMarking.Ascii("BOWIRE01"),
        Capabilities: 0);

    [Fact]
    public void Marshal_ProducesExactly144Bytes()
    {
        var bytes = BuildSample().Marshal();
        Assert.Equal(EntityStatePdu.MinimumWireLength, bytes.Length);
    }

    [Fact]
    public void Marshal_HeaderBytesMatchSpec()
    {
        var bytes = BuildSample().Marshal();

        Assert.Equal(6, bytes[0]);   // V6 protocol version
        Assert.Equal(42, bytes[1]);  // exercise id
        Assert.Equal(1, bytes[2]);   // PDU type: Entity State
        Assert.Equal(1, bytes[3]);   // protocol family: Entity Info
        Assert.Equal(144, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2)));
    }

    [Fact]
    public void Marshal_EntityIdIsBigEndianAtOffset12()
    {
        var bytes = BuildSample().Marshal();
        Assert.Equal(0x0102, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(12, 2)));
        Assert.Equal(0x0304, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(14, 2)));
        Assert.Equal(0x0506, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(16, 2)));
    }

    [Fact]
    public void Marshal_LocationDoublesLandAtOffset48()
    {
        var bytes = BuildSample().Marshal();
        Assert.Equal(3765000.5, BinaryPrimitives.ReadDoubleBigEndian(bytes.AsSpan(48, 8)));
        Assert.Equal(661000.25, BinaryPrimitives.ReadDoubleBigEndian(bytes.AsSpan(56, 8)));
        Assert.Equal(5108000.125, BinaryPrimitives.ReadDoubleBigEndian(bytes.AsSpan(64, 8)));
    }

    [Fact]
    public void Marshal_MarkingSlotIsNulPaddedAsciiAt128()
    {
        var bytes = BuildSample().Marshal();
        Assert.Equal(1, bytes[128]);   // ASCII charset
        Assert.Equal("BOWIRE01", System.Text.Encoding.ASCII.GetString(bytes.AsSpan(129, 8)));
        Assert.Equal(0, bytes[137]);   // NUL-padded
        Assert.Equal(0, bytes[138]);
        Assert.Equal(0, bytes[139]);
    }

    [Fact]
    public void Roundtrip_MarshalThenUnmarshal_PreservesEveryField()
    {
        var original = BuildSample();
        var bytes = original.Marshal();
        var decoded = EntityStatePdu.Unmarshal(bytes);

        Assert.Equal(original.Header.ProtocolVersion, decoded.Header.ProtocolVersion);
        Assert.Equal(original.Header.ExerciseId, decoded.Header.ExerciseId);
        Assert.Equal(original.Header.PduType, decoded.Header.PduType);
        Assert.Equal(original.Header.ProtocolFamily, decoded.Header.ProtocolFamily);
        Assert.Equal(original.Header.Length, decoded.Header.Length);

        Assert.Equal(original.EntityId, decoded.EntityId);
        Assert.Equal(original.Force, decoded.Force);
        Assert.Equal(original.EntityType, decoded.EntityType);
        Assert.Equal(original.AlternativeEntityType, decoded.AlternativeEntityType);
        Assert.Equal(original.LinearVelocity, decoded.LinearVelocity);
        Assert.Equal(original.Location, decoded.Location);
        Assert.Equal(original.Orientation, decoded.Orientation);
        Assert.Equal(original.Appearance, decoded.Appearance);
        Assert.Equal(original.DeadReckoning.Algorithm, decoded.DeadReckoning.Algorithm);
        Assert.Equal(original.DeadReckoning.LinearAcceleration, decoded.DeadReckoning.LinearAcceleration);
        Assert.Equal(original.DeadReckoning.AngularVelocity, decoded.DeadReckoning.AngularVelocity);
        Assert.Equal(original.Marking, decoded.Marking);
        Assert.Equal(original.Capabilities, decoded.Capabilities);
    }

    [Fact]
    public void WithTwoVariableParameters_ExpandsLengthBy32_RoundTrips()
    {
        var articulation = new ArticulatedPartParameter(
            ChangeIndicator: 3,
            PartAttachedTo: 0,
            ParameterType: 0x00_00_09_0C, // secondary turret azimuth
            ParameterValue: 0x4040_0000_0000_0000UL);
        var attached = new AttachedPartParameter(
            DetachedIndicator: 0,
            PartAttachedTo: 1,
            ParameterType: 0x00_00_00_01,
            AttachedPartType: new EntityType(2, 2, 225, 1, 2, 0, 0));

        var original = BuildSample() with
        {
            VariableParameters = new VariableParameter[] { articulation, attached }
        };

        var bytes = original.Marshal();
        Assert.Equal(EntityStatePdu.MinimumWireLength + (VariableParameter.WireLength * 2), bytes.Length);

        var decoded = EntityStatePdu.Unmarshal(bytes);
        Assert.NotNull(decoded.VariableParameters);
        Assert.Equal(2, decoded.VariableParameters!.Count);

        var decodedArticulation = Assert.IsType<ArticulatedPartParameter>(decoded.VariableParameters[0]);
        Assert.Equal(articulation.ParameterValue, decodedArticulation.ParameterValue);

        var decodedAttached = Assert.IsType<AttachedPartParameter>(decoded.VariableParameters[1]);
        Assert.Equal(attached.AttachedPartType, decodedAttached.AttachedPartType);
    }

    [Fact]
    public void V7Header_SetsProtocolVersionSeven()
    {
        var pdu = BuildSample() with
        {
            Header = PduHeader.ForV7(
                exerciseId: 1,
                pduType: DisPduType.EntityState,
                family: DisProtocolFamily.EntityInformation,
                length: EntityStatePdu.MinimumWireLength)
        };
        var bytes = pdu.Marshal();
        Assert.Equal(7, bytes[0]);
    }
}
