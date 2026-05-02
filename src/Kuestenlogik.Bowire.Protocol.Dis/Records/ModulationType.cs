// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Modulation Type record (8 bytes). Describes how the transmitter
/// modulates its carrier — spread-spectrum technique, major
/// modulation type, detail, and system kind. IEEE 1278.1 §5.2.21.
/// </summary>
/// <param name="SpreadSpectrum">Bitfield — bit 0 frequency hopping, bit 1 pseudo-noise, bit 2 time hopping, rest reserved.</param>
/// <param name="MajorModulation">Major modulation class (1=Amplitude, 2=AmplitudeAndAngle, 3=Angle, 4=Combination, 5=Pulse, 6=Unmodulated, 7=CPSM).</param>
/// <param name="Detail">Refinement of MajorModulation — see SISO-REF-010.</param>
/// <param name="System">Radio system (1=GenericRadio, 2=HQ, 3=HQII, 4=HQIIA, 5=SINCGARS, 6=CCTT_SINCGARS, ...).</param>
public readonly record struct ModulationType(
    ushort SpreadSpectrum,
    ushort MajorModulation,
    ushort Detail,
    ushort System)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt16(SpreadSpectrum);
        w.WriteUInt16(MajorModulation);
        w.WriteUInt16(Detail);
        w.WriteUInt16(System);
    }

    internal static ModulationType Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16());
}
