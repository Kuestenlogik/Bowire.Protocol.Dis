// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

/// <summary>
/// The sandbox's own geometry. Reference values are independent of both
/// the sandbox and the plugin; the plugin's converter is used only as a
/// second opinion on the way back.
/// </summary>
public sealed class GeoConversionTests
{
    private const double Deg = Math.PI / 180.0;
    private static readonly GeoPosition s_hamburg = new(53.5511, 9.9937, 12.0);

    [Fact]
    public void Equator_PrimeMeridian_IsTheSemiMajorAxis()
    {
        var e = GeoConversion.ToEcef(new GeoPosition(0, 0, 0));
        Assert.Equal(6_378_137.0, e.X, 3);
        Assert.Equal(0.0, e.Y, 3);
        Assert.Equal(0.0, e.Z, 3);
    }

    [Fact]
    public void NorthPole_IsTheSemiMinorAxis()
    {
        var e = GeoConversion.ToEcef(new GeoPosition(90, 0, 0));
        Assert.Equal(0.0, e.X, 3);
        Assert.Equal(6_356_752.314245, e.Z, 3);
    }

    [Fact]
    public void Hamburg_RoundTrips()
    {
        var back = GeoConversion.FromEcef(GeoConversion.ToEcef(s_hamburg));
        Assert.Equal(s_hamburg.Latitude, back.Latitude, 9);
        Assert.Equal(s_hamburg.Longitude, back.Longitude, 9);
        Assert.Equal(s_hamburg.Altitude, back.Altitude, 3);
    }

    [Fact]
    public void Hamburg_AgreesWithThePluginsConverter()
    {
        var e = GeoConversion.ToEcef(s_hamburg);
        var plugin = GeocentricCoordinate.ToWgs84(new Vector3Double(e.X, e.Y, e.Z));
        Assert.NotNull(plugin);
        Assert.Equal(s_hamburg.Latitude, plugin!.Value.Latitude, 8);
        Assert.Equal(s_hamburg.Longitude, plugin.Value.Longitude, 8);
        Assert.Equal(s_hamburg.Altitude, plugin.Value.Altitude, 2);
    }

    [Fact]
    public void HeadingEast_InHamburg_GivesTheExpectedEulerAngles()
    {
        // Body x points east: ψ = 90° + longitude, θ = 0, φ = −(90° + latitude).
        var o = GeoConversion.ToDisOrientation(s_hamburg, 90);
        Assert.Equal(99.9937 * Deg, o.Psi, 9);
        Assert.Equal(0.0, o.Theta, 9);
        Assert.Equal(-143.5511 * Deg, o.Phi, 9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    [InlineData(359.5)]
    public void Heading_RoundTripsThroughDisOrientation(double heading)
    {
        var o = GeoConversion.ToDisOrientation(s_hamburg, heading);
        Assert.Equal(heading, GeoConversion.HeadingFromDisOrientation(s_hamburg, o), 6);
    }

    [Fact]
    public void Velocity_NorthAtTheOrigin_PointsAlongZ()
    {
        var v = GeoConversion.VelocityToEcef(new GeoPosition(0, 0, 0), 10, 0);
        Assert.Equal(0.0, v.X, 9);
        Assert.Equal(0.0, v.Y, 9);
        Assert.Equal(10.0, v.Z, 9);
    }

    [Fact]
    public void Velocity_EastAtTheOrigin_PointsAlongY()
    {
        var v = GeoConversion.VelocityToEcef(new GeoPosition(0, 0, 0), 10, 90);
        Assert.Equal(0.0, v.X, 9);
        Assert.Equal(10.0, v.Y, 9);
        Assert.Equal(0.0, v.Z, 9);
    }

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(360, 0)]
    [InlineData(725, 5)]
    public void NormalizeDegrees_WrapsIntoZeroTo360(double input, double expected) =>
        Assert.Equal(expected, GeoConversion.NormalizeDegrees(input), 9);
}
