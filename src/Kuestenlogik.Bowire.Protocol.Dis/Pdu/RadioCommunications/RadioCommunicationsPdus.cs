// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Pdu.RadioCommunications;

// --- PDU: Transmitter (25) ---------------------------------------------------

/// <summary>
/// Transmitter PDU (type 25, family 4). Reports the state and
/// configuration of a radio transmitter — which radio on which
/// entity, what band and modulation, how much power, and where the
/// antenna is relative to the entity. Heartbeat-style: the sender
/// re-emits on state changes or a slow periodic timer.
/// IEEE 1278.1 §5.3.8.1.
/// </summary>
/// <remarks>
/// <para>
/// Layout (104 bytes fixed; optional modulation-parameter and
/// antenna-pattern trailing blobs):
/// </para>
/// <list type="bullet">
///   <item>0   : 12 — <see cref="PduHeader"/></item>
///   <item>12  :  6 — <see cref="RadioReferenceId"/> (the entity the radio lives on)</item>
///   <item>18  :  2 — <see cref="RadioNumber"/> (which radio on that entity)</item>
///   <item>20  :  8 — <see cref="RadioEntityType"/></item>
///   <item>28  :  1 — <see cref="TransmitState"/></item>
///   <item>29  :  1 — <see cref="InputSource"/></item>
///   <item>30  :  2 — reserved padding (V7: variable-parameter count)</item>
///   <item>32  : 24 — <see cref="AntennaLocation"/> (ECEF)</item>
///   <item>56  : 12 — <see cref="RelativeAntennaLocation"/> (entity body coords)</item>
///   <item>68  :  2 — <see cref="AntennaPatternType"/></item>
///   <item>70  :  2 — antenna pattern length (bytes of trailing blob)</item>
///   <item>72  :  8 — <see cref="Frequency"/> (Hz, uint64)</item>
///   <item>80  :  4 — <see cref="TransmitFrequencyBandwidth"/> (Hz)</item>
///   <item>84  :  4 — <see cref="Power"/> (dBm)</item>
///   <item>88  :  8 — <see cref="ModulationType"/></item>
///   <item>96  :  2 — <see cref="CryptoSystem"/></item>
///   <item>98  :  2 — <see cref="CryptoKeyId"/></item>
///   <item>100 :  1 — modulation parameter length (bytes of trailing blob)</item>
///   <item>101 :  3 — reserved padding</item>
///   <item>104 :  N — modulation parameters blob</item>
///   <item>... :  M — antenna pattern blob</item>
/// </list>
/// </remarks>
public sealed record TransmitterPdu(
    PduHeader Header,
    EntityId RadioReferenceId,
    ushort RadioNumber,
    EntityType RadioEntityType,
    TransmitState TransmitState,
    byte InputSource,
    Vector3Double AntennaLocation,
    Vector3Float RelativeAntennaLocation,
    AntennaPatternType AntennaPatternType,
    ulong Frequency,
    float TransmitFrequencyBandwidth,
    float Power,
    ModulationType ModulationType,
    ushort CryptoSystem,
    ushort CryptoKeyId,
    byte[] ModulationParameters,
    byte[] AntennaPattern)
{
    /// <summary>Fixed wire length in bytes before the modulation-parameter and antenna-pattern blobs.</summary>
    public const int MinimumWireLength = 104;

    /// <summary>Total wire length including trailing blobs.</summary>
    public int WireLength => MinimumWireLength + ModulationParameters.Length + AntennaPattern.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.Transmitter,
            ProtocolFamily = DisProtocolFamily.RadioCommunications,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        RadioReferenceId.Marshal(ref w);
        w.WriteUInt16(RadioNumber);
        RadioEntityType.Marshal(ref w);
        w.WriteByte((byte)TransmitState);
        w.WriteByte(InputSource);
        w.WriteUInt16(0); // padding

        AntennaLocation.Marshal(ref w);
        RelativeAntennaLocation.Marshal(ref w);
        w.WriteUInt16((ushort)AntennaPatternType);
        w.WriteUInt16((ushort)AntennaPattern.Length);

        w.WriteUInt64(Frequency);
        w.WriteSingle(TransmitFrequencyBandwidth);
        w.WriteSingle(Power);
        ModulationType.Marshal(ref w);
        w.WriteUInt16(CryptoSystem);
        w.WriteUInt16(CryptoKeyId);

        w.WriteByte((byte)ModulationParameters.Length);
        w.WritePadding(3);

        w.WriteBytes(ModulationParameters);
        w.WriteBytes(AntennaPattern);

        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static TransmitterPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var radioReference = EntityId.Unmarshal(ref r);
        var radioNumber = r.ReadUInt16();
        var radioEntityType = EntityType.Unmarshal(ref r);
        var transmitState = (TransmitState)r.ReadByte();
        var inputSource = r.ReadByte();
        r.SkipPadding(2);
        var antennaLocation = Vector3Double.Unmarshal(ref r);
        var relativeAntennaLocation = Vector3Float.Unmarshal(ref r);
        var antennaPatternType = (AntennaPatternType)r.ReadUInt16();
        var antennaPatternLength = r.ReadUInt16();
        var frequency = r.ReadUInt64();
        var bandwidth = r.ReadSingle();
        var power = r.ReadSingle();
        var modulationType = Records.ModulationType.Unmarshal(ref r);
        var cryptoSystem = r.ReadUInt16();
        var cryptoKeyId = r.ReadUInt16();
        var modulationParamLength = r.ReadByte();
        r.SkipPadding(3);

        var modulationParams = modulationParamLength > 0
            ? r.ReadBytes(modulationParamLength).ToArray()
            : [];
        var antennaPattern = antennaPatternLength > 0
            ? r.ReadBytes(antennaPatternLength).ToArray()
            : [];

        return new TransmitterPdu(
            header, radioReference, radioNumber, radioEntityType,
            transmitState, inputSource, antennaLocation, relativeAntennaLocation,
            antennaPatternType, frequency, bandwidth, power,
            modulationType, cryptoSystem, cryptoKeyId,
            modulationParams, antennaPattern);
    }
}

