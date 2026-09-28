// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.Minefield;

/// <summary>
/// The Minefield Data PDU's data filter: which per-mine fields the PDU
/// carries (#25).
/// </summary>
/// <remarks>
/// Bit assignment as KDIS has it (<c>MinefieldDataFilter.hpp</c>). There is no
/// second open source for it and no SISO test vector, which is why the typed
/// view in <see cref="MinefieldMineFields"/> falls back to the raw bytes
/// whenever they do not add up.
/// </remarks>
[Flags]
#pragma warning disable CA1028 // the wire field is an unsigned 32-bit bit set
public enum MinefieldDataFilter : uint
#pragma warning restore CA1028
{
    /// <summary>No optional field.</summary>
    None = 0,
    /// <summary>Bit 0 — ground burial depth offset, float32 per mine.</summary>
    GroundBurialDepthOffset = 1u << 0,
    /// <summary>Bit 1 — water burial depth offset, float32 per mine.</summary>
    WaterBurialDepthOffset = 1u << 1,
    /// <summary>Bit 2 — snow burial depth offset, float32 per mine.</summary>
    SnowBurialDepthOffset = 1u << 2,
    /// <summary>Bit 3 — mine orientation, Euler angles per mine.</summary>
    MineOrientation = 1u << 3,
    /// <summary>Bit 4 — thermal contrast, float32 per mine.</summary>
    ThermalContrast = 1u << 4,
    /// <summary>Bit 5 — reflectance, float32 per mine.</summary>
    Reflectance = 1u << 5,
    /// <summary>Bit 6 — mine emplacement age, clock time per mine.</summary>
    MineEmplacementAge = 1u << 6,
    /// <summary>Bit 7 — trip / detonation wires and their vertices.</summary>
    TripDetonationWire = 1u << 7,
    /// <summary>Bit 8 — fusing, 16 bits per mine.</summary>
    Fusing = 1u << 8,
    /// <summary>Bit 9 — scalar detection coefficient, one byte per mine and sensor type.</summary>
    ScalarDetectionCoefficient = 1u << 9,
    /// <summary>Bit 10 — paint scheme, one byte per mine.</summary>
    PaintScheme = 1u << 10,
}

/// <summary>Minefield Data fusing (SISO-REF-010 "Minefield Data-Fusing").</summary>
/// <param name="Raw">The 16 bits as on the wire.</param>
public readonly record struct MineFusing(ushort Raw)
{
    /// <summary>Bits 0–6: primary fuse type.</summary>
    public int Primary => Raw & 0x7F;
    /// <summary>Bits 7–13: secondary fuse type.</summary>
    public int Secondary => (Raw >> 7) & 0x7F;
    /// <summary>Bit 14: the mine has an anti-handling device.</summary>
    public bool HasAntiHandlingDevice => (Raw & (1 << 14)) != 0;
}

/// <summary>Minefield Data paint scheme (SISO-REF-010 "Minefield Data-Paint Scheme").</summary>
/// <param name="Raw">The 8 bits as on the wire.</param>
public readonly record struct MinePaintScheme(byte Raw)
{
    /// <summary>Bits 0–1: algae build-up.</summary>
    public int Algae => Raw & 0x03;
    /// <summary>Bits 2–7: paint scheme.</summary>
    public int Scheme => Raw >> 2;
}

/// <summary>
/// One mine's optional fields out of a Minefield Data PDU. A field the data
/// filter does not select is null; the entity number is always there.
/// </summary>
public sealed record MinefieldMine(
    ushort MineEntityNumber,
    float? GroundBurialDepthOffset = null,
    float? WaterBurialDepthOffset = null,
    float? SnowBurialDepthOffset = null,
    EulerAngles? Orientation = null,
    float? ThermalContrast = null,
    float? Reflectance = null,
    ClockTime? EmplacementAge = null,
    MineFusing? Fusing = null,
    IReadOnlyList<byte>? ScalarDetectionCoefficients = null,
    MinePaintScheme? PaintScheme = null,
    IReadOnlyList<IReadOnlyList<Vector3Float>>? TripDetonationWires = null);

/// <summary>
/// Reads and writes the per-mine block of a Minefield Data PDU — everything
/// after the mine locations (#25).
/// </summary>
/// <remarks>
/// <para>
/// Layout as KDIS decodes it (<c>Minefield_Data_PDU.cpp</c>); the field order
/// agrees with open-dis's generated dis7 code, which ignores the filter. Each
/// array has one entry per mine: ground, water and snow burial depth offsets,
/// orientation, thermal contrast, reflectance, emplacement age, the mine
/// entity number (always present), fusing, scalar detection coefficients
/// (one per mine and sensor type), paint scheme, padding to 32 bits, then the
/// trip wires: a wire count per mine, padding, a vertex count per wire,
/// padding, the vertices.
/// </para>
/// <para>
/// The PDU keeps the raw bytes either way and writes them back unchanged.
/// This is a view on them, and it declines — returns null — when the bytes
/// do not add up to exactly what the filter announces, or when the filter
/// sets a bit this reader does not know. A wrong reading would label fields
/// silently; no reading leaves the bytes as they are.
/// </para>
/// </remarks>
public static class MinefieldMineFields
{
    private const MinefieldDataFilter Known = (MinefieldDataFilter)0x7FF;

