// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.SyntheticEnvironment;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class SyntheticEnvironmentTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.SyntheticEnvironment, length);

    [Fact]
    public void EnvironmentalProcess_RoundTrip_WithTypedEnvironmentRecords()
    {
        var recordA = new StandardVariableRecord(
            RecordType: 0x0F_01_02_00, // IEEE 1278.1 environment record type
            Content: [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
        var recordB = new StandardVariableRecord(
            RecordType: 0x0F_01_02_01,
            Content: [0xAA, 0xBB]);
        var original = new EnvironmentalProcessPdu(
            HeaderFor(DisPduType.EnvironmentalProcess, 0),
            EnvironmentalProcessId: new EntityId(1, 1, 7000),
            EnvironmentType: new EntityType(4, 1, 225, 1, 1, 0, 0),
            ModelType: 1,
            EnvironmentStatus: 0b0000_0001,
            SequenceNumber: 42,
            EnvironmentRecords: [recordA, recordB]);

        var bytes = original.Marshal();
        Assert.Equal((byte)DisPduType.EnvironmentalProcess, bytes[2]);

        var decoded = EnvironmentalProcessPdu.Unmarshal(bytes);
        Assert.Equal(original.EnvironmentalProcessId, decoded.EnvironmentalProcessId);
        Assert.Equal(original.EnvironmentType, decoded.EnvironmentType);
        Assert.Equal(42, decoded.SequenceNumber);
        Assert.Equal(2, decoded.EnvironmentRecords.Count);
        Assert.Equal(recordA.RecordType, decoded.EnvironmentRecords[0].RecordType);
        Assert.Equal(recordA.Content, decoded.EnvironmentRecords[0].Content);
        Assert.Equal(recordB.RecordType, decoded.EnvironmentRecords[1].RecordType);
        Assert.Equal(recordB.Content, decoded.EnvironmentRecords[1].Content);
    }

    [Fact]
    public void GriddedData_RoundTrip_WithTypedAxes()
    {
        var axisX = new GridAxisDescriptor(
            DomainInitialXi: 0.0,
            DomainFinalXi: 1000.0,
            DomainPointsXi: 10,
            InterleafFactor: 0,
            AxisType: 0,
            DataRepresentation: 1, // 32-bit float
            Values: [0x3F, 0x80, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00]); // 1.0f, 2.0f
        var axisY = new GridAxisDescriptor(
            DomainInitialXi: 0.0,
            DomainFinalXi: 500.0,
            DomainPointsXi: 5,
            InterleafFactor: 0,
            AxisType: 1,
            DataRepresentation: 0,
            Values: []);

        var original = new GriddedDataPdu(
            HeaderFor(DisPduType.GriddedData, 0),
            EnvironmentalSimulationApplicationId: new SimulationAddress(1, 1),
            FieldNumber: 1,
            PduNumber: 1,
            PduTotal: 3,
            CoordinateSystem: 0,
            ConstantGrid: 0,
            EnvironmentType: new EntityType(4, 1, 225, 1, 1, 0, 0),
            Orientation: EulerAngles.Zero,
            SampleTime: 0x0102_0304_0506_0708UL,
            TotalValues: 1000,
            VectorDimension: 1,
            Axes: [axisX, axisY]);

        var decoded = GriddedDataPdu.Unmarshal(original.Marshal());
        Assert.Equal(original.EnvironmentalSimulationApplicationId, decoded.EnvironmentalSimulationApplicationId);
        Assert.Equal(2, decoded.Axes.Count);
        Assert.Equal(1000u, decoded.TotalValues);
        Assert.Equal(0x0102_0304_0506_0708UL, decoded.SampleTime);
        Assert.Equal(axisX.DomainFinalXi, decoded.Axes[0].DomainFinalXi);
        Assert.Equal(axisX.DomainPointsXi, decoded.Axes[0].DomainPointsXi);
        Assert.Equal(axisX.DataRepresentation, decoded.Axes[0].DataRepresentation);
        Assert.Equal(axisX.Values, decoded.Axes[0].Values);
        Assert.Equal(axisY.AxisType, decoded.Axes[1].AxisType);
        Assert.Empty(decoded.Axes[1].Values);
    }

    [Fact]
    public void PointObjectState_RoundTrip()
    {
        var original = new PointObjectStatePdu(
            HeaderFor(DisPduType.PointObjectState, PointObjectStatePdu.WireLength),
            ObjectId: new EntityId(1, 1, 8000),
            ReferencedObjectId: new EntityId(0, 0, 0),
            UpdateNumber: 1,
            ForceId: ForceId.Neutral,
            Modifications: 0,
            ObjectType: new ObjectType(Domain: 1, ObjectKind: 1, Category: 1, Subcategory: 0),
            ObjectLocation: new Vector3Double(3765000.0, 661000.0, 5108000.0),
            ObjectOrientation: EulerAngles.Zero,
            SpecificObjectAppearance: 0x1234_5678,
            GeneralObjectAppearance: 0x00FF,
            RequesterId: new SimulationAddress(1, 1),
            ReceivingId: new SimulationAddress(2, 2));

        var bytes = original.Marshal();
        Assert.Equal(PointObjectStatePdu.WireLength, bytes.Length);

        var decoded = PointObjectStatePdu.Unmarshal(bytes);
        Assert.Equal(original.ObjectId, decoded.ObjectId);
        Assert.Equal(original.ObjectType, decoded.ObjectType);
        Assert.Equal(original.ObjectLocation, decoded.ObjectLocation);
        Assert.Equal(0x1234_5678u, decoded.SpecificObjectAppearance);
        Assert.Equal(0x00FF, decoded.GeneralObjectAppearance);
    }

    [Fact]
    public void LinearObjectState_RoundTrip_WithTypedSegment()
    {
        var segment = new LinearSegmentParameter(
            SegmentNumber: 1,
            SegmentModifications: 0b11,
            GeneralSegmentAppearance: 0x00A5,
            SpecificSegmentAppearance: 0xDEAD_BEEF,
            SegmentLocation: new Vector3Double(3765000.0, 661000.0, 5108000.0),
            SegmentOrientation: new EulerAngles(0.1f, 0.2f, 0.3f),
            SegmentLength: 250f,
            SegmentWidth: 0.5f,
            SegmentHeight: 2f,
            SegmentDepth: 0f);

        var original = new LinearObjectStatePdu(
            HeaderFor(DisPduType.LinearObjectState, 0),
            ObjectId: new EntityId(1, 1, 8100),
            ReferencedObjectId: new EntityId(0, 0, 0),
            UpdateNumber: 1,
            ForceId: ForceId.Friendly,
            RequesterId: new SimulationAddress(1, 1),
            ReceivingId: new SimulationAddress(2, 2),
            ObjectType: new ObjectType(1, 1, 2, 0),
            Segments: new[] { segment });

        var bytes = original.Marshal();
        Assert.Equal(LinearObjectStatePdu.MinimumWireLength + LinearSegmentParameter.WireLength, bytes.Length);

        var decoded = LinearObjectStatePdu.Unmarshal(bytes);
        Assert.Single(decoded.Segments!);
        Assert.Equal(segment, decoded.Segments![0]);
    }

    [Fact]
    public void ArealObjectState_RoundTrip_WithTypedPoints()
    {
        var points = new[]
        {
            new Vector3Double(3765000.0, 661000.0, 5108000.0),
            new Vector3Double(3765100.0, 661000.0, 5108000.0),
            new Vector3Double(3765100.0, 661100.0, 5108000.0),
            new Vector3Double(3765000.0, 661100.0, 5108000.0),
        };
        var original = new ArealObjectStatePdu(
            HeaderFor(DisPduType.ArealObjectState, 0),
            ObjectId: new EntityId(1, 1, 8200),
            ReferencedObjectId: new EntityId(0, 0, 0),
            UpdateNumber: 1,
            ForceId: ForceId.Opposing,
            Modifications: 0,
            ObjectType: new ObjectType(1, 1, 3, 0),
            SpecificObjectAppearance: 0,
            GeneralObjectAppearance: 0,
            RequesterId: new SimulationAddress(1, 1),
            ReceivingId: new SimulationAddress(2, 2),
            Points: points);

        var bytes = original.Marshal();
        Assert.Equal(
            ArealObjectStatePdu.MinimumWireLength + (points.Length * Vector3Double.WireLength),
            bytes.Length);

        var decoded = ArealObjectStatePdu.Unmarshal(bytes);
        Assert.Equal(4, decoded.Points!.Count);
        Assert.Equal(points, decoded.Points);
    }
}
