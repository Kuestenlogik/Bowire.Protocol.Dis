// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Text.Json;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.Minefield;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// The per-mine fields of a Minefield Data PDU, typed where they read cleanly
/// and left as bytes where they do not (#25).
/// </summary>
/// <remarks>
/// The layout follows KDIS, the one open implementation that honours the data
/// filter. With no SISO test vector to hold it to, the hand-built vectors
/// below are written from that layout independently of the encoder, and the
/// typed view declines instead of guessing whenever the bytes do not add up.
/// </remarks>
public sealed class MinefieldMineFieldsTests
{
    private const uint Ground = (uint)MinefieldDataFilter.GroundBurialDepthOffset;
    private const uint Fusing = (uint)MinefieldDataFilter.Fusing;
    private const uint Paint = (uint)MinefieldDataFilter.PaintScheme;
    private const uint Wires = (uint)MinefieldDataFilter.TripDetonationWire;

    private static MinefieldDataPdu Pdu(uint filter, int mines, ushort[] sensors, byte[] block) => new(
        PduHeader.ForV7(1, DisPduType.MinefieldData, DisProtocolFamily.Minefield, 0),
        MinefieldId: new EntityId(1, 1, 5000),
        RequestingSimulationId: new SimulationAddress(1, 1),
        MinefieldSequenceNumber: 1, RequestId: 1, PduSequenceNumber: 1, NumberOfPdus: 1,
        DataFilter: filter,
        MineType: new EntityType(8, 1, 225, 1, 1, 0, 0),
        MineLocations: Enumerable.Range(0, mines).Select(i => new Vector3Float(i, i, 0f)).ToArray(),
        SensorTypes: sensors,
        OptionalFieldsBlob: block);

    private static byte[] Bytes(params object[] fields)
    {
        var list = new List<byte>();
        foreach (var f in fields)
        {
            switch (f)
            {
                case float v:
                    var fb = new byte[4]; BinaryPrimitives.WriteSingleBigEndian(fb, v); list.AddRange(fb); break;
                case ushort v:
                    var ub = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(ub, v); list.AddRange(ub); break;
                case byte v:
                    list.Add(v); break;
                default:
                    throw new ArgumentException(f.GetType().Name);
            }
        }
        return [.. list];
    }

    // ---- hand-built vectors ----

    [Fact]
    public void Each_Array_Holds_One_Entry_Per_Mine_In_Field_Order()
    {
        // Ground depths for both mines, then both entity numbers, then both fusings.
        var block = Bytes(1.5f, 2.5f, (ushort)11, (ushort)12, (ushort)0x0085, (ushort)0x4000);
        var mines = Pdu(Ground | Fusing, 2, [], block).Mines;

        Assert.NotNull(mines);
        Assert.Equal([1.5f, 2.5f], mines!.Select(m => m.GroundBurialDepthOffset!.Value));
        Assert.Equal([(ushort)11, (ushort)12], mines.Select(m => m.MineEntityNumber));
        Assert.Null(mines[0].WaterBurialDepthOffset);
        Assert.Equal(5, mines[0].Fusing!.Value.Primary);
        Assert.Equal(1, mines[0].Fusing!.Value.Secondary);
        Assert.True(mines[1].Fusing!.Value.HasAntiHandlingDevice);
    }

    [Fact]
    public void Without_Any_Filter_Bit_Only_The_Entity_Numbers_Are_There()
    {
        var mines = Pdu(0, 2, [], Bytes((ushort)7, (ushort)8)).Mines;
        Assert.Equal([(ushort)7, (ushort)8], mines!.Select(m => m.MineEntityNumber));
        Assert.All(mines!, m => Assert.Null(m.Fusing));
    }

    [Fact]
    public void The_Short_Fields_Are_Padded_To_32_Bits()
    {
        // One mine: entity number (2) + paint scheme (1) = 3 bytes, one byte of padding.
        var block = Bytes((ushort)9, (byte)0b1101_0110, (byte)0);
        var mine = Assert.Single(Pdu(Paint, 1, [], block).Mines!);
        Assert.Equal(2, mine.PaintScheme!.Value.Algae);
        Assert.Equal(0b110101, mine.PaintScheme!.Value.Scheme);
    }

    [Fact]
    public void Scalar_Detection_Has_One_Coefficient_Per_Mine_And_Sensor_Type()
    {
        const uint sdc = (uint)MinefieldDataFilter.ScalarDetectionCoefficient;
        // 2 mines × 3 sensors: ids (4) + coefficients (6) = 10, two bytes of padding.
        var block = Bytes((ushort)1, (ushort)2, (byte)10, (byte)11, (byte)12, (byte)20, (byte)21, (byte)22, (byte)0, (byte)0);
        var mines = Pdu(sdc, 2, [1, 2, 3], block).Mines!;
        Assert.Equal([(byte)10, 11, 12], mines[0].ScalarDetectionCoefficients!);
        Assert.Equal([(byte)20, 21, 22], mines[1].ScalarDetectionCoefficients!);
    }

