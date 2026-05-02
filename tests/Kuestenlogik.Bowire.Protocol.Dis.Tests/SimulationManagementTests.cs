// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagement;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class SimulationManagementTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.SimulationManagement, length);

    private static EntityId Orig() => new(1, 1, 100);
    private static EntityId Recv() => new(1, 1, 200);

    [Fact]
    public void CreateEntity_RoundTrip()
    {
        var original = new CreateEntityPdu(
            HeaderFor(DisPduType.CreateEntity, CreateEntityPdu.WireLength),
            Orig(), Recv(), RequestId: 0x1234_5678);
        var bytes = original.Marshal();
        Assert.Equal(CreateEntityPdu.WireLength, bytes.Length);
        var decoded = CreateEntityPdu.Unmarshal(bytes);
        Assert.Equal(original.RequestId, decoded.RequestId);
        Assert.Equal((byte)DisPduType.CreateEntity, bytes[2]);
    }

    [Fact]
    public void RemoveEntity_RoundTrip()
    {
        var original = new RemoveEntityPdu(
            HeaderFor(DisPduType.RemoveEntity, RemoveEntityPdu.WireLength),
            Orig(), Recv(), RequestId: 42);
        var decoded = RemoveEntityPdu.Unmarshal(original.Marshal());
        Assert.Equal(original.RequestId, decoded.RequestId);
    }

    [Fact]
    public void StartResume_RoundTrip_PreservesBothClocks()
    {
        var original = new StartResumePdu(
            HeaderFor(DisPduType.StartResume, StartResumePdu.WireLength),
            Orig(), Recv(),
            RealWorldTime: new ClockTime(12345, 0x0102_0304),
            SimulationTime: new ClockTime(0, 0x0000_0001),
            RequestId: 7);

        var bytes = original.Marshal();
        Assert.Equal(StartResumePdu.WireLength, bytes.Length);

        var decoded = StartResumePdu.Unmarshal(bytes);
        Assert.Equal(original.RealWorldTime, decoded.RealWorldTime);
        Assert.Equal(original.SimulationTime, decoded.SimulationTime);
        Assert.Equal(original.RequestId, decoded.RequestId);
    }

    [Fact]
    public void StopFreeze_RoundTrip_PreservesReasonAndFrozenBehavior()
    {
        var original = new StopFreezePdu(
            HeaderFor(DisPduType.StopFreeze, StopFreezePdu.WireLength),
            Orig(), Recv(),
            RealWorldTime: new ClockTime(12345, 0),
            Reason: StopFreezeReason.Recess,
            FrozenBehavior: 0b0000_0101,
            RequestId: 99);

        var decoded = StopFreezePdu.Unmarshal(original.Marshal());
        Assert.Equal(original.Reason, decoded.Reason);
        Assert.Equal(original.FrozenBehavior, decoded.FrozenBehavior);
        Assert.Equal(original.RequestId, decoded.RequestId);
    }

    [Fact]
    public void Acknowledge_RoundTrip_PreservesFlags()
    {
        var original = new AcknowledgePdu(
            HeaderFor(DisPduType.Acknowledge, AcknowledgePdu.WireLength),
            Orig(), Recv(),
            AcknowledgeFlag: AcknowledgeFlag.StartResume,
            ResponseFlag: ResponseFlag.AbleToComply,
            RequestId: 5);

        var bytes = original.Marshal();
        Assert.Equal(AcknowledgePdu.WireLength, bytes.Length);
        var decoded = AcknowledgePdu.Unmarshal(bytes);
        Assert.Equal(AcknowledgeFlag.StartResume, decoded.AcknowledgeFlag);
        Assert.Equal(ResponseFlag.AbleToComply, decoded.ResponseFlag);
    }

    [Fact]
    public void ActionRequest_WithMixedDatums_RoundTrips()
    {
        var fixedDatums = new[] { new FixedDatum(1, 100), new FixedDatum(2, 200) };
        var variableDatums = new[] { new VariableDatum(10, 16, new byte[] { 0xAA, 0xBB }) };

        var original = new ActionRequestPdu(
            HeaderFor(DisPduType.ActionRequest, 0),
            Orig(), Recv(),
            RequestId: 1,
            ActionId: 42,
            FixedDatums: fixedDatums,
            VariableDatums: variableDatums);

        var bytes = original.Marshal();
        var decoded = ActionRequestPdu.Unmarshal(bytes);

        Assert.Equal(original.RequestId, decoded.RequestId);
        Assert.Equal(original.ActionId, decoded.ActionId);
        Assert.Equal(2, decoded.FixedDatums!.Count);
        Assert.Equal(100u, decoded.FixedDatums[0].DatumValue);
        Assert.Single(decoded.VariableDatums!);
        Assert.Equal(16u, decoded.VariableDatums![0].DatumLengthBits);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, decoded.VariableDatums[0].Value);
    }

    [Fact]
    public void ActionResponse_RoundTrips_WithoutDatums()
    {
        var original = new ActionResponsePdu(
            HeaderFor(DisPduType.ActionResponse, ActionResponsePdu.MinimumWireLength),
            Orig(), Recv(),
            RequestId: 1, RequestStatus: 2);
        var decoded = ActionResponsePdu.Unmarshal(original.Marshal());
        Assert.Equal(original.RequestStatus, decoded.RequestStatus);
    }

    [Fact]
    public void DataQuery_RoundTrip_PreservesDatumIds()
    {
        var original = new DataQueryPdu(
            HeaderFor(DisPduType.DataQuery, 0),
            Orig(), Recv(),
            RequestId: 7, TimeInterval: 100,
            FixedDatumIds: new uint[] { 11, 22, 33 },
            VariableDatumIds: new uint[] { 500 });

        var decoded = DataQueryPdu.Unmarshal(original.Marshal());
        Assert.Equal(new uint[] { 11, 22, 33 }, decoded.FixedDatumIds);
        Assert.Equal(new uint[] { 500 }, decoded.VariableDatumIds);
    }

    [Fact]
    public void SetData_RoundTrips_WithFixedDatum()
    {
        var original = new SetDataPdu(
            HeaderFor(DisPduType.SetData, 0),
            Orig(), Recv(),
            RequestId: 1,
            FixedDatums: new[] { new FixedDatum(0xDEAD, 0xBEEF) });

        var decoded = SetDataPdu.Unmarshal(original.Marshal());
        Assert.Single(decoded.FixedDatums!);
        Assert.Equal(0xBEEFu, decoded.FixedDatums![0].DatumValue);
    }

    [Fact]
    public void Data_RoundTrips_WithVariableDatum()
    {
        var variableDatum = new VariableDatum(0x4242, 64, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        var original = new DataPdu(
            HeaderFor(DisPduType.Data, 0),
            Orig(), Recv(),
            RequestId: 1,
            VariableDatums: new[] { variableDatum });

        var bytes = original.Marshal();
        // Variable datum with 8 bytes of value = 8-byte header + 8-byte value = 16 bytes total.
        Assert.Equal(DataPdu.MinimumWireLength + 16, bytes.Length);

        var decoded = DataPdu.Unmarshal(bytes);
        Assert.Equal(variableDatum.Value, decoded.VariableDatums![0].Value);
        Assert.Equal(variableDatum.DatumLengthBits, decoded.VariableDatums[0].DatumLengthBits);
    }

    [Fact]
    public void EventReport_RoundTrip_PreservesEventType()
    {
        var original = new EventReportPdu(
            HeaderFor(DisPduType.EventReport, EventReportPdu.MinimumWireLength),
            Orig(), Recv(),
            EventType: 15);
        var decoded = EventReportPdu.Unmarshal(original.Marshal());
        Assert.Equal(15u, decoded.EventType);
    }

    [Fact]
    public void Comment_RoundTrip_WithFreeformTextInVariableDatum()
    {
        var text = System.Text.Encoding.ASCII.GetBytes("Hello from BOWIRE");
        var datum = new VariableDatum(1, (uint)(text.Length * 8), text);
        var original = new CommentPdu(
            HeaderFor(DisPduType.Comment, 0),
            Orig(), Recv(),
            VariableDatums: new[] { datum });

        var decoded = CommentPdu.Unmarshal(original.Marshal());
        Assert.Single(decoded.VariableDatums!);
        Assert.Equal(text, decoded.VariableDatums![0].Value[..text.Length]);
    }

    [Fact]
    public void VariableDatum_Padding_RoundsToEightByteBoundary()
    {
        // Verify the 8-byte-boundary padding semantics through a
        // real PDU round-trip. A 5-byte value advertises 40 bits of
        // significance and occupies an 8-byte value slot on the wire
        // — 8 header bytes + 8 padded value bytes = 16 total.
        var datum = new VariableDatum(1, 40, new byte[] { 1, 2, 3, 4, 5 });
        Assert.Equal(16, datum.WireLength);

        var comment = new CommentPdu(
            HeaderFor(DisPduType.Comment, 0),
            Orig(), Recv(),
            VariableDatums: new[] { datum });

        var bytes = comment.Marshal();
        Assert.Equal(CommentPdu.MinimumWireLength + 16, bytes.Length);

        var decoded = CommentPdu.Unmarshal(bytes);
        Assert.Equal(40u, decoded.VariableDatums![0].DatumLengthBits);
        // Significant bytes come back intact (the Value array carries
        // the full 8-byte slot after round-trip, first 5 being the
        // original content and the rest zero padding).
        var value = decoded.VariableDatums[0].Value;
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, value[..5]);
    }
}