// --- PDU: Signal (26) --------------------------------------------------------

/// <summary>
/// Signal PDU (type 26, family 4). Carries the audio / digital
/// samples a radio is transmitting. The payload bytes are opaque
/// (encoding picked via <see cref="EncodingScheme"/>), padded to a
/// 4-byte boundary on the wire. IEEE 1278.1 §5.3.8.2.
/// </summary>
public sealed record SignalPdu(
    PduHeader Header,
    EntityId RadioReferenceId,
    ushort RadioNumber,
    ushort EncodingScheme,
    ushort TdlType,
    uint SampleRate,
    ushort DataLengthBits,
    ushort Samples,
    byte[] Data)
{
    /// <summary>Fixed wire length before the data blob.</summary>
    public const int MinimumWireLength = 32;

    /// <summary>Total wire length: fixed part + data padded to 4-byte boundary.</summary>
    public int WireLength => MinimumWireLength + PaddedDataLength(Data.Length);

    private static int PaddedDataLength(int dataLength) => ((dataLength + 3) / 4) * 4;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.Signal,
            ProtocolFamily = DisProtocolFamily.RadioCommunications,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        RadioReferenceId.Marshal(ref w);
        w.WriteUInt16(RadioNumber);
        w.WriteUInt16(EncodingScheme);
        w.WriteUInt16(TdlType);
        w.WriteUInt32(SampleRate);
        w.WriteUInt16(DataLengthBits);
        w.WriteUInt16(Samples);
        w.WriteBytes(Data);
        var padLength = PaddedDataLength(Data.Length) - Data.Length;
        if (padLength > 0) w.WritePadding(padLength);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static SignalPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var radioReference = EntityId.Unmarshal(ref r);
        var radioNumber = r.ReadUInt16();
        var encodingScheme = r.ReadUInt16();
        var tdlType = r.ReadUInt16();
        var sampleRate = r.ReadUInt32();
        var dataLengthBits = r.ReadUInt16();
        var samples = r.ReadUInt16();

        var dataByteCount = (dataLengthBits + 7) / 8;
        var padded = PaddedDataLength(dataByteCount);
        var data = dataByteCount > 0
            ? r.ReadBytes(dataByteCount).ToArray()
            : [];
        if (padded > dataByteCount) r.SkipPadding(padded - dataByteCount);

        return new SignalPdu(
            header, radioReference, radioNumber, encodingScheme, tdlType,
            sampleRate, dataLengthBits, samples, data);
    }
}

