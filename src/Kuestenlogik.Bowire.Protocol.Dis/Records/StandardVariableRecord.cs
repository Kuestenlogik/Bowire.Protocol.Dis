// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Standard Variable Record (IEEE 1278.1-2012 §6.2.82). Self-describing
/// record with a 32-bit <see cref="RecordType"/> code, an in-wire length
/// field, and an opaque content body. The same 8-byte header shape
/// recurs across the standard in DE Record Sets (§7.3.4), IO Standard
/// Variable records (§7.3.13), and every "record set" container that
/// carries heterogeneous payloads.
/// </summary>
/// <remarks>
/// <para>Layout on the wire:</para>
/// <list type="bullet">
///   <item>0 : 4 — <see cref="RecordType"/></item>
///   <item>4 : 2 — record length (octets, including the 8-byte header; excludes 64-bit alignment padding)</item>
///   <item>6 : 2 — reserved padding</item>
///   <item>8 : N — <see cref="Content"/>, padded out to the next 64-bit boundary on the wire</item>
/// </list>
/// <para>
/// Per-<see cref="RecordType"/> content shape is decided by the
/// SISO-REF-010 record-type enumeration; this codec keeps the body as
/// a byte array so every PDU that embeds a Standard Variable Record
/// can share the wire-format wrapper without committing to a typed
/// body schema. Typed record-type specialisations can layer on top
/// when needed.
/// </para>
/// </remarks>
public sealed record StandardVariableRecord(uint RecordType, byte[] Content)
{
    /// <summary>Size of the fixed 8-byte header (type + length + pad).</summary>
    public const int HeaderLength = 8;

    /// <summary>
    /// Wire length in bytes: 8-byte header plus the content, rounded up
    /// to the next 64-bit boundary. Both encoder and decoder honour
    /// this alignment so downstream record chains stay aligned.
    /// </summary>
    public int WireLength
    {
        get
        {
            var total = HeaderLength + Content.Length;
            return ((total + 7) / 8) * 8;
        }
    }

    internal void Marshal(ref DisWireWriter w)
    {
        var contentLen = Content.Length;
        var payloadLen = HeaderLength + contentLen;
        var paddedLen = ((payloadLen + 7) / 8) * 8;

        w.WriteUInt32(RecordType);
        w.WriteUInt16((ushort)payloadLen);
        w.WriteUInt16(0); // reserved padding
        w.WriteBytes(Content);
        var pad = paddedLen - payloadLen;
        if (pad > 0) w.WritePadding(pad);
    }

    internal static StandardVariableRecord Unmarshal(ref DisWireReader r)
    {
        var recordType = r.ReadUInt32();
        var recordLength = r.ReadUInt16();
        r.SkipPadding(2);

        // RecordLength counts header + content in bytes. Defensive
        // clamp: treat a malformed under-8 value as an empty record so
        // we don't emit a negative-length read.
        var contentLen = Math.Max(0, recordLength - HeaderLength);
        var content = contentLen > 0 ? r.ReadBytes(contentLen).ToArray() : [];

        var paddedLen = ((recordLength + 7) / 8) * 8;
        var pad = paddedLen - recordLength;
        if (pad > 0) r.SkipPadding(pad);

        return new StandardVariableRecord(recordType, content);
    }
}
