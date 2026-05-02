// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Track/Jam Data record (8 bytes). Identifies a target being
/// tracked or jammed by a specific beam of an emitter system.
/// IEEE 1278.1 §5.3.7.1.3.
/// </summary>
/// <param name="EntityId">Target entity.</param>
/// <param name="EmitterNumber">Emitter id on the tracked entity (for jamming). 0 when not applicable.</param>
/// <param name="BeamNumber">Beam id on the tracked entity. 0 when not applicable.</param>
public readonly record struct TrackJamData(EntityId EntityId, byte EmitterNumber, byte BeamNumber)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        EntityId.Marshal(ref w);
        w.WriteByte(EmitterNumber);
        w.WriteByte(BeamNumber);
    }

    internal static TrackJamData Unmarshal(ref DisWireReader r)
    {
        var entity = EntityId.Unmarshal(ref r);
        var emitter = r.ReadByte();
        var beam = r.ReadByte();
        return new TrackJamData(entity, emitter, beam);
    }
}

/// <summary>
/// Fundamental Parameter Data record (20 bytes). Shared between
/// every active beam to describe its signal characteristics.
/// IEEE 1278.1 §5.3.7.1.2.1.
/// </summary>
public readonly record struct FundamentalParameterData(
    float Frequency,
    float FrequencyRange,
    float EffectiveRadiatedPower,
    float PulseRepetitionFrequency,
    float PulseWidth)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 20;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteSingle(Frequency);
        w.WriteSingle(FrequencyRange);
        w.WriteSingle(EffectiveRadiatedPower);
        w.WriteSingle(PulseRepetitionFrequency);
        w.WriteSingle(PulseWidth);
    }

    internal static FundamentalParameterData Unmarshal(ref DisWireReader r) =>
        new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}

/// <summary>
/// Electromagnetic Emission Beam record (variable length, min 52 B
/// + 8 B per track/jam target). Describes one steered beam within
/// an emitter system. IEEE 1278.1 §5.3.7.1.2.
/// </summary>
public sealed record ElectromagneticEmissionBeam(
    byte BeamNumber,
    ushort BeamParameterIndex,
    FundamentalParameterData FundamentalParameters,
    float BeamAzimuthCenter,
    float BeamAzimuthSweep,
    float BeamElevationCenter,
    float BeamElevationSweep,
    float BeamSweepSync,
    byte BeamFunction,
    byte HighDensityTrackJam,
    uint JammingModeSequence,
    IReadOnlyList<TrackJamData> TrackJamTargets)
{
    /// <summary>Fixed wire length before the track/jam targets.</summary>
    public const int FixedWireLength = 52;

    /// <summary>
    /// Wire length in bytes: 52 fixed + 8 per track/jam target. The
    /// on-wire BeamDataLength field is measured in 32-bit words.
    /// </summary>
    public int WireLength => FixedWireLength + (TrackJamTargets.Count * TrackJamData.WireLength);

    internal void Marshal(ref DisWireWriter w)
    {
        // Beam data length is in 32-bit words.
        w.WriteByte((byte)(WireLength / 4));
        w.WriteByte(BeamNumber);
        w.WriteUInt16(BeamParameterIndex);
        FundamentalParameters.Marshal(ref w);
        w.WriteSingle(BeamAzimuthCenter);
        w.WriteSingle(BeamAzimuthSweep);
        w.WriteSingle(BeamElevationCenter);
        w.WriteSingle(BeamElevationSweep);
        w.WriteSingle(BeamSweepSync);
        w.WriteByte(BeamFunction);
        w.WriteByte((byte)TrackJamTargets.Count);
        w.WriteByte(HighDensityTrackJam);
        w.WriteByte(0); // padding
        w.WriteUInt32(JammingModeSequence);
        foreach (var target in TrackJamTargets) target.Marshal(ref w);
    }

    internal static ElectromagneticEmissionBeam Unmarshal(ref DisWireReader r)
    {
        var _beamDataLengthWords = r.ReadByte();
        var beamNumber = r.ReadByte();
        var beamParameterIndex = r.ReadUInt16();
        var fundamentalParameters = FundamentalParameterData.Unmarshal(ref r);
        var azimuthCenter = r.ReadSingle();
        var azimuthSweep = r.ReadSingle();
        var elevationCenter = r.ReadSingle();
        var elevationSweep = r.ReadSingle();
        var sweepSync = r.ReadSingle();
        var beamFunction = r.ReadByte();
        var numTargets = r.ReadByte();
        var highDensity = r.ReadByte();
        r.SkipPadding(1);
        var jammingModeSequence = r.ReadUInt32();
        var targets = new List<TrackJamData>(numTargets);
        for (var i = 0; i < numTargets; i++) targets.Add(TrackJamData.Unmarshal(ref r));
        return new ElectromagneticEmissionBeam(
            beamNumber, beamParameterIndex, fundamentalParameters,
            azimuthCenter, azimuthSweep, elevationCenter, elevationSweep,
            sweepSync, beamFunction, highDensity, jammingModeSequence, targets);
    }
}

/// <summary>
/// Electromagnetic Emission Emitter System record (variable length,
/// min 20 B + beam records). Describes one radar / jammer / ESM
/// emitter on the platform and its current beams.
/// IEEE 1278.1 §5.3.7.1.1.
/// </summary>
public sealed record ElectromagneticEmissionSystem(
    ushort EmitterName,
    byte EmitterFunction,
    byte EmitterIdNumber,
    Vector3Float Location,
    IReadOnlyList<ElectromagneticEmissionBeam> Beams)
{
    /// <summary>Fixed wire length before the beam list.</summary>
    public const int FixedWireLength = 20;

    /// <summary>
    /// Wire length in bytes: 20 fixed + beams. The on-wire
    /// SystemDataLength field is in 32-bit words.
    /// </summary>
    public int WireLength
    {
        get
        {
            var total = FixedWireLength;
            foreach (var beam in Beams) total += beam.WireLength;
            return total;
        }
    }

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte((byte)(WireLength / 4));
        w.WriteByte((byte)Beams.Count);
        w.WriteUInt16(0); // padding
        w.WriteUInt16(EmitterName);
        w.WriteByte(EmitterFunction);
        w.WriteByte(EmitterIdNumber);
        Location.Marshal(ref w);
        foreach (var beam in Beams) beam.Marshal(ref w);
    }

    internal static ElectromagneticEmissionSystem Unmarshal(ref DisWireReader r)
    {
        var _systemDataLengthWords = r.ReadByte();
        var numBeams = r.ReadByte();
        r.SkipPadding(2);
        var emitterName = r.ReadUInt16();
        var emitterFunction = r.ReadByte();
        var emitterIdNumber = r.ReadByte();
        var location = Vector3Float.Unmarshal(ref r);
        var beams = new List<ElectromagneticEmissionBeam>(numBeams);
        for (var i = 0; i < numBeams; i++) beams.Add(ElectromagneticEmissionBeam.Unmarshal(ref r));
        return new ElectromagneticEmissionSystem(
            emitterName, emitterFunction, emitterIdNumber, location, beams);
    }
}
