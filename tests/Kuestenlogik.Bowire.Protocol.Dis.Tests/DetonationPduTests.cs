// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class DetonationPduTests
{
    private static DetonationPdu Build(
        IReadOnlyList<VariableParameter>? variableParameters = null) => new(
        Header: PduHeader.ForV6(1, DisPduType.Detonation, DisProtocolFamily.Warfare, DetonationPdu.MinimumWireLength),
        FiringEntityId: new EntityId(1, 1, 100),
        TargetEntityId: new EntityId(1, 1, 200),
        MunitionId: new EntityId(1, 1, 9001),
        EventId: new EventId(1, 1, 42),
        Velocity: new Vector3Float(50f, -5f, 0f),
        LocationInWorldCoordinates: new Vector3Double(3765100.0, 661100.0, 5108100.0),
        MunitionDescriptor: new MunitionDescriptor(
            MunitionType: new EntityType(2, 2, 225, 2, 1, 0, 0),
            Warhead: 1000, Fuse: 1000, Quantity: 1, Rate: 0),
        LocationInEntityCoordinates: new Vector3Float(0.5f, 0f, 1.2f),
        DetonationResult: DetonationResult.EntityImpact,
        VariableParameters: variableParameters);

    [Fact]
    public void Marshal_WithoutVariableParams_Is104Bytes() =>
        Assert.Equal(DetonationPdu.MinimumWireLength, Build().Marshal().Length);

    [Fact]
    public void Marshal_HeaderBytesIdentifyDetonation()
    {
        var bytes = Build().Marshal();
        Assert.Equal((byte)DisPduType.Detonation, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.Warfare, bytes[3]);
    }

    [Fact]
    public void Marshal_DetonationResultLandsAtOffset100()
    {
        var bytes = Build().Marshal();
        Assert.Equal((byte)DetonationResult.EntityImpact, bytes[100]);
    }

    [Fact]
    public void Roundtrip_PreservesEveryField()
    {
        var original = Build();
        var decoded = DetonationPdu.Unmarshal(original.Marshal());
        Assert.Equal(original.FiringEntityId, decoded.FiringEntityId);
        Assert.Equal(original.TargetEntityId, decoded.TargetEntityId);
        Assert.Equal(original.MunitionId, decoded.MunitionId);
        Assert.Equal(original.EventId, decoded.EventId);
        Assert.Equal(original.Velocity, decoded.Velocity);
        Assert.Equal(original.LocationInWorldCoordinates, decoded.LocationInWorldCoordinates);
        Assert.Equal(original.MunitionDescriptor, decoded.MunitionDescriptor);
        Assert.Equal(original.LocationInEntityCoordinates, decoded.LocationInEntityCoordinates);
        Assert.Equal(original.DetonationResult, decoded.DetonationResult);
    }

    [Fact]
    public void WithArticulatedPart_AddsSixteenBytes_RoundTrips()
    {
        var vp = new ArticulatedPartParameter(
            ChangeIndicator: 5,
            PartAttachedTo: 0,
            ParameterType: 0x00_00_09_04,
            ParameterValue: 0x4000_0000_0000_0000UL);

        var original = Build(new[] { (VariableParameter)vp });
        var bytes = original.Marshal();
        Assert.Equal(DetonationPdu.MinimumWireLength + VariableParameter.WireLength, bytes.Length);

        var decoded = DetonationPdu.Unmarshal(bytes);
        Assert.NotNull(decoded.VariableParameters);
        Assert.Single(decoded.VariableParameters!);
        var rt = Assert.IsType<ArticulatedPartParameter>(decoded.VariableParameters![0]);
        Assert.Equal(vp.ParameterValue, rt.ParameterValue);
    }
}
