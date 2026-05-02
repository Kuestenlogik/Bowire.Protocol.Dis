// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.RadioCommunications;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class RadioCommunicationsTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.RadioCommunications, length);

    [Fact]
    public void Transmitter_RoundTrip_FixedPartOnly()
    {
        var original = new TransmitterPdu(
            HeaderFor(DisPduType.Transmitter, TransmitterPdu.MinimumWireLength),
            RadioReferenceId: new EntityId(1, 1, 100),
            RadioNumber: 1,
            RadioEntityType: new EntityType(7, 1, 225, 1, 1, 0, 0),
            TransmitState: TransmitState.OnAndTransmitting,
            InputSource: 1,
            AntennaLocation: new Vector3Double(3765000.0, 661000.0, 5108000.0),
            RelativeAntennaLocation: new Vector3Float(0.5f, 0f, 2f),
            AntennaPatternType: AntennaPatternType.Omnidirectional,
            Frequency: 243_000_000UL,
            TransmitFrequencyBandwidth: 25000f,
            Power: 30f,
            ModulationType: new ModulationType(
                SpreadSpectrum: 0, MajorModulation: 3, Detail: 3, System: 1),
            CryptoSystem: 0,
            CryptoKeyId: 0,
            ModulationParameters: [],
            AntennaPattern: []);

        var bytes = original.Marshal();
        Assert.Equal(TransmitterPdu.MinimumWireLength, bytes.Length);
        Assert.Equal((byte)DisPduType.Transmitter, bytes[2]);

        var decoded = TransmitterPdu.Unmarshal(bytes);
        Assert.Equal(original.RadioReferenceId, decoded.RadioReferenceId);
        Assert.Equal(original.RadioNumber, decoded.RadioNumber);
        Assert.Equal(original.RadioEntityType, decoded.RadioEntityType);
        Assert.Equal(original.TransmitState, decoded.TransmitState);
        Assert.Equal(original.AntennaLocation, decoded.AntennaLocation);
        Assert.Equal(original.RelativeAntennaLocation, decoded.RelativeAntennaLocation);
        Assert.Equal(original.AntennaPatternType, decoded.AntennaPatternType);
        Assert.Equal(original.Frequency, decoded.Frequency);
        Assert.Equal(original.TransmitFrequencyBandwidth, decoded.TransmitFrequencyBandwidth);
        Assert.Equal(original.Power, decoded.Power);
        Assert.Equal(original.ModulationType, decoded.ModulationType);
    }

    [Fact]
    public void Transmitter_RoundTrip_WithModulationParamsAndAntennaPattern()
    {
        var modulationParams = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var antennaPattern = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };

        var original = new TransmitterPdu(
            HeaderFor(DisPduType.Transmitter, 0),
            RadioReferenceId: new EntityId(1, 1, 100),
            RadioNumber: 1,
            RadioEntityType: new EntityType(7, 1, 225, 1, 1, 0, 0),
            TransmitState: TransmitState.OnAndTransmitting,
            InputSource: 1,
            AntennaLocation: Vector3Double.Zero,
            RelativeAntennaLocation: Vector3Float.Zero,
            AntennaPatternType: AntennaPatternType.Beam,
            Frequency: 30_000_000UL,
            TransmitFrequencyBandwidth: 200_000f,
            Power: 50f,
            ModulationType: new ModulationType(0, 3, 3, 5),
            CryptoSystem: 1,
            CryptoKeyId: 42,
            ModulationParameters: modulationParams,
            AntennaPattern: antennaPattern);

        var bytes = original.Marshal();
        Assert.Equal(
            TransmitterPdu.MinimumWireLength + modulationParams.Length + antennaPattern.Length,
            bytes.Length);

        var decoded = TransmitterPdu.Unmarshal(bytes);
        Assert.Equal(modulationParams, decoded.ModulationParameters);
        Assert.Equal(antennaPattern, decoded.AntennaPattern);
        Assert.Equal(AntennaPatternType.Beam, decoded.AntennaPatternType);
    }

    [Fact]
    public void Signal_RoundTrip_PadsDataToFourByteBoundary()
    {
        // 5 bytes of data → 8 bytes of padded slot → 32 + 8 = 40 total.
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var original = new SignalPdu(
            HeaderFor(DisPduType.Signal, 0),
            RadioReferenceId: new EntityId(1, 1, 100),
            RadioNumber: 1,
            EncodingScheme: 0x0000,
            TdlType: 0,
            SampleRate: 8000,
            DataLengthBits: (ushort)(data.Length * 8),
            Samples: 1,
            Data: data);

        var bytes = original.Marshal();
        Assert.Equal(SignalPdu.MinimumWireLength + 8, bytes.Length);

        var decoded = SignalPdu.Unmarshal(bytes);
        Assert.Equal(data, decoded.Data);
        Assert.Equal(original.SampleRate, decoded.SampleRate);
        Assert.Equal(original.DataLengthBits, decoded.DataLengthBits);
    }

    [Fact]
    public void Receiver_RoundTrip()
    {
        var original = new ReceiverPdu(
            HeaderFor(DisPduType.Receiver, ReceiverPdu.WireLength),
            RadioReferenceId: new EntityId(1, 1, 100),
            RadioNumber: 1,
            ReceiverState: ReceiverState.OnAndReceiving,
            ReceivedPower: -50f,
            TransmitterEntityId: new EntityId(1, 1, 200),
            TransmitterRadioNumber: 3);

        var bytes = original.Marshal();
        Assert.Equal(ReceiverPdu.WireLength, bytes.Length);

        var decoded = ReceiverPdu.Unmarshal(bytes);
        Assert.Equal(ReceiverState.OnAndReceiving, decoded.ReceiverState);
        Assert.Equal(-50f, decoded.ReceivedPower);
        Assert.Equal(original.TransmitterEntityId, decoded.TransmitterEntityId);
        Assert.Equal(original.TransmitterRadioNumber, decoded.TransmitterRadioNumber);
    }

    [Fact]
    public void IntercomSignal_RoundTrip()
    {
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var original = new IntercomSignalPdu(
            HeaderFor(DisPduType.IntercomSignal, 0),
            EntityId: new EntityId(1, 1, 100),
            IntercomDeviceId: 7,
            EncodingScheme: 0,
            TdlType: 0,
            SampleRate: 8000,
            DataLengthBits: (ushort)(data.Length * 8),
            Samples: 1,
            Data: data);

        var decoded = IntercomSignalPdu.Unmarshal(original.Marshal());
        Assert.Equal(data, decoded.Data);
        Assert.Equal(7, decoded.IntercomDeviceId);
    }

    [Fact]
    public void IntercomControl_RoundTrip_WithParameters()
    {
        var parameters = new byte[] { 0x01, 0x02, 0x03 };
        var original = new IntercomControlPdu(
            HeaderFor(DisPduType.IntercomControl, 0),
            ControlType: IntercomControlType.Request,
            CommunicationsChannelType: 1,
            SourceEntityId: new EntityId(1, 1, 100),
            SourceCommunicationsDeviceId: 1,
            SourceLineId: 0,
            TransmitPriority: 0,
            TransmitLineState: 0,
            Command: 1,
            MasterEntityId: new EntityId(1, 1, 200),
            MasterCommunicationsDeviceId: 1,
            MasterChannelId: 42,
            IntercomParameters: parameters);

        var decoded = IntercomControlPdu.Unmarshal(original.Marshal());
        Assert.Equal(IntercomControlType.Request, decoded.ControlType);
        Assert.Equal(original.SourceEntityId, decoded.SourceEntityId);
        Assert.Equal(original.MasterEntityId, decoded.MasterEntityId);
        Assert.Equal(original.MasterChannelId, decoded.MasterChannelId);
        Assert.Equal(parameters, decoded.IntercomParameters);
    }
}
