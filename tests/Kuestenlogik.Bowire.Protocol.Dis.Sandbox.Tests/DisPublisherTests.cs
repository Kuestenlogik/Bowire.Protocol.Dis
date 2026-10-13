// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PluginComment = Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagement.CommentPdu;
using PluginDeadReckoning = Kuestenlogik.Bowire.Protocol.Dis.Enumerations.DeadReckoningAlgorithm;
using PluginEntityState = Kuestenlogik.Bowire.Protocol.Dis.Pdu.EntityStatePdu;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

public sealed class DisPublisherTests : IDisposable
{
    private const byte EntityStateType = 1;
    private const byte CommentType = 22;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingSink _sink = new();
    private readonly EntityStore _store;
    private readonly PublisherStatus _status;
    private readonly DisPublisher _publisher;

    public DisPublisherTests()
    {
        var options = Options.Create(new SandboxOptions());
        _store = new EntityStore(_time);
        _status = new PublisherStatus(options);
        _publisher = new DisPublisher(
            _store, new PduFactory(options, _time), _sink, _status, options, _time, NullLogger<DisPublisher>.Instance);
    }

    public void Dispose() => _publisher.Dispose();

    private static double Latitude(byte[] datagram) =>
        GeocentricCoordinate.ToWgs84(PluginEntityState.Unmarshal(datagram).Location)!.Value.Latitude;

    [Fact]
    public void Add_Update_AndStandingMoves_SendAnEntityStateAtOnce()
    {
        var e = _store.Add("tank", Force.Friendly, 53.5, 10.0);
        _store.Update(e.Number, "Leo", "tank", Force.Friendly, 0);
        _store.Move(e.Number, 53.6, 10.0, following: false);

        Assert.Equal([EntityStateType, EntityStateType, EntityStateType], _sink.Types);
        Assert.Equal(53.6, Latitude(_sink.Datagrams[^1]), 7);
        Assert.Equal(3, _status.Sent);
    }

    [Fact]
    public void PairsChanged_SendsAComment()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _sink.Datagrams.Clear();

        _store.TrySetPairs(e.Number, [new("status", "ready")], out _);

