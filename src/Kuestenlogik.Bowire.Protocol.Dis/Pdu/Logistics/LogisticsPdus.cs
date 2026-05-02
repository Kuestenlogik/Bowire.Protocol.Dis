// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.Logistics;

// Shared helpers for the Logistics family — all PDUs share a similar
// "two entity ids + a small fixed block + zero or more SupplyQuantity
// records" shape.
internal static class LogisticsCodec
{
    internal static void WriteSupplies(ref DisWireWriter w, IReadOnlyList<SupplyQuantity>? supplies)
    {
        if (supplies is null) return;
        foreach (var s in supplies) s.Marshal(ref w);
    }

    internal static List<SupplyQuantity> ReadSupplies(ref DisWireReader r, byte count)
    {
        var list = new List<SupplyQuantity>(count);
        for (var i = 0; i < count; i++) list.Add(SupplyQuantity.Unmarshal(ref r));
        return list;
    }

    internal static int SuppliesWireLength(IReadOnlyList<SupplyQuantity>? supplies) =>
        (supplies?.Count ?? 0) * SupplyQuantity.WireLength;
}

// --- PDU: Service Request (5) ------------------------------------------------

/// <summary>
/// Service Request PDU (type 5, family 3). The requesting entity
/// asks the servicing entity for resupply or repair, with an
/// optional list of supply quantities. IEEE 1278.1 §5.3.5.1.
/// </summary>
public sealed record ServiceRequestPdu(
    PduHeader Header,
    EntityId RequestingEntityId,
    EntityId ServicingEntityId,
    ServiceTypeRequested ServiceType,
    IReadOnlyList<SupplyQuantity>? Supplies = null)
{
    /// <summary>Fixed wire length before the supply records.</summary>
    public const int MinimumWireLength = 28;

    /// <summary>Total wire length including supplies.</summary>
    public int WireLength => MinimumWireLength + LogisticsCodec.SuppliesWireLength(Supplies);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ServiceRequest,
            ProtocolFamily = DisProtocolFamily.Logistics,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);
        RequestingEntityId.Marshal(ref w);
        ServicingEntityId.Marshal(ref w);
        w.WriteByte((byte)ServiceType);
        w.WriteByte((byte)(Supplies?.Count ?? 0));
        w.WriteUInt16(0); // padding
        LogisticsCodec.WriteSupplies(ref w, Supplies);
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
    public static ServiceRequestPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var requesting = EntityId.Unmarshal(ref r);
        var servicing = EntityId.Unmarshal(ref r);
        var serviceType = (ServiceTypeRequested)r.ReadByte();
        var numSupplies = r.ReadByte();
        r.SkipPadding(2);
        var supplies = LogisticsCodec.ReadSupplies(ref r, numSupplies);
        return new ServiceRequestPdu(header, requesting, servicing, serviceType, supplies);
    }
}

// --- PDU: Resupply Offer (6) -------------------------------------------------

/// <summary>
/// Resupply Offer PDU (type 6, family 3). The supplying entity
/// offers resupply to the receiving entity. IEEE 1278.1 §5.3.5.2.
/// </summary>
public sealed record ResupplyOfferPdu(
    PduHeader Header,
    EntityId ReceivingEntityId,
    EntityId SupplyingEntityId,
    IReadOnlyList<SupplyQuantity>? Supplies = null)
{
    /// <summary>Fixed wire length before the supply records.</summary>
    public const int MinimumWireLength = 28;

    /// <summary>Total wire length including supplies.</summary>
    public int WireLength => MinimumWireLength + LogisticsCodec.SuppliesWireLength(Supplies);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ResupplyOffer,
            ProtocolFamily = DisProtocolFamily.Logistics,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        SupplyingEntityId.Marshal(ref w);
        w.WriteByte((byte)(Supplies?.Count ?? 0));
        w.WritePadding(3);
        LogisticsCodec.WriteSupplies(ref w, Supplies);
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
    public static ResupplyOfferPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var supplying = EntityId.Unmarshal(ref r);
        var numSupplies = r.ReadByte();
        r.SkipPadding(3);
        var supplies = LogisticsCodec.ReadSupplies(ref r, numSupplies);
        return new ResupplyOfferPdu(header, receiving, supplying, supplies);
    }
}

