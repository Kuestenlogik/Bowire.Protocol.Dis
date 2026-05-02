// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.Logistics;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class LogisticsTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.Logistics, length);

    private static SupplyQuantity[] SampleSupplies() => new[]
    {
        new SupplyQuantity(new EntityType(2, 2, 225, 2, 1, 0, 0), 30f),
        new SupplyQuantity(new EntityType(2, 2, 225, 2, 2, 0, 0), 100f),
    };

    [Fact]
    public void ServiceRequest_WithSupplies_RoundTrip()
    {
        var original = new ServiceRequestPdu(
            HeaderFor(DisPduType.ServiceRequest, 0),
            RequestingEntityId: new EntityId(1, 1, 100),
            ServicingEntityId: new EntityId(1, 1, 200),
            ServiceType: ServiceTypeRequested.Resupply,
            Supplies: SampleSupplies());

        var bytes = original.Marshal();
        Assert.Equal(ServiceRequestPdu.MinimumWireLength + (2 * SupplyQuantity.WireLength), bytes.Length);
        Assert.Equal((byte)DisPduType.ServiceRequest, bytes[2]);

        var decoded = ServiceRequestPdu.Unmarshal(bytes);
        Assert.Equal(ServiceTypeRequested.Resupply, decoded.ServiceType);
        Assert.Equal(2, decoded.Supplies!.Count);
        Assert.Equal(30f, decoded.Supplies[0].Quantity);
        Assert.Equal(100f, decoded.Supplies[1].Quantity);
    }

    [Fact]
    public void ResupplyOffer_RoundTrip()
    {
        var original = new ResupplyOfferPdu(
            HeaderFor(DisPduType.ResupplyOffer, 0),
            ReceivingEntityId: new EntityId(1, 1, 100),
            SupplyingEntityId: new EntityId(1, 1, 200),
            Supplies: SampleSupplies());
        var decoded = ResupplyOfferPdu.Unmarshal(original.Marshal());
        Assert.Equal(2, decoded.Supplies!.Count);
        Assert.Equal(30f, decoded.Supplies[0].Quantity);
    }

    [Fact]
    public void ResupplyReceived_RoundTrip_EmptySupplies()
    {
        var original = new ResupplyReceivedPdu(
            HeaderFor(DisPduType.ResupplyReceived, ResupplyReceivedPdu.MinimumWireLength),
            ReceivingEntityId: new EntityId(1, 1, 100),
            SupplyingEntityId: new EntityId(1, 1, 200));
        var bytes = original.Marshal();
        Assert.Equal(ResupplyReceivedPdu.MinimumWireLength, bytes.Length);
        var decoded = ResupplyReceivedPdu.Unmarshal(bytes);
        Assert.NotNull(decoded.Supplies);
        Assert.Empty(decoded.Supplies!);
    }

    [Fact]
    public void ResupplyCancel_RoundTrip()
    {
        var original = new ResupplyCancelPdu(
            HeaderFor(DisPduType.ResupplyCancel, ResupplyCancelPdu.WireLength),
            ReceivingEntityId: new EntityId(1, 1, 100),
            SupplyingEntityId: new EntityId(1, 1, 200));
        var bytes = original.Marshal();
        Assert.Equal(ResupplyCancelPdu.WireLength, bytes.Length);
        var decoded = ResupplyCancelPdu.Unmarshal(bytes);
        Assert.Equal(original.ReceivingEntityId, decoded.ReceivingEntityId);
        Assert.Equal(original.SupplyingEntityId, decoded.SupplyingEntityId);
    }

    [Fact]
    public void RepairComplete_RoundTrip_PreservesRepairCode()
    {
        var original = new RepairCompletePdu(
            HeaderFor(DisPduType.RepairComplete, RepairCompletePdu.WireLength),
            ReceivingEntityId: new EntityId(1, 1, 100),
            RepairingEntityId: new EntityId(1, 1, 200),
            Repair: RepairCode.AllMechanicalRepairsPerformed);
        var decoded = RepairCompletePdu.Unmarshal(original.Marshal());
        Assert.Equal(RepairCode.AllMechanicalRepairsPerformed, decoded.Repair);
    }

    [Fact]
    public void RepairResponse_RoundTrip_PreservesResult()
    {
        var original = new RepairResponsePdu(
            HeaderFor(DisPduType.RepairResponse, RepairResponsePdu.WireLength),
            ReceivingEntityId: new EntityId(1, 1, 100),
            RepairingEntityId: new EntityId(1, 1, 200),
            RepairResult: RepairResult.RepairEnded);
        var decoded = RepairResponsePdu.Unmarshal(original.Marshal());
        Assert.Equal(RepairResult.RepairEnded, decoded.RepairResult);
    }
}
