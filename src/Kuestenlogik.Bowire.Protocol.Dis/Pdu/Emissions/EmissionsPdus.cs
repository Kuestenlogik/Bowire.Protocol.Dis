// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.Emissions;

// --- PDU: Electromagnetic Emission (23) --------------------------------------

/// <summary>
/// Electromagnetic Emission PDU (type 23, family 6). Describes the
/// electromagnetic emissions (radar, jammers, ESM, comms) an entity
/// currently emits: one or more emitter systems, each with one or
/// more beams, each with a track/jam target list.
/// IEEE 1278.1 §5.3.7.1.
/// </summary>
public sealed record ElectromagneticEmissionPdu(
    PduHeader Header,
    EntityId EmittingEntityId,
    EventId EventId,
    byte StateUpdateIndicator,
    IReadOnlyList<ElectromagneticEmissionSystem> Systems)
{
    /// <summary>Fixed wire length before the emitter-system list.</summary>
    public const int MinimumWireLength = 28;

    /// <summary>Total wire length including systems.</summary>
    public int WireLength
    {
        get
        {
            var total = MinimumWireLength;
            foreach (var s in Systems) total += s.WireLength;
            return total;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ElectromagneticEmission,
            ProtocolFamily = DisProtocolFamily.DistributedEmissionRegeneration,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        EmittingEntityId.Marshal(ref w);
        EventId.Marshal(ref w);
        w.WriteByte(StateUpdateIndicator);
        w.WriteByte((byte)Systems.Count);
        w.WriteUInt16(0); // padding
        foreach (var system in Systems) system.Marshal(ref w);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static ElectromagneticEmissionPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var emittingEntityId = EntityId.Unmarshal(ref r);
        var eventId = Records.EventId.Unmarshal(ref r);
        var stateUpdate = r.ReadByte();
        var numSystems = r.ReadByte();
        r.SkipPadding(2);
        var systems = new List<ElectromagneticEmissionSystem>(numSystems);
        for (var i = 0; i < numSystems; i++)
            systems.Add(ElectromagneticEmissionSystem.Unmarshal(ref r));
        return new ElectromagneticEmissionPdu(header, emittingEntityId, eventId, stateUpdate, systems);
    }
}

// --- PDU: Designator (24) ----------------------------------------------------

/// <summary>
/// Designator PDU (type 24, family 6). Reports the state of a laser
/// designator — what entity is designating, what entity is being
/// designated, designator power + wavelength, spot location, and
/// the dead-reckoning terms for receivers that want to extrapolate
/// the spot between updates. IEEE 1278.1 §5.3.7.2.
/// </summary>
public sealed record DesignatorPdu(
    PduHeader Header,
    EntityId DesignatingEntityId,
    ushort CodeName,
    EntityId DesignatedEntityId,
    ushort DesignatorCode,
    float DesignatorPower,
    float DesignatorWavelength,
    Vector3Float DesignatorSpotWrtDesignated,
    Vector3Double DesignatorSpotLocation,
    DeadReckoningAlgorithm DeadReckoningAlgorithm,
    Vector3Float EntityLinearAcceleration)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 88;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.Designator,
            ProtocolFamily = DisProtocolFamily.DistributedEmissionRegeneration,
            Length = WireLength,
        };
        header.Marshal(ref w);

        DesignatingEntityId.Marshal(ref w);
        w.WriteUInt16(CodeName);
        DesignatedEntityId.Marshal(ref w);
        w.WriteUInt16(DesignatorCode);
        w.WriteSingle(DesignatorPower);
        w.WriteSingle(DesignatorWavelength);
        DesignatorSpotWrtDesignated.Marshal(ref w);
        DesignatorSpotLocation.Marshal(ref w);
        w.WriteByte((byte)DeadReckoningAlgorithm);
        w.WritePadding(3);
        EntityLinearAcceleration.Marshal(ref w);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static DesignatorPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var designating = EntityId.Unmarshal(ref r);
        var codeName = r.ReadUInt16();
        var designated = EntityId.Unmarshal(ref r);
        var designatorCode = r.ReadUInt16();
        var designatorPower = r.ReadSingle();
        var designatorWavelength = r.ReadSingle();
        var spotWrt = Vector3Float.Unmarshal(ref r);
        var spotLocation = Vector3Double.Unmarshal(ref r);
        var drAlgorithm = (DeadReckoningAlgorithm)r.ReadByte();
        r.SkipPadding(3);
        var linearAccel = Vector3Float.Unmarshal(ref r);
        return new DesignatorPdu(
            header, designating, codeName, designated, designatorCode,
            designatorPower, designatorWavelength,
            spotWrt, spotLocation, drAlgorithm, linearAccel);
    }
}

