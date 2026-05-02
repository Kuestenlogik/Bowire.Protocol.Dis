// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Enumerations;

/// <summary>
/// Transmitter state (byte). IEEE 1278.1 §5.2.32. Reported on every
/// Transmitter PDU so receivers know whether the radio is silent or
/// actively emitting.
/// </summary>
public enum TransmitState
{
    /// <summary>Radio is off.</summary>
    Off = 0,
    /// <summary>Radio is powered up but not transmitting.</summary>
    OnButNotTransmitting = 1,
    /// <summary>Radio is actively transmitting.</summary>
    OnAndTransmitting = 2,
}

/// <summary>
/// Receiver state (16-bit enumeration). IEEE 1278.1 §5.2.24.
/// Reported on Receiver PDU.
/// </summary>
public enum ReceiverState
{
    /// <summary>Receiver is off.</summary>
    Off = 0,
    /// <summary>Receiver is on but not receiving any signal.</summary>
    OnButNotReceiving = 1,
    /// <summary>Receiver is actively receiving.</summary>
    OnAndReceiving = 2,
}

/// <summary>
/// Antenna Pattern Type (16-bit enumeration). Describes the shape
/// of the antenna's emission pattern so receivers can compute
/// effective received power. IEEE 1278.1 §5.2.4.
/// </summary>
public enum AntennaPatternType
{
    /// <summary>Omni-directional — equal emission in all directions.</summary>
    Omnidirectional = 0,
    /// <summary>Beam pattern — directional.</summary>
    Beam = 1,
    /// <summary>Spherical harmonic — parameterised complex 3D pattern.</summary>
    SphericalHarmonic = 2,
}

/// <summary>
/// Intercom Control Type (byte). Indicates the nature of an
/// intercom-control exchange. IEEE 1278.1 §5.2.10.
/// </summary>
public enum IntercomControlType
{
    /// <summary>Reserved.</summary>
    Other = 0,
    /// <summary>Connection request from source to master.</summary>
    Request = 1,
    /// <summary>Connection acknowledgement.</summary>
    Acknowledge = 2,
    /// <summary>Connection refusal.</summary>
    Refusal = 3,
    /// <summary>Announce — non-negotiated broadcast connection.</summary>
    Announce = 4,
    /// <summary>Query — status interrogation.</summary>
    Query = 5,
}
