// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis;

/// <summary>
/// Replay-time changes to a captured PDU (#24): re-scope a capture to another
/// exercise, stamp it with the time it is sent, or keep only some PDU types
/// or entities — without editing raw bytes in the recording.
/// </summary>
/// <remarks>
/// <para>
/// Opt-in, read from the first DIS step's metadata next to the network keys.
/// A recording without any of these keys replays byte for byte, as it always
/// did; <see cref="Parse"/> returns null for it and the emitter never touches
/// the bytes.
/// </para>
/// <list type="bullet">
///   <item><c>exercise-id</c>: 1–255, written to header byte 1.</item>
///   <item><c>pdu-types</c>: comma-separated <see cref="DisPduType"/> names or
///   numeric ids; every other PDU is not sent.</item>
///   <item><c>entities</c>: comma-separated <c>site:app:entity</c> triples; a
///   PDU is sent when it names one of them in any role (the same rule as an
///   entity stream). A PDU that does not decode names no entity and is not
///   sent.</item>
///   <item><c>retime</c>: <c>now</c> — the header timestamp becomes the wall
///   clock at send time, in DIS timestamp units. Whether the PDU carried an
///   absolute or a relative timestamp (bit 0) is kept.</item>
/// </list>
/// <para>
/// Only the header is rewritten; the body goes out as captured. A misspelt
/// key value fails the start rather than filtering everything away quietly.
/// </para>
/// </remarks>
internal sealed record DisReplayRewrite(
    byte? ExerciseId,
    IReadOnlySet<DisPduType>? PduTypes,
    IReadOnlySet<EntityId>? Entities,
    bool Retime)
{
    internal const string ExerciseIdKey = "exercise-id";
    internal const string PduTypesKey = "pdu-types";
    internal const string EntitiesKey = "entities";
    internal const string RetimeKey = "retime";

    /// <summary>
    /// The rewrite the metadata asks for, or null when it asks for none.
    /// </summary>
    /// <exception cref="FormatException">A key is present but its value is not understood.</exception>
    internal static DisReplayRewrite? Parse(IDictionary<string, string>? metadata)
    {
        if (metadata is null) return null;

        byte? exercise = null;
        if (metadata.TryGetValue(ExerciseIdKey, out var ex))
        {
            if (!byte.TryParse(ex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) || b == 0)
                throw new FormatException($"'{ExerciseIdKey}' must be 1-255, got '{ex}'.");
            exercise = b;
        }

        HashSet<DisPduType>? types = null;
        if (metadata.TryGetValue(PduTypesKey, out var tv))
        {
            types = [];
            foreach (var token in Tokens(tv))
            {
                // A name or an id, but one the enum knows: a typo that parsed
                // to an undefined number would filter every PDU away.
                DisPduType type;
                if (byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    type = (DisPduType)id;
                else if (!char.IsDigit(token[0]) && Enum.TryParse(token, ignoreCase: true, out DisPduType named))
                    type = named;
                else
                    throw new FormatException($"'{PduTypesKey}': '{token}' is not a DIS PDU type.");
                if (!Enum.IsDefined(type) || type == DisPduType.Other)
                    throw new FormatException($"'{PduTypesKey}': '{token}' is not a DIS PDU type.");
                types.Add(type);
            }
            if (types.Count == 0) throw new FormatException($"'{PduTypesKey}' names no PDU type.");
        }

        HashSet<EntityId>? entities = null;
        if (metadata.TryGetValue(EntitiesKey, out var ev))
        {
            entities = [];
            foreach (var token in Tokens(ev))
            {
                var id = BowireDisProtocol.TryParseEntityServiceName(token)
                    ?? throw new FormatException($"'{EntitiesKey}': '{token}' is not a site:app:entity triple.");
                entities.Add(id);
            }
            if (entities.Count == 0) throw new FormatException($"'{EntitiesKey}' names no entity.");
        }

        var retime = false;
        if (metadata.TryGetValue(RetimeKey, out var rt))
        {
            if (!string.Equals(rt.Trim(), "now", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"'{RetimeKey}' understands 'now', got '{rt}'.");
            retime = true;
        }

        return exercise is null && types is null && entities is null && !retime
            ? null
            : new DisReplayRewrite(exercise, types, entities, retime);
    }

    private static string[] Tokens(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The bytes to send for one captured PDU, or null when the filters drop
    /// it. The input is not modified.
    /// </summary>
    internal byte[]? Apply(byte[] pdu, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pdu);
        if (pdu.Length < PduHeader.WireLength)
            // Too short to carry a header: nothing to rewrite, and nothing a
            // filter could match.
            return PduTypes is null && Entities is null ? pdu : null;

        var reader = new DisWireReader(pdu);
        var header = PduHeader.Unmarshal(ref reader);

        if (PduTypes is not null && !PduTypes.Contains(header.PduType)) return null;

        if (Entities is not null)
        {
            var decoded = DisPduDecoder.TryDecode(pdu);
            if (decoded is null) return null;
            try
            {
                if (!DisPduDecoder.RelatedEntities(decoded).Any(Entities.Contains)) return null;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
        }

        if (ExerciseId is null && !Retime) return pdu;

        var rewritten = header;
        if (ExerciseId is { } exercise) rewritten = rewritten with { ExerciseId = exercise };
        if (Retime) rewritten = rewritten with { Timestamp = Timestamp(now, absolute: (header.Timestamp & 1) == 1) };

        var copy = (byte[])pdu.Clone();
        var writer = new DisWireWriter(copy.AsSpan(0, PduHeader.WireLength));
        rewritten.Marshal(ref writer);
        return copy;
    }

    /// <summary>
    /// A DIS timestamp: the time past the hour scaled to 0..2^31-1, shifted
    /// left one bit, bit 0 set for absolute and clear for relative — the
    /// encoding open-dis's <c>DisTime</c> uses.
    /// </summary>
    internal static uint Timestamp(DateTimeOffset now, bool absolute)
    {
        var utc = now.UtcDateTime;
        var pastHourMs = (utc - new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        var units = (uint)(pastHourMs / 3_600_000.0 * int.MaxValue);
        var shifted = units << 1;
        return absolute ? shifted | 1u : shifted & 0xFFFFFFFEu;
    }
}
