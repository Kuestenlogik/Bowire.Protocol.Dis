// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using Kuestenlogik.Bowire.Mocking;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Round-trip tests for <see cref="DisMockEmitter"/>. Starts the
/// emitter against a synthetic recording and listens on a UDP
/// receiver bound to the same multicast group + port to verify that
/// the captured PDU bytes flow through verbatim.
/// </summary>
public sealed class DisMockEmitterTests
{
    [Fact]
    public async Task CanEmit_TrueWhenRecordingHasDisStep()
    {
        await using var emitter = new DisMockEmitter();
        var rec = new BowireRecording
        {
            Steps =
            {
                new BowireRecordingStep { Protocol = "rest" },
                new BowireRecordingStep { Protocol = "dis" }
            }
        };
        Assert.True(emitter.CanEmit(rec));
    }

    [Fact]
    public async Task CanEmit_FalseWhenRecordingHasNoDisStep()
    {
        await using var emitter = new DisMockEmitter();
        var rec = new BowireRecording
        {
            Steps = { new BowireRecordingStep { Protocol = "mqtt" } }
        };
        Assert.False(emitter.CanEmit(rec));
    }

    [Fact]
    public async Task EmitsPduBytesOnMulticastGroup()
    {
        // Pick a fresh ephemeral UDP port + a documentation-range
        // IPv4 multicast group so concurrent test runs on the same
        // box don't collide. Listener joins the group and asserts
        // the recorded bytes arrive verbatim.
        var group = IPAddress.Parse("239.192.4.2");
        // Random.Shared is fine here — port selection is a collision-
        // avoidance heuristic, not a security boundary.
#pragma warning disable CA5394
        var port = 40000 + Random.Shared.Next(0, 5000);
#pragma warning restore CA5394

        var payloadA = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var payloadB = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        var recording = new BowireRecording
        {
            Id = "rec_dis",
            Name = "dis round-trip",
            RecordingFormatVersion = 2,
            Steps =
            {
                new BowireRecordingStep
                {
                    Id = "pdu_a",
                    Protocol = "dis",
                    Service = "EntityState",
                    Method = "Send",
                    MethodType = "Unary",
                    CapturedAt = 0,
                    ResponseBinary = Convert.ToBase64String(payloadA),
                    Metadata = new Dictionary<string, string>
                    {
                        ["multicast-group"] = group.ToString(),
                        ["port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["ttl"] = "1"
                    }
                },
                new BowireRecordingStep
                {
                    Id = "pdu_b",
                    Protocol = "dis",
                    Service = "EntityState",
                    Method = "Send",
                    MethodType = "Unary",
                    CapturedAt = 1,
                    ResponseBinary = Convert.ToBase64String(payloadB)
                }
            }
        };

        using var listener = new UdpClient(AddressFamily.InterNetwork);
        listener.ExclusiveAddressUse = false;
        listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        listener.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        listener.JoinMulticastGroup(group);

        await using (var emitter = new DisMockEmitter())
        {
            await emitter.StartAsync(
                recording,
                new MockEmitterOptions { ReplaySpeed = 0 }, // emit instantly
                NullLogger.Instance,
                CancellationToken.None);

            try
            {
                var received = new List<byte[]>();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                while (received.Count < 2 && !cts.IsCancellationRequested)
                {
                    UdpReceiveResult result;
                    try { result = await listener.ReceiveAsync(cts.Token); }
                    catch (OperationCanceledException) { break; }
                    received.Add(result.Buffer);
                }

                Assert.Equal(2, received.Count);
                Assert.Contains(received, r => r.SequenceEqual(payloadA));
                Assert.Contains(received, r => r.SequenceEqual(payloadB));
            }
            finally
            {
                listener.DropMulticastGroup(group);
            }
        }
    }
}
