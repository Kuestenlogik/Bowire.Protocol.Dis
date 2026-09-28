// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using Kuestenlogik.Bowire.Mocking;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Replay-time changes to captured PDUs (#24).
/// </summary>
/// <remarks>
/// The emitter shipped captured bytes verbatim, which is right for a faithful
/// replay and a wall for everything else: re-using a capture in another
/// exercise meant editing base64 in the recording. The changes are opt-in —
/// the first test holds that a recording without the keys still replays byte
/// for byte.
/// </remarks>
public sealed class DisReplayRewriteTests
{
    private static readonly EntityId Tank = new(1, 1, 100);
    private static readonly EntityId Target = new(1, 1, 200);
    private static readonly DateTimeOffset HalfPast = new(2026, 9, 28, 14, 30, 0, TimeSpan.Zero);

    private static byte[] Fire(uint timestamp = 0) => new FirePdu(
        Header: PduHeader.ForV6(1, DisPduType.Fire, DisProtocolFamily.Warfare, FirePdu.WireLength) with { Timestamp = timestamp },
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
        Range: 2500f).Marshal();

    private static byte[] Collision() => new CollisionPdu(
        Header: PduHeader.ForV6(1, DisPduType.Collision, DisProtocolFamily.EntityInformation, CollisionPdu.WireLength),
        IssuingEntityId: new EntityId(2, 2, 2),
        CollidingEntityId: new EntityId(2, 2, 3),
        EventId: new EventId(1, 1, 7),
        CollisionType: CollisionType.Elastic,
        Velocity: Vector3Float.Zero,
        Mass: 1f,
        Location: Vector3Float.Zero).Marshal();

    private static DisReplayRewrite Rewrite(params (string Key, string Value)[] keys) =>
        DisReplayRewrite.Parse(keys.ToDictionary(k => k.Key, k => k.Value))!;

    private static PduHeader Header(byte[] pdu)
    {
        var r = new DisWireReader(pdu);
        return PduHeader.Unmarshal(ref r);
    }

    // ---- opt-in ----

    [Fact]
    public void Without_Any_Rewrite_Key_There_Is_No_Rewrite()
    {
        var metadata = new Dictionary<string, string> { ["multicast-group"] = "239.1.2.3", ["port"] = "3000" };
        Assert.Null(DisReplayRewrite.Parse(metadata));
        Assert.Null(DisReplayRewrite.Parse(null));
    }

    // ---- exercise id ----

    [Fact]
    public void The_Exercise_Id_Is_Rewritten_And_Nothing_Else()
    {
        var original = Fire(timestamp: 0x12345679);
        var sent = Rewrite(("exercise-id", "7")).Apply(original, HalfPast)!;

        Assert.Equal(7, Header(sent).ExerciseId);
        // Byte 1 is the only difference; the body goes out as captured.
        Assert.Equal(original.Length, sent.Length);
        Assert.Equal([1], Enumerable.Range(0, sent.Length).Where(i => sent[i] != original[i]));
        // The capture itself is left alone — the next loop starts from it again.
        Assert.Equal(1, Header(original).ExerciseId);
    }

    // ---- retime ----

    [Fact]
    public void Retime_Stamps_The_Send_Time_And_Keeps_An_Absolute_Timestamp_Absolute()
    {
        var sent = Rewrite(("retime", "now")).Apply(Fire(timestamp: 0x00000011), HalfPast)!;
        // Half past: half of 2^31-1, shifted left, bit 0 set.
        Assert.Equal(0x7FFFFFFFu, Header(sent).Timestamp);
    }

    [Fact]
    public void Retime_Keeps_A_Relative_Timestamp_Relative()
    {
        var sent = Rewrite(("retime", "now")).Apply(Fire(timestamp: 0x00000010), HalfPast)!;
        Assert.Equal(0x7FFFFFFEu, Header(sent).Timestamp);
    }

    [Fact]
    public void The_Top_Of_The_Hour_Is_Zero()
    {
        var top = new DateTimeOffset(2026, 9, 28, 15, 0, 0, TimeSpan.Zero);
        Assert.Equal(1u, DisReplayRewrite.Timestamp(top, absolute: true));
        Assert.Equal(0u, DisReplayRewrite.Timestamp(top, absolute: false));
    }

    [Fact]
    public void Retime_Counts_From_The_Utc_Hour_Whatever_The_Offset()
    {
        // A zone a whole number of hours off would hide a local-time bug; one
        // that is a quarter hour off does not. 20:15 at +05:45 is 14:30 UTC.
        var quarterZone = new DateTimeOffset(2026, 9, 28, 20, 15, 0, TimeSpan.FromMinutes(345));
        Assert.Equal(DisReplayRewrite.Timestamp(HalfPast, true), DisReplayRewrite.Timestamp(quarterZone, true));
    }

    // ---- filters ----

