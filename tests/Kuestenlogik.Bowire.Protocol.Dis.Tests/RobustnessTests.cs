// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Whatever arrives on an exercise network — a PDU type Bowire does not know,
/// a custom one, a truncated one, one whose header lies about its length — the
/// stream, the discovery and the mock replay go on.
/// </summary>
/// <remarks>
/// A single PDU that throws on the stream path ends the subscription for every
/// PDU after it. The decoders are written against well-formed input; this
/// holds the code around them to not letting anything else through.
/// </remarks>
public sealed class RobustnessTests
{
    private static readonly EntityId Somebody = new(1, 1, 1);

    [Fact]
    public void A_Custom_Pdu_Type_Reaches_The_Unfiltered_Stream_As_Raw_Bytes()
    {
        // 200: outside anything SISO-REF-010 assigns — a site's own PDU.
        var bytes = new byte[24];
        bytes[0] = 7; bytes[1] = 1; bytes[2] = 200; bytes[3] = 129;
        bytes[9] = 24;

        using var doc = JsonDocument.Parse(BowireDisProtocol.TryBuildEnvelope(bytes, filter: null)!);
        Assert.Equal(200, doc.RootElement.GetProperty("pduTypeId").GetInt32());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("pdu").ValueKind);
        Assert.Equal(bytes, Convert.FromBase64String(doc.RootElement.GetProperty("raw").GetString()!));
    }

    [Fact]
    public void A_Custom_Pdu_Type_Names_No_Entity_And_Stays_Off_An_Entity_Stream()
    {
        var bytes = new byte[24];
        bytes[2] = 200; bytes[9] = 24;
        Assert.Null(BowireDisProtocol.TryBuildEnvelope(bytes, filter: Somebody));
    }

    [Fact]
    public void A_Custom_Pdu_Type_Replays_And_Can_Be_Selected_By_Its_Number()
    {
        var bytes = new byte[24];
        bytes[2] = 200; bytes[9] = 24;
        var rewrite = DisReplayRewrite.Parse(new Dictionary<string, string> { ["exercise-id"] = "5" })!;
        Assert.Equal(5, rewrite.Apply(bytes, DateTimeOffset.UtcNow)![1]);
    }

    [Fact]
    public void A_Count_Of_Two_Billion_Is_A_Malformed_Pdu_Not_An_Allocation()
    {
        // Action Request: header, two entity ids, request id, action id, then
        // the fixed and variable datum counts. The decoder used to reserve a
        // list of that capacity before reading one datum — gigabytes, seconds
        // of stall, or an OutOfMemoryException nobody caught.
        var bytes = new byte[40];
        bytes[0] = 6; bytes[2] = 16; bytes[3] = 5; bytes[9] = 40;
        bytes[32] = 0x7F; bytes[33] = 0xFF; bytes[34] = 0xFF; bytes[35] = 0xF0;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Null(DisPduDecoder.TryDecode(bytes));
        Assert.True(watch.ElapsedMilliseconds < 1000, $"took {watch.ElapsedMilliseconds} ms");
        Assert.NotNull(BowireDisProtocol.TryBuildEnvelope(bytes, filter: null));
    }

    [Fact]
    public void No_Byte_Sequence_Of_Any_Pdu_Type_Throws_On_The_Stream_Or_Replay_Path()
    {
        // Seeded, so a failure reproduces; not a security boundary.
#pragma warning disable CA5394
        var rng = new Random(1278);
        var entityFilter = DisReplayRewrite.Parse(new Dictionary<string, string> { ["entities"] = "1:1:1", ["retime"] = "now" })!;
        var failures = new List<string>();

        for (var type = 0; type < 256; type++)
        {
            for (var round = 0; round < 60; round++)
            {
                var length = round switch
                {
                    0 => 0,
                    1 => 11,
                    2 => 12,
                    _ => rng.Next(12, 400),
                };
                var bytes = new byte[length];
                rng.NextBytes(bytes);
                if (length >= 12)
                {
                    bytes[2] = (byte)type;
                    // Half the time the header's length is honest, half the time not.
                    if (round % 2 == 0) { bytes[8] = (byte)(length >> 8); bytes[9] = (byte)length; }
                }

                try
                {
                    BowireDisProtocol.TryBuildEnvelope(bytes, filter: null);
                    BowireDisProtocol.TryBuildEnvelope(bytes, filter: Somebody);
                    entityFilter.Apply(bytes, DateTimeOffset.UtcNow);
                }
#pragma warning disable CA1031 // collecting every failure is the point of the test
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    failures.Add($"type {type}, {length} bytes: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

#pragma warning restore CA5394
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct().Take(40)));
    }
}
