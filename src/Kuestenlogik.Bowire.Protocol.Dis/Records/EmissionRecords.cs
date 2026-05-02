// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Propulsion System record (8 bytes). Ships with Supplemental
/// Emission/Entity State. Reports the current power setting and
/// engine RPM of one propulsion system. IEEE 1278.1-2012 §6.2.68.
/// </summary>
public readonly record struct PropulsionSystem(float PowerSetting, float EngineRpm)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteSingle(PowerSetting);
        w.WriteSingle(EngineRpm);
    }

    internal static PropulsionSystem Unmarshal(ref DisWireReader r) =>
        new(r.ReadSingle(), r.ReadSingle());
}

/// <summary>
/// Vectoring Nozzle System record (8 bytes). Ships with Supplemental
/// Emission/Entity State. Reports the current horizontal + vertical
/// deflection angles (radians) of one thrust-vectoring nozzle.
/// IEEE 1278.1-2012 §6.2.98.
/// </summary>
public readonly record struct VectoringNozzleSystem(float HorizontalDeflectionAngle, float VerticalDeflectionAngle)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteSingle(HorizontalDeflectionAngle);
        w.WriteSingle(VerticalDeflectionAngle);
    }

    internal static VectoringNozzleSystem Unmarshal(ref DisWireReader r) =>
        new(r.ReadSingle(), r.ReadSingle());
}
