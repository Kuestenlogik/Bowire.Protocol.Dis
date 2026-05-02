// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Linear Segment Parameter record (64 bytes). Describes one linear
/// segment of a <see cref="Pdu.SyntheticEnvironment.LinearObjectStatePdu"/>.
/// IEEE 1278.1 §5.2.23.
/// </summary>
/// <param name="SegmentNumber">Sequence number of the segment within the linear object.</param>
/// <param name="SegmentModifications">Bitfield — bit 0 flags location changed since previous update, bit 1 flags orientation changed.</param>
/// <param name="GeneralSegmentAppearance">16-bit generic appearance (damage state, smoke, predawn).</param>
/// <param name="SpecificSegmentAppearance">32-bit type-specific appearance bitfield.</param>
/// <param name="SegmentLocation">ECEF location of the segment's centroid.</param>
/// <param name="SegmentOrientation">Segment orientation (body-axis rotation).</param>
/// <param name="SegmentLength">Length of the segment along the X axis (metres).</param>
/// <param name="SegmentWidth">Width of the segment along the Y axis (metres).</param>
/// <param name="SegmentHeight">Height of the segment along the Z axis (metres).</param>
/// <param name="SegmentDepth">Depth of the segment below ground level (metres).</param>
public readonly record struct LinearSegmentParameter(
    byte SegmentNumber,
    byte SegmentModifications,
    ushort GeneralSegmentAppearance,
    uint SpecificSegmentAppearance,
    Vector3Double SegmentLocation,
    EulerAngles SegmentOrientation,
    float SegmentLength,
    float SegmentWidth,
    float SegmentHeight,
    float SegmentDepth)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 64;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte(SegmentNumber);
        w.WriteByte(SegmentModifications);
        w.WriteUInt16(GeneralSegmentAppearance);
        w.WriteUInt32(SpecificSegmentAppearance);
        SegmentLocation.Marshal(ref w);
        SegmentOrientation.Marshal(ref w);
        w.WriteSingle(SegmentLength);
        w.WriteSingle(SegmentWidth);
        w.WriteSingle(SegmentHeight);
        w.WriteSingle(SegmentDepth);
    }

    internal static LinearSegmentParameter Unmarshal(ref DisWireReader r)
    {
        var segmentNumber = r.ReadByte();
        var modifications = r.ReadByte();
        var generalAppearance = r.ReadUInt16();
        var specificAppearance = r.ReadUInt32();
        var location = Vector3Double.Unmarshal(ref r);
        var orientation = EulerAngles.Unmarshal(ref r);
        var length = r.ReadSingle();
        var width = r.ReadSingle();
        var height = r.ReadSingle();
        var depth = r.ReadSingle();
        return new LinearSegmentParameter(
            segmentNumber, modifications, generalAppearance, specificAppearance,
            location, orientation, length, width, height, depth);
    }
}