// --- PDU: Resupply Received (7) ----------------------------------------------

/// <summary>
/// Resupply Received PDU (type 7, family 3). The receiving entity
/// confirms that supplies have been received. IEEE 1278.1 §5.3.5.3.
/// </summary>
public sealed record ResupplyReceivedPdu(
    PduHeader Header,
    EntityId ReceivingEntityId,
    EntityId SupplyingEntityId,
    IReadOnlyList<SupplyQuantity>? Supplies = null)
{
    /// <summary>Fixed wire length before the supply records.</summary>
    public const int MinimumWireLength = 28;

    /// <summary>Total wire length including supplies.</summary>
    public int WireLength => MinimumWireLength + LogisticsCodec.SuppliesWireLength(Supplies);

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ResupplyReceived,
            ProtocolFamily = DisProtocolFamily.Logistics,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        SupplyingEntityId.Marshal(ref w);
        w.WriteByte((byte)(Supplies?.Count ?? 0));
        w.WritePadding(3);
        LogisticsCodec.WriteSupplies(ref w, Supplies);
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
    public static ResupplyReceivedPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var supplying = EntityId.Unmarshal(ref r);
        var numSupplies = r.ReadByte();
        r.SkipPadding(3);
        var supplies = LogisticsCodec.ReadSupplies(ref r, numSupplies);
        return new ResupplyReceivedPdu(header, receiving, supplying, supplies);
    }
}

// --- PDU: Resupply Cancel (8) ------------------------------------------------

/// <summary>
/// Resupply Cancel PDU (type 8, family 3). Cancels a resupply offer
/// or request already in progress. IEEE 1278.1 §5.3.5.4.
/// </summary>
public sealed record ResupplyCancelPdu(
    PduHeader Header,
    EntityId ReceivingEntityId,
    EntityId SupplyingEntityId)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 24;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.ResupplyCancel,
            ProtocolFamily = DisProtocolFamily.Logistics,
            Length = WireLength,
        };
        header.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        SupplyingEntityId.Marshal(ref w);
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
    public static ResupplyCancelPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var supplying = EntityId.Unmarshal(ref r);
        return new ResupplyCancelPdu(header, receiving, supplying);
    }
}

// --- PDU: Repair Complete (9) ------------------------------------------------

/// <summary>
/// Repair Complete PDU (type 9, family 3). The repairing entity
/// reports that a repair has been performed on the receiving entity.
/// IEEE 1278.1 §5.3.5.5.
/// </summary>
public sealed record RepairCompletePdu(
    PduHeader Header,
    EntityId ReceivingEntityId,
    EntityId RepairingEntityId,
    RepairCode Repair)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 28;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.RepairComplete,
            ProtocolFamily = DisProtocolFamily.Logistics,
            Length = WireLength,
        };
        header.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        RepairingEntityId.Marshal(ref w);
        w.WriteUInt16((ushort)Repair);
        w.WriteUInt16(0); // padding
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
    public static RepairCompletePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var repairing = EntityId.Unmarshal(ref r);
        var repair = (RepairCode)r.ReadUInt16();
        r.SkipPadding(2);
        return new RepairCompletePdu(header, receiving, repairing, repair);
    }
}

// --- PDU: Repair Response (10) -----------------------------------------------

/// <summary>
/// Repair Response PDU (type 10, family 3). The receiving entity
/// acknowledges or rejects a repair. IEEE 1278.1 §5.3.5.6.
/// </summary>
public sealed record RepairResponsePdu(
    PduHeader Header,
    EntityId ReceivingEntityId,
    EntityId RepairingEntityId,
    RepairResult RepairResult)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 28;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.RepairResponse,
            ProtocolFamily = DisProtocolFamily.Logistics,
            Length = WireLength,
        };
        header.Marshal(ref w);
        ReceivingEntityId.Marshal(ref w);
        RepairingEntityId.Marshal(ref w);
        w.WriteByte((byte)RepairResult);
        w.WritePadding(3);
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
    public static RepairResponsePdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var receiving = EntityId.Unmarshal(ref r);
        var repairing = EntityId.Unmarshal(ref r);
        var result = (RepairResult)r.ReadByte();
        r.SkipPadding(3);
        return new RepairResponsePdu(header, receiving, repairing, result);
    }
}