// --- PDU: Underwater Acoustic (29) -------------------------------------------

/// <summary>
/// Underwater Acoustic PDU (type 29, family 6). Reports the sonar
/// state of a subsurface or surface platform — shafts, acoustic
/// parameters, and emitter systems. IEEE 1278.1 §5.3.7.3.
/// </summary>
public sealed record UnderwaterAcousticPdu(
    PduHeader Header,
    EntityId EmittingEntityId,
    EventId EventId,
    byte StateChangeIndicator,
    ushort PassiveParameterIndex,
    byte PropulsionPlantConfiguration,
    IReadOnlyList<UnderwaterAcousticShaft> Shafts,
    IReadOnlyList<UnderwaterAcousticApa> Apas,
    IReadOnlyList<UnderwaterAcousticEmitterSystem> EmitterSystems)
{
    /// <summary>Fixed wire length before the shaft / APA / emitter-system lists.</summary>
    public const int MinimumWireLength = 32;

    /// <summary>Total wire length including all sub-records.</summary>
    public int WireLength
    {
        get
        {
            var total = MinimumWireLength
                + (Shafts.Count * UnderwaterAcousticShaft.WireLength)
                + (Apas.Count * UnderwaterAcousticApa.WireLength);
            foreach (var sys in EmitterSystems) total += sys.WireLength;
            return total;
        }
    }

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.UnderwaterAcoustic,
            ProtocolFamily = DisProtocolFamily.DistributedEmissionRegeneration,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        EmittingEntityId.Marshal(ref w);
        EventId.Marshal(ref w);
        w.WriteByte(StateChangeIndicator);
        w.WriteByte(0); // padding
        w.WriteUInt16(PassiveParameterIndex);
        w.WriteByte(PropulsionPlantConfiguration);
        w.WriteByte((byte)Shafts.Count);
        w.WriteByte((byte)Apas.Count);
        w.WriteByte((byte)EmitterSystems.Count);
        foreach (var shaft in Shafts) shaft.Marshal(ref w);
        foreach (var apa in Apas) apa.Marshal(ref w);
        foreach (var sys in EmitterSystems) sys.Marshal(ref w);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static UnderwaterAcousticPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var emittingEntityId = EntityId.Unmarshal(ref r);
        var eventId = Records.EventId.Unmarshal(ref r);
        var stateChange = r.ReadByte();
        r.SkipPadding(1);
        var passiveParam = r.ReadUInt16();
        var propulsion = r.ReadByte();
        var numShafts = r.ReadByte();
        var numApas = r.ReadByte();
        var numUaEmitters = r.ReadByte();

        var shafts = new List<UnderwaterAcousticShaft>(numShafts);
        for (var i = 0; i < numShafts; i++) shafts.Add(UnderwaterAcousticShaft.Unmarshal(ref r));
        var apas = new List<UnderwaterAcousticApa>(numApas);
        for (var i = 0; i < numApas; i++) apas.Add(UnderwaterAcousticApa.Unmarshal(ref r));
        var emitterSystems = new List<UnderwaterAcousticEmitterSystem>(numUaEmitters);
        for (var i = 0; i < numUaEmitters; i++)
            emitterSystems.Add(UnderwaterAcousticEmitterSystem.Unmarshal(ref r));

        return new UnderwaterAcousticPdu(
            header, emittingEntityId, eventId, stateChange, passiveParam,
            propulsion, shafts, apas, emitterSystems);
    }
}

