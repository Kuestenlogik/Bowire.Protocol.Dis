// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Shaft record (20 bytes). Ships with Underwater Acoustic PDU —
/// one per propeller shaft. IEEE 1278.1 §5.3.7.3.1.
/// </summary>
/// <param name="CurrentShaftRpm">Current rotation rate (RPM; positive = ahead, negative = reverse).</param>
/// <param name="OrderedShaftRpm">Ordered rotation rate.</param>
/// <param name="RpmRateOfChange">Shaft RPM rate of change (RPM per second).</param>
public readonly record struct UnderwaterAcousticShaft(
    short CurrentShaftRpm,
    short OrderedShaftRpm,
    float RpmRateOfChange)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteInt16(CurrentShaftRpm);
        w.WriteInt16(OrderedShaftRpm);
        w.WriteSingle(RpmRateOfChange);
    }

    internal static UnderwaterAcousticShaft Unmarshal(ref DisWireReader r) =>
        new(r.ReadInt16(), r.ReadInt16(), r.ReadSingle());
}

/// <summary>
/// APA (Additional Passive Activity) record (4 bytes). Ships with
/// Underwater Acoustic PDU to describe passive-sonar-detectable
/// activities beyond propulsion — hull slapping, towed arrays,
/// flow noise. IEEE 1278.1 §5.3.7.3.2.
/// </summary>
/// <param name="ParameterIndex">APA parameter identifier per SISO-REF-010.</param>
/// <param name="ParameterValue">APA parameter value — units depend on index.</param>
public readonly record struct UnderwaterAcousticApa(ushort ParameterIndex, short ParameterValue)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 4;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt16(ParameterIndex);
        w.WriteInt16(ParameterValue);
    }

    internal static UnderwaterAcousticApa Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt16(), r.ReadInt16());
}

/// <summary>
/// Underwater Acoustic Emitter System record (variable length, min
/// 20 bytes + beams). Describes one active sonar / echosounder /
/// comms emitter on the platform. IEEE 1278.1 §5.3.7.3.3.
/// </summary>
public sealed record UnderwaterAcousticEmitterSystem(
    byte NumberOfBeams,
    ushort AcousticEmitterSystemName,
    byte AcousticEmitterFunction,
    byte AcousticEmitterIdNumber,
    Vector3Float EmitterLocation,
    IReadOnlyList<UnderwaterAcousticBeam> Beams)
{
    /// <summary>
    /// Wire length in bytes: 20 fixed + 24 per beam. System-data-
    /// length field on the wire is measured in 32-bit words, so the
    /// minimum (20 B) is 5 words.
    /// </summary>
    public int WireLength => 20 + (Beams.Count * UnderwaterAcousticBeam.WireLength);

    internal void Marshal(ref DisWireWriter w)
    {
        // System data length is in 32-bit words.
        w.WriteByte((byte)(WireLength / 4));
        w.WriteByte(NumberOfBeams);
        w.WriteUInt16(0); // padding
        w.WriteUInt16(AcousticEmitterSystemName);
        w.WriteByte(AcousticEmitterFunction);
        w.WriteByte(AcousticEmitterIdNumber);
        EmitterLocation.Marshal(ref w);
        foreach (var beam in Beams) beam.Marshal(ref w);
    }

    internal static UnderwaterAcousticEmitterSystem Unmarshal(ref DisWireReader r)
    {
        var _systemDataLengthWords = r.ReadByte(); // in 32-bit words
        var numBeams = r.ReadByte();
        r.SkipPadding(2);
        var name = r.ReadUInt16();
        var function = r.ReadByte();
        var idNumber = r.ReadByte();
        var location = Vector3Float.Unmarshal(ref r);
        var beams = new List<UnderwaterAcousticBeam>(numBeams);
        for (var i = 0; i < numBeams; i++) beams.Add(UnderwaterAcousticBeam.Unmarshal(ref r));
        return new UnderwaterAcousticEmitterSystem(
            numBeams, name, function, idNumber, location, beams);
    }
}

/// <summary>
/// Underwater Acoustic Beam record (24 bytes). Describes one
/// steered acoustic beam within a UA emitter system.
/// IEEE 1278.1 §5.3.7.3.4.
/// </summary>
public readonly record struct UnderwaterAcousticBeam(
    byte BeamDataLength,
    byte BeamIdNumber,
    ushort AcousticBeamParameterIndex,
    ushort ActiveEmissionParameterIndex,
    ushort ScanPattern,
    float BeamCenterAzimuth,
    float AzimuthalBeamwidth,
    float BeamCenterDepressionElevation,
    float DepressionElevationBeamwidth)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 24;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte(BeamDataLength);
        w.WriteByte(BeamIdNumber);
        w.WriteUInt16(AcousticBeamParameterIndex);
        w.WriteUInt16(ActiveEmissionParameterIndex);
        w.WriteUInt16(ScanPattern);
        w.WriteSingle(BeamCenterAzimuth);
        w.WriteSingle(AzimuthalBeamwidth);
        w.WriteSingle(BeamCenterDepressionElevation);
        w.WriteSingle(DepressionElevationBeamwidth);
    }

    internal static UnderwaterAcousticBeam Unmarshal(ref DisWireReader r) =>
        new(
            r.ReadByte(), r.ReadByte(),
            r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(),
            r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}
