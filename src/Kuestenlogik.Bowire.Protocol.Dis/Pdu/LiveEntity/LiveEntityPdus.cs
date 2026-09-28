// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.LiveEntity;

// Base container for the Live Entity family's PDU layout:
// Header + LiveEntityId + payload bytes.
//
// TSPI, Appearance, LE Fire and LE Detonation pack their fields behind flag
// bytes. The payload bytes are what these records write back, so a PDU
// round-trips byte for byte whatever it carries; each record also offers a
// typed view on them (LiveEntityFields, #26) that is null whenever the bytes
// do not read cleanly.
internal static class LiveEntityCodec
{
    internal static int Marshal(
        Span<byte> destination,
        PduHeader header,
        DisPduType pduType,
        LiveEntityId entityId,
        byte[] payload)
    {
        var w = new DisWireWriter(destination);
        var rewritten = header with
        {
            PduType = pduType,
            ProtocolFamily = DisProtocolFamily.LiveEntity,
            Length = (ushort)(PduHeader.WireLength + LiveEntityId.WireLength + payload.Length),
        };
        rewritten.Marshal(ref w);
        entityId.Marshal(ref w);
        w.WriteBytes(payload);
        return w.Offset;
    }

    internal static (PduHeader Header, LiveEntityId EntityId, byte[] Payload)
        Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var entityId = LiveEntityId.Unmarshal(ref r);
        var payloadLength = Math.Max(0, header.Length - PduHeader.WireLength - LiveEntityId.WireLength);
        var payload = r.ReadBytes(payloadLength).ToArray();
        return (header, entityId, payload);
    }
}

// --- PDU: TSPI (46) ----------------------------------------------------------

/// <summary>
/// Time Space Position Information PDU (type 46, family 11). The
/// Live Entity family's compressed equivalent of Entity State —
/// reports the position / velocity / orientation of a live-range
/// entity using bit-packed fields to fit inside tactical-data-link
/// bandwidth budgets. IEEE 1278.1 §5.3.13.1.
/// </summary>
/// <remarks>
/// <see cref="Payload"/> is written back as it is; <see cref="Fields"/>
/// reads it (#26).
/// </remarks>
public sealed record TimeSpacePositionInformationPdu(
    PduHeader Header,
    LiveEntityId LiveEntityId,
    byte[] Payload)
{
    /// <summary>Wire length before the payload.</summary>
    public const int MinimumWireLength = PduHeader.WireLength + LiveEntityId.WireLength;

    /// <summary>
    /// The payload's fields, read per its flag bytes (#26) — null when the
    /// payload does not add up to what the flags announce. <see cref="Payload"/>
    /// is what is written back either way.
    /// </summary>
    public TspiFields? Fields => LiveEntityFields.TryDecodeTspi(Payload);

    /// <summary>Total wire length including the payload.</summary>
    public int WireLength => MinimumWireLength + Payload.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination) =>
        LiveEntityCodec.Marshal(destination, Header, DisPduType.TimeSpacePositionInformation, LiveEntityId, Payload);

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static TimeSpacePositionInformationPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var (header, entityId, payload) = LiveEntityCodec.Unmarshal(source);
        return new TimeSpacePositionInformationPdu(header, entityId, payload);
    }
}

// --- PDU: Appearance (47) ----------------------------------------------------

/// <summary>
/// Appearance PDU (type 47, family 11). Live-entity equivalent of
/// the Entity State appearance word — reports visual state changes
/// (lights, damage, smoke) for a live-range entity.
/// IEEE 1278.1 §5.3.13.2.
/// </summary>
public sealed record AppearancePdu(
    PduHeader Header,
    LiveEntityId LiveEntityId,
    byte[] Payload)
{
    /// <summary>Wire length before the payload.</summary>
    public const int MinimumWireLength = PduHeader.WireLength + LiveEntityId.WireLength;

    /// <summary>
    /// The payload's fields, read per its flag bytes (#26) — null when the
    /// payload does not add up to what the flags announce. <see cref="Payload"/>
    /// is what is written back either way.
    /// </summary>
    public LeAppearanceFields? Fields => LiveEntityFields.TryDecodeAppearance(Payload);

    /// <summary>Total wire length including the payload.</summary>
    public int WireLength => MinimumWireLength + Payload.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination) =>
        LiveEntityCodec.Marshal(destination, Header, DisPduType.Appearance, LiveEntityId, Payload);

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static AppearancePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var (header, entityId, payload) = LiveEntityCodec.Unmarshal(source);
        return new AppearancePdu(header, entityId, payload);
    }
}

