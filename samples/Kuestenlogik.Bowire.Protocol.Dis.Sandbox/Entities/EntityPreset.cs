// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

/// <summary>
/// A type you can pick instead of typing SISO codes. Codes per
/// SISO-REF-010 v37; country 78 is Germany.
/// </summary>
internal sealed record EntityPreset(
    string Id, string Label, byte Kind, byte Domain, ushort Country,
    byte Category, byte Subcategory, double DefaultAltitude)
{
    public static IReadOnlyList<EntityPreset> All { get; } =
    [
        new("tank", "Kampfpanzer", 1, 1, 78, 1, 0, 0),          // Platform, Land, Tank
        new("truck", "LKW", 1, 1, 78, 7, 0, 0),                 // Platform, Land, Large Wheeled Utility Vehicle
        new("helicopter", "Hubschrauber", 1, 2, 78, 21, 0, 500), // Platform, Air, Utility Helicopter
        new("aircraft", "Flugzeug", 1, 2, 78, 4, 0, 500),        // Platform, Air, Cargo/Tanker
        new("ship", "Schiff", 1, 3, 78, 50, 0, 0),               // Platform, Surface, Frigate (including Corvette)
        new("person", "Person", 3, 1, 78, 11, 0, 0),             // Life form, Land, Army
    ];

    public static EntityPreset Get(string id) =>
        All.FirstOrDefault(p => p.Id == id)
        ?? throw new ArgumentException($"Unknown entity preset '{id}'.", nameof(id));
}
