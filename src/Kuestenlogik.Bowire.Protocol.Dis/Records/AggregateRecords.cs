// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Aggregate Type record (8 bytes). Wire-identical to
/// <see cref="EntityType"/> but semantically an aggregate-kind
/// descriptor (Military, CivilianHierarchy, ...) rather than a
/// single-entity one. IEEE 1278.1 §5.2.3.
/// </summary>
public readonly record struct AggregateType(
    byte AggregateKind,
    byte Domain,
    ushort Country,
    byte Category,
    byte Subcategory,
    byte Specific,
    byte Extra)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte(AggregateKind);
        w.WriteByte(Domain);
        w.WriteUInt16(Country);
        w.WriteByte(Category);
        w.WriteByte(Subcategory);
        w.WriteByte(Specific);
        w.WriteByte(Extra);
    }

    internal static AggregateType Unmarshal(ref DisWireReader r) =>
        new(r.ReadByte(), r.ReadByte(), r.ReadUInt16(),
            r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
}

/// <summary>
/// Aggregate Marking record (32 bytes). Like <see cref="EntityMarking"/>
/// but with 31 content characters instead of 11, to hold longer
/// human-readable aggregate names. IEEE 1278.1 §5.2.2.
/// </summary>
/// <param name="CharacterSet">1 = ASCII (the common case).</param>
/// <param name="Marking">ASCII text up to 31 chars. Shorter strings are NUL-padded; longer ones truncate.</param>
public readonly record struct AggregateMarking(byte CharacterSet, string Marking)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 32;

    /// <summary>Number of character bytes on the wire (after the charset byte).</summary>
    public const int MarkingLength = 31;

    /// <summary>Convenience factory for ASCII.</summary>
    public static AggregateMarking Ascii(string marking) => new(1, marking);

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte(CharacterSet);
        w.WriteAsciiFixed(Marking.AsSpan(), MarkingLength);
    }

    internal static AggregateMarking Unmarshal(ref DisWireReader r)
    {
        var charset = r.ReadByte();
        var text = r.ReadAsciiFixed(MarkingLength);
        return new AggregateMarking(charset, text);
    }
}

/// <summary>
/// Named Location Identification record (4 bytes). Ships with
/// IsPartOf to address the connection point between a part and its
/// host entity by symbolic station name + station number.
/// IEEE 1278.1 §5.2.22.
/// </summary>
/// <param name="StationName">Symbolic station name (e.g. 601 = Weapon Station 1).</param>
/// <param name="StationNumber">Station number within the named area.</param>
public readonly record struct NamedLocationId(ushort StationName, ushort StationNumber)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 4;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt16(StationName);
        w.WriteUInt16(StationNumber);
    }

    internal static NamedLocationId Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt16(), r.ReadUInt16());
}
