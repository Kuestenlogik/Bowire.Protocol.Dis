// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// IEEE 1278.1 Entity Type seven-tuple. Matches the "Enumeration of
/// Simulation Entity Type" record layout: a compact categorical
/// identity for the simulated entity. SISO-REF-010 publishes the
/// canonical mapping from tuples to real-world platform names.
/// </summary>
/// <param name="Kind">1=Platform, 2=Munition, 3=Life form, 4=Environmental, ...</param>
/// <param name="Domain">Within Kind — for platforms: 1=Land, 2=Air, 3=Surface, 4=Subsurface, 5=Space.</param>
/// <param name="Country">ISO-3166-ish numeric country code (e.g. 225=United States, 78=Germany).</param>
/// <param name="Category">Top-level class within the domain (e.g. 1=Tank for Platform/Land).</param>
/// <param name="Subcategory">Refinement of category (e.g. 1=M1 Abrams).</param>
/// <param name="Specific">Model-specific variant.</param>
/// <param name="Extra">Extra-specific variant — rarely used.</param>
public readonly record struct EntityType(
    byte Kind,
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
        w.WriteByte(Kind);
        w.WriteByte(Domain);
        w.WriteUInt16(Country);
        w.WriteByte(Category);
        w.WriteByte(Subcategory);
        w.WriteByte(Specific);
        w.WriteByte(Extra);
    }

    internal static EntityType Unmarshal(ref DisWireReader r) =>
        new(
            r.ReadByte(),
            r.ReadByte(),
            r.ReadUInt16(),
            r.ReadByte(),
            r.ReadByte(),
            r.ReadByte(),
            r.ReadByte());
}

/// <summary>
/// Entity Marking record (12 bytes on the wire). Combines a one-byte
/// character-set selector with 11 fixed characters identifying the
/// entity in a human-readable way (e.g. a tail number or call sign).
/// IEEE 1278.1 §5.2.20.
/// </summary>
/// <param name="CharacterSet">1 = ASCII (the common case); 2 = Army Marking; 3 = Digit Chevron.</param>
/// <param name="Marking">ASCII text up to 11 chars. Shorter strings are NUL-padded on the wire; longer ones truncate.</param>
public readonly record struct EntityMarking(byte CharacterSet, string Marking)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 12;

    /// <summary>Number of character bytes on the wire (after the charset byte).</summary>
    public const int MarkingLength = 11;

    /// <summary>Convenience factory for the overwhelmingly common ASCII case.</summary>
    public static EntityMarking Ascii(string marking) => new(1, marking);

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte(CharacterSet);
        w.WriteAsciiFixed(Marking.AsSpan(), MarkingLength);
    }

    internal static EntityMarking Unmarshal(ref DisWireReader r)
    {
        var charset = r.ReadByte();
        var text = r.ReadAsciiFixed(MarkingLength);
        return new EntityMarking(charset, text);
    }
}
