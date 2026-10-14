// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

/// <summary>DIS force ids; values are the wire values. The editor offers all but <see cref="Other"/>.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "A parameter type of the public Blazor components (EntityEditor, EntityList, MapView).")]
public enum Force
{
    /// <summary>Other.</summary>
    Other = 0,
    /// <summary>Friendly.</summary>
    Friendly = 1,
    /// <summary>Opposing.</summary>
    Opposing = 2,
    /// <summary>Neutral.</summary>
    Neutral = 3,
}

/// <summary>One object on the map, as it stands. Immutable; the store swaps it.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "A parameter type of the public Blazor components (EntityEditor, EntityList, MapView).")]
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
