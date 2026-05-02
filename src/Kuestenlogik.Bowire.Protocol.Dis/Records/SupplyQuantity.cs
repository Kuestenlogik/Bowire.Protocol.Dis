// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Supply Quantity record (12 bytes). Used by Logistics family PDUs
/// (Service Request, Resupply Offer / Received / Cancel) to describe
/// one line item of supplies: the munition or fuel type as a DIS
/// entity-type seven-tuple, plus a quantity (rounds, kilograms,
/// litres — the type's usual unit). IEEE 1278.1 §5.2.37.
/// </summary>
/// <param name="SupplyType">Entity-type seven-tuple of the item being supplied.</param>
/// <param name="Quantity">Amount of this supply line — unit depends on the supply type.</param>
public readonly record struct SupplyQuantity(EntityType SupplyType, float Quantity)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 12;

    internal void Marshal(ref DisWireWriter w)
    {
        SupplyType.Marshal(ref w);
        w.WriteSingle(Quantity);
    }

    internal static SupplyQuantity Unmarshal(ref DisWireReader r)
    {
        var type = EntityType.Unmarshal(ref r);
        var quantity = r.ReadSingle();
        return new SupplyQuantity(type, quantity);
    }
}
