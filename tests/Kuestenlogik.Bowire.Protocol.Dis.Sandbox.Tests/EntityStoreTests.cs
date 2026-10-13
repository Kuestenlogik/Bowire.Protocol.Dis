// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
using Microsoft.Extensions.Time.Testing;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

public sealed class EntityStoreTests
{
    // 0.001° of latitude at the equator, on the mean earth radius.
    private const double MillidegreeMetres = 111.19508;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly List<EntityChange> _changes = [];
    private readonly EntityStore _store;

    public EntityStoreTests()
    {
        _store = new EntityStore(_time);
        _store.Changed += _changes.Add;
    }

    private static KeyValuePair<string, string> P(string key, string value) => new(key, value);

    [Fact]
    public void Add_NumbersFromOne_NamesAndPlacesFromThePreset()
    {
        var a = _store.Add("tank", Force.Friendly, 53.5, 10.0);
        var b = _store.Add("helicopter", Force.Opposing, 53.6, 10.1);

        Assert.Equal((ushort)1, a.Number);
        Assert.Equal((ushort)2, b.Number);
        Assert.Equal("Objekt 2", b.Name);
        Assert.Equal(0.0, a.Position.Altitude);
        Assert.Equal(500.0, b.Position.Altitude);
        Assert.Equal([EntityChangeKind.Added, EntityChangeKind.Added], _changes.Select(c => c.Kind));
    }

    [Fact]
    public void Add_UnknownPreset_Throws() =>
        Assert.Throws<ArgumentException>(() => _store.Add("submarine", Force.Friendly, 0, 0));

    [Fact]
    public void Remove_NeverReusesTheNumber()
    {
        _store.Add("tank", Force.Friendly, 0, 0);
        var second = _store.Add("tank", Force.Friendly, 0, 0);
        Assert.True(_store.Remove(second.Number));

        var third = _store.Add("tank", Force.Friendly, 0, 0);

        Assert.Equal((ushort)3, third.Number);
        Assert.Equal(EntityChangeKind.Removed, _changes[2].Kind);
        Assert.Equal(second.Number, _changes[2].Entity.Number);
    }

