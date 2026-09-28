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
    public void MinefieldData_Sensor_Types_Come_Before_The_Mine_Locations()
    {
        // A round trip cannot show this: it read its own order back. Until
        // the fix the sensor types went to the end, and a Data PDU from any
        // other implementation decoded its sensor types as a mine location.
        // KDIS and open-dis both put them right after the mine type.
        var pdu = new MinefieldDataPdu(
            PduHeader.ForV7(1, DisPduType.MinefieldData, DisProtocolFamily.Minefield, 0),
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(1, 1),
            MinefieldSequenceNumber: 1, RequestId: 1, PduSequenceNumber: 1, NumberOfPdus: 1,
            DataFilter: 0,
            MineType: new EntityType(8, 1, 225, 1, 1, 0, 0),
            MineLocations: [new Vector3Float(1f, 2f, 3f)],
            SensorTypes: [0x0102],
            OptionalFieldsBlob: []);

        var bytes = pdu.Marshal();
        var fixedEnd = MinefieldDataPdu.MinimumWireLength;
        Assert.Equal(0x01, bytes[fixedEnd]);
        Assert.Equal(0x02, bytes[fixedEnd + 1]);
        // 42 + 2 is on a 32-bit boundary: the mine location follows directly.
        Assert.Equal(1f, System.Buffers.Binary.BinaryPrimitives.ReadSingleBigEndian(bytes.AsSpan(fixedEnd + 2)));
        Assert.Equal(fixedEnd + 2 + 12, bytes.Length);
    }

    [Theory]
    [InlineData(6, 0)]
    [InlineData(6, 1)]
    [InlineData(6, 2)]
    [InlineData(6, 3)]
    [InlineData(7, 0)]
    [InlineData(7, 1)]
    [InlineData(7, 2)]
    [InlineData(7, 3)]
    public void MinefieldData_Mine_Locations_Start_On_A_32_Bit_Boundary(int version, int sensorCount)
    {
        // V6 and V7 differ in the fixed part (44 vs 42 bytes, #58), so the
        // padding after the sensor types differs too; the boundary does not.
        var sensors = Enumerable.Range(1, sensorCount).Select(i => (ushort)i).ToArray();
        var header = version == 7
            ? PduHeader.ForV7(1, DisPduType.MinefieldData, DisProtocolFamily.Minefield, 0)
            : HeaderFor(DisPduType.MinefieldData, 0);
        var pdu = new MinefieldDataPdu(
            header,
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(1, 1),
            MinefieldSequenceNumber: 1, RequestId: 1, PduSequenceNumber: 1, NumberOfPdus: 1,
            DataFilter: 0,
            MineType: new EntityType(8, 1, 225, 1, 1, 0, 0),
            MineLocations: [new Vector3Float(7f, 8f, 9f)],
            SensorTypes: sensors,
            OptionalFieldsBlob: [0xAA, 0xBB, 0xCC, 0xDD]);

        var bytes = pdu.Marshal();
        var locationAt = bytes.Length - 4 - 12;
        Assert.Equal(0, locationAt % 4);
        Assert.Equal(7f, System.Buffers.Binary.BinaryPrimitives.ReadSingleBigEndian(bytes.AsSpan(locationAt)));

        var decoded = MinefieldDataPdu.Unmarshal(bytes);
        Assert.Equal(sensors, decoded.SensorTypes);
        Assert.Equal([new Vector3Float(7f, 8f, 9f)], decoded.MineLocations);
        Assert.Equal([0xAA, 0xBB, 0xCC, 0xDD], decoded.OptionalFieldsBlob);
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
        Assert.Equal(original.FixedWireLength + missing.Length, bytes.Length);

        var decoded = MinefieldResponseNackPdu.Unmarshal(bytes);
        Assert.Equal(3, decoded.RequestId);
        Assert.Equal(3, decoded.NumberOfMissingPdus);
        Assert.Equal(missing, decoded.MissingPduSequenceNumbers);
    }

    [Fact]
    public void MinefieldResponseNack_Writes_Type_40_In_The_Minefield_Family()
    {
        // This test used to claim Collision-Elastic shared id 40; it is 66 (#57).
        var original = new MinefieldResponseNackPdu(
            HeaderFor(DisPduType.MinefieldResponseNack, 0),
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(1, 1),
            RequestId: 0,
            NumberOfMissingPdus: 0,
            MissingPduSequenceNumbers: []);

        var bytes = original.Marshal();
        Assert.Equal(40, bytes[2]);                                        // PDU type
        Assert.Equal((byte)DisProtocolFamily.Minefield, bytes[3]);         // family disambiguator
    }

    // ---- #58: the requester field changed size in 2012 ----

    private static MinefieldQueryPdu Query(PduHeader header) => new(
        header,
        MinefieldId: new EntityId(1, 1, 5000),
        RequestingSimulationId: new SimulationAddress(0x0A0B, 0x0C0D),
        RequestId: 0x77,
        DataFilter: 0,
        RequestedMineType: new EntityType(8, 1, 225, 1, 1, 0, 0),
        PerimeterPoints: [],
        SensorTypes: [])
    { RequestingEntity = 0x0E0F };

    [Fact]
    public void A_V6_Query_Carries_A_Six_Byte_Requesting_Entity()
    {
        // KDIS and open-dis dis6 read site, application, entity here.
        var bytes = Query(HeaderFor(DisPduType.MinefieldQuery, 0)).Marshal();
        Assert.Equal([0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F], bytes[18..24]);
        Assert.Equal(0x77, bytes[24]);
        Assert.Equal(40, bytes.Length);

        var decoded = MinefieldQueryPdu.Unmarshal(bytes);
        Assert.Equal(0x77, decoded.RequestId);
        Assert.Equal(0x0E0F, decoded.RequestingEntity);
    }

    [Fact]
    public void A_V7_Query_Carries_A_Four_Byte_Simulation_Id()
    {
        var bytes = Query(PduHeader.ForV7(1, DisPduType.MinefieldQuery, DisProtocolFamily.Minefield, 0)).Marshal();
        Assert.Equal([0x0A, 0x0B, 0x0C, 0x0D], bytes[18..22]);
        Assert.Equal(0x77, bytes[22]);
        Assert.Equal(MinefieldQueryPdu.MinimumWireLength, bytes.Length);
        // A 2012 requester is a simulation: there is no entity number to keep.
        Assert.Equal(0, MinefieldQueryPdu.Unmarshal(bytes).RequestingEntity);
    }

    [Fact]
    public void A_Nack_Lists_The_Missing_Sequence_Numbers_Right_After_Their_Count()
    {
        // The plugin had two padding bytes here that no other implementation has.
        var nack = new MinefieldResponseNackPdu(
            PduHeader.ForV7(1, DisPduType.MinefieldResponseNack, DisProtocolFamily.Minefield, 0),
            MinefieldId: new EntityId(1, 1, 5000),
            RequestingSimulationId: new SimulationAddress(1, 1),
            RequestId: 3,
            NumberOfMissingPdus: 2,
            MissingPduSequenceNumbers: [5, 9]);

        var bytes = nack.Marshal();
        Assert.Equal(2, bytes[23]);
        Assert.Equal([5, 9], bytes[24..26]);
        Assert.Equal(26, bytes.Length);
    }
}
