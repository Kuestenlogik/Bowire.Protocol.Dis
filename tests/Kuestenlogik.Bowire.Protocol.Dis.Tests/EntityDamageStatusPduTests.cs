// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class EntityDamageStatusPduTests
{
    private static EntityDamageStatusPdu Build(IReadOnlyList<StandardVariableRecord>? records = null) => new(
        Header: PduHeader.ForV7(1, DisPduType.EntityDamageStatus,
            DisProtocolFamily.Warfare, EntityDamageStatusPdu.MinimumWireLength),
        DamagedEntityId: new EntityId(1, 1, 200),
        DamageDescriptionRecords: records ?? []);

    [Fact]
    public void Marshal_NoRecords_IsMinimumWireLength() =>
        Assert.Equal(EntityDamageStatusPdu.MinimumWireLength, Build().Marshal().Length);

    [Fact]
    public void Marshal_HeaderBytesIdentifyV7AndPduType()
    {
        var bytes = Build().Marshal();
        Assert.Equal(7, bytes[0]);
        Assert.Equal((byte)DisPduType.EntityDamageStatus, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.Warfare, bytes[3]);
    }

    [Fact]
    public void Roundtrip_WithTypedRecords_PreservesFieldsAndPayload()
    {
        var damageA = new StandardVariableRecord(
            RecordType: 4500, // directed-energy damage record id
            Content: new byte[32]);
        var damageB = new StandardVariableRecord(
            RecordType: 4501,
            Content: new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });

        var original = Build([damageA, damageB]);
        var bytes = original.Marshal();

        var decoded = EntityDamageStatusPdu.Unmarshal(bytes);
        Assert.Equal(original.DamagedEntityId, decoded.DamagedEntityId);
        Assert.Equal(2, decoded.DamageDescriptionRecords.Count);
        Assert.Equal(damageA.RecordType, decoded.DamageDescriptionRecords[0].RecordType);
        Assert.Equal(damageA.Content, decoded.DamageDescriptionRecords[0].Content);
        Assert.Equal(damageB.RecordType, decoded.DamageDescriptionRecords[1].RecordType);
        Assert.Equal(damageB.Content, decoded.DamageDescriptionRecords[1].Content);
    }
}
