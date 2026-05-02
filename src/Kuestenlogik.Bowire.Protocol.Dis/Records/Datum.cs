// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Fixed Datum record (8 bytes). Carries a 32-bit datum id + a 32-bit
/// datum value on the wire. The id comes from SISO-REF-010's
/// "Variable Record Types" enumeration; value semantics depend on
/// the id. IEEE 1278.1 §5.2.33.
/// </summary>
/// <param name="DatumId">Datum id per SISO-REF-010.</param>
/// <param name="DatumValue">The 32-bit value. Interpretation depends on the id.</param>
public readonly record struct FixedDatum(uint DatumId, uint DatumValue)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt32(DatumId);
        w.WriteUInt32(DatumValue);
    }

    internal static FixedDatum Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt32(), r.ReadUInt32());
}

/// <summary>
/// Variable Datum record. 8 bytes of fixed header (4-byte id + 4-byte
/// length-in-bits) followed by the datum value padded out to a
/// multiple of 8 bytes. IEEE 1278.1 §5.2.34.
/// </summary>
/// <param name="DatumId">Datum id per SISO-REF-010.</param>
/// <param name="DatumLengthBits">Significant length of the datum value in <b>bits</b> — not bytes. Receivers use this to know how many trailing pad bits to ignore.</param>
/// <param name="Value">Datum value bytes. Marshal pads out to the next 8-byte boundary automatically.</param>
public sealed record VariableDatum(uint DatumId, uint DatumLengthBits, byte[] Value)
{
    /// <summary>
    /// Wire length in bytes: 8-byte header plus the value bytes padded
    /// to the next 8-byte boundary.
    /// </summary>
    public int WireLength
    {
        get
        {
            // Value bytes needed for the advertised bit count.
            var valueByteCount = Math.Max(Value.Length, (int)((DatumLengthBits + 7) / 8));
            // Round up to the next 8-byte boundary.
            var padded = ((valueByteCount + 7) / 8) * 8;
            return 8 + padded;
        }
    }

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt32(DatumId);
        w.WriteUInt32(DatumLengthBits);

        var valueByteCount = Math.Max(Value.Length, (int)((DatumLengthBits + 7) / 8));
        var padded = ((valueByteCount + 7) / 8) * 8;

        w.WriteBytes(Value);
        // Trailing padding — zero-fill to the 8-byte boundary.
        w.WritePadding(padded - Value.Length);
    }

    internal static VariableDatum Unmarshal(ref DisWireReader r)
    {
        var id = r.ReadUInt32();
        var lengthBits = r.ReadUInt32();
        var valueByteCount = (int)((lengthBits + 7) / 8);
        var padded = ((valueByteCount + 7) / 8) * 8;

        // Value bytes are the first valueByteCount; the rest up to
        // padded is zero padding we advance over.
        var value = valueByteCount > 0
            ? r.ReadBytes(valueByteCount).ToArray()
            : [];
        if (padded > valueByteCount) r.SkipPadding(padded - valueByteCount);

        return new VariableDatum(id, lengthBits, value);
    }
}