// --- PDU: Receiver (27) ------------------------------------------------------

/// <summary>
/// Receiver PDU (type 27, family 4). Reports the state of a radio
/// receiver: on/off, received power, and which transmitter it's
/// listening to. IEEE 1278.1 §5.3.8.3.
/// </summary>
public sealed record ReceiverPdu(
    PduHeader Header,
    EntityId RadioReferenceId,
    ushort RadioNumber,
    ReceiverState ReceiverState,
    float ReceivedPower,
    EntityId TransmitterEntityId,
    ushort TransmitterRadioNumber)
{
    /// <summary>Fixed wire length in bytes.</summary>
    public const int WireLength = 36;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.Receiver,
            ProtocolFamily = DisProtocolFamily.RadioCommunications,
            Length = WireLength,
        };
        header.Marshal(ref w);

        RadioReferenceId.Marshal(ref w);
        w.WriteUInt16(RadioNumber);
        w.WriteUInt16((ushort)ReceiverState);
        w.WriteUInt16(0); // padding
        w.WriteSingle(ReceivedPower);
        TransmitterEntityId.Marshal(ref w);
        w.WriteUInt16(TransmitterRadioNumber);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static ReceiverPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var radioReference = EntityId.Unmarshal(ref r);
        var radioNumber = r.ReadUInt16();
        var receiverState = (ReceiverState)r.ReadUInt16();
        r.SkipPadding(2);
        var receivedPower = r.ReadSingle();
        var transmitterEntityId = EntityId.Unmarshal(ref r);
        var transmitterRadioNumber = r.ReadUInt16();
        return new ReceiverPdu(
            header, radioReference, radioNumber, receiverState,
            receivedPower, transmitterEntityId, transmitterRadioNumber);
    }
}

// --- PDU: Intercom Signal (31) -----------------------------------------------

/// <summary>
/// Intercom Signal PDU (type 31, family 4). Like <see cref="SignalPdu"/>
/// but for intercom devices rather than radios. Same wire framing
/// minus the radio-number distinction. IEEE 1278.1 §5.3.8.4.
/// </summary>
public sealed record IntercomSignalPdu(
    PduHeader Header,
    EntityId EntityId,
    ushort IntercomDeviceId,
    ushort EncodingScheme,
    ushort TdlType,
    uint SampleRate,
    ushort DataLengthBits,
    ushort Samples,
    byte[] Data)
{
    /// <summary>Fixed wire length before the data blob.</summary>
    public const int MinimumWireLength = 32;

    /// <summary>Total wire length: fixed part + data padded to 4-byte boundary.</summary>
    public int WireLength => MinimumWireLength + PaddedDataLength(Data.Length);

    private static int PaddedDataLength(int dataLength) => ((dataLength + 3) / 4) * 4;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.IntercomSignal,
            ProtocolFamily = DisProtocolFamily.RadioCommunications,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        EntityId.Marshal(ref w);
        w.WriteUInt16(IntercomDeviceId);
        w.WriteUInt16(EncodingScheme);
        w.WriteUInt16(TdlType);
        w.WriteUInt32(SampleRate);
        w.WriteUInt16(DataLengthBits);
        w.WriteUInt16(Samples);
        w.WriteBytes(Data);
        var padLength = PaddedDataLength(Data.Length) - Data.Length;
        if (padLength > 0) w.WritePadding(padLength);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static IntercomSignalPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var entityId = Records.EntityId.Unmarshal(ref r);
        var deviceId = r.ReadUInt16();
        var encodingScheme = r.ReadUInt16();
        var tdlType = r.ReadUInt16();
        var sampleRate = r.ReadUInt32();
        var dataLengthBits = r.ReadUInt16();
        var samples = r.ReadUInt16();

        var dataByteCount = (dataLengthBits + 7) / 8;
        var padded = PaddedDataLength(dataByteCount);
        var data = dataByteCount > 0
            ? r.ReadBytes(dataByteCount).ToArray()
            : [];
        if (padded > dataByteCount) r.SkipPadding(padded - dataByteCount);

        return new IntercomSignalPdu(
            header, entityId, deviceId, encodingScheme, tdlType,
            sampleRate, dataLengthBits, samples, data);
    }
}