        Assert.Equal([CommentType], _sink.Types);
        Assert.Single(PluginComment.Unmarshal(_sink.Datagrams[0]).VariableDatums!);
    }

    [Fact]
    public void Remove_SendsADeactivatedEntityState()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _sink.Datagrams.Clear();

        _store.Remove(e.Number);

        var pdu = PluginEntityState.Unmarshal(Assert.Single(_sink.Datagrams));
        Assert.Equal(PduFactory.DeactivatedAppearance, pdu.Appearance);
    }

    [Fact]
    public void FollowMoves_WaitForTheTick_AndOnlyTheLatestGoesOut()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _sink.Datagrams.Clear();

        for (var i = 1; i <= 5; i++)
        {
            _time.Advance(TimeSpan.FromMilliseconds(10));
            _store.Move(e.Number, i * 0.0001, 0, following: true);
        }
        Assert.Empty(_sink.Datagrams);

        _publisher.Tick();

        var datagram = Assert.Single(_sink.Datagrams);
        Assert.Equal(0.0005, Latitude(datagram), 7);
        Assert.Equal(PluginDeadReckoning.FPW, PluginEntityState.Unmarshal(datagram).DeadReckoning.Algorithm);

        _publisher.Tick();
        Assert.Single(_sink.Datagrams);
    }

    [Fact]
    public void Following_WhenTheMouseRests_SettlesToStandingStill()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _store.Move(e.Number, 0, 0, following: true);
        _time.Advance(TimeSpan.FromSeconds(1));
        _store.Move(e.Number, 0.001, 0, following: true);
        _publisher.Tick();
        _sink.Datagrams.Clear();

        // No move for two ticks: the mouse has stopped, the object has not left.
        _time.Advance(TimeSpan.FromMilliseconds(250));
        _publisher.Tick();

        var pdu = PluginEntityState.Unmarshal(Assert.Single(_sink.Datagrams));
        Assert.Equal(PluginDeadReckoning.Static, pdu.DeadReckoning.Algorithm);
        Assert.Equal(default, pdu.LinearVelocity);
        Assert.Equal(0.001, Latitude(_sink.Datagrams[0]), 7);
        Assert.True(_store.Get(e.Number)!.Following);

        _time.Advance(TimeSpan.FromMilliseconds(250));
        _publisher.Tick();
        Assert.Single(_sink.Datagrams);
    }

    [Fact]
    public void StopFollowing_SendsTheRestingStateAtOnce()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _store.Move(e.Number, 0, 0, following: true);
        _time.Advance(TimeSpan.FromSeconds(1));
        _store.Move(e.Number, 0.001, 0, following: true);
        _sink.Datagrams.Clear();

        _store.StopFollowing(e.Number);

        var pdu = PluginEntityState.Unmarshal(Assert.Single(_sink.Datagrams));
        Assert.Equal(PluginDeadReckoning.Static, pdu.DeadReckoning.Algorithm);
        Assert.Equal(default, pdu.LinearVelocity);
    }

    [Fact]
    public void Heartbeat_EveryFiveSeconds_SendsEveryEntity_AndCommentsOnlyWhereThereArePairs()
    {
        var withPairs = _store.Add("tank", Force.Friendly, 0, 0);
        _store.Add("truck", Force.Friendly, 0, 0);
        _store.TrySetPairs(withPairs.Number, [new("status", "ready")], out _);
        _sink.Datagrams.Clear();

        _time.Advance(TimeSpan.FromSeconds(4.9));
        _publisher.Tick();
        Assert.Empty(_sink.Datagrams);

        _time.Advance(TimeSpan.FromSeconds(0.1));
        _publisher.Tick();
        Assert.Equal([EntityStateType, CommentType, EntityStateType], _sink.Types);

        _sink.Datagrams.Clear();
        _publisher.Tick();
        Assert.Empty(_sink.Datagrams);
    }

    [Fact]
    public void ClearingThePairs_SendsOneEmptyComment_AndTheHeartbeatThenSkipsIt()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _store.TrySetPairs(e.Number, [new("status", "ready")], out _);
        _sink.Datagrams.Clear();

        _store.TrySetPairs(e.Number, [], out _);

        var empty = PluginComment.Unmarshal(Assert.Single(_sink.Datagrams));
        Assert.Empty(empty.VariableDatums ?? []);

        _sink.Datagrams.Clear();
        _time.Advance(TimeSpan.FromSeconds(5));
        _publisher.Tick();
        Assert.Equal([EntityStateType], _sink.Types);
    }

    [Fact]
    public void SinkFailures_AreCountedAndNeverEscape()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _sink.Datagrams.Clear();
        _sink.Fail = true;

        _store.Move(e.Number, 1, 1, following: false);
        _time.Advance(TimeSpan.FromSeconds(5));
        _publisher.Tick();
        _time.Advance(TimeSpan.FromSeconds(5));
        _publisher.Tick();

        Assert.Equal(3, _status.Errors);
        Assert.Equal("network unreachable", _status.LastError);
        Assert.Equal(_time.GetUtcNow(), _status.LastErrorAt);

        _sink.Fail = false;
        _time.Advance(TimeSpan.FromSeconds(5));
        _publisher.Tick();
        Assert.Single(_sink.Datagrams);
    }

    [Fact]
    public void RemovingAFollowedEntity_DropsItsPendingMove()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _store.Move(e.Number, 0.001, 0, following: true);
        _store.Remove(e.Number);
        _sink.Datagrams.Clear();

        _publisher.Tick();
        _time.Advance(TimeSpan.FromSeconds(5));
        _publisher.Tick();

        Assert.Empty(_sink.Datagrams);
    }

    [Fact]
    public void Dispose_StopsListeningToTheStore()
    {
        _publisher.Dispose();

        _store.Add("tank", Force.Friendly, 0, 0);

        Assert.Empty(_sink.Datagrams);
    }
}
