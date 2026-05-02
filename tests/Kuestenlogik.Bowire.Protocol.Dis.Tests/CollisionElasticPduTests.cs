// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class CollisionElasticPduTests
{
    private static CollisionElasticPdu Build() => new(
        Header: PduHeader.ForV7(1, DisPduType.CollisionElastic,
            DisProtocolFamily.EntityInformation, CollisionElasticPdu.WireLength),
        IssuingEntityId: new EntityId(1, 1, 100),
        CollidingEntityId: new EntityId(1, 1, 200),
        EventId: new EventId(1, 1, 42),
        ContactVelocity: new Vector3Float(12.5f, 0f, 0f),
        Mass: 8000f,
        LocationOfImpact: new Vector3Float(1f, 0f, 0.5f),
        IntermediateResultXX: 1f,
        IntermediateResultXY: 0f,
        IntermediateResultXZ: 0.1f,
        IntermediateResultYY: 2f,
        IntermediateResultYZ: -0.05f,
        IntermediateResultZZ: 1.5f,
        UnitSurfaceNormal: new Vector3Float(1f, 0f, 0f),
        CoefficientOfRestitution: 0.8f);

    [Fact]
    public void Marshal_IsExactly100Bytes() =>
        Assert.Equal(CollisionElasticPdu.WireLength, Build().Marshal().Length);

    [Fact]
    public void Marshal_EmitsV7HeaderByte()
    {
        var bytes = Build().Marshal();
        Assert.Equal(7, bytes[0]);
        Assert.Equal((byte)DisPduType.CollisionElastic, bytes[2]);
    }

    [Fact]
    public void Roundtrip_PreservesEveryField()
    {
        var original = Build();
        var decoded = CollisionElasticPdu.Unmarshal(original.Marshal());
        Assert.Equal(original.IssuingEntityId, decoded.IssuingEntityId);
        Assert.Equal(original.CollidingEntityId, decoded.CollidingEntityId);
        Assert.Equal(original.EventId, decoded.EventId);
        Assert.Equal(original.ContactVelocity, decoded.ContactVelocity);
        Assert.Equal(original.Mass, decoded.Mass);
        Assert.Equal(original.LocationOfImpact, decoded.LocationOfImpact);
        Assert.Equal(original.IntermediateResultXX, decoded.IntermediateResultXX);
        Assert.Equal(original.IntermediateResultXY, decoded.IntermediateResultXY);
        Assert.Equal(original.IntermediateResultXZ, decoded.IntermediateResultXZ);
        Assert.Equal(original.IntermediateResultYY, decoded.IntermediateResultYY);
        Assert.Equal(original.IntermediateResultYZ, decoded.IntermediateResultYZ);
        Assert.Equal(original.IntermediateResultZZ, decoded.IntermediateResultZZ);
        Assert.Equal(original.UnitSurfaceNormal, decoded.UnitSurfaceNormal);
        Assert.Equal(original.CoefficientOfRestitution, decoded.CoefficientOfRestitution);
    }
}
