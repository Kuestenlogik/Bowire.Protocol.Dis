// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class FirePduTests
{
    private static FirePdu Build() => new(
        Header: PduHeader.ForV6(1, DisPduType.Fire, DisProtocolFamily.Warfare, FirePdu.WireLength),
        FiringEntityId: new EntityId(1, 1, 100),
        TargetEntityId: new EntityId(1, 1, 200),
        MunitionId: new EntityId(1, 1, 9001),
        EventId: new EventId(1, 1, 42),
        FireMissionIndex: 0,
        LocationInWorldCoordinates: new Vector3Double(3765000.0, 661000.0, 5108000.0),
        MunitionDescriptor: new MunitionDescriptor(
            MunitionType: new EntityType(2, 2, 225, 2, 1, 0, 0),
            Warhead: 1000,
            Fuse: 1000,
            Quantity: 1,
            Rate: 0),
        Velocity: new Vector3Float(100f, 0f, 0f),
        Range: 2500f);

    [Fact]
    public void Marshal_IsExactly96Bytes() =>
        Assert.Equal(FirePdu.WireLength, Build().Marshal().Length);

    [Fact]
    public void Marshal_HeaderBytesIdentifyFire()
    {
        var bytes = Build().Marshal();
        Assert.Equal((byte)DisPduType.Fire, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.Warfare, bytes[3]);
        Assert.Equal(96, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2)));
    }

    [Fact]
    public void Marshal_LocationLandsAtOffset40_DoubleBigEndian()
    {
        var bytes = Build().Marshal();
        Assert.Equal(3765000.0, BinaryPrimitives.ReadDoubleBigEndian(bytes.AsSpan(40, 8)));
        Assert.Equal(661000.0, BinaryPrimitives.ReadDoubleBigEndian(bytes.AsSpan(48, 8)));
    }

    [Fact]
    public void Roundtrip_PreservesEveryField()
    {
        var original = Build();
        var decoded = FirePdu.Unmarshal(original.Marshal());
        Assert.Equal(original.FiringEntityId, decoded.FiringEntityId);
        Assert.Equal(original.TargetEntityId, decoded.TargetEntityId);
        Assert.Equal(original.MunitionId, decoded.MunitionId);
        Assert.Equal(original.EventId, decoded.EventId);
        Assert.Equal(original.FireMissionIndex, decoded.FireMissionIndex);
        Assert.Equal(original.LocationInWorldCoordinates, decoded.LocationInWorldCoordinates);
        Assert.Equal(original.MunitionDescriptor, decoded.MunitionDescriptor);
        Assert.Equal(original.Velocity, decoded.Velocity);
        Assert.Equal(original.Range, decoded.Range);
    }
}
