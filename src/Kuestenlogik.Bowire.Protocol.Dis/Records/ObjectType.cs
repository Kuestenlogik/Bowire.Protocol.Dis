// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Object Type record (4 bytes). Identifies the kind of synthetic-
/// environment object carried by Point/Linear/Areal Object State
/// PDUs. IEEE 1278.1 §5.2.44. Narrower than the 8-byte
/// <see cref="EntityType"/> — environment objects don't get country
/// codes or specific-variant bytes.
/// </summary>
/// <param name="Domain">Top-level domain (1=Land, 2=Air, 3=Surface, 4=Subsurface, 5=Space).</param>
/// <param name="ObjectKind">Object category (1=Obstacle, 2=Building, 3=Sign, 4=Culturalfeature, 5=Passageway, 6=Tacticalsmoke, ...).</param>
/// <param name="Category">Refinement of ObjectKind.</param>
/// <param name="Subcategory">Further refinement.</param>
public readonly record struct ObjectType(
    byte Domain,
    byte ObjectKind,
    byte Category,
    byte Subcategory)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 4;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte(Domain);
        w.WriteByte(ObjectKind);
        w.WriteByte(Category);
        w.WriteByte(Subcategory);
    }

    internal static ObjectType Unmarshal(ref DisWireReader r) =>
        new(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
}
