// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class CollisionPduTests
{
    private static CollisionPdu Build() => new(
        Header: PduHeader.ForV6(1, DisPduType.Collision, DisProtocolFamily.EntityInformation, CollisionPdu.WireLength),
        IssuingEntityId: new EntityId(1, 1, 100),
        CollidingEntityId: new EntityId(1, 1, 200),
        EventId: new EventId(1, 1, 7),
        CollisionType: CollisionType.Elastic,
        Velocity: new Vector3Float(5f, 0f, 0f),
        Mass: 1250.5f,
        Location: new Vector3Float(0.5f, -0.25f, 0f));

    [Fact]
    public void Marshal_IsExactly60Bytes()
    {
        Assert.Equal(CollisionPdu.WireLength, Build().Marshal().Length);
    }

    [Fact]
    public void Marshal_HeaderBytesIdentifyCollision()
    {
        var bytes = Build().Marshal();
        Assert.Equal((byte)DisPduType.Collision, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.EntityInformation, bytes[3]);
        Assert.Equal(60, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2)));
    }

    [Fact]
    public void Marshal_CollisionTypeLandsAtOffset30()
    {
        var bytes = Build().Marshal();
        Assert.Equal((byte)CollisionType.Elastic, bytes[30]);
    }

    [Fact]
    public void Roundtrip_PreservesAllFields()
    {
        var original = Build();
        var decoded = CollisionPdu.Unmarshal(original.Marshal());
        Assert.Equal(original.IssuingEntityId, decoded.IssuingEntityId);
        Assert.Equal(original.CollidingEntityId, decoded.CollidingEntityId);
        Assert.Equal(original.EventId, decoded.EventId);
        Assert.Equal(original.CollisionType, decoded.CollisionType);
        Assert.Equal(original.Velocity, decoded.Velocity);
        Assert.Equal(original.Mass, decoded.Mass);
        Assert.Equal(original.Location, decoded.Location);
    }
}
