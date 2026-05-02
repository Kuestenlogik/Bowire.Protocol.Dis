// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Text;

namespace Kuestenlogik.Bowire.Protocol.Dis.Wire;

/// <summary>
/// Big-endian writer for DIS wire format. Thin wrapper over
/// <see cref="BinaryPrimitives"/> that tracks a running offset so
/// PDU marshalers can emit field after field without arithmetic in
/// the call sites. All primitive writes advance the cursor by the
/// size of the field written.
/// </summary>
/// <remarks>
/// <para>
/// DIS wire format is strictly network byte order (big-endian) —
/// IEEE 1278.1 §5.2. This writer never offers little-endian helpers
/// on purpose: if calling code writes <see cref="WriteUInt32"/> it
/// gets the right bytes on every host architecture.
/// </para>
/// <para>
/// Construction requires a caller-owned buffer (usually a stack-
/// allocated or pooled array). The writer never grows or allocates;
/// <see cref="Offset"/> at end-of-marshal equals the emitted length.
/// </para>
/// </remarks>
public ref struct DisWireWriter
{
    private readonly Span<byte> _buffer;
    private int _offset;

    /// <summary>
    /// Start a writer over the given caller-owned buffer.
    /// </summary>
    public DisWireWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _offset = 0;
    }

    /// <summary>Current write position — bytes already emitted.</summary>
    public readonly int Offset => _offset;

    /// <summary>Total capacity of the underlying buffer.</summary>
    public readonly int Capacity => _buffer.Length;

    /// <summary>Bytes still unwritten.</summary>
    public readonly int Remaining => _buffer.Length - _offset;

    /// <summary>Write one byte. Advances by 1.</summary>
    public void WriteByte(byte value) { _buffer[_offset++] = value; }

    /// <summary>Write a signed byte. Advances by 1.</summary>
    public void WriteSByte(sbyte value) { _buffer[_offset++] = (byte)value; }

    /// <summary>Write a big-endian unsigned 16-bit integer. Advances by 2.</summary>
    public void WriteUInt16(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(_buffer.Slice(_offset, 2), value);
        _offset += 2;
    }

    /// <summary>Write a big-endian signed 16-bit integer. Advances by 2.</summary>
    public void WriteInt16(short value)
    {
        BinaryPrimitives.WriteInt16BigEndian(_buffer.Slice(_offset, 2), value);
        _offset += 2;
    }

    /// <summary>Write a big-endian unsigned 32-bit integer. Advances by 4.</summary>
    public void WriteUInt32(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(_buffer.Slice(_offset, 4), value);
        _offset += 4;
    }

    /// <summary>Write a big-endian signed 32-bit integer. Advances by 4.</summary>
    public void WriteInt32(int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(_buffer.Slice(_offset, 4), value);
        _offset += 4;
    }

    /// <summary>Write a big-endian unsigned 64-bit integer. Advances by 8.</summary>
    public void WriteUInt64(ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(_buffer.Slice(_offset, 8), value);
        _offset += 8;
    }

    /// <summary>Write a big-endian IEEE 754 single-precision float. Advances by 4.</summary>
    public void WriteSingle(float value)
    {
        BinaryPrimitives.WriteSingleBigEndian(_buffer.Slice(_offset, 4), value);
        _offset += 4;
    }

    /// <summary>Write a big-endian IEEE 754 double-precision float. Advances by 8.</summary>
    public void WriteDouble(double value)
    {
        BinaryPrimitives.WriteDoubleBigEndian(_buffer.Slice(_offset, 8), value);
        _offset += 8;
    }

    /// <summary>Write a span of bytes verbatim. Advances by <c>bytes.Length</c>.</summary>
    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(_buffer.Slice(_offset, bytes.Length));
        _offset += bytes.Length;
    }

    /// <summary>Advance past <paramref name="count"/> bytes of zero. Used for reserved padding.</summary>
    public void WritePadding(int count)
    {
        _buffer.Slice(_offset, count).Clear();
        _offset += count;
    }

    /// <summary>
    /// Write an ASCII string into a fixed-size slot, NUL-padding when
    /// the string is shorter and silently truncating when longer.
    /// Used for fixed-length fields like Entity Marking (11 chars).
    /// </summary>
    public void WriteAsciiFixed(ReadOnlySpan<char> text, int fieldLength)
    {
        var slot = _buffer.Slice(_offset, fieldLength);
        slot.Clear();
        _ = Encoding.ASCII.GetBytes(text, slot);
        _offset += fieldLength;
    }
}
