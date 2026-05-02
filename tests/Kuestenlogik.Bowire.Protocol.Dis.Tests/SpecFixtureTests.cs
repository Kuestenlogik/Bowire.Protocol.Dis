// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Byte-exact wire fixture tests. Hand-computed expected byte
/// sequences from IEEE 1278.1 / SISO-REF-010 field tables pin the
/// encoder's output against the spec's authoritative layout. Any
/// silent field shift shows up as a byte-level diff rather than
/// a structural test that happens to still pass.
/// </summary>
/// <remarks>
/// Each fixture documents its derivation inline — which spec
/// section the layout comes from and how the numeric values map to
/// bytes. The hand-computed constants make the tests auditable
/// against the standard without a second decoder.
/// </remarks>
public sealed class SpecFixtureTests
{
    [Fact]
    public void PduHeader_V6_EntityStateEntityInfo_Length144_ProducesExactTwelveBytes()
    {
        // IEEE 1278.1 §5.2.29: PDU header layout.
        //   Offset 0: Protocol Version = 6 (IEEE 1278.1A-1998)
        //   Offset 1: Exercise ID = 1
        //   Offset 2: PDU Type = 1 (Entity State)
        //   Offset 3: Protocol Family = 1 (Entity Information)
        //   Offset 4: Timestamp = 0x0102_0304
        //   Offset 8: Length = 144 = 0x0090
        //   Offset 10: Padding = 0x0000
        var expected = new byte[]
        {
            0x06,                   // protocol version
            0x01,                   // exercise id
            0x01,                   // PDU type
            0x01,                   // protocol family
            0x01, 0x02, 0x03, 0x04, // timestamp (big-endian)
            0x00, 0x90,             // length 144
            0x00, 0x00,             // padding
        };

        var header = new PduHeader(
            DisProtocolVersion.Ieee1278_1A_1998,
            ExerciseId: 1,
            PduType: DisPduType.EntityState,
            ProtocolFamily: DisProtocolFamily.EntityInformation,
            Timestamp: 0x0102_0304,
            Length: 144,
            Padding: 0);

        var buffer = new byte[PduHeader.WireLength];
        var w = new Wire.DisWireWriter(buffer);
        header.Marshal(ref w);

        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void EntityId_Site0102_App0304_Entity0506_ProducesExactSixBytes()
    {
        // §5.2.14 — EntityId is three uint16 big-endian fields.
        var entityId = new EntityId(Site: 0x0102, Application: 0x0304, Entity: 0x0506);

        var buffer = new byte[EntityId.WireLength];
        var w = new Wire.DisWireWriter(buffer);
        entityId.Marshal(ref w);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }, buffer);
    }

    [Fact]
    public void EntityType_PlatformLandUSATank_ProducesExactEightBytes()
    {
        // §5.2.16 — Entity Type seven-tuple.
        //   Kind = 1 (Platform)
        //   Domain = 1 (Land)
        //   Country = 225 (USA, 0x00E1) big-endian
        //   Category = 1 (Tank)
        //   Subcategory = 1 (M1)
        //   Specific = 0
        //   Extra = 0
        var entityType = new EntityType(1, 1, 225, 1, 1, 0, 0);

        var buffer = new byte[EntityType.WireLength];
        var w = new Wire.DisWireWriter(buffer);
        entityType.Marshal(ref w);

        Assert.Equal(
            new byte[] { 0x01, 0x01, 0x00, 0xE1, 0x01, 0x01, 0x00, 0x00 },
            buffer);
    }

    [Fact]
    public void FixedDatum_Id100_Value200_ProducesExactEightBytes()
    {
        // §5.2.33 — Fixed Datum record: two uint32 big-endian.
        var datum = new FixedDatum(DatumId: 100, DatumValue: 200);

        var buffer = new byte[FixedDatum.WireLength];
        var w = new Wire.DisWireWriter(buffer);
        datum.Marshal(ref w);

        Assert.Equal(
            new byte[]
            {
                0x00, 0x00, 0x00, 0x64, // id = 100
                0x00, 0x00, 0x00, 0xC8, // value = 200
            },
            buffer);
    }

    [Fact]
    public void Vector3Double_BigEndian_ProducesIEEE754Bytes()
    {
        // §5.2.32 — 3x double big-endian. IEEE 754 for 1.0 is
        // 0x3FF0_0000_0000_0000.
        var vector = new Vector3Double(1.0, 2.0, 0.5);

        var buffer = new byte[Vector3Double.WireLength];
        var w = new Wire.DisWireWriter(buffer);
        vector.Marshal(ref w);

        Assert.Equal(0x3FF0_0000_0000_0000UL, BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(0, 8)));
        Assert.Equal(0x4000_0000_0000_0000UL, BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(8, 8)));
        Assert.Equal(0x3FE0_0000_0000_0000UL, BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(16, 8)));
    }

    [Fact]
    public void MunitionDescriptor_HE_ProducesExactSixteenBytes()
    {
        // §5.2.19 — EntityType (8) + Warhead (2) + Fuse (2) + Qty (2) + Rate (2).
        //   MunitionType = Munition/Land/USA/HE
        //   Warhead = 1000 = 0x03E8 (HE)
        //   Fuse = 1000 = 0x03E8 (Point detonating)
        //   Quantity = 1
        //   Rate = 0
        var descriptor = new MunitionDescriptor(
            MunitionType: new EntityType(2, 2, 225, 2, 1, 0, 0),
            Warhead: 1000,
            Fuse: 1000,
            Quantity: 1,
            Rate: 0);

        var buffer = new byte[MunitionDescriptor.WireLength];
        var w = new Wire.DisWireWriter(buffer);
        descriptor.Marshal(ref w);

        Assert.Equal(
            new byte[]
            {
                // MunitionType: Kind=2 Domain=2 Country=0x00E1 Cat=2 Sub=1 Spec=0 Extra=0
                0x02, 0x02, 0x00, 0xE1, 0x02, 0x01, 0x00, 0x00,
                // Warhead = 1000
                0x03, 0xE8,
                // Fuse = 1000
                0x03, 0xE8,
                // Quantity = 1
                0x00, 0x01,
                // Rate = 0
                0x00, 0x00,
            },
            buffer);
    }

    [Fact]
    public void EntityState_MinimalPdu_WireLengthFieldIs144()
    {
        // End-to-end: the PDU's Length field (bytes 8-9) must encode
        // 144 = 0x0090 for a minimal Entity State (no variable params).
        var pdu = new EntityStatePdu(
            Header: PduHeader.ForV6(1, DisPduType.EntityState,
                DisProtocolFamily.EntityInformation, EntityStatePdu.MinimumWireLength),
            EntityId: new EntityId(1, 1, 1000),
            Force: ForceId.Friendly,
            EntityType: new EntityType(1, 1, 225, 1, 1, 0, 0),
            AlternativeEntityType: new EntityType(1, 1, 225, 1, 1, 0, 0),
            LinearVelocity: Vector3Float.Zero,
            Location: Vector3Double.Zero,
            Orientation: EulerAngles.Zero,
            Appearance: 0,
            DeadReckoning: DeadReckoningParameters.Default,
            Marking: EntityMarking.Ascii("TEST"),
            Capabilities: 0);

        var bytes = pdu.Marshal();
        Assert.Equal(144, bytes.Length);
        Assert.Equal(0x00, bytes[8]);
        Assert.Equal(0x90, bytes[9]);
    }

    [Fact]
    public void FirePdu_LocationAtOffset40_MunitionDescriptorAtOffset64()
    {
        // Pin Fire PDU field positions against §5.3.4.1 Table.
        var pdu = new FirePdu(
            Header: PduHeader.ForV6(1, DisPduType.Fire, DisProtocolFamily.Warfare, FirePdu.WireLength),
            FiringEntityId: new EntityId(1, 1, 100),
            TargetEntityId: new EntityId(1, 1, 200),
            MunitionId: new EntityId(1, 1, 9001),
            EventId: new EventId(1, 1, 42),
            FireMissionIndex: 0,
            LocationInWorldCoordinates: new Vector3Double(1.0, 2.0, 3.0),
            MunitionDescriptor: new MunitionDescriptor(
                MunitionType: new EntityType(2, 2, 225, 2, 1, 0, 0),
                Warhead: 1000, Fuse: 1000, Quantity: 1, Rate: 0),
            Velocity: Vector3Float.Zero,
            Range: 2500f);

        var bytes = pdu.Marshal();
        // Location ECEF X at offset 40 as IEEE 754 double BE of 1.0.
        Assert.Equal(0x3FF0_0000_0000_0000UL,
            BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(40, 8)));
        // MunitionDescriptor starts at offset 64 — first byte is
        // munition-type Kind = 2.
        Assert.Equal(0x02, bytes[64]);
        // Warhead = 1000 at offset 72.
        Assert.Equal(1000, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(72, 2)));
    }

    [Fact]
    public void DisProtocolVersion_Enum_ValuesMatchSpecWireCodes()
    {
        // SISO-REF-010 §3.3 — Protocol Version codes. Pin the enum
        // values so a refactor can't silently renumber them.
        Assert.Equal(0, (int)DisProtocolVersion.Other);
        Assert.Equal(1, (int)DisProtocolVersion.Version1May92);
        Assert.Equal(5, (int)DisProtocolVersion.Ieee1278_1_1995);
        Assert.Equal(6, (int)DisProtocolVersion.Ieee1278_1A_1998); // colloquial "DIS v6"
        Assert.Equal(7, (int)DisProtocolVersion.Ieee1278_1_2012); // colloquial "DIS v7"
    }

    [Fact]
    public void DisPduType_Enum_ValuesMatchSpecWireCodes()
    {
        // IEEE 1278.1-2012 Table 5 — PDU type codes. Pin the enum
        // values against the on-wire ids.
        Assert.Equal(1, (int)DisPduType.EntityState);
        Assert.Equal(2, (int)DisPduType.Fire);
        Assert.Equal(3, (int)DisPduType.Detonation);
        Assert.Equal(4, (int)DisPduType.Collision);
        Assert.Equal(15, (int)DisPduType.Acknowledge);
        Assert.Equal(20, (int)DisPduType.Data);
        Assert.Equal(23, (int)DisPduType.ElectromagneticEmission);
        Assert.Equal(25, (int)DisPduType.Transmitter);
        Assert.Equal(26, (int)DisPduType.Signal);
        Assert.Equal(40, (int)DisPduType.CollisionElastic); // family 1 — disambiguated from id 40 family 8
        Assert.Equal(40, (int)DisPduType.MinefieldResponseNack); // family 8
        Assert.Equal(66, (int)DisPduType.TimeSpacePositionInformation);
        Assert.Equal(67, (int)DisPduType.EntityStateUpdate);
        Assert.Equal(68, (int)DisPduType.DirectedEnergyFire);
        Assert.Equal(71, (int)DisPduType.Attribute);
        Assert.Equal(82, (int)DisPduType.InformationOperationsReport);
    }
}
