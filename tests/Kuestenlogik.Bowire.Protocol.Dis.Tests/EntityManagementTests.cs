// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.EntityManagement;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class EntityManagementTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.EntityManagement, length);

    [Fact]
    public void AggregateState_RoundTrip_WithTypedIdListsAndDatums()
    {
        var datum = new VariableDatum(
            DatumId: 500,
            DatumLengthBits: 32,
            Value: [0xDE, 0xAD, 0xBE, 0xEF]);

        var original = new AggregateStatePdu(
            HeaderFor(DisPduType.AggregateState, 0),
            AggregateId: new EntityId(1, 1, 9000),
            ForceId: ForceId.Friendly,
            AggregateState: AggregateState.Aggregated,
            AggregateType: new AggregateType(1, 1, 225, 1, 1, 0, 0),
            Formation: 3,
            AggregateMarking: AggregateMarking.Ascii("Alpha Company"),
            Dimensions: new Vector3Float(100f, 100f, 50f),
            Orientation: EulerAngles.Zero,
            CenterOfMass: new Vector3Double(3765000.0, 661000.0, 5108000.0),
            Velocity: new Vector3Float(10f, 0f, 0f),
            AggregateIds: [new EntityId(1, 1, 9100), new EntityId(1, 1, 9101)],
            EntityIds: [new EntityId(1, 1, 100), new EntityId(1, 1, 101), new EntityId(1, 1, 102)],
            SilentAggregateSystems: [new EntityType(1, 1, 225, 1, 1, 0, 0)],
            SilentEntitySystems: [new EntityType(1, 1, 225, 1, 1, 0, 0), new EntityType(1, 1, 225, 1, 2, 0, 0)],
            VariableDatums: [datum]);

        var bytes = original.Marshal();
        Assert.Equal((byte)DisPduType.AggregateState, bytes[2]);

        var decoded = AggregateStatePdu.Unmarshal(bytes);
        Assert.Equal(original.AggregateId, decoded.AggregateId);
        Assert.Equal(ForceId.Friendly, decoded.ForceId);
        Assert.Equal(AggregateState.Aggregated, decoded.AggregateState);
        Assert.Equal(original.AggregateType, decoded.AggregateType);
        Assert.Equal(original.AggregateMarking.Marking, decoded.AggregateMarking.Marking);
        Assert.Equal(original.CenterOfMass, decoded.CenterOfMass);
        Assert.Equal(2, decoded.AggregateIds.Count);
        Assert.Equal(3, decoded.EntityIds.Count);
        Assert.Single(decoded.SilentAggregateSystems);
        Assert.Equal(2, decoded.SilentEntitySystems.Count);
        Assert.Single(decoded.VariableDatums);
        Assert.Equal(500u, decoded.VariableDatums[0].DatumId);
        Assert.Equal(datum.Value, decoded.VariableDatums[0].Value);
    }

    [Fact]
    public void IsGroupOf_RoundTrip_WithTypedGedRecords()
    {
        // Basic Ground category → 8 bytes per record per §6.2.38.
        var gedA = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var gedB = new byte[] { 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        var gedC = new byte[] { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28 };
        var original = new IsGroupOfPdu(
            HeaderFor(DisPduType.IsGroupOf, 0),
            GroupEntityId: new EntityId(1, 1, 9001),
            GroupedEntityCategory: 1, // Basic Ground
            Latitude: 53.55,
            Longitude: 9.99,
            GroupedEntityDescriptions: [gedA, gedB, gedC]);

        var decoded = IsGroupOfPdu.Unmarshal(original.Marshal());
        Assert.Equal(original.GroupEntityId, decoded.GroupEntityId);
        Assert.Equal(1, decoded.GroupedEntityCategory);
        Assert.Equal(3, decoded.GroupedEntityDescriptions.Count);
        Assert.Equal(53.55, decoded.Latitude);
        Assert.Equal(9.99, decoded.Longitude);
        Assert.Equal(gedA, decoded.GroupedEntityDescriptions[0]);
        Assert.Equal(gedB, decoded.GroupedEntityDescriptions[1]);
        Assert.Equal(gedC, decoded.GroupedEntityDescriptions[2]);
    }

    [Fact]
    public void TransferOwnership_RoundTrip_WithRecordSetsBlob()
    {
        var blob = new byte[] { 0x11, 0x22 };
        var original = new TransferOwnershipPdu(
            HeaderFor(DisPduType.TransferOwnership, 0),
            OriginatingEntityId: new EntityId(1, 1, 100),
            ReceivingEntityId: new EntityId(1, 1, 200),
            RequestId: 42,
            Reliability: RequiredReliabilityService.Acknowledged,
            TransferType: TransferType.Controller,
            TransferEntityId: new EntityId(1, 1, 300),
            NumberOfRecordSets: 1,
            RecordSetsBlob: blob);

        var decoded = TransferOwnershipPdu.Unmarshal(original.Marshal());
        Assert.Equal(RequiredReliabilityService.Acknowledged, decoded.Reliability);
        Assert.Equal(TransferType.Controller, decoded.TransferType);
        Assert.Equal(original.TransferEntityId, decoded.TransferEntityId);
        Assert.Equal(1u, decoded.NumberOfRecordSets);
        Assert.Equal(blob, decoded.RecordSetsBlob);
    }

    [Fact]
    public void IsPartOf_RoundTrip_PreservesAllFields()
    {
        var original = new IsPartOfPdu(
            HeaderFor(DisPduType.IsPartOf, IsPartOfPdu.WireLength),
            OriginatingEntityId: new EntityId(1, 1, 100),
            ReceivingEntityId: new EntityId(1, 1, 200),
            RelationshipNature: 2, // Child-part
            RelationshipPosition: 1,
            PartLocation: new Vector3Float(0.5f, 0f, 1.5f),
            NamedLocationId: new NamedLocationId(StationName: 601, StationNumber: 3),
            PartEntityType: new EntityType(1, 1, 225, 1, 1, 0, 0));

        var bytes = original.Marshal();
        Assert.Equal(IsPartOfPdu.WireLength, bytes.Length);

        var decoded = IsPartOfPdu.Unmarshal(bytes);
        Assert.Equal(2, decoded.RelationshipNature);
        Assert.Equal(1, decoded.RelationshipPosition);
        Assert.Equal(original.PartLocation, decoded.PartLocation);
        Assert.Equal(original.NamedLocationId, decoded.NamedLocationId);
        Assert.Equal(original.PartEntityType, decoded.PartEntityType);
    }
}
