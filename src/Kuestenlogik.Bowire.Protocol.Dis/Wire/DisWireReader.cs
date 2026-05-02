// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Text;

namespace Kuestenlogik.Bowire.Protocol.Dis.Wire;

/// <summary>
/// Big-endian reader for DIS wire format — counterpart to
/// <see cref="DisWireWriter"/>. Tracks a running offset so PDU
/// decoders can consume field after field without arithmetic.
/// Every read advances the cursor by the field size.
/// </summary>
/// <remarks>
/// The reader holds a <see cref="ReadOnlySpan{T}"/> so it's
/// allocation-free and safe against buffer lifetime issues at the
/// type-system level. Bounds errors surface as ordinary <c>IndexOutOfRangeException</c>
/// — callers are expected to have validated the PDU length from the
/// header before starting to decode.
/// </remarks>
public ref struct DisWireReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _offset;

    /// <summary>Wrap the given span for reading.</summary>
    public DisWireReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _offset = 0;
    }

    /// <summary>Current read position — bytes already consumed.</summary>
    public readonly int Offset => _offset;

    /// <summary>Total size of the wrapped span.</summary>
    public readonly int Length => _buffer.Length;

    /// <summary>Bytes still unread.</summary>
    public readonly int Remaining => _buffer.Length - _offset;

    /// <summary>Read one byte. Advances by 1.</summary>
    public byte ReadByte() => _buffer[_offset++];

    /// <summary>Read a signed byte. Advances by 1.</summary>
    public sbyte ReadSByte() => (sbyte)_buffer[_offset++];

    /// <summary>Read a big-endian unsigned 16-bit integer. Advances by 2.</summary>
    public ushort ReadUInt16()
    {
        var v = BinaryPrimitives.ReadUInt16BigEndian(_buffer.Slice(_offset, 2));
        _offset += 2;
        return v;
    }

    /// <summary>Read a big-endian signed 16-bit integer. Advances by 2.</summary>
    public short ReadInt16()
    {
        var v = BinaryPrimitives.ReadInt16BigEndian(_buffer.Slice(_offset, 2));
        _offset += 2;
        return v;
    }

    /// <summary>Read a big-endian unsigned 32-bit integer. Advances by 4.</summary>
    public uint ReadUInt32()
    {
        var v = BinaryPrimitives.ReadUInt32BigEndian(_buffer.Slice(_offset, 4));
        _offset += 4;
        return v;
    }

    /// <summary>Read a big-endian signed 32-bit integer. Advances by 4.</summary>
    public int ReadInt32()
    {
        var v = BinaryPrimitives.ReadInt32BigEndian(_buffer.Slice(_offset, 4));
        _offset += 4;
        return v;
    }

    /// <summary>Read a big-endian unsigned 64-bit integer. Advances by 8.</summary>
    public ulong ReadUInt64()
    {
        var v = BinaryPrimitives.ReadUInt64BigEndian(_buffer.Slice(_offset, 8));
        _offset += 8;
        return v;
    }

    /// <summary>Read a big-endian IEEE 754 single-precision float. Advances by 4.</summary>
    public float ReadSingle()
    {
        var v = BinaryPrimitives.ReadSingleBigEndian(_buffer.Slice(_offset, 4));
        _offset += 4;
        return v;
    }

    /// <summary>Read a big-endian IEEE 754 double-precision float. Advances by 8.</summary>
    public double ReadDouble()
    {
        var v = BinaryPrimitives.ReadDoubleBigEndian(_buffer.Slice(_offset, 8));
        _offset += 8;
        return v;
    }

    /// <summary>Read a span of bytes. Advances by <paramref name="count"/>.</summary>
    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        var slice = _buffer.Slice(_offset, count);
        _offset += count;
        return slice;
    }

    /// <summary>Skip <paramref name="count"/> bytes without reading them.</summary>
    public void SkipPadding(int count) { _offset += count; }

    /// <summary>
    /// Read an ASCII string out of a fixed-size slot. Stops at the
    /// first NUL byte so "ABC\0\0..." round-trips to "ABC".
    /// </summary>
    public string ReadAsciiFixed(int fieldLength)
    {
        var slot = _buffer.Slice(_offset, fieldLength);
        _offset += fieldLength;
        var nul = slot.IndexOf((byte)0);
        var effective = nul < 0 ? slot : slot[..nul];
        return Encoding.ASCII.GetString(effective);
    }
}
