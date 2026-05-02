// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Round-trip tests for <see cref="BowireDisProtocol"/>. Stand up a
/// UDP multicast sender on a documentation-range group, then verify
/// that discovery surfaces the emitted entity and that the streaming
/// feed yields a decoded envelope.
/// </summary>
public sealed class BowireDisProtocolTests
{
    [Fact]
    public async Task Discover_WithMalformedUrl_ReturnsEmptyList()
    {
        var plugin = new BowireDisProtocol();
        var services = await plugin.DiscoverAsync("http://example.com", false, TestContext.Current.CancellationToken);
        Assert.Empty(services);
    }

    [Fact]
    public async Task Discover_WithQuietGroup_ReturnsExerciseServiceOnly()
    {
        // No sender is running — discovery should still surface the
        // exercise-wide feed so the user can open the raw stream.
        var plugin = new BowireDisProtocol();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var services = await plugin.DiscoverAsync(
            $"dis://239.192.4.10:{RandomPort()}", false, cts.Token);

        Assert.Single(services);
        Assert.Equal(BowireDisProtocol.ExerciseServiceName, services[0].Name);
        var method = Assert.Single(services[0].Methods);
        Assert.Equal(BowireDisProtocol.MonitorMethodName, method.Name);
        Assert.True(method.ServerStreaming);
    }

    [Fact]
    public async Task Discover_WithEmittingEntity_ReturnsEntityService()
    {
        var group = IPAddress.Parse("239.192.4.11");
        var port = RandomPort();

        using var sender = new UdpClient(AddressFamily.InterNetwork);
        sender.Client.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

        var pdu = BuildEntityStatePdu(marking: "T-72A", entity: 4242);
        var bytes = pdu.Marshal();

        using var heartbeat = new CancellationTokenSource();
        var heartbeatTask = Task.Run(async () =>
        {
            while (!heartbeat.IsCancellationRequested)
            {
                try { await sender.SendAsync(bytes, new IPEndPoint(group, port)); }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }
                try { await Task.Delay(100, heartbeat.Token); }
                catch (OperationCanceledException) { break; }
            }
        }, TestContext.Current.CancellationToken);