    /// <summary>The mines as typed records, or null when the bytes do not read cleanly.</summary>
    public static IReadOnlyList<MinefieldMine>? TryDecode(
        ReadOnlySpan<byte> block, uint dataFilter, int mineCount, int sensorTypeCount)
    {
        var filter = (MinefieldDataFilter)dataFilter;
        if ((filter & ~Known) != 0) return null;
        try
        {
            var r = new DisWireReader(block);
            var mines = Decode(ref r, filter, mineCount, sensorTypeCount);
            if (r.Remaining < 0) return null;
            // Anything left over means a different layout than the one read:
            // decline rather than guess. Trailing zero padding to 32 bits is
            // the only exception.
            var rest = block[(block.Length - r.Remaining)..];
            if (rest.Length >= 4 || rest.IndexOfAnyExcept((byte)0) >= 0) return null;
            return mines;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static List<MinefieldMine> Decode(
        ref DisWireReader r, MinefieldDataFilter filter, int n, int sensors)
    {
        float[]? Floats(ref DisWireReader r, MinefieldDataFilter bit)
        {
            if (!filter.HasFlag(bit)) return null;
            var a = new float[n];
            for (var i = 0; i < n; i++) a[i] = r.ReadSingle();
            return a;
        }

        var ground = Floats(ref r, MinefieldDataFilter.GroundBurialDepthOffset);
        var water = Floats(ref r, MinefieldDataFilter.WaterBurialDepthOffset);
        var snow = Floats(ref r, MinefieldDataFilter.SnowBurialDepthOffset);
        EulerAngles[]? orientation = null;
        if (filter.HasFlag(MinefieldDataFilter.MineOrientation))
        {
            orientation = new EulerAngles[n];
            for (var i = 0; i < n; i++) orientation[i] = EulerAngles.Unmarshal(ref r);
        }
        var thermal = Floats(ref r, MinefieldDataFilter.ThermalContrast);
        var reflectance = Floats(ref r, MinefieldDataFilter.Reflectance);
        ClockTime[]? age = null;
        if (filter.HasFlag(MinefieldDataFilter.MineEmplacementAge))
        {
            age = new ClockTime[n];
            for (var i = 0; i < n; i++) age[i] = ClockTime.Unmarshal(ref r);
        }
        var ids = new ushort[n];
        for (var i = 0; i < n; i++) ids[i] = r.ReadUInt16();
        ushort[]? fusing = null;
        if (filter.HasFlag(MinefieldDataFilter.Fusing))
        {
            fusing = new ushort[n];
            for (var i = 0; i < n; i++) fusing[i] = r.ReadUInt16();
        }
        byte[][]? sdc = null;
        if (filter.HasFlag(MinefieldDataFilter.ScalarDetectionCoefficient))
        {
            sdc = new byte[n][];
            for (var i = 0; i < n; i++)
            {
                sdc[i] = new byte[sensors];
                for (var j = 0; j < sensors; j++) sdc[i][j] = r.ReadByte();
            }
        }
        byte[]? paint = null;
        if (filter.HasFlag(MinefieldDataFilter.PaintScheme))
        {
            paint = new byte[n];
            for (var i = 0; i < n; i++) paint[i] = r.ReadByte();
        }
        r.SkipPadding(PadTo32(r.Offset));

        List<Vector3Float>[][]? wires = null;
        if (filter.HasFlag(MinefieldDataFilter.TripDetonationWire))
        {
            var wireCounts = new byte[n];
            for (var i = 0; i < n; i++) wireCounts[i] = r.ReadByte();
            r.SkipPadding(PadTo32(r.Offset));
            var vertexCounts = new byte[wireCounts.Sum(c => c)];
            for (var i = 0; i < vertexCounts.Length; i++) vertexCounts[i] = r.ReadByte();
            r.SkipPadding(PadTo32(r.Offset));
            wires = new List<Vector3Float>[n][];
            var w = 0;
            for (var i = 0; i < n; i++)
            {
                wires[i] = new List<Vector3Float>[wireCounts[i]];
                for (var j = 0; j < wireCounts[i]; j++, w++)
                {
                    wires[i][j] = new List<Vector3Float>(vertexCounts[w]);
                    for (var k = 0; k < vertexCounts[w]; k++) wires[i][j].Add(Vector3Float.Unmarshal(ref r));
                }
            }
        }

        var mines = new List<MinefieldMine>(n);
        for (var i = 0; i < n; i++)
        {
            mines.Add(new MinefieldMine(
                ids[i],
                ground?[i], water?[i], snow?[i],
                orientation?[i], thermal?[i], reflectance?[i], age?[i],
                fusing is null ? null : new MineFusing(fusing[i]),
                sdc?[i],
                paint is null ? null : new MinePaintScheme(paint[i]),
                wires?[i]));
        }
        return mines;
    }

    /// <summary>
    /// The per-mine block for <paramref name="mines"/>, laid out for
    /// <paramref name="dataFilter"/> — what a Minefield Data PDU carries as its
    /// raw optional fields. A field the filter selects must be set on every
    /// mine.
    /// </summary>
    /// <exception cref="ArgumentException">A selected field is missing, or the filter sets an unknown bit.</exception>
    public static byte[] Encode(uint dataFilter, int sensorTypeCount, IReadOnlyList<MinefieldMine> mines)
    {
        ArgumentNullException.ThrowIfNull(mines);
        var filter = (MinefieldDataFilter)dataFilter;
        if ((filter & ~Known) != 0) throw new ArgumentException("Unknown data filter bit.", nameof(dataFilter));

        T Need<T>(T? value, string field) where T : struct =>
            value ?? throw new ArgumentException($"The data filter selects {field}, a mine does not have it.", nameof(mines));

        // Sized generously, then trimmed: the vertices dominate and are known up front.
        var vertices = mines.Sum(m => m.TripDetonationWires?.Sum(w => w.Count) ?? 0);
        var wireCount = mines.Sum(m => m.TripDetonationWires?.Count ?? 0);
        var buffer = new byte[(mines.Count * (4 * 5 + 12 + 8 + 2 + 2 + 1 + 1 + sensorTypeCount)) + wireCount + (vertices * 12) + 16];
        var w = new DisWireWriter(buffer);

        void Floats(ref DisWireWriter w, MinefieldDataFilter bit, Func<MinefieldMine, float?> pick, string name)
        {
            if (!filter.HasFlag(bit)) return;
            foreach (var m in mines) w.WriteSingle(Need(pick(m), name));
        }

        Floats(ref w, MinefieldDataFilter.GroundBurialDepthOffset, m => m.GroundBurialDepthOffset, "ground burial depth");
        Floats(ref w, MinefieldDataFilter.WaterBurialDepthOffset, m => m.WaterBurialDepthOffset, "water burial depth");
        Floats(ref w, MinefieldDataFilter.SnowBurialDepthOffset, m => m.SnowBurialDepthOffset, "snow burial depth");
        if (filter.HasFlag(MinefieldDataFilter.MineOrientation))
            foreach (var m in mines) Need(m.Orientation, "orientation").Marshal(ref w);
        Floats(ref w, MinefieldDataFilter.ThermalContrast, m => m.ThermalContrast, "thermal contrast");
        Floats(ref w, MinefieldDataFilter.Reflectance, m => m.Reflectance, "reflectance");
        if (filter.HasFlag(MinefieldDataFilter.MineEmplacementAge))
            foreach (var m in mines) Need(m.EmplacementAge, "emplacement age").Marshal(ref w);
        foreach (var m in mines) w.WriteUInt16(m.MineEntityNumber);
        if (filter.HasFlag(MinefieldDataFilter.Fusing))
            foreach (var m in mines) w.WriteUInt16(Need(m.Fusing, "fusing").Raw);
        if (filter.HasFlag(MinefieldDataFilter.ScalarDetectionCoefficient))
        {
            foreach (var m in mines)
            {
                var c = m.ScalarDetectionCoefficients
                    ?? throw new ArgumentException("The data filter selects scalar detection coefficients, a mine does not have them.", nameof(mines));
                if (c.Count != sensorTypeCount)
                    throw new ArgumentException("A mine needs one scalar detection coefficient per sensor type.", nameof(mines));
                foreach (var b in c) w.WriteByte(b);
            }
        }
        if (filter.HasFlag(MinefieldDataFilter.PaintScheme))
            foreach (var m in mines) w.WriteByte(Need(m.PaintScheme, "paint scheme").Raw);
        w.WritePadding(PadTo32(w.Offset));

        if (filter.HasFlag(MinefieldDataFilter.TripDetonationWire))
        {
            var wires = mines.Select(m => m.TripDetonationWires
                ?? throw new ArgumentException("The data filter selects trip wires, a mine does not have them.", nameof(mines))).ToList();
            foreach (var mw in wires) w.WriteByte(checked((byte)mw.Count));
            w.WritePadding(PadTo32(w.Offset));
            foreach (var mw in wires)
                foreach (var wire in mw) w.WriteByte(checked((byte)wire.Count));
            w.WritePadding(PadTo32(w.Offset));
            foreach (var mw in wires)
                foreach (var wire in mw)
                    foreach (var v in wire) v.Marshal(ref w);
        }
        return buffer[..w.Offset];
    }

    // The block starts on a 32-bit boundary of the PDU (after the 12-byte
    // mine locations, which start on one), so aligning within the block
    // aligns within the PDU.
    private static int PadTo32(int offset) => (4 - (offset % 4)) % 4;
}