    [Fact]
    public void Move_WithoutFollowing_StandsStill()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);

        Assert.True(_store.Move(e.Number, 0.01, 0.02, following: false));

        var moved = _store.Get(e.Number)!;
        Assert.Equal(0.01, moved.Position.Latitude);
        Assert.Equal(0.02, moved.Position.Longitude);
        Assert.Equal(0.0, moved.SpeedMetresPerSecond);
        Assert.False(moved.Following);
        Assert.Equal(EntityChangeKind.Moved, _changes[^1].Kind);
    }

    [Fact]
    public void Move_Following_DerivesHeadingAndSpeedFromTheLastTwoPositions()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _store.Move(e.Number, 0, 0, following: true);
        _time.Advance(TimeSpan.FromSeconds(1));

        _store.Move(e.Number, 0.001, 0, following: true);

        var moved = _store.Get(e.Number)!;
        Assert.True(moved.Following);
        Assert.Equal(0.0, moved.HeadingDegrees, 6);
        Assert.Equal(MillidegreeMetres, moved.SpeedMetresPerSecond, 2);
    }

    [Fact]
    public void Move_AcrossTheAntimeridian_IsAShortHopEast()
    {
        var e = _store.Add("ship", Force.Neutral, 0, 179.9995);
        _store.Move(e.Number, 0, 179.9995, following: true);
        _time.Advance(TimeSpan.FromSeconds(1));

        _store.Move(e.Number, 0, -179.9995, following: true);

        var moved = _store.Get(e.Number)!;
        Assert.Equal(90.0, moved.HeadingDegrees, 6);
        Assert.Equal(MillidegreeMetres, moved.SpeedMetresPerSecond, 2);
    }

    [Fact]
    public void Move_WithNoElapsedTime_KeepsSpeedAndHeading()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _store.Move(e.Number, 0, 0, following: true);
        _time.Advance(TimeSpan.FromSeconds(1));
        _store.Move(e.Number, 0.001, 0, following: true);

        _store.Move(e.Number, 0.002, 0.001, following: true);   // same instant

        var moved = _store.Get(e.Number)!;
        Assert.True(double.IsFinite(moved.SpeedMetresPerSecond));
        Assert.Equal(MillidegreeMetres, moved.SpeedMetresPerSecond, 2);
        Assert.Equal(0.0, moved.HeadingDegrees, 6);
    }

    [Fact]
    public void StopFollowing_ComesToRest()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _store.Move(e.Number, 0, 0, following: true);
        _time.Advance(TimeSpan.FromSeconds(1));
        _store.Move(e.Number, 0.001, 0, following: true);

        Assert.True(_store.StopFollowing(e.Number));

        var stopped = _store.Get(e.Number)!;
        Assert.False(stopped.Following);
        Assert.Equal(0.0, stopped.SpeedMetresPerSecond);
        Assert.Equal(EntityChangeKind.Moved, _changes[^1].Kind);
    }

    [Fact]
    public void Update_ChangesNameTypeForceAndAltitude()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);

        Assert.True(_store.Update(e.Number, "  Leo  ", "truck", Force.Neutral, 25));

        var updated = _store.Get(e.Number)!;
        Assert.Equal("Leo", updated.Name);
        Assert.Equal("truck", updated.PresetId);
        Assert.Equal(Force.Neutral, updated.Force);
        Assert.Equal(25.0, updated.Position.Altitude);
        Assert.Equal(EntityChangeKind.Updated, _changes[^1].Kind);
    }

    [Fact]
    public void TrySetPairs_StoresACopyAndRaisesPairsChanged()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        var pairs = new List<KeyValuePair<string, string>> { P("status", "ready"), P("fuel", "80") };

        Assert.True(_store.TrySetPairs(e.Number, pairs, out var error));
        pairs.Clear();

        Assert.Null(error);
        Assert.Equal([P("status", "ready"), P("fuel", "80")], _store.Get(e.Number)!.Pairs);
        Assert.Equal(EntityChangeKind.PairsChanged, _changes[^1].Kind);
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("  ", "x")]
    [InlineData("a=b", "x")]
    public void TrySetPairs_RejectsKeysThatCannotBeSent(string key, string value)
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);
        _changes.Clear();

        Assert.False(_store.TrySetPairs(e.Number, [P(key, value)], out var error));

        Assert.NotNull(error);
        Assert.Empty(_changes);
    }

    [Fact]
    public void TrySetPairs_RejectsDuplicateKeys()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);

        Assert.False(_store.TrySetPairs(e.Number, [P("status", "a"), P("status", "b")], out var error));
        Assert.Contains("status", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySetPairs_RejectsWhatWouldNotFitInOneDatagram()
    {
        var e = _store.Add("tank", Force.Friendly, 0, 0);

        Assert.False(_store.TrySetPairs(e.Number, [P("text", new string('x', 1400))], out var error));
        Assert.Contains("1400", error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownNumber_ChangesNothingAndRaisesNothing()
    {
        Assert.False(_store.Move(42, 0, 0, following: false));
        Assert.False(_store.StopFollowing(42));
        Assert.False(_store.Update(42, "x", "tank", Force.Friendly, 0));
        Assert.False(_store.TrySetPairs(42, [], out _));
        Assert.False(_store.Remove(42));
        Assert.Empty(_changes);
    }

    [Fact]
    public void CommentPduLength_MatchesTheWireLayout()
    {
        // 32 bytes of header and counts; each record 8 bytes plus its value padded to 8.
        Assert.Equal(32, PairRules.CommentPduLength([]));
        Assert.Equal(56, PairRules.CommentPduLength([P("status", "ready")]));          // 12 → 16
        Assert.Equal(48, PairRules.CommentPduLength([P("k", "123456")]));             // 8 → 8
        Assert.Equal(56, PairRules.CommentPduLength([P("ä", "1234567")]));            // 10 UTF-8 bytes → 16
    }
}
