// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagementReliability;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class SimulationManagementReliabilityTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.SimulationManagementWithReliability, length);

    private static EntityId Orig() => new(1, 1, 100);
    private static EntityId Recv() => new(1, 1, 200);

    [Fact]
    public void CreateEntityR_RoundTrip_PreservesReliabilityAndRequestId()
    {
        var original = new CreateEntityRPdu(
            HeaderFor(DisPduType.CreateEntityR, CreateEntityRPdu.WireLength),
            Orig(), Recv(), RequiredReliabilityService.Acknowledged, RequestId: 7);
        var bytes = original.Marshal();
        Assert.Equal(CreateEntityRPdu.WireLength, bytes.Length);
        Assert.Equal((byte)DisPduType.CreateEntityR, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.SimulationManagementWithReliability, bytes[3]);

        var decoded = CreateEntityRPdu.Unmarshal(bytes);
        Assert.Equal(RequiredReliabilityService.Acknowledged, decoded.Reliability);
        Assert.Equal(7u, decoded.RequestId);
    }

    [Fact]
    public void RemoveEntityR_RoundTrip()
    {
        var original = new RemoveEntityRPdu(
            HeaderFor(DisPduType.RemoveEntityR, RemoveEntityRPdu.WireLength),
            Orig(), Recv(), RequiredReliabilityService.Unacknowledged, RequestId: 42);
        var decoded = RemoveEntityRPdu.Unmarshal(original.Marshal());
        Assert.Equal(RequiredReliabilityService.Unacknowledged, decoded.Reliability);
        Assert.Equal(42u, decoded.RequestId);
    }

    [Fact]
    public void StartResumeR_RoundTrip_PreservesBothClocksAndReliability()
    {
        var original = new StartResumeRPdu(
            HeaderFor(DisPduType.StartResumeR, StartResumeRPdu.WireLength),
            Orig(), Recv(),
            RealWorldTime: new ClockTime(12345, 100),
            SimulationTime: new ClockTime(0, 1),
            Reliability: RequiredReliabilityService.Acknowledged,
            RequestId: 7);

        var decoded = StartResumeRPdu.Unmarshal(original.Marshal());
        Assert.Equal(original.RealWorldTime, decoded.RealWorldTime);
        Assert.Equal(original.SimulationTime, decoded.SimulationTime);
        Assert.Equal(original.Reliability, decoded.Reliability);
    }

    [Fact]
    public void StopFreezeR_RoundTrip()
    {
        var original = new StopFreezeRPdu(
            HeaderFor(DisPduType.StopFreezeR, StopFreezeRPdu.WireLength),
            Orig(), Recv(),
            new ClockTime(12345, 0),
            StopFreezeReason.StopForReset,
            FrozenBehavior: 0b0000_0011,
            Reliability: RequiredReliabilityService.Acknowledged,
            RequestId: 99);
        var decoded = StopFreezeRPdu.Unmarshal(original.Marshal());
        Assert.Equal(StopFreezeReason.StopForReset, decoded.Reason);
        Assert.Equal(0b0000_0011, decoded.FrozenBehavior);
        Assert.Equal(RequiredReliabilityService.Acknowledged, decoded.Reliability);
    }

    [Fact]
    public void AcknowledgeR_RoundTrip_NoReliabilityField()
    {
        var original = new AcknowledgeRPdu(
            HeaderFor(DisPduType.AcknowledgeR, AcknowledgeRPdu.WireLength),
            Orig(), Recv(),
            AcknowledgeFlag.StartResume, ResponseFlag.AbleToComply, RequestId: 5);

        var decoded = AcknowledgeRPdu.Unmarshal(original.Marshal());
        Assert.Equal(AcknowledgeFlag.StartResume, decoded.AcknowledgeFlag);
        Assert.Equal(ResponseFlag.AbleToComply, decoded.ResponseFlag);
    }

    [Fact]
    public void ActionRequestR_WithDatums_RoundTrips()
    {
        var fixedDatums = new[] { new FixedDatum(1, 100) };
        var variableDatums = new[] { new VariableDatum(10, 16, new byte[] { 0xAA, 0xBB }) };
        var original = new ActionRequestRPdu(
            HeaderFor(DisPduType.ActionRequestR, 0),
            Orig(), Recv(),
            RequiredReliabilityService.Acknowledged,
            RequestId: 1, ActionId: 42,
            FixedDatums: fixedDatums, VariableDatums: variableDatums);

        var decoded = ActionRequestRPdu.Unmarshal(original.Marshal());
        Assert.Equal(42u, decoded.ActionId);
        Assert.Single(decoded.FixedDatums!);
        Assert.Single(decoded.VariableDatums!);
    }

    [Fact]
    public void ActionResponseR_RoundTrips()
    {
        var original = new ActionResponseRPdu(
            HeaderFor(DisPduType.ActionResponseR, ActionResponseRPdu.MinimumWireLength),
            Orig(), Recv(), RequestId: 1, RequestStatus: 2);
        var decoded = ActionResponseRPdu.Unmarshal(original.Marshal());
        Assert.Equal(2u, decoded.RequestStatus);
    }

    [Fact]
    public void DataQueryR_RoundTrip_PreservesDatumIds()
    {
        var original = new DataQueryRPdu(
            HeaderFor(DisPduType.DataQueryR, 0),
            Orig(), Recv(),
            RequiredReliabilityService.Unacknowledged,
            RequestId: 7, TimeInterval: 200,
            FixedDatumIds: new uint[] { 11, 22 },
            VariableDatumIds: new uint[] { 500 });

        var decoded = DataQueryRPdu.Unmarshal(original.Marshal());
        Assert.Equal(new uint[] { 11, 22 }, decoded.FixedDatumIds);
        Assert.Equal(new uint[] { 500 }, decoded.VariableDatumIds);
    }

    [Fact]
    public void SetDataR_RoundTrips_WithFixedDatum()
    {
        var original = new SetDataRPdu(
            HeaderFor(DisPduType.SetDataR, 0),
            Orig(), Recv(),
            RequiredReliabilityService.Acknowledged,
            RequestId: 1,
            FixedDatums: new[] { new FixedDatum(0xDEAD, 0xBEEF) });
        var decoded = SetDataRPdu.Unmarshal(original.Marshal());
        Assert.Single(decoded.FixedDatums!);
        Assert.Equal(0xBEEFu, decoded.FixedDatums![0].DatumValue);
    }

    [Fact]
    public void DataR_RoundTrips_WithVariableDatum()
    {
        var datum = new VariableDatum(0x42, 64, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var original = new DataRPdu(
            HeaderFor(DisPduType.DataR, 0),
            Orig(), Recv(),
            RequiredReliabilityService.Unacknowledged,
            RequestId: 1,
            VariableDatums: new[] { datum });
        var decoded = DataRPdu.Unmarshal(original.Marshal());
        Assert.Equal(datum.Value, decoded.VariableDatums![0].Value);
    }

    [Fact]
    public void EventReportR_RoundTrip_PreservesEventType()
    {
        var original = new EventReportRPdu(
            HeaderFor(DisPduType.EventReportR, EventReportRPdu.MinimumWireLength),
            Orig(), Recv(), EventType: 15);
        var decoded = EventReportRPdu.Unmarshal(original.Marshal());
        Assert.Equal(15u, decoded.EventType);
    }

    [Fact]
    public void CommentR_RoundTrip()
    {
        var text = System.Text.Encoding.ASCII.GetBytes("Reliable comment");
        var datum = new VariableDatum(1, (uint)(text.Length * 8), text);
        var original = new CommentRPdu(
            HeaderFor(DisPduType.CommentR, 0),
            Orig(), Recv(),
            VariableDatums: new[] { datum });
        var decoded = CommentRPdu.Unmarshal(original.Marshal());
        Assert.Single(decoded.VariableDatums!);
    }

    [Fact]
    public void RecordR_RoundTrip_WithTypedRecordSet()
    {
        var records = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var recordSet = new RecordSet(
            RecordId: 0x1000,
            RecordSetSerialNumber: 42,
            RecordLengthBits: 64,
            RecordCount: 1,
            Records: records);

        var original = new RecordRPdu(
            HeaderFor(DisPduType.RecordR, 0),
            Orig(), Recv(),
            RequestId: 1,
            Reliability: RequiredReliabilityService.Acknowledged,
            EventType: 42,
            ResponseSerialNumber: 7,
            RecordSets: new[] { recordSet });

        var decoded = RecordRPdu.Unmarshal(original.Marshal());
        Assert.Equal(42, decoded.EventType);
        Assert.Equal(7u, decoded.ResponseSerialNumber);
        Assert.Single(decoded.RecordSets);
        Assert.Equal(0x1000u, decoded.RecordSets[0].RecordId);
        Assert.Equal(42u, decoded.RecordSets[0].RecordSetSerialNumber);
        Assert.Equal(records, decoded.RecordSets[0].Records);
    }

    [Fact]
    public void SetRecordR_RoundTrip_WithTypedRecordSet()
    {
        var records = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var recordSet = new RecordSet(
            RecordId: 0x2000,
            RecordSetSerialNumber: 99,
            RecordLengthBits: 32,
            RecordCount: 1,
            Records: records);

        var original = new SetRecordRPdu(
            HeaderFor(DisPduType.SetRecordR, 0),
            Orig(), Recv(),
            RequestId: 3,
            Reliability: RequiredReliabilityService.Unacknowledged,
            RecordSets: new[] { recordSet });

        var decoded = SetRecordRPdu.Unmarshal(original.Marshal());
        Assert.Single(decoded.RecordSets);
        Assert.Equal(0x2000u, decoded.RecordSets[0].RecordId);
        Assert.Equal(records, decoded.RecordSets[0].Records);
    }

    [Fact]
    public void RecordQueryR_RoundTrip_PreservesRecordIds()
    {
        var original = new RecordQueryRPdu(
            HeaderFor(DisPduType.RecordQueryR, 0),
            Orig(), Recv(),
            RequestId: 1,
            Reliability: RequiredReliabilityService.Acknowledged,
            EventType: 7,
            TimeInterval: 1000,
            RecordIds: new uint[] { 100, 200, 300 });
        var decoded = RecordQueryRPdu.Unmarshal(original.Marshal());
        Assert.Equal(new uint[] { 100, 200, 300 }, decoded.RecordIds);
    }
}
