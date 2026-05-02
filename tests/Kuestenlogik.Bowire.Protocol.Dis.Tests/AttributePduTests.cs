// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class AttributePduTests
{
    private static AttributePdu Build(IReadOnlyList<AttributeRecordSet>? sets = null) => new(
        Header: PduHeader.ForV7(1, DisPduType.Attribute,
            DisProtocolFamily.EntityInformation, AttributePdu.MinimumWireLength),
        OriginatingSimulationAddress: new SimulationAddress(1, 1),
        AttributeRecordPduType: (byte)DisPduType.EntityState,
        AttributeRecordProtocolVersion: 7,
        MasterAttributeRecordType: 0,
        ActionCode: 1,
        AttributeRecordSets: sets ?? []);

    [Fact]
    public void Marshal_EmptyRecordSets_Is36Bytes()
    {
        Assert.Equal(AttributePdu.MinimumWireLength, Build().Marshal().Length);
    }

    [Fact]
    public void Marshal_HeaderBytesIdentifyAttributePduAndV7()
    {
        var bytes = Build().Marshal();
        Assert.Equal(7, bytes[0]);
        Assert.Equal((byte)DisPduType.Attribute, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.EntityInformation, bytes[3]);
    }

    [Fact]
    public void Roundtrip_WithTypedRecordSets_PreservesFieldsAndPayload()
    {
        var recordA = new StandardVariableRecord(RecordType: 0x1001,
            Content: [0x10, 0x20, 0x30, 0x40]);
        var recordB = new StandardVariableRecord(RecordType: 0x1002,
            Content: [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04]);
        var set = new AttributeRecordSet(
            RecordEntityId: new EntityId(1, 1, 42),
            AttributeRecords: [recordA, recordB]);
        var original = Build([set]);

        var bytes = original.Marshal();
        var decoded = AttributePdu.Unmarshal(bytes);

        Assert.Equal(original.OriginatingSimulationAddress, decoded.OriginatingSimulationAddress);
        Assert.Equal(original.AttributeRecordPduType, decoded.AttributeRecordPduType);
        Assert.Equal(original.AttributeRecordProtocolVersion, decoded.AttributeRecordProtocolVersion);
        Assert.Equal(original.MasterAttributeRecordType, decoded.MasterAttributeRecordType);
        Assert.Equal(original.ActionCode, decoded.ActionCode);
        Assert.Single(decoded.AttributeRecordSets);
        var decodedSet = decoded.AttributeRecordSets[0];
        Assert.Equal(set.RecordEntityId, decodedSet.RecordEntityId);
        Assert.Equal(2, decodedSet.AttributeRecords.Count);
        Assert.Equal(recordA.RecordType, decodedSet.AttributeRecords[0].RecordType);
        Assert.Equal(recordA.Content, decodedSet.AttributeRecords[0].Content);
        Assert.Equal(recordB.RecordType, decodedSet.AttributeRecords[1].RecordType);
        Assert.Equal(recordB.Content, decodedSet.AttributeRecords[1].Content);
    }
}