    [Fact]
    public void Trip_Wires_Are_Counts_Then_Vertex_Counts_Then_Vertices()
    {
        // One mine, ids + 2 padding; one wire; pad; two vertices on it; pad; the vertices.
        var block = Bytes(
            (ushort)5, (byte)0, (byte)0,
            (byte)1, (byte)0, (byte)0, (byte)0,
            (byte)2, (byte)0, (byte)0, (byte)0,
            1f, 2f, 3f, 4f, 5f, 6f);
        var wire = Assert.Single(Assert.Single(Pdu(Wires, 1, [], block).Mines!).TripDetonationWires!);
        Assert.Equal([new Vector3Float(1f, 2f, 3f), new Vector3Float(4f, 5f, 6f)], wire);
    }

    // ---- the encoder writes what the reader reads ----

    [Fact]
    public void Everything_Selected_Round_Trips_Through_The_Pdu()
    {
        const uint all = 0x7FF;
        MinefieldMine Mine(ushort id) => new(
            id,
            GroundBurialDepthOffset: id + 0.1f, WaterBurialDepthOffset: id + 0.2f, SnowBurialDepthOffset: id + 0.3f,
            Orientation: new EulerAngles(0.1f, 0.2f, 0.3f),
            ThermalContrast: 0.5f, Reflectance: 0.25f,
            EmplacementAge: new ClockTime(3, 1000),
            Fusing: new MineFusing(0x0102),
            ScalarDetectionCoefficients: [1, 2, 3],
            PaintScheme: new MinePaintScheme(0x0D),
            TripDetonationWires: id == 1
                ? [[new Vector3Float(1f, 1f, 1f)], [new Vector3Float(2f, 2f, 2f), new Vector3Float(3f, 3f, 3f)]]
                : []);
        var mines = new[] { Mine(1), Mine(2) };
        var block = MinefieldMineFields.Encode(all, 3, mines);
        Assert.Equal(0, block.Length % 4);

        var decoded = MinefieldDataPdu.Unmarshal(Pdu(all, 2, [1, 2, 3], block).Marshal()).Mines;

        Assert.NotNull(decoded);
        Assert.Equal(mines.Length, decoded!.Count);
        for (var i = 0; i < mines.Length; i++)
        {
            Assert.Equal(mines[i] with { ScalarDetectionCoefficients = null, TripDetonationWires = null },
                decoded[i] with { ScalarDetectionCoefficients = null, TripDetonationWires = null });
            Assert.Equal(mines[i].ScalarDetectionCoefficients!, decoded[i].ScalarDetectionCoefficients!);
            Assert.Equal(mines[i].TripDetonationWires!.Select(w => w.ToArray()), decoded[i].TripDetonationWires!.Select(w => w.ToArray()));
        }
    }

    [Fact]
    public void A_Selected_Field_A_Mine_Lacks_Is_An_Error_When_Writing()
    {
        Assert.Throws<ArgumentException>(() => MinefieldMineFields.Encode(Ground, 0, [new MinefieldMine(1)]));
    }

    // ---- the fallback ----

    [Fact]
    public void Bytes_That_Run_Short_Leave_The_Blob_Untyped_And_The_Pdu_Intact()
    {
        var block = Bytes(1.5f, (ushort)11);          // announces two mines' worth of ground depths
        var pdu = Pdu(Ground, 2, [], block);
        Assert.Null(pdu.Mines);

        var again = MinefieldDataPdu.Unmarshal(pdu.Marshal());
        Assert.Equal(block, again.OptionalFieldsBlob);
    }

    [Fact]
    public void Bytes_Left_Over_Mean_A_Different_Layout_And_No_Typed_View()
    {
        var block = Bytes((ushort)7, (ushort)8, 9f);
        Assert.Null(Pdu(0, 2, [], block).Mines);
    }

    [Fact]
    public void A_Filter_Bit_This_Reader_Does_Not_Know_Means_No_Typed_View()
    {
        Assert.Null(Pdu(1u << 20, 1, [], Bytes((ushort)7, (byte)0, (byte)0)).Mines);
    }

    // ---- on the stream ----

    [Fact]
    public void The_Stream_Envelope_Shows_The_Mines()
    {
        var block = Bytes(1.5f, (ushort)11, (byte)0, (byte)0);
        using var doc = JsonDocument.Parse(BowireDisProtocol.TryBuildEnvelope(Pdu(Ground, 1, [], block).Marshal(), filter: null)!);
        var mine = doc.RootElement.GetProperty("pdu").GetProperty("mines")[0];
        Assert.Equal(11, mine.GetProperty("mineEntityNumber").GetInt32());
        Assert.Equal(1.5f, mine.GetProperty("groundBurialDepthOffset").GetSingle());
    }
}
