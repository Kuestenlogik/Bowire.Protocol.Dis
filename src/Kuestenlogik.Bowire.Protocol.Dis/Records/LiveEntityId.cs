// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Live Entity Id record (4 bytes). The Live Entity family uses a
/// compressed 4-byte id instead of the 6-byte
/// <see cref="EntityId"/> to save bandwidth over tactical-data-link
/// transports: 1 byte site, 1 byte application, 2 bytes entity.
/// IEEE 1278.1 §5.2.44.
/// </summary>
/// <param name="Site">Site id — 1 byte (0 = no site).</param>
/// <param name="Application">Application id — 1 byte (0 = no application).</param>
/// <param name="Entity">Entity number within the site / application — 2 bytes.</param>
public readonly record struct LiveEntityId(byte Site, byte Application, ushort Entity)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 4;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteByte(Site);
        w.WriteByte(Application);
        w.WriteUInt16(Entity);
    }

    internal static LiveEntityId Unmarshal(ref DisWireReader r) =>
        new(r.ReadByte(), r.ReadByte(), r.ReadUInt16());
}
