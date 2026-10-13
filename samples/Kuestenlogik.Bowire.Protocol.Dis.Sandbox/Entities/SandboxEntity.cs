// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

/// <summary>DIS force ids the sandbox offers; values are the wire values.</summary>
internal enum Force : byte
{
    Friendly = 1,
    Opposing = 2,
    Neutral = 3,
}

/// <summary>One object on the map, as it stands. Immutable; the store swaps it.</summary>
internal sealed record SandboxEntity(
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