// --- PDU: Articulated Parts (48) --------------------------------------------

/// <summary>
/// Articulated Parts PDU (type 48, family 11). Reports the current
/// state of one or more articulated parts on a live-range entity
/// (turret azimuth, hatch position, gear, ...). Compressed format
/// per IEEE 1278.1 §5.3.13.3.
/// </summary>
/// <remarks>
/// Unlike the other compressed Live Entity PDUs, this one carries a
/// count-prefixed list of <see cref="VariableParameter"/>s (the same
/// 16-byte records used by Entity State), not a flag-gated bit-packed
/// payload — so the codec exposes them fully typed.
/// </remarks>
public sealed record ArticulatedPartsPdu(
    PduHeader Header,
    LiveEntityId LiveEntityId,
    IReadOnlyList<VariableParameter> VariableParameters)
{
    /// <summary>Wire length before the variable-parameters list (header + LiveEntityId + count byte).</summary>
    public const int MinimumWireLength = PduHeader.WireLength + LiveEntityId.WireLength + 1;

    /// <summary>Total wire length including all typed variable parameters (16 bytes each).</summary>
    public int WireLength => MinimumWireLength + (VariableParameters.Count * VariableParameter.WireLength);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ArticulatedParts,
            ProtocolFamily = DisProtocolFamily.LiveEntity,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);
        LiveEntityId.Marshal(ref w);
        w.WriteByte((byte)VariableParameters.Count);
        foreach (var vp in VariableParameters) vp.Marshal(ref w);
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
    public static ArticulatedPartsPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var entityId = LiveEntityId.Unmarshal(ref r);
        var count = r.ReadByte();
        var parameters = new List<VariableParameter>(count);
        for (var i = 0; i < count; i++) parameters.Add(VariableParameter.Unmarshal(ref r));
        return new ArticulatedPartsPdu(header, entityId, parameters);
    }
}

// --- PDU: LE Fire (49) ------------------------------------------------------

/// <summary>
/// Live Entity Fire PDU (type 49, family 11). Compressed Fire PDU
/// for live-range exercises. IEEE 1278.1 §5.3.13.4.
/// </summary>
public sealed record LiveEntityFirePdu(
    PduHeader Header,
    LiveEntityId LiveEntityId,
    byte[] Payload)
{
    /// <summary>Wire length before the payload.</summary>
    public const int MinimumWireLength = PduHeader.WireLength + LiveEntityId.WireLength;

    /// <summary>
    /// The payload's fields, read per its flag bytes (#26) — null when the
    /// payload does not add up to what the flags announce. <see cref="Payload"/>
    /// is what is written back either way.
    /// </summary>
    public LeFireFields? Fields => LiveEntityFields.TryDecodeFire(Payload, LiveEntityId);

    /// <summary>Total wire length including the payload.</summary>
    public int WireLength => MinimumWireLength + Payload.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination) =>
        LiveEntityCodec.Marshal(destination, Header, DisPduType.LiveEntityFire, LiveEntityId, Payload);

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static LiveEntityFirePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var (header, entityId, payload) = LiveEntityCodec.Unmarshal(source);
        return new LiveEntityFirePdu(header, entityId, payload);
    }
}

// --- PDU: LE Detonation (50) ------------------------------------------------

/// <summary>
/// Live Entity Detonation PDU (type 50, family 11). Compressed
/// Detonation PDU for live-range exercises.
/// IEEE 1278.1 §5.3.13.5.
/// </summary>
public sealed record LiveEntityDetonationPdu(
    PduHeader Header,
    LiveEntityId LiveEntityId,
    byte[] Payload)
{
    /// <summary>Wire length before the payload.</summary>
    public const int MinimumWireLength = PduHeader.WireLength + LiveEntityId.WireLength;

    /// <summary>
    /// The payload's fields, read per its flag bytes (#26) — null when the
    /// payload does not add up to what the flags announce. <see cref="Payload"/>
    /// is what is written back either way.
    /// </summary>
    public LeDetonationFields? Fields => LiveEntityFields.TryDecodeDetonation(Payload, LiveEntityId);

    /// <summary>Total wire length including the payload.</summary>
    public int WireLength => MinimumWireLength + Payload.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination) =>
        LiveEntityCodec.Marshal(destination, Header, DisPduType.LiveEntityDetonation, LiveEntityId, Payload);

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static LiveEntityDetonationPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var (header, entityId, payload) = LiveEntityCodec.Unmarshal(source);
        return new LiveEntityDetonationPdu(header, entityId, payload);
    }
}
