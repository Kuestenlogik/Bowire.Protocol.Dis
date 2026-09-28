// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.LiveEntity;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// The flag-gated fields of the Live Entity PDUs, typed where they read
/// cleanly (#26).
/// </summary>
/// <remarks>
/// Layouts after KDIS, the one open implementation that decodes these PDUs.
/// The hand-built vectors are written from that layout, independently of the
/// encoder; the fixed-point numbers stay raw integers, since not even KDIS
/// knows their binary point for certain.
/// </remarks>
public sealed class LiveEntityFieldsTests
{
    private static readonly LiveEntityId Shooter = new(1, 2, 300);
    private static readonly EntityType Round = new(2, 2, 225, 2, 1, 0, 0);

    private static PduHeader HeaderFor(DisPduType pduType) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.LiveEntity, 0);

    // ---- TSPI ----

    [Fact]
    public void A_Tspi_With_No_Flag_Has_Only_Its_Location()
    {
        byte[] payload = [0x00, 0x00, 0x07, 0x00, 0x10, 0xFF, 0xF0, 0x00, 0x01];
        var fields = TspiView(payload);
        Assert.NotNull(fields);
        Assert.Equal(new LeRelativeWorldCoordinates(7, 16, -16, 1), fields!.Location);
        Assert.Null(fields.LinearVelocity);
        Assert.Null(fields.SystemSpecificData);
    }

    [Fact]
    public void A_Tspi_Flag_Adds_Exactly_Its_Field_In_Order()
    {
        // Flags: linear velocity (bit 0), orientation (bit 1), speed (bit 5).
        byte[] payload =
        [
            0b0010_0011,
            0x00, 0x01, 0x00, 0x02, 0x00, 0x03, 0x00, 0x04,   // location
            0x00, 0x0A, 0xFF, 0xF6, 0x00, 0x00,               // velocity 10, -10, 0
            0x01, 0x02, 0xFF,                                 // orientation 1, 2, -1
            0x01, 0x00,                                       // speed 256
        ];
        var fields = TspiView(payload)!;
        Assert.Equal(new LeVector16(10, -10, 0), fields.LinearVelocity);
        Assert.Equal(new LeEulerAngles(1, 2, -1), fields.Orientation);
        Assert.Equal((short)256, fields.MeasuredSpeed);
        Assert.Null(fields.PositionError);
    }

    [Fact]
    public void Tspi_Fields_Round_Trip_Through_The_Encoder()
    {
        var fields = new TspiFields(
            new LeRelativeWorldCoordinates(1, 2, 3, 4),
            LinearVelocity: new LeVector16(5, 6, 7),
            Orientation: new LeEulerAngles(8, 9, 10),
            PositionError: new LePositionError(11, 12),
            OrientationError: new LeOrientationError(13, 14, 15),
            DeadReckoning: new LeDeadReckoning(4, new LeVector8(1, 2, 3), new LeVector8(-1, -2, -3)),
            MeasuredSpeed: 99,
            SystemSpecificData: [0xAA, 0xBB]);
        var back = TspiView(LiveEntityFields.Encode(fields))!;
        Assert.Equal(fields with { SystemSpecificData = null }, back with { SystemSpecificData = null });
        Assert.Equal([0xAA, 0xBB], back.SystemSpecificData!);
    }

    private static TspiFields? TspiView(byte[] payload) =>
        TimeSpacePositionInformationPdu.Unmarshal(
            new TimeSpacePositionInformationPdu(HeaderFor(DisPduType.TimeSpacePositionInformation), Shooter, payload).Marshal()).Fields;

    // ---- Appearance ----

    [Fact]
    public void An_Appearance_Reads_The_Second_Flag_Octet_Only_When_The_First_Says_So()
    {
        // Flag 1: force id (bit 0) and flag 2 present (bit 7); flag 2: audio (bit 1).
        byte[] payload = [0x81, 0x02, 0x03, 0x12, 0x34, 0x56, 0x78];
        var fields = new AppearancePdu(HeaderFor(DisPduType.Appearance), Shooter, payload).Fields!;
        Assert.Equal((byte)3, fields.ForceId);
        Assert.Equal(0x12345678u, fields.AudioAppearance);
        Assert.Null(fields.VisualAppearance);
    }

    [Fact]
    public void Appearance_Fields_Round_Trip_Through_The_Encoder()
    {
        var fields = new LeAppearanceFields(
            ForceId: 1, EntityType: Round, AlternateEntityType: Round,
            Marking: EntityMarking.Ascii("LIVE-1"), Capabilities: 5,
            VisualAppearance: 6, InfraredAppearance: 7, ElectromagneticAppearance: 8, AudioAppearance: 9);
        var payload = LiveEntityFields.Encode(fields);
        var back = new AppearancePdu(HeaderFor(DisPduType.Appearance), Shooter, payload).Fields!;
        Assert.Equal(fields.Marking!.Value.Marking, back.Marking!.Value.Marking.TrimEnd('\0'));
        Assert.Equal(fields with { Marking = null }, back with { Marking = null });
    }

    // ---- Fire ----

    [Fact]
    public void A_Fire_Munition_Id_Without_Site_And_App_Takes_Them_From_The_Shooter()
    {
        // Flags: munition id present (bit 2), short; event id short (bit 3 clear).
        byte[] payload =
        [
            0x04,
            0x01, 0xF4,                                     // munition entity 500
            0x00, 0x2A,                                     // event number 42
            2, 2, 0x00, 0xE1, 2, 1, 0, 0,                   // munition type
            0x00, 0x01, 0x00, 0x02, 0x00, 0x03,             // velocity
            0x03, 0xE8,                                     // range 1000
        ];
        var fields = new LiveEntityFirePdu(HeaderFor(DisPduType.LiveEntityFire), Shooter, payload).Fields!;
        Assert.Equal(new LiveEntityId(1, 2, 500), fields.MunitionId);
        Assert.Equal(new LiveEntityId(1, 2, 42), fields.EventId);
        Assert.Null(fields.TargetId);
        Assert.Equal(Round, fields.Munition.MunitionType);
        Assert.Null(fields.Munition.Warhead);
        Assert.Equal((ushort)1000, fields.Range);
    }

    [Fact]
    public void Fire_Fields_Round_Trip_Through_The_Encoder()
    {
        var fields = new LeFireFields(
            TargetId: new LiveEntityId(9, 9, 900),
            MunitionId: new LiveEntityId(3, 3, 33),           // another site: sent in full
            EventId: new LiveEntityId(1, 2, 7),               // the shooter's: sent short
            Location: new LeRelativeWorldCoordinates(1, 2, 3, 4),
            Munition: new LeMunitionDescriptor(Round, 1000, 2000, 1, 60),
            Velocity: new LeVector16(1, 2, 3),
            Range: 1500);
        var payload = LiveEntityFields.Encode(fields, Shooter);
        Assert.Equal(fields, new LiveEntityFirePdu(HeaderFor(DisPduType.LiveEntityFire), Shooter, payload).Fields);
    }

    // ---- Detonation ----

    [Fact]
    public void A_Detonation_In_Entity_Coordinates_Puts_The_Location_After_The_Munition()
    {
        // Flag 1: target (bit 0), entity coordinates (bit 6); no flag 2.
        byte[] payload =
        [
            0x41,
            9, 9, 0x03, 0x84,                               // target 9:9:900
            0x00, 0x01, 0x00, 0x02, 0x00, 0x03,             // velocity
            2, 2, 0x00, 0xE1, 2, 1, 0, 0,                   // munition type
            0x00, 0x0A, 0x00, 0x0B, 0x00, 0x0C,             // location relative to the target
            5,                                              // result
        ];
        var fields = new LiveEntityDetonationPdu(HeaderFor(DisPduType.LiveEntityDetonation), Shooter, payload).Fields!;
        Assert.Equal(new LiveEntityId(9, 9, 900), fields.TargetId);
        Assert.Null(fields.WorldLocation);
        Assert.Equal(new LeVector16(10, 11, 12), fields.EntityLocation);
        Assert.Equal(5, fields.DetonationResult);
        Assert.Null(fields.EventId);
    }

    [Fact]
    public void Detonation_Fields_Round_Trip_Through_The_Encoder()
    {
        var fields = new LeDetonationFields(
            TargetId: null,
            MunitionId: new LiveEntityId(1, 2, 55),
            EventId: new LiveEntityId(4, 4, 8),
            WorldLocation: new LeRelativeWorldCoordinates(1, -2, 3, -4),
            Velocity: new LeVector16(1, 2, 3),
            MunitionOrientation: new LeEulerAngles(1, 1, 1),
            Munition: new LeMunitionDescriptor(Round, null, null, 3, 10),
            EntityLocation: null,
            DetonationResult: 1);
        var payload = LiveEntityFields.Encode(fields, Shooter);
        Assert.Equal(fields, new LiveEntityDetonationPdu(HeaderFor(DisPduType.LiveEntityDetonation), Shooter, payload).Fields);
    }

    // ---- the fallback ----

    [Fact]
    public void A_Payload_That_Runs_Short_Leaves_No_Typed_View_And_The_Pdu_Intact()
    {
        byte[] payload = [0x01, 0x00, 0x01];               // announces velocity, has not even a location
        var pdu = new TimeSpacePositionInformationPdu(HeaderFor(DisPduType.TimeSpacePositionInformation), Shooter, payload);
        Assert.Null(pdu.Fields);
        Assert.Equal(payload, TimeSpacePositionInformationPdu.Unmarshal(pdu.Marshal()).Payload);
    }

    [Fact]
    public void Bytes_Left_Over_Mean_No_Typed_View()
    {
        byte[] payload = [0x00, 0, 1, 0, 2, 0, 3, 0, 4, 0xEE];
        Assert.Null(new TimeSpacePositionInformationPdu(HeaderFor(DisPduType.TimeSpacePositionInformation), Shooter, payload).Fields);
    }

    [Theory]
    [InlineData(0x80)]                                      // TSPI bit 7 is reserved
    public void A_Reserved_Flag_Bit_Means_No_Typed_View(byte flags)
    {
        byte[] payload = [flags, 0, 1, 0, 2, 0, 3, 0, 4];
        Assert.Null(new TimeSpacePositionInformationPdu(HeaderFor(DisPduType.TimeSpacePositionInformation), Shooter, payload).Fields);
    }

    [Fact]
    public void An_Unknown_Second_Flag_Octet_Bit_Means_No_Typed_View()
    {
        byte[] payload = [0x80, 0x04];
        Assert.Null(new AppearancePdu(HeaderFor(DisPduType.Appearance), Shooter, payload).Fields);
    }

    [Fact]
    public void An_Empty_Payload_Is_No_Typed_View_Rather_Than_An_Exception()
    {
        Assert.Null(new LiveEntityFirePdu(HeaderFor(DisPduType.LiveEntityFire), Shooter, []).Fields);
        Assert.Null(new LiveEntityDetonationPdu(HeaderFor(DisPduType.LiveEntityDetonation), Shooter, []).Fields);
        Assert.Null(new AppearancePdu(HeaderFor(DisPduType.Appearance), Shooter, []).Fields);
    }

    // ---- on the stream ----

    [Fact]
    public void The_Stream_Envelope_Shows_The_Fields()
    {
        var payload = LiveEntityFields.Encode(new TspiFields(new LeRelativeWorldCoordinates(1, 2, 3, 4), MeasuredSpeed: 12));
        var bytes = new TimeSpacePositionInformationPdu(HeaderFor(DisPduType.TimeSpacePositionInformation), Shooter, payload).Marshal();
        using var doc = JsonDocument.Parse(BowireDisProtocol.TryBuildEnvelope(bytes, filter: null)!);
        var fields = doc.RootElement.GetProperty("pdu").GetProperty("fields");
        Assert.Equal(12, fields.GetProperty("measuredSpeed").GetInt32());
        Assert.Equal(4, fields.GetProperty("location").GetProperty("deltaZ").GetInt32());
    }
}
