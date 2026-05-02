// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu;

/// <summary>
/// Directed Energy Fire PDU (type 68, family 2, V7). Reports a
/// directed-energy weapon firing event — lasers, high-power
/// microwave emitters, the wider class of non-kinetic weapons the
/// 2012 revision introduced to DIS. IEEE 1278.1-2012 §7.3.4.
/// </summary>
/// <remarks>
/// <para>
/// Layout (96 bytes fixed, then a sequence of DE Record Sets):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="FiringEntityId"/></item>
///   <item>18 :  6 — <see cref="EventId"/></item>
///   <item>24 :  8 — <see cref="MunitionType"/></item>
///   <item>32 :  8 — <see cref="ShotStartTime"/></item>
///   <item>40 :  4 — <see cref="CumulativeShotTime"/></item>
///   <item>44 : 12 — <see cref="ApertureEmitterLocation"/></item>
///   <item>56 :  4 — <see cref="ApertureDiameter"/></item>
///   <item>60 :  4 — <see cref="Wavelength"/></item>
///   <item>64 :  4 — reserved padding</item>
///   <item>68 :  4 — <see cref="PulseRepetitionFrequency"/></item>
///   <item>72 :  4 — <see cref="PulseWidth"/></item>
///   <item>76 :  2 — <see cref="Flags"/></item>
///   <item>78 :  1 — <see cref="PulseShape"/></item>
///   <item>79 :  1 — reserved padding</item>
///   <item>80 :  4 — reserved padding</item>
///   <item>84 :  4 — reserved padding</item>
///   <item>88 :  2 — reserved padding</item>
///   <item>90 :  2 — <see cref="DeRecords"/> count</item>
///   <item>92 :  4 — reserved padding (trailing align)</item>
///   <item>96 :  N — DE record sets (<see cref="StandardVariableRecord"/> per §6.2.82)</item>
/// </list>
/// <para>
/// The DE record payloads are exposed as a list of
/// <see cref="StandardVariableRecord"/>s; per-record-type field
/// shapes (beam records, target records, area records) are decided by
/// the <see cref="StandardVariableRecord.RecordType"/> code per
/// SISO-REF-010.
/// </para>
/// </remarks>
public sealed record DirectedEnergyFirePdu(
    PduHeader Header,
    EntityId FiringEntityId,
    EventId EventId,
    EntityType MunitionType,
    ClockTime ShotStartTime,
    float CumulativeShotTime,
    Vector3Float ApertureEmitterLocation,
    float ApertureDiameter,
    float Wavelength,
    float PulseRepetitionFrequency,
    float PulseWidth,
    ushort Flags,
    byte PulseShape,
    IReadOnlyList<StandardVariableRecord> DeRecords)
{
    /// <summary>Fixed wire length in bytes before the DE record set blob.</summary>
    public const int MinimumWireLength = 96;

    /// <summary>Wire length including every DE record (each padded to 64-bit).</summary>
    public int WireLength
    {
        get
        {
            var sum = MinimumWireLength;
            foreach (var record in DeRecords) sum += record.WireLength;
            return sum;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>; returns bytes written.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var length = WireLength;
        var header = Header with
        {
            PduType = DisPduType.DirectedEnergyFire,
            ProtocolFamily = DisProtocolFamily.Warfare,
            Length = (ushort)length,
        };
        header.Marshal(ref w);

        FiringEntityId.Marshal(ref w);
        EventId.Marshal(ref w);
        MunitionType.Marshal(ref w);
        ShotStartTime.Marshal(ref w);
        w.WriteSingle(CumulativeShotTime);
        ApertureEmitterLocation.Marshal(ref w);
        w.WriteSingle(ApertureDiameter);
        w.WriteSingle(Wavelength);
        w.WritePadding(4);
        w.WriteSingle(PulseRepetitionFrequency);
        w.WriteSingle(PulseWidth);
        w.WriteUInt16(Flags);
        w.WriteByte(PulseShape);
        w.WritePadding(1);
        w.WritePadding(4);
        w.WritePadding(4);
        w.WritePadding(2);
        w.WriteUInt16((ushort)DeRecords.Count);
        w.WritePadding(4);
        foreach (var record in DeRecords) record.Marshal(ref w);

        return w.Offset;
    }

    /// <summary>Allocation-included shortcut; returns a <see cref="WireLength"/>-byte array.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static DirectedEnergyFirePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var firing = EntityId.Unmarshal(ref r);
        var evt = Records.EventId.Unmarshal(ref r);
        var munitionType = EntityType.Unmarshal(ref r);
        var shotStart = ClockTime.Unmarshal(ref r);
        var cumulativeShot = r.ReadSingle();
        var apertureLocation = Vector3Float.Unmarshal(ref r);
        var apertureDiameter = r.ReadSingle();
        var wavelength = r.ReadSingle();
        r.SkipPadding(4);
        var pulseRepFreq = r.ReadSingle();
        var pulseWidth = r.ReadSingle();
        var flags = r.ReadUInt16();
        var pulseShape = r.ReadByte();
        r.SkipPadding(1);
        r.SkipPadding(4);
        r.SkipPadding(4);
        r.SkipPadding(2);
        var numDeRecords = r.ReadUInt16();
        r.SkipPadding(4);

        var records = new List<StandardVariableRecord>(numDeRecords);
        for (var i = 0; i < numDeRecords; i++)
            records.Add(StandardVariableRecord.Unmarshal(ref r));

        return new DirectedEnergyFirePdu(
            header, firing, evt, munitionType, shotStart, cumulativeShot,
            apertureLocation, apertureDiameter, wavelength,
            pulseRepFreq, pulseWidth, flags, pulseShape,
            records);
    }
}
