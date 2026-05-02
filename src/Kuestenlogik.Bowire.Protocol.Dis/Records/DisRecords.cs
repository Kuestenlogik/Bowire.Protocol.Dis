// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Wire;

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// DIS Simulation Address — the (site, application) pair that
/// uniquely identifies a simulator in an exercise. Forms the first
/// two fields of <see cref="EntityId"/> and <see cref="EventId"/>,
/// but also stands alone in Simulation Management PDUs.
/// </summary>
/// <param name="Site">Unique id of the network site.</param>
/// <param name="Application">Unique id of the application within the site.</param>
public readonly record struct SimulationAddress(ushort Site, ushort Application)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 4;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt16(Site);
        w.WriteUInt16(Application);
    }

    internal static SimulationAddress Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt16(), r.ReadUInt16());
}

/// <summary>
/// Entity Id record — (site, application, entity) three-tuple.
/// Identifies a simulated entity uniquely across the exercise.
/// IEEE 1278.1 §5.2.14.1.
/// </summary>
/// <param name="Site">Site id part of the <see cref="SimulationAddress"/>.</param>
/// <param name="Application">Application id within the site.</param>
/// <param name="Entity">Entity id within the application.</param>
public readonly record struct EntityId(ushort Site, ushort Application, ushort Entity)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 6;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt16(Site);
        w.WriteUInt16(Application);
        w.WriteUInt16(Entity);
    }

    internal static EntityId Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16());
}

/// <summary>
/// Event Id record — pairs a <see cref="SimulationAddress"/> with an
/// event counter to uniquely identify events (Fire, Detonation,
/// Collision, ...) across the exercise. IEEE 1278.1 §5.2.18.
/// </summary>
public readonly record struct EventId(ushort Site, ushort Application, ushort Event)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 6;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteUInt16(Site);
        w.WriteUInt16(Application);
        w.WriteUInt16(Event);
    }

    internal static EventId Unmarshal(ref DisWireReader r) =>
        new(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16());
}

/// <summary>
/// Two-component single-precision vector. Used for 2D grid / perimeter
/// coordinates in Minefield PDUs (§5.3.10) — latitude/longitude or
/// grid x/y, depending on minefield coordinate system.
/// </summary>
public readonly record struct Vector2Float(float X, float Y)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 8;

    /// <summary>Origin vector (0, 0).</summary>
    public static Vector2Float Zero => default;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteSingle(X);
        w.WriteSingle(Y);
    }

    internal static Vector2Float Unmarshal(ref DisWireReader r) =>
        new(r.ReadSingle(), r.ReadSingle());
}

/// <summary>
/// Three-component float vector. Used for linear velocity and linear
/// acceleration fields — anything measured in metres or m/s that
/// fits float32 precision.
/// </summary>
public readonly record struct Vector3Float(float X, float Y, float Z)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 12;

    /// <summary>Origin vector (0, 0, 0) — handy for stationary entities.</summary>
    public static Vector3Float Zero => default;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteSingle(X);
        w.WriteSingle(Y);
        w.WriteSingle(Z);
    }

    internal static Vector3Float Unmarshal(ref DisWireReader r) =>
        new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}

/// <summary>
/// Three-component double vector. Used exclusively for Entity
/// Location (ECEF), where double precision is mandated by the spec
/// because single-precision floats can't represent earth-scale
/// positions to sub-metre accuracy.
/// </summary>
public readonly record struct Vector3Double(double X, double Y, double Z)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 24;

    /// <summary>Origin vector (0, 0, 0).</summary>
    public static Vector3Double Zero => default;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteDouble(X);
        w.WriteDouble(Y);
        w.WriteDouble(Z);
    }

    internal static Vector3Double Unmarshal(ref DisWireReader r) =>
        new(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
}

/// <summary>
/// Tait-Bryan / Euler angles describing entity orientation in
/// world coordinates. Radians, in PSI-THETA-PHI (yaw-pitch-roll)
/// order. IEEE 1278.1 §5.2.17.
/// </summary>
public readonly record struct EulerAngles(float Psi, float Theta, float Phi)
{
    /// <summary>Serialised wire length, in bytes.</summary>
    public const int WireLength = 12;

    /// <summary>Identity orientation (0, 0, 0 rad).</summary>
    public static EulerAngles Zero => default;

    internal void Marshal(ref DisWireWriter w)
    {
        w.WriteSingle(Psi);
        w.WriteSingle(Theta);
        w.WriteSingle(Phi);
    }

    internal static EulerAngles Unmarshal(ref DisWireReader r) =>
        new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}