        try
        {
            var plugin = new BowireDisProtocol();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var services = await plugin.DiscoverAsync(
                $"dis://{group}:{port}", false, cts.Token);

            Assert.Contains(services, s => s.Name == BowireDisProtocol.ExerciseServiceName);
            Assert.Contains(services, s => s.Name.Contains("T-72A", StringComparison.Ordinal));
        }
        finally
        {
            await heartbeat.CancelAsync();
            try { await heartbeatTask; } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task InvokeStream_YieldsEnvelopeForEntityStatePdu()
    {
        var group = IPAddress.Parse("239.192.4.12");
        var port = RandomPort();

        using var sender = new UdpClient(AddressFamily.InterNetwork);
        sender.Client.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

        var pdu = BuildEntityStatePdu(marking: "MERKAVA", entity: 500);
        var bytes = pdu.Marshal();

        using var heartbeat = new CancellationTokenSource();
        var heartbeatTask = Task.Run(async () =>
        {
            while (!heartbeat.IsCancellationRequested)
            {
                try { await sender.SendAsync(bytes, new IPEndPoint(group, port)); }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }
                try { await Task.Delay(100, heartbeat.Token); }
                catch (OperationCanceledException) { break; }
            }
        }, TestContext.Current.CancellationToken);

        try
        {
            var plugin = new BowireDisProtocol();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            string? envelope = null;
            await foreach (var msg in plugin.InvokeStreamAsync(
                $"dis://{group}:{port}",
                BowireDisProtocol.ExerciseServiceName,
                BowireDisProtocol.MonitorMethodName,
                [],
                false,
                null,
                cts.Token))
            {
                envelope = msg;
                break;
            }

            Assert.NotNull(envelope);
            using var doc = JsonDocument.Parse(envelope!);
            var root = doc.RootElement;
            Assert.Equal("EntityState", root.GetProperty("pduType").GetString());
            Assert.Equal(1, root.GetProperty("pduTypeId").GetInt32());
            Assert.Equal("MERKAVA", root.GetProperty("marking").GetString());
            Assert.Equal("1:1:500", root.GetProperty("entityId").GetString());
            Assert.False(string.IsNullOrEmpty(root.GetProperty("raw").GetString()));
        }
        finally
        {
            await heartbeat.CancelAsync();
            try { await heartbeatTask; } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task InvokeStream_YieldsEnvelopeForBroadcastSender()
    {
        // Classic DIS deployments sometimes use UDP broadcast
        // (255.255.255.255) instead of multicast. The plugin must bind
        // and receive without attempting a multicast-group join.
        var port = RandomPort();

        using var sender = new UdpClient(AddressFamily.InterNetwork);
        sender.EnableBroadcast = true;

        var pdu = BuildEntityStatePdu(marking: "BROADCAST", entity: 777);
        var bytes = pdu.Marshal();

        using var heartbeat = new CancellationTokenSource();
        var heartbeatTask = Task.Run(async () =>
        {
            while (!heartbeat.IsCancellationRequested)
            {
                try { await sender.SendAsync(bytes, new IPEndPoint(IPAddress.Broadcast, port)); }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }
                try { await Task.Delay(100, heartbeat.Token); }
                catch (OperationCanceledException) { break; }
            }
        }, TestContext.Current.CancellationToken);

        try
        {
            var plugin = new BowireDisProtocol();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            string? envelope = null;
            await foreach (var msg in plugin.InvokeStreamAsync(
                $"dis://255.255.255.255:{port}",
                BowireDisProtocol.ExerciseServiceName,
                BowireDisProtocol.MonitorMethodName,
                [],
                false,
                null,
                cts.Token))
            {
                envelope = msg;
                break;
            }

            Assert.NotNull(envelope);
            using var doc = JsonDocument.Parse(envelope!);
            Assert.Equal("BROADCAST", doc.RootElement.GetProperty("marking").GetString());
        }
        finally
        {
            await heartbeat.CancelAsync();
            try { await heartbeatTask; } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void TryParseEntityServiceName_ExtractsTripleFromParenthesizedForm()
    {
        var id = BowireDisProtocol.TryParseEntityServiceName("T-72 (1:2:3)");
        Assert.NotNull(id);
        Assert.Equal((ushort)1, id!.Value.Site);
        Assert.Equal((ushort)2, id.Value.Application);
        Assert.Equal((ushort)3, id.Value.Entity);
    }

    [Fact]
    public void TryParseEntityServiceName_ExtractsBareTriple()
    {
        var id = BowireDisProtocol.TryParseEntityServiceName("5:6:7");
        Assert.NotNull(id);
        Assert.Equal((ushort)5, id!.Value.Site);
    }

    [Fact]
    public void TryParseEntityServiceName_ExerciseServiceReturnsNull()
    {
        Assert.Null(BowireDisProtocol.TryParseEntityServiceName(BowireDisProtocol.ExerciseServiceName));
    }

    [Fact]
    public void TryBuildEnvelope_NonPduBuffer_ReturnsNull()
    {
        Assert.Null(BowireDisProtocol.TryBuildEnvelope([0x01, 0x02], filter: null));
    }

    [Fact]
    public void TryBuildEnvelope_EntityFilterMismatch_ReturnsNull()
    {
        var pdu = BuildEntityStatePdu(marking: "OTHER", entity: 1);
        var bytes = pdu.Marshal();
        var envelope = BowireDisProtocol.TryBuildEnvelope(
            bytes, filter: new EntityId(1, 1, 999));
        Assert.Null(envelope);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsBroadcastOnlyMessage()
    {
        var plugin = new BowireDisProtocol();
        var result = await plugin.InvokeAsync(
            "dis://239.1.2.3:3000", "Exercise", "monitor", [], false, null, TestContext.Current.CancellationToken);
        Assert.Contains("broadcast-only", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    private static EntityStatePdu BuildEntityStatePdu(string marking, ushort entity) =>
        new(
            Header: PduHeader.ForV6(
                exerciseId: 1,
                pduType: DisPduType.EntityState,
                family: DisProtocolFamily.EntityInformation,
                length: EntityStatePdu.MinimumWireLength),
            EntityId: new EntityId(1, 1, entity),
            Force: ForceId.Friendly,
            EntityType: new EntityType(1, 1, 225, 1, 1, 0, 0),
            AlternativeEntityType: new EntityType(1, 1, 225, 1, 1, 0, 0),
            LinearVelocity: Vector3Float.Zero,
            Location: Vector3Double.Zero,
            Orientation: EulerAngles.Zero,
            Appearance: 0,
            DeadReckoning: DeadReckoningParameters.Default,
            Marking: EntityMarking.Ascii(marking),
            Capabilities: 0);

    private static int RandomPort()
    {
        // Random.Shared is fine here — port selection is a
        // collision-avoidance heuristic, not a security boundary.
#pragma warning disable CA5394
        return 41000 + Random.Shared.Next(0, 5000);
#pragma warning restore CA5394
    }
}
