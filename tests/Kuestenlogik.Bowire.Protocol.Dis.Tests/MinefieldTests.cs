// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.Minefield;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class MinefieldTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.Minefield, length);

    [Fact]
    public void MinefieldState_RoundTrip_WithTypedPerimeterAndMineTypes()
    {
        var perimeter = new[]
        {
            new Vector2Float(0f, 0f),
            new Vector2Float(100f, 0f),
            new Vector2Float(100f, 100f),
            new Vector2Float(0f, 100f),
        };
        var mineTypes = new[]
        {
            new EntityType(8, 1, 225, 1, 1, 0, 0), // anti-tank mine
            new EntityType(8, 1, 225, 1, 2, 0, 0), // anti-personnel mine
        };
        var original = new MinefieldStatePdu(
            HeaderFor(DisPduType.MinefieldState, 0),
            MinefieldId: new EntityId(1, 1, 5000),
            MinefieldSequence: 7,
            ForceId: ForceId.Opposing,
            MinefieldType: new EntityType(8, 1, 225, 1, 1, 0, 0), // mine
            MinefieldLocation: new Vector3Double(3765000.0, 661000.0, 5108000.0),
            MinefieldOrientation: EulerAngles.Zero,
            AppearanceCode: 0,
            ProtocolMode: 0,
            PerimeterPoints: perimeter,
            MineTypes: mineTypes);

        var bytes = original.Marshal();
        Assert.Equal((byte)DisPduType.MinefieldState, bytes[2]);

        var decoded = MinefieldStatePdu.Unmarshal(bytes);
        Assert.Equal(original.MinefieldId, decoded.MinefieldId);
        Assert.Equal(7, decoded.MinefieldSequence);
        Assert.Equal(ForceId.Opposing, decoded.ForceId);
        Assert.Equal(4, decoded.PerimeterPoints.Count);
        Assert.Equal(2, decoded.MineTypes.Count);
        Assert.Equal(perimeter, decoded.PerimeterPoints);
        Assert.Equal(mineTypes, decoded.MineTypes);
    }

    [Fact]
    public void MinefieldQuery_RoundTrip_WithTypedPerimeterAndSensors()
    {
        var perimeter = new[] { new Vector2Float(5f, 10f), new Vector2Float(15f, 20f) };
        var sensors = new ushort[] { 1, 2, 3 };
        var original = new MinefieldQueryPdu(
            HeaderFor(DisPduType.MinefieldQuery, 0),
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(2, 3),
            RequestId: 5,
            DataFilter: 0x0000_00FF,
            RequestedMineType: new EntityType(8, 1, 225, 1, 1, 0, 0),
            PerimeterPoints: perimeter,
            SensorTypes: sensors);

        var decoded = MinefieldQueryPdu.Unmarshal(original.Marshal());
        Assert.Equal(new SimulationAddress(2, 3), decoded.RequestingSimulationId);
        Assert.Equal(5, decoded.RequestId);
        Assert.Equal(0x0000_00FFu, decoded.DataFilter);
        Assert.Equal(perimeter, decoded.PerimeterPoints);
        Assert.Equal(sensors, decoded.SensorTypes);
    }

    [Fact]
    public void MinefieldData_RoundTrip_WithTypedMineLocationsAndSensorTypes()
    {
        var mines = new[]
        {
            new Vector3Float(1f, 2f, 0f),
            new Vector3Float(3f, 4f, 0f),
            new Vector3Float(5f, 6f, 0f),
        };
        var sensors = new ushort[] { 100, 200 };
        var optional = new byte[] { 0xA0, 0xB0, 0xC0, 0xD0 };
        var original = new MinefieldDataPdu(
            HeaderFor(DisPduType.MinefieldData, 0),
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(1, 1),
            MinefieldSequenceNumber: 42,
            RequestId: 5,
            PduSequenceNumber: 1,
            NumberOfPdus: 3,
            DataFilter: 0x0000_0003,
            MineType: new EntityType(8, 1, 225, 1, 1, 0, 0),
            MineLocations: mines,
            SensorTypes: sensors,
            OptionalFieldsBlob: optional);

        var decoded = MinefieldDataPdu.Unmarshal(original.Marshal());
        Assert.Equal(42, decoded.MinefieldSequenceNumber);
        Assert.Equal(1, decoded.PduSequenceNumber);
        Assert.Equal(3, decoded.NumberOfPdus);
        Assert.Equal(3, decoded.MineLocations.Count);
        Assert.Equal(mines, decoded.MineLocations);
        Assert.Equal(0x0000_0003u, decoded.DataFilter);
        Assert.Equal(sensors, decoded.SensorTypes);
        Assert.Equal(optional, decoded.OptionalFieldsBlob);
    }

    [Fact]
    public void MinefieldResponseNack_RoundTrip_PreservesMissingList()
    {
        var missing = new byte[] { 5, 7, 9 };
        var original = new MinefieldResponseNackPdu(
            HeaderFor(DisPduType.MinefieldResponseNack, 0),
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(1, 1),
            RequestId: 3,
            NumberOfMissingPdus: 3,
            MissingPduSequenceNumbers: missing);

        var bytes = original.Marshal();
        Assert.Equal(MinefieldResponseNackPdu.MinimumWireLength + missing.Length, bytes.Length);

        var decoded = MinefieldResponseNackPdu.Unmarshal(bytes);
        Assert.Equal(3, decoded.RequestId);
        Assert.Equal(3, decoded.NumberOfMissingPdus);
        Assert.Equal(missing, decoded.MissingPduSequenceNumbers);
    }

    [Fact]
    public void MinefieldResponseNack_AndCollisionElastic_SharePduTypeId_40_DisambiguatedByFamily()
    {
        // Both PDUs have id 40 but different family bytes. Verify the
        // Minefield variant writes family 8 (Minefield), not family 1
        // (Entity Information) that CollisionElastic uses.
        var original = new MinefieldResponseNackPdu(
            HeaderFor(DisPduType.MinefieldResponseNack, MinefieldResponseNackPdu.MinimumWireLength),
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(1, 1),
            RequestId: 0,
            NumberOfMissingPdus: 0,
            MissingPduSequenceNumbers: []);

        var bytes = original.Marshal();
        Assert.Equal(40, bytes[2]);                                        // PDU type
        Assert.Equal((byte)DisProtocolFamily.Minefield, bytes[3]);         // family disambiguator
    }
}
