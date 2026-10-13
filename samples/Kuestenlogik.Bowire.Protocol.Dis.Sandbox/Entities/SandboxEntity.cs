// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

/// <summary>DIS force ids; values are the wire values. The editor offers all but <see cref="Other"/>.</summary>
public enum Force
{
    Other = 0,
    Friendly = 1,
    Opposing = 2,
    Neutral = 3,
}

/// <summary>One object on the map, as it stands. Immutable; the store swaps it.</summary>
public sealed record SandboxEntity(
    ushort Number,
    string Name,
    string PresetId,
    Force Force,
    GeoPosition Position,
    double HeadingDegrees,
    double SpeedMetresPerSecond,
    bool Following,
    IReadOnlyList<KeyValuePair<string, string>> Pairs);

internal enum EntityChangeKind
{
    Added,
    Moved,
    Updated,
    PairsChanged,
    Removed,
}

/// <summary>What changed, with the entity after the change (for Removed: as it was last).</summary>
internal sealed record EntityChange(EntityChangeKind Kind, SandboxEntity Entity);
