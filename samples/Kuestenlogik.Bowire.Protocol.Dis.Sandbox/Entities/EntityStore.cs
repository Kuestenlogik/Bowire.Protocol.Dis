// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

/// <summary>
/// The objects on the map. Knows neither the map nor DIS: it changes
/// entities and says so through <see cref="Changed"/>, raised outside the
/// lock so a handler may read the store again.
/// </summary>
internal sealed class EntityStore(TimeProvider time)
{
    private const double MeanEarthRadius = 6_371_008.8;
    private const double DegToRad = Math.PI / 180.0;

    private readonly Lock _gate = new();
    private readonly Dictionary<ushort, SandboxEntity> _entities = [];
    private readonly Dictionary<ushort, long> _lastFollowMove = [];

    // Only ever counts up, so a number is never handed out twice in a
    // session: Bowire would take a new object for the old one.
    private int _nextNumber = 1;

    public event Action<EntityChange>? Changed;

    public IReadOnlyList<SandboxEntity> Snapshot()
    {
        lock (_gate)
            return [.. _entities.Values.OrderBy(e => e.Number)];
    }

    public SandboxEntity? Get(ushort number)
    {
        lock (_gate)
            return _entities.GetValueOrDefault(number);
    }

    public SandboxEntity Add(string presetId, Force force, double latitude, double longitude)
    {
        var preset = EntityPreset.Get(presetId);
        SandboxEntity entity;
        lock (_gate)
        {
            if (_nextNumber > ushort.MaxValue)
                throw new InvalidOperationException("Alle 65535 Entity-Nummern dieser Sitzung sind vergeben.");
            var number = (ushort)_nextNumber++;
            entity = new SandboxEntity(
                number, $"Objekt {number}", preset.Id, force,
                new GeoPosition(latitude, longitude, preset.DefaultAltitude),
                HeadingDegrees: 0, SpeedMetresPerSecond: 0, Following: false, Pairs: []);
            _entities[number] = entity;
        }
        Raise(EntityChangeKind.Added, entity);
        return entity;
    }

    /// <summary>
    /// Moves an entity. While following the mouse, heading and speed come
    /// from this and the previous follow move; otherwise it stands still.
    /// </summary>
    public bool Move(ushort number, double latitude, double longitude, bool following)
    {
        var now = time.GetTimestamp();
        var moved = Mutate(number, e =>
        {
            var heading = e.HeadingDegrees;
            var speed = 0.0;
            if (following)
            {
                speed = e.SpeedMetresPerSecond;
                if (_lastFollowMove.TryGetValue(number, out var last))
                {
                    var elapsed = time.GetElapsedTime(last, now).TotalSeconds;
                    var (distance, bearing) = Displacement(e.Position, latitude, longitude);
                    // Two moves at the same instant say nothing about speed.
                    if (elapsed > 0 && distance > 0.01)
                    {
                        speed = distance / elapsed;
                        heading = bearing;
                    }
                }
                _lastFollowMove[number] = now;
            }
            else
            {
                _lastFollowMove.Remove(number);
            }

            return e with
            {
                Position = e.Position with { Latitude = latitude, Longitude = longitude },
                HeadingDegrees = heading,
                SpeedMetresPerSecond = speed,
                Following = following,
            };
        });
        return Raise(EntityChangeKind.Moved, moved);
    }

    public bool StopFollowing(ushort number)
    {
        var stopped = Mutate(number, e =>
        {
            _lastFollowMove.Remove(number);
            return e with { Following = false, SpeedMetresPerSecond = 0 };
        });
        return Raise(EntityChangeKind.Moved, stopped);
    }

    public bool Update(ushort number, string name, string presetId, Force force, double altitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var preset = EntityPreset.Get(presetId);
        var updated = Mutate(number, e => e with
        {
            Name = name.Trim(),
            PresetId = preset.Id,
            Force = force,
            Position = e.Position with { Altitude = altitude },
        });
        return Raise(EntityChangeKind.Updated, updated);
    }

    public bool TrySetPairs(ushort number, IReadOnlyList<KeyValuePair<string, string>> pairs, out string? error)
    {
        error = PairRules.Validate(pairs);
        if (error is not null)
            return false;

        KeyValuePair<string, string>[] copy = [.. pairs];
        var updated = Mutate(number, e => e with { Pairs = copy });
        if (updated is null)
        {
            error = $"Objekt {number} gibt es nicht.";
            return false;
        }
        return Raise(EntityChangeKind.PairsChanged, updated);
    }

    public bool Remove(ushort number)
    {
        SandboxEntity? removed;
        lock (_gate)
        {
            if (_entities.Remove(number, out removed))
                _lastFollowMove.Remove(number);
        }
        return Raise(EntityChangeKind.Removed, removed);
    }

    private SandboxEntity? Mutate(ushort number, Func<SandboxEntity, SandboxEntity> change)
    {
        lock (_gate)
        {
            if (!_entities.TryGetValue(number, out var current))
                return null;
            var next = change(current);
            _entities[number] = next;
            return next;
        }
    }

    private bool Raise(EntityChangeKind kind, SandboxEntity? entity)
    {
        if (entity is null)
            return false;
        Changed?.Invoke(new EntityChange(kind, entity));
        return true;
    }

    // Short hops only (mouse moves); equirectangular is exact enough and
    // the longitude difference is wrapped so the antimeridian is a hop.
    private static (double Distance, double Bearing) Displacement(GeoPosition from, double latitude, double longitude)
    {
        var dLon = longitude - from.Longitude;
        if (dLon > 180) dLon -= 360;
        else if (dLon < -180) dLon += 360;

        var meanLat = (from.Latitude + latitude) / 2.0 * DegToRad;
        var east = dLon * DegToRad * Math.Cos(meanLat) * MeanEarthRadius;
        var north = (latitude - from.Latitude) * DegToRad * MeanEarthRadius;
        var bearing = GeoConversion.NormalizeDegrees(Math.Atan2(east, north) / DegToRad);
        return (Math.Sqrt((east * east) + (north * north)), bearing);
    }
}
