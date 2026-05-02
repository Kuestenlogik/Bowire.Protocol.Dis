// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// The 12-byte PDU header that opens every IEEE 1278.1 PDU. Both
/// V6 and V7 headers share bytes 0-11; V7 adds an extra byte (the
/// <c>pduStatus</c>) that lives at offset 21 inside the Entity State
/// PDU header (where V6 keeps a reserved-zero byte). The generic
/// "header" here is what all PDUs carry.
/// </summary>
/// <param name="ProtocolVersion">First byte — <see cref="DisProtocolVersion"/>.</param>
/// <param name="ExerciseId">Exercise id (1–255). Receivers filter traffic by this.</param>
/// <param name="PduType">Third byte — <see cref="DisPduType"/>.</param>
/// <param name="ProtocolFamily">Fourth byte — <see cref="DisProtocolFamily"/>. Disambiguates PDU type ids that collide across families.</param>
/// <param name="Timestamp">32-bit relative or absolute timestamp per §5.2.31.</param>
/// <param name="Length">Total PDU length in bytes, including the header itself.</param>
/// <param name="Padding">Two bytes of reserved padding. V7 repurposes the first as <c>pduStatus</c>; V6 requires zero here.</param>
public readonly record struct PduHeader(
    DisProtocolVersion ProtocolVersion,
    byte ExerciseId,
    DisPduType PduType,
    DisProtocolFamily ProtocolFamily,
    uint Timestamp,
    ushort Length,
    ushort Padding)
{
    /// <summary>Wire length of the header, in bytes — identical V6 and V7.</summary>
    public const int WireLength = 12;

    /// <summary>
    /// Convenience constructor when the caller doesn't care about
    /// timestamp or padding — both default to zero. Colloquial
    /// "DIS v6" maps to the IEEE 1278.1A-1998 amendment (wire byte 6),
    /// which is the baseline most real-world exercises run.
    /// </summary>
    public static PduHeader ForV6(
        byte exerciseId, DisPduType pduType, DisProtocolFamily family, ushort length) =>
        new(
            DisProtocolVersion.Ieee1278_1A_1998,
            exerciseId,
            pduType,
            family,
            Timestamp: 0,
            Length: length,
            Padding: 0);

    /// <summary>
    /// V7 factory. Version 7 sets <see cref="ProtocolVersion"/> to
    /// <see cref="DisProtocolVersion.Ieee1278_1_2012"/>. Per-PDU V7
    /// deltas (pduStatus byte on Entity State et al.) are handled by
    /// the respective PDU marshalers, not here.
    /// </summary>
    public static PduHeader ForV7(
        byte exerciseId, DisPduType pduType, DisProtocolFamily family, ushort length) =>
        new(
            DisProtocolVersion.Ieee1278_1_2012,
            exerciseId,
            pduType,
            family,
            Timestamp: 0,
            Length: length,
            Padding: 0);

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte((byte)ProtocolVersion);
        w.WriteByte(ExerciseId);
        w.WriteByte((byte)PduType);
        w.WriteByte((byte)ProtocolFamily);
        w.WriteUInt32(Timestamp);
        w.WriteUInt16(Length);
        w.WriteUInt16(Padding);
    }

    internal static PduHeader Unmarshal(ref DisWireReader r) =>
        new(
            (DisProtocolVersion)r.ReadByte(),
            r.ReadByte(),
            (DisPduType)r.ReadByte(),
            (DisProtocolFamily)r.ReadByte(),
            r.ReadUInt32(),
            r.ReadUInt16(),
            r.ReadUInt16());
}
