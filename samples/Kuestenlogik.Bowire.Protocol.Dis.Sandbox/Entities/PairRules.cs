// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

/// <summary>
/// Which key-value pairs can be sent, and how large the Comment PDU that
/// carries them gets. The length is checked against what the encoder
/// actually writes in the publisher's tests.
/// </summary>
internal static class PairRules
{
    /// <summary>Keeps every Comment PDU in one UDP datagram without fragmentation.</summary>
    public const int MaxCommentPduBytes = 1400;

    // PDU header 12, originating and receiving id 6 + 6, two record counts 4 + 4.
    private const int CommentFixedBytes = 32;

    // Datum id 4, datum length 4.
    private const int DatumHeaderBytes = 8;

    public static string Text(KeyValuePair<string, string> pair) => $"{pair.Key}={pair.Value}";

    public static int CommentPduLength(IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        var total = CommentFixedBytes;
        foreach (var pair in pairs)
        {
            var valueBytes = Encoding.UTF8.GetByteCount(Text(pair));
            total += DatumHeaderBytes + ((valueBytes + 7) / 8 * 8);
        }
        return total;
    }

    /// <summary>Null when the pairs can be sent; otherwise why not, in German for the editor.</summary>
    public static string? Validate(IReadOnlyList<KeyValuePair<string, string>> pairs)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, _) in pairs)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "Ein Schlüssel ist leer.";
            if (key.Contains('=', StringComparison.Ordinal))
                return $"Der Schlüssel '{key}' enthält '='. Gesendet wird key=value, ein Empfänger könnte Schlüssel und Wert nicht mehr trennen.";
            if (!seen.Add(key))
                return $"Der Schlüssel '{key}' kommt doppelt vor.";
        }

        var length = CommentPduLength(pairs);
        return length > MaxCommentPduBytes
            ? $"Die Comment-PDU wäre {length} Byte groß, erlaubt sind {MaxCommentPduBytes}."
            : null;
    }
}
