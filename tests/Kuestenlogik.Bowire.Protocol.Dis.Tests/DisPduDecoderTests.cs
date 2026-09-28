// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Every PDU through its typed record on the stream (#22), and on an entity
/// stream when it names the entity in any role (#23).
/// </summary>
/// <remarks>
/// Before this, only EntityState was decoded on the stream path. Everything
/// else reached the workbench as a header plus a base64 blob, and an entity
/// subscription dropped it — a Fire from the filtered tank never appeared in
/// that tank's feed.
/// </remarks>
public sealed class DisPduDecoderTests
{
    private static readonly EntityId Tank = new(1, 1, 100);
    private static readonly EntityId Target = new(1, 1, 200);
    private static readonly EntityId Bystander = new(1, 1, 999);

    private static FirePdu Fire() => new(
        Header: PduHeader.ForV6(1, DisPduType.Fire, DisProtocolFamily.Warfare, FirePdu.WireLength),
        FiringEntityId: Tank,
        TargetEntityId: Target,
        MunitionId: new EntityId(1, 1, 9001),
        EventId: new EventId(1, 1, 42),
        FireMissionIndex: 0,
        LocationInWorldCoordinates: new Vector3Double(1.0, 2.0, 3.0),
        MunitionDescriptor: new MunitionDescriptor(
            MunitionType: new EntityType(2, 2, 225, 2, 1, 0, 0),
            Warhead: 1000, Fuse: 1000, Quantity: 1, Rate: 0),
        Velocity: Vector3Float.Zero,
        Range: 2500f);

    private static CollisionPdu Collision() => new(
        Header: PduHeader.ForV6(1, DisPduType.Collision, DisProtocolFamily.EntityInformation, CollisionPdu.WireLength),
        IssuingEntityId: Target,
        CollidingEntityId: Tank,
        EventId: new EventId(1, 1, 7),
        CollisionType: CollisionType.Elastic,
        Velocity: new Vector3Float(5f, 0f, 0f),
        Mass: 1250.5f,
        Location: new Vector3Float(0.5f, -0.25f, 0f));

    private static CollisionElasticPdu CollisionElastic() => new(
        Header: PduHeader.ForV7(1, DisPduType.CollisionElastic, DisProtocolFamily.EntityInformation, CollisionElasticPdu.WireLength),
        IssuingEntityId: Tank,
        CollidingEntityId: Target,
        EventId: new EventId(1, 1, 42),
        ContactVelocity: new Vector3Float(12.5f, 0f, 0f),
        Mass: 8000f,
        LocationOfImpact: new Vector3Float(1f, 0f, 0.5f),
        IntermediateResultXX: 1f, IntermediateResultXY: 0f, IntermediateResultXZ: 0.1f,
        IntermediateResultYY: 2f, IntermediateResultYZ: -0.05f, IntermediateResultZZ: 1.5f,
        UnitSurfaceNormal: new Vector3Float(1f, 0f, 0f),
        CoefficientOfRestitution: 0.8f);

    // ---- the table ----

