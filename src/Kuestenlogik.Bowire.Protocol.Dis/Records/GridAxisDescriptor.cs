// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Grid Axis Descriptor record (IEEE 1278.1 §6.2.41). Describes one
/// axis of the 3D grid carried by a Gridded Data PDU — which
/// parameter is sampled, its unit, the inclusive value range, the
/// number of samples along the axis, the sample interleaf factor,
/// and per-sample data values.
/// </summary>
/// <remarks>
/// <para>Layout on the wire (24 bytes header + variable payload):</para>
/// <list type="bullet">
///   <item>0  : 8 — <see cref="DomainInitialXi"/> (inclusive start)</item>
///   <item>8  : 8 — <see cref="DomainFinalXi"/> (inclusive end)</item>
///   <item>16 : 2 — <see cref="DomainPointsXi"/> (count)</item>
///   <item>18 : 1 — <see cref="InterleafFactor"/></item>
///   <item>19 : 1 — <see cref="AxisType"/></item>
///   <item>20 : 2 — number of values bytes (length of <see cref="Values"/>)</item>
///   <item>22 : 2 — <see cref="DataRepresentation"/></item>
///   <item>24 : N — <see cref="Values"/> (padded to 64-bit boundary on the wire)</item>
/// </list>
/// <para>
/// The sample-data interpretation is driven by
/// <see cref="DataRepresentation"/> (16-bit float / 32-bit float /
/// varies per SISO-REF-010). The codec exposes the raw
/// <see cref="Values"/> bytes so callers can decode against whichever
/// representation a given PDU uses without committing to a single shape.
/// </para>
/// </remarks>
public sealed record GridAxisDescriptor(
    double DomainInitialXi,
    double DomainFinalXi,
    ushort DomainPointsXi,
    byte InterleafFactor,
    byte AxisType,
    ushort DataRepresentation,
    byte[] Values)
{
    /// <summary>Fixed wire length of the axis-descriptor header.</summary>
    public const int FixedHeaderLength = 24;

    /// <summary>
    /// Wire length in bytes: 24-byte header plus the values payload
    /// padded out to the next 64-bit boundary.
    /// </summary>
    public int WireLength
    {
        get
        {
            var payload = FixedHeaderLength + Values.Length;
            return ((payload + 7) / 8) * 8;
        }
    }

    internal void Marshal(ref DisWireWriter w)
    {
        var valuesLen = Values.Length;
        var payload = FixedHeaderLength + valuesLen;
        var padded = ((payload + 7) / 8) * 8;

        w.WriteDouble(DomainInitialXi);
        w.WriteDouble(DomainFinalXi);
        w.WriteUInt16(DomainPointsXi);
        w.WriteByte(InterleafFactor);
        w.WriteByte(AxisType);
        w.WriteUInt16((ushort)valuesLen);
        w.WriteUInt16(DataRepresentation);
        w.WriteBytes(Values);
        var pad = padded - payload;
        if (pad > 0) w.WritePadding(pad);
    }

    internal static GridAxisDescriptor Unmarshal(ref DisWireReader r)
    {
        var initial = r.ReadDouble();
        var final = r.ReadDouble();
        var points = r.ReadUInt16();
        var interleaf = r.ReadByte();
        var axisType = r.ReadByte();
        var valuesLen = r.ReadUInt16();
        var dataRepresentation = r.ReadUInt16();
        var values = valuesLen > 0 ? r.ReadBytes(valuesLen).ToArray() : [];

        var payload = FixedHeaderLength + valuesLen;
        var padded = ((payload + 7) / 8) * 8;
        var pad = padded - payload;
        if (pad > 0) r.SkipPadding(pad);

        return new GridAxisDescriptor(
            initial, final, points, interleaf, axisType,
            dataRepresentation, values);
    }
}