// --- PDU: Supplemental Emission/Entity State (30, V7) ------------------------

/// <summary>
/// Supplemental Emission/Entity State PDU (type 30, family 6, V7).
/// Extra signature-index metadata + propulsion / vectoring-nozzle
/// state alongside the regular Entity State updates. Exists so new
/// sensor models can share high-fidelity data without exploding the
/// core Entity State PDU's size. IEEE 1278.1-2012 §7.3.8.4.
/// </summary>
/// <remarks>
/// <para>
/// Layout (28 bytes fixed + propulsion systems (8 B each) +
/// vectoring nozzle systems (8 B each)):
/// </para>
/// <list type="bullet">
///   <item>0  : 12 — <see cref="PduHeader"/></item>
///   <item>12 :  6 — <see cref="OriginatingEntityId"/></item>
///   <item>18 :  2 — <see cref="InfraredSignatureIndex"/></item>
///   <item>20 :  2 — <see cref="AcousticSignatureIndex"/></item>
///   <item>22 :  2 — <see cref="RadarCrossSectionSignatureIndex"/></item>
///   <item>24 :  2 — number of propulsion systems</item>
///   <item>26 :  2 — number of vectoring-nozzle systems</item>
///   <item>28 :  N×8 — propulsion systems</item>
///   <item>... :  M×8 — vectoring nozzle systems</item>
/// </list>
/// </remarks>
public sealed record SupplementalEmissionEntityStatePdu(
    PduHeader Header,
    EntityId OriginatingEntityId,
    ushort InfraredSignatureIndex,
    ushort AcousticSignatureIndex,
    ushort RadarCrossSectionSignatureIndex,
    IReadOnlyList<PropulsionSystem>? PropulsionSystems = null,
    IReadOnlyList<VectoringNozzleSystem>? VectoringNozzleSystems = null)
{
    /// <summary>Fixed wire length before the systems lists.</summary>
    public const int MinimumWireLength = 28;

    /// <summary>Total wire length including systems.</summary>
    public int WireLength =>
        MinimumWireLength
        + ((PropulsionSystems?.Count ?? 0) * PropulsionSystem.WireLength)
        + ((VectoringNozzleSystems?.Count ?? 0) * VectoringNozzleSystem.WireLength);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.SupplementalEmissionEntityState,
            ProtocolFamily = DisProtocolFamily.DistributedEmissionRegeneration,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        OriginatingEntityId.Marshal(ref w);
        w.WriteUInt16(InfraredSignatureIndex);
        w.WriteUInt16(AcousticSignatureIndex);
        w.WriteUInt16(RadarCrossSectionSignatureIndex);
        w.WriteUInt16((ushort)(PropulsionSystems?.Count ?? 0));
        w.WriteUInt16((ushort)(VectoringNozzleSystems?.Count ?? 0));
        if (PropulsionSystems is not null)
            foreach (var ps in PropulsionSystems) ps.Marshal(ref w);
        if (VectoringNozzleSystems is not null)
            foreach (var vns in VectoringNozzleSystems) vns.Marshal(ref w);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static SupplementalEmissionEntityStatePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var originating = EntityId.Unmarshal(ref r);
        var irIndex = r.ReadUInt16();
        var acousticIndex = r.ReadUInt16();
        var rcsIndex = r.ReadUInt16();
        var numPropulsion = r.ReadUInt16();
        var numVectoring = r.ReadUInt16();

        var propulsionSystems = new List<PropulsionSystem>(numPropulsion);
        for (var i = 0; i < numPropulsion; i++)
            propulsionSystems.Add(PropulsionSystem.Unmarshal(ref r));
        var vectoringSystems = new List<VectoringNozzleSystem>(numVectoring);
        for (var i = 0; i < numVectoring; i++)
            vectoringSystems.Add(VectoringNozzleSystem.Unmarshal(ref r));

        return new SupplementalEmissionEntityStatePdu(
            header, originating, irIndex, acousticIndex, rcsIndex,
            propulsionSystems, vectoringSystems);
    }
}