    [Fact]
    public void A_Pdu_Type_Filter_Keeps_What_It_Names_By_Name_Or_Id()
    {
        var byName = Rewrite(("pdu-types", "fire"));
        Assert.NotNull(byName.Apply(Fire(), HalfPast));
        Assert.Null(byName.Apply(Collision(), HalfPast));

        var byId = Rewrite(("pdu-types", "2, 3"));
        Assert.NotNull(byId.Apply(Fire(), HalfPast));
        Assert.Null(byId.Apply(Collision(), HalfPast));
    }

    [Fact]
    public void An_Entity_Filter_Keeps_A_Pdu_That_Names_The_Entity_In_Any_Role()
    {
        // The same rule as an entity stream: the target of a Fire is in it.
        var targetOnly = Rewrite(("entities", "1:1:200"));
        Assert.NotNull(targetOnly.Apply(Fire(), HalfPast));
        Assert.Null(targetOnly.Apply(Collision(), HalfPast));
    }

    [Fact]
    public void A_Pdu_That_Does_Not_Decode_Names_No_Entity()
    {
        var truncated = Fire()[..40];
        truncated[8] = 0; truncated[9] = 40;
        Assert.Null(Rewrite(("entities", "1:1:100")).Apply(truncated, HalfPast));
    }

    [Fact]
    public void Filters_And_Rewrites_Combine()
    {
        var rewrite = Rewrite(("pdu-types", "Fire"), ("entities", "1:1:100"), ("exercise-id", "9"));
        Assert.Equal(9, Header(rewrite.Apply(Fire(), HalfPast)!).ExerciseId);
        Assert.Null(rewrite.Apply(Collision(), HalfPast));
    }

    // ---- misspelt values fail loudly ----

    [Theory]
    [InlineData("exercise-id", "0")]
    [InlineData("exercise-id", "256")]
    [InlineData("pdu-types", "Fyre")]
    [InlineData("pdu-types", "250")]
    [InlineData("pdu-types", "Other")]
    [InlineData("pdu-types", " , ")]
    [InlineData("entities", "1:1")]
    [InlineData("retime", "yesterday")]
    public void A_Value_That_Is_Not_Understood_Is_An_Error_Not_An_Empty_Replay(string key, string value)
    {
        Assert.Throws<FormatException>(() => DisReplayRewrite.Parse(new Dictionary<string, string> { [key] = value }));
    }

    // ---- through the emitter ----

    [Fact]
    public async Task The_Emitter_Sends_The_Rewritten_Pdu_And_Drops_The_Filtered_One()
    {
        var group = IPAddress.Parse("239.192.4.3");
#pragma warning disable CA5394 // collision avoidance, not a security boundary
        var port = 45000 + Random.Shared.Next(0, 5000);
#pragma warning restore CA5394

        var recording = new BowireRecording
        {
            Id = "rec_dis_rewrite",
            RecordingFormatVersion = 2,
            Steps =
            {
                new BowireRecordingStep
                {
                    Id = "collision", Protocol = "dis", CapturedAt = 0,
                    ResponseBinary = Convert.ToBase64String(Collision()),
                    Metadata = new Dictionary<string, string>
                    {
                        ["multicast-group"] = group.ToString(),
                        ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["pdu-types"] = "Fire",
                        ["exercise-id"] = "42",
                    }
                },
                new BowireRecordingStep
                {
                    Id = "fire", Protocol = "dis", CapturedAt = 1,
                    ResponseBinary = Convert.ToBase64String(Fire())
                }
            }
        };

        using var listener = new UdpClient(AddressFamily.InterNetwork);
        listener.ExclusiveAddressUse = false;
        listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.JoinMulticastGroup(group);
        try
        {
            await using var emitter = new DisMockEmitter();
            await emitter.StartAsync(recording, new MockEmitterOptions { ReplaySpeed = 0 },
                NullLogger.Instance, TestContext.Current.CancellationToken);

            var received = new List<byte[]>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                while (true) received.Add((await listener.ReceiveAsync(cts.Token)).Buffer);
            }
            catch (OperationCanceledException) { }

            var only = Assert.Single(received);
            Assert.Equal(DisPduType.Fire, Header(only).PduType);
            Assert.Equal(42, Header(only).ExerciseId);
        }
        finally { listener.DropMulticastGroup(group); }
    }

    [Fact]
    public async Task A_Misspelt_Rewrite_Fails_The_Start()
    {
        var recording = new BowireRecording
        {
            Steps =
            {
                new BowireRecordingStep
                {
                    Protocol = "dis", ResponseBinary = Convert.ToBase64String(Fire()),
                    Metadata = new Dictionary<string, string> { ["pdu-types"] = "Fyre" }
                }
            }
        };
        await using var emitter = new DisMockEmitter();
        await Assert.ThrowsAsync<FormatException>(() => emitter.StartAsync(
            recording, new MockEmitterOptions(), NullLogger.Instance, TestContext.Current.CancellationToken));
    }
}
