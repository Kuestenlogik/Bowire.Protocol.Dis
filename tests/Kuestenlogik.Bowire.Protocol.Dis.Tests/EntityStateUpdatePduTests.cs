// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class EntityStateUpdatePduTests
{
    private static EntityStateUpdatePdu Build(
        IReadOnlyList<VariableParameter>? variableParameters = null) => new(
        Header: PduHeader.ForV6(1, DisPduType.EntityStateUpdate,
            DisProtocolFamily.EntityInformation, EntityStateUpdatePdu.MinimumWireLength),
        EntityId: new EntityId(1, 1, 1000),
        LinearVelocity: new Vector3Float(10f, 0f, 0f),
        Location: new Vector3Double(3765000.5, 661000.25, 5108000.125),
        Orientation: new EulerAngles(0.1f, 0.2f, 0.3f),
        Appearance: 0xCAFEBABE,
        VariableParameters: variableParameters);

    [Fact]
    public void Marshal_WithoutVariableParams_Is72Bytes()
    {
        Assert.Equal(EntityStateUpdatePdu.MinimumWireLength, Build().Marshal().Length);
    }

    [Fact]
    public void Marshal_HeaderBytesIdentifyEntityStateUpdate()
    {
        var bytes = Build().Marshal();
        Assert.Equal((byte)DisPduType.EntityStateUpdate, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.EntityInformation, bytes[3]);
    }

    [Fact]
    public void Roundtrip_PreservesKinematicFields()
    {
        var original = Build();
        var decoded = EntityStateUpdatePdu.Unmarshal(original.Marshal());
        Assert.Equal(original.EntityId, decoded.EntityId);
        Assert.Equal(original.LinearVelocity, decoded.LinearVelocity);
        Assert.Equal(original.Location, decoded.Location);
        Assert.Equal(original.Orientation, decoded.Orientation);
        Assert.Equal(original.Appearance, decoded.Appearance);
    }

    [Fact]
    public void WithSingleArticulatedPart_AddsSixteenBytes_RoundTrips()
    {
        var articulation = new ArticulatedPartParameter(
            ChangeIndicator: 1,
            PartAttachedTo: 0,
            ParameterType: 0x00_00_09_04, // turret azimuth
            ParameterValue: 0x3F80_0000_0000_0000UL);

        var original = Build(new[] { (VariableParameter)articulation });
        var bytes = original.Marshal();

        Assert.Equal(EntityStateUpdatePdu.MinimumWireLength + VariableParameter.WireLength, bytes.Length);

        var decoded = EntityStateUpdatePdu.Unmarshal(bytes);
        Assert.NotNull(decoded.VariableParameters);
        Assert.Single(decoded.VariableParameters!);
        var roundtripped = Assert.IsType<ArticulatedPartParameter>(decoded.VariableParameters![0]);
        Assert.Equal(articulation.ChangeIndicator, roundtripped.ChangeIndicator);
        Assert.Equal(articulation.PartAttachedTo, roundtripped.PartAttachedTo);
        Assert.Equal(articulation.ParameterType, roundtripped.ParameterType);
        Assert.Equal(articulation.ParameterValue, roundtripped.ParameterValue);
    }
}
