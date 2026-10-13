// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
using Microsoft.Extensions.Options;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

/// <summary>
/// Puts every change in the store on the wire. Most changes go out at
/// once; moves while following the mouse wait for the next tick, so at most
/// <see cref="SandboxOptions.FollowMaxUpdatesPerSecond"/> go out per entity.
/// The tick also sends the heartbeat, so a receiver that joins late has the
/// full picture after one interval. Nothing that fails while sending
/// stops the UI or the heartbeat.
/// </summary>
internal sealed class DisPublisher : BackgroundService
{
    private readonly EntityStore _store;
    private readonly PduFactory _factory;
    private readonly IDatagramSink _sink;
    private readonly PublisherStatus _status;
    private readonly TimeProvider _time;
    private readonly ILogger<DisPublisher> _logger;
    private readonly TimeSpan _tickInterval;
    private readonly TimeSpan _heartbeatInterval;
    private readonly Lock _gate = new();
    private readonly HashSet<ushort> _pendingFollowMoves = [];
    private DateTimeOffset _nextHeartbeat;

    public DisPublisher(
        EntityStore store, PduFactory factory, IDatagramSink sink, PublisherStatus status,
        IOptions<SandboxOptions> options, TimeProvider time, ILogger<DisPublisher> logger)
    {
        _store = store;
        _factory = factory;
        _sink = sink;
        _status = status;
        _time = time;
        _logger = logger;
        _tickInterval = TimeSpan.FromSeconds(1.0 / options.Value.FollowMaxUpdatesPerSecond);
        _heartbeatInterval = TimeSpan.FromSeconds(options.Value.HeartbeatSeconds);
        _nextHeartbeat = time.GetUtcNow() + _heartbeatInterval;
        _store.Changed += OnChanged;
    }

    public override void Dispose()
    {
        _store.Changed -= OnChanged;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DIS sandbox sending to udp://{Target}.", _status.Target);
        using var timer = new PeriodicTimer(_tickInterval, _time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                Tick();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <summary>Sends pending follow moves, and the heartbeat when it is due.</summary>
    internal void Tick()
    {
        ushort[] due;
        lock (_gate)
        {
            due = [.. _pendingFollowMoves];
            _pendingFollowMoves.Clear();
        }
        foreach (var number in due)
        {
            if (_store.Get(number) is { } entity)
                SendEntityState(entity, deactivated: false);
        }

        var now = _time.GetUtcNow();
        if (now < _nextHeartbeat)
            return;
        _nextHeartbeat = now + _heartbeatInterval;
        foreach (var entity in _store.Snapshot())
        {
            SendEntityState(entity, deactivated: false);
            if (entity.Pairs.Count > 0)
                SendComment(entity);
        }
    }

    private void OnChanged(EntityChange change)
    {
        var entity = change.Entity;
        switch (change.Kind)
        {
            case EntityChangeKind.Moved when entity.Following:
                lock (_gate)
                    _pendingFollowMoves.Add(entity.Number);
                break;
            case EntityChangeKind.Added or EntityChangeKind.Moved or EntityChangeKind.Updated:
                SendEntityState(entity, deactivated: false);
                break;
            case EntityChangeKind.PairsChanged:
                // Sent even when empty: the receiver has to see the pairs go.
                SendComment(entity);
                break;
            case EntityChangeKind.Removed:
                lock (_gate)
                    _pendingFollowMoves.Remove(entity.Number);
                SendEntityState(entity, deactivated: true);
                break;
        }
    }

    private void SendEntityState(SandboxEntity entity, bool deactivated) =>
        Send(entity.Number, () => _factory.EntityState(entity, deactivated));

    private void SendComment(SandboxEntity entity) =>
        Send(entity.Number, () => _factory.Comment(entity));

    private void Send(ushort number, Func<byte[]> encode)
    {
        byte[] datagram;
        try
        {
            datagram = encode();
            _status.ClearEncodeFailure(number);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not encode a PDU for entity {Number}.", number);
            _status.MarkEncodeFailed(number, ex.Message);
            return;
        }

        try
        {
            _sink.Send(datagram);
            _status.RecordSent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Sending to udp://{Target} failed: {Message}", _status.Target, ex.Message);
            _status.RecordSendError(ex, _time.GetUtcNow());
        }
    }
}
