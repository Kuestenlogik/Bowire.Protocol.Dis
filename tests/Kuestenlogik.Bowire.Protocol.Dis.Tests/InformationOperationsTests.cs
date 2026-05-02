// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.InformationOperations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class InformationOperationsTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV7(1, pduType, DisProtocolFamily.InformationOperations, length);

    [Fact]
    public void IoAction_RoundTrip_PreservesAllFixedFields()
    {
        var record = new StandardVariableRecord(
            RecordType: 0x2001,
            Content: [0xA0, 0xB0, 0xC0, 0xD0]);
        var original = new InformationOperationsActionPdu(
            HeaderFor(DisPduType.InformationOperationsAction, 0),
            OriginatingEntityId: new EntityId(1, 1, 100),
            ReceivingEntityId: new EntityId(1, 1, 200),
            RequestId: 42,
            IoWarfareType: 1,
            IoSimulationSource: 0,
            IoActionType: 5,
            IoActionPhase: 2,
            IoActionParameter1: 0xDEAD_BEEF,
            IoActionParameter2: 0xCAFE_BABE,
            IoAttackerId: new EntityId(1, 1, 500),
            IoPrimaryTargetId: new EntityId(2, 2, 900),
            IoRecordSets: [record]);

        var bytes = original.Marshal();
        Assert.Equal(7, bytes[0]); // V7
        Assert.Equal((byte)DisPduType.InformationOperationsAction, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.InformationOperations, bytes[3]);

        var decoded = InformationOperationsActionPdu.Unmarshal(bytes);
        Assert.Equal(42u, decoded.RequestId);
        Assert.Equal(1, decoded.IoWarfareType);
        Assert.Equal(5, decoded.IoActionType);
        Assert.Equal(0xDEAD_BEEFu, decoded.IoActionParameter1);
        Assert.Equal(0xCAFE_BABEu, decoded.IoActionParameter2);
        Assert.Equal(original.IoAttackerId, decoded.IoAttackerId);
        Assert.Equal(original.IoPrimaryTargetId, decoded.IoPrimaryTargetId);
        Assert.Single(decoded.IoRecordSets);
        Assert.Equal(record.RecordType, decoded.IoRecordSets[0].RecordType);
        Assert.Equal(record.Content, decoded.IoRecordSets[0].Content);
    }

    [Fact]
    public void IoReport_RoundTrip_PreservesAllFixedFields()
    {
        var record = new StandardVariableRecord(
            RecordType: 0x3000,
            Content: [0x11, 0x22, 0x33, 0x44]);
        var original = new InformationOperationsReportPdu(
            HeaderFor(DisPduType.InformationOperationsReport, 0),
            OriginatingEntityId: new EntityId(1, 1, 100),
            IoSimulationSource: 2,
            IoReportType: 3,
            IoAttackerId: new EntityId(1, 1, 500),
            IoPrimaryTargetId: new EntityId(2, 2, 900),
            IoRecordSets: [record]);

        var bytes = original.Marshal();
        Assert.Equal(7, bytes[0]);
        Assert.Equal((byte)DisPduType.InformationOperationsReport, bytes[2]);

        var decoded = InformationOperationsReportPdu.Unmarshal(bytes);
        Assert.Equal(2, decoded.IoSimulationSource);
        Assert.Equal(3, decoded.IoReportType);
        Assert.Equal(original.IoAttackerId, decoded.IoAttackerId);
        Assert.Equal(original.IoPrimaryTargetId, decoded.IoPrimaryTargetId);
        Assert.Single(decoded.IoRecordSets);
        Assert.Equal(record.RecordType, decoded.IoRecordSets[0].RecordType);
        Assert.Equal(record.Content, decoded.IoRecordSets[0].Content);
    }
}
