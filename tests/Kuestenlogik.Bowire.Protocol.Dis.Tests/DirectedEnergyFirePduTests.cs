// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class DirectedEnergyFirePduTests
{
    private static DirectedEnergyFirePdu Build(IReadOnlyList<StandardVariableRecord>? deRecords = null) => new(
        Header: PduHeader.ForV7(1, DisPduType.DirectedEnergyFire,
            DisProtocolFamily.Warfare, DirectedEnergyFirePdu.MinimumWireLength),
        FiringEntityId: new EntityId(1, 1, 100),
        EventId: new EventId(1, 1, 99),
        MunitionType: new EntityType(2, 2, 225, 9, 1, 0, 0), // directed energy
        ShotStartTime: new ClockTime(Hour: 12345, TimePastHour: 0x0102_0304),
        CumulativeShotTime: 0.5f,
        ApertureEmitterLocation: new Vector3Float(1f, 0f, 0f),
        ApertureDiameter: 0.1f,
        Wavelength: 1.064e-6f,
        PulseRepetitionFrequency: 20f,
        PulseWidth: 0.0001f,
        Flags: 1,
        PulseShape: 2,
        DeRecords: deRecords ?? []);

    [Fact]
    public void Marshal_NoRecords_IsMinimumWireLength() =>
        Assert.Equal(DirectedEnergyFirePdu.MinimumWireLength, Build().Marshal().Length);

    [Fact]
    public void Marshal_HeaderBytesIdentifyV7AndPduType()
    {
        var bytes = Build().Marshal();
        Assert.Equal(7, bytes[0]);
        Assert.Equal((byte)DisPduType.DirectedEnergyFire, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.Warfare, bytes[3]);
    }

    [Fact]
    public void Roundtrip_WithTypedRecords_PreservesAllFieldsAndRecords()
    {
        // Typical DE damage record: 8-byte header + 32-byte body = 40 bytes total.
        var damageRecord = new StandardVariableRecord(
            RecordType: 5500,
            Content: new byte[32] // filled zeros — round-trips verbatim
            {
                0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80,
                0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0, 0x00,
                0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
                0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10,
            });

        var original = Build([damageRecord]);
        var bytes = original.Marshal();
        Assert.Equal(DirectedEnergyFirePdu.MinimumWireLength + 40, bytes.Length);

        var decoded = DirectedEnergyFirePdu.Unmarshal(bytes);
        Assert.Equal(original.FiringEntityId, decoded.FiringEntityId);
        Assert.Equal(original.EventId, decoded.EventId);
        Assert.Equal(original.MunitionType, decoded.MunitionType);
        Assert.Equal(original.ShotStartTime, decoded.ShotStartTime);
        Assert.Equal(original.CumulativeShotTime, decoded.CumulativeShotTime);
        Assert.Equal(original.ApertureEmitterLocation, decoded.ApertureEmitterLocation);
        Assert.Equal(original.ApertureDiameter, decoded.ApertureDiameter);
        Assert.Equal(original.Wavelength, decoded.Wavelength);
        Assert.Equal(original.PulseRepetitionFrequency, decoded.PulseRepetitionFrequency);
        Assert.Equal(original.PulseWidth, decoded.PulseWidth);
        Assert.Equal(original.Flags, decoded.Flags);
        Assert.Equal(original.PulseShape, decoded.PulseShape);
        Assert.Single(decoded.DeRecords);
        Assert.Equal(damageRecord.RecordType, decoded.DeRecords[0].RecordType);
        Assert.Equal(damageRecord.Content, decoded.DeRecords[0].Content);
    }
}