    [Fact]
    public void Every_Pdu_Type_Has_A_Decoder()
    {
        // The table is generated from the records; this holds it to the enum.
        // A PDU type added without a decoder shows up here, not as a blob on a
        // live stream.
        var missing = Enum.GetValues<DisPduType>()
            .Where(t => t != DisPduType.Other && !DisPduDecoder.Decoders.ContainsKey(t))
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Every_Record_With_An_Unmarshal_Is_Reachable_From_The_Table()
    {
        // The other direction: a record type nobody routes to would be the same
        // silent blob, just one layer further in.
        var records = typeof(FirePdu).Assembly.GetTypes()
            .Where(t => t.Name.EndsWith("Pdu", StringComparison.Ordinal) && t != typeof(PduHeader))
            .Where(t => t.GetMethod("Unmarshal", BindingFlags.Public | BindingFlags.Static,
                [typeof(ReadOnlySpan<byte>)]) is { } m && m.ReturnType == t)
            .ToList();
        Assert.Equal(records.Count, DisPduDecoder.Decoders.Count);
    }

    [Fact]
    public void A_Collision_Elastic_Decodes_As_Itself_Not_As_A_Tspi()
    {
        // #57 — on the old ids a Collision-Elastic's type byte was a TSPI's.
        var decoded = DisPduDecoder.TryDecode(CollisionElastic().Marshal());
        Assert.IsType<CollisionElasticPdu>(decoded);
    }

    [Fact]
    public void A_Truncated_Pdu_Is_No_Record_Rather_Than_An_Exception()
    {
        var bytes = Fire().Marshal();
        Assert.Null(DisPduDecoder.TryDecode(bytes[..20]));
    }

    // ---- related entities ----

    [Fact]
    public void A_Fire_Names_The_Firer_The_Target_And_The_Munition_In_That_Order()
    {
        var related = DisPduDecoder.RelatedEntities(Fire());
        Assert.Equal([Tank, Target, new EntityId(1, 1, 9001)], related);
    }

    [Fact]
    public void The_All_Zero_Id_Means_No_Entity_And_Is_Left_Out()
    {
        var fire = Fire() with { TargetEntityId = new EntityId(0, 0, 0) };
        Assert.DoesNotContain(new EntityId(0, 0, 0), DisPduDecoder.RelatedEntities(fire));
    }

    // ---- the stream envelope ----

    [Fact]
    public void On_The_Tanks_Stream_Its_Fire_Now_Shows()
    {
        // #23 — this returned null for every PDU that was not EntityState.
        var envelope = BowireDisProtocol.TryBuildEnvelope(Fire().Marshal(), filter: Tank);
        Assert.NotNull(envelope);
    }

    [Fact]
    public void On_The_Targets_Stream_The_Same_Fire_Shows_Too()
    {
        Assert.NotNull(BowireDisProtocol.TryBuildEnvelope(Fire().Marshal(), filter: Target));
    }

    [Fact]
    public void A_Collision_Shows_On_The_Stream_Of_Either_Party()
    {
        var bytes = Collision().Marshal();
        Assert.NotNull(BowireDisProtocol.TryBuildEnvelope(bytes, filter: Tank));
        Assert.NotNull(BowireDisProtocol.TryBuildEnvelope(bytes, filter: Target));
    }

    [Fact]
    public void An_Entity_The_Pdu_Does_Not_Name_Does_Not_Get_It()
    {
        Assert.Null(BowireDisProtocol.TryBuildEnvelope(Fire().Marshal(), filter: Bystander));
    }

    [Fact]
    public void The_Envelope_Carries_The_Typed_Fields_And_Keeps_The_Raw_Bytes()
    {
        // #22 — structured data instead of only a blob; raw stays, so nothing
        // is lost when a typed view is not available.
        using var doc = JsonDocument.Parse(BowireDisProtocol.TryBuildEnvelope(Fire().Marshal(), filter: null)!);
        var root = doc.RootElement;

        Assert.Equal("1:1:100", root.GetProperty("entityId").GetString());
        Assert.Equal(["1:1:100", "1:1:200", "1:1:9001"],
            root.GetProperty("relatedEntityIds").EnumerateArray().Select(e => e.GetString()!).ToArray());

        var pdu = root.GetProperty("pdu");
        Assert.Equal(200, pdu.GetProperty("targetEntityId").GetProperty("entity").GetInt32());
        Assert.Equal(2500f, pdu.GetProperty("range").GetSingle());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("raw").GetString()));
    }

    [Fact]
    public void A_Pdu_That_Does_Not_Decode_Still_Reaches_An_Unfiltered_Stream_As_Raw()
    {
        // Malformed traffic on an exercise network is ordinary; the unfiltered
        // stream shows it as it always did, just without a typed view.
        var bytes = Fire().Marshal()[..40];
        // Keep the header's length consistent with what is actually there.
        bytes[8] = 0; bytes[9] = 40;
        using var doc = JsonDocument.Parse(BowireDisProtocol.TryBuildEnvelope(bytes, filter: null)!);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("pdu").ValueKind);
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("raw").GetString()));
    }
}