// --- PDU: Intercom Control (32) ----------------------------------------------

/// <summary>
/// Intercom Control PDU (type 32, family 4). Controls intercom
/// connections — requests, acknowledgements, refusals, announces.
/// Intercom-parameter blob carries opaque per-call state.
/// IEEE 1278.1 §5.3.8.5.
/// </summary>
public sealed record IntercomControlPdu(
    PduHeader Header,
    IntercomControlType ControlType,
    byte CommunicationsChannelType,
    EntityId SourceEntityId,
    ushort SourceCommunicationsDeviceId,
    byte SourceLineId,
    byte TransmitPriority,
    byte TransmitLineState,
    byte Command,
    EntityId MasterEntityId,
    ushort MasterCommunicationsDeviceId,
    uint MasterChannelId,
    byte[] IntercomParameters)
{
    /// <summary>Fixed wire length before the intercom-parameter blob.</summary>
    public const int MinimumWireLength = 44;

    /// <summary>Total wire length including parameters.</summary>
    public int WireLength => MinimumWireLength + IntercomParameters.Length;

    /// <summary>Serialise into <paramref name="destination"/>.</summary>
    public int Marshal(Span<byte> destination)
    {
        var w = new DisWireWriter(destination);
        var header = Header with
        {
            PduType = DisPduType.IntercomControl,
            ProtocolFamily = DisProtocolFamily.RadioCommunications,
            Length = (ushort)WireLength,
        };
        header.Marshal(ref w);

        w.WriteByte((byte)ControlType);
        w.WriteByte(CommunicationsChannelType);
        SourceEntityId.Marshal(ref w);
        w.WriteUInt16(SourceCommunicationsDeviceId);
        w.WriteByte(SourceLineId);
        w.WriteByte(TransmitPriority);
        w.WriteByte(TransmitLineState);
        w.WriteByte(Command);
        MasterEntityId.Marshal(ref w);
        w.WriteUInt16(MasterCommunicationsDeviceId);
        w.WriteUInt32(MasterChannelId);
        w.WriteUInt32((uint)IntercomParameters.Length);
        w.WriteBytes(IntercomParameters);
        return w.Offset;
    }

    /// <summary>Allocation-included shortcut.</summary>
    public byte[] Marshal()
    {
        var buffer = new byte[WireLength];
        Marshal(buffer);
        return buffer;
    }

    /// <summary>Parse off the wire.</summary>
    public static IntercomControlPdu Unmarshal(ReadOnlySpan<byte> source)
    {
        var r = new DisWireReader(source);
        var header = PduHeader.Unmarshal(ref r);
        var controlType = (IntercomControlType)r.ReadByte();
        var channelType = r.ReadByte();
        var sourceEntityId = EntityId.Unmarshal(ref r);
        var sourceDeviceId = r.ReadUInt16();
        var sourceLineId = r.ReadByte();
        var transmitPriority = r.ReadByte();
        var transmitLineState = r.ReadByte();
        var command = r.ReadByte();
        var masterEntityId = EntityId.Unmarshal(ref r);
        var masterDeviceId = r.ReadUInt16();
        var masterChannelId = r.ReadUInt32();
        var paramLength = r.ReadUInt32();
        var parameters = paramLength > 0 ? r.ReadBytes((int)paramLength).ToArray() : [];
        return new IntercomControlPdu(
            header, controlType, channelType, sourceEntityId, sourceDeviceId,
            sourceLineId, transmitPriority, transmitLineState, command,
            masterEntityId, masterDeviceId, masterChannelId, parameters);
    }
}
