// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// ECEF → WGS84, the conversion that lets a DIS stream reach the map.
/// </summary>
/// <remarks>
/// Until this landed the envelope carried no position at all: every
/// EntityState PDU has a location, the plugin decoded it, and it went
/// nowhere. So the interesting cases are not "is the arithmetic right"
/// alone — they are the inputs that would produce a plausible-looking
/// pin from something that is not a position.
/// </remarks>
public sealed class GeocentricCoordinateTests
{
    [Fact]
    public void RoundTrips_ThroughEcefAndBack()
    {
        // Hamburg-ish, on the ground.
        var (lat, lon, alt) = (53.5511, 9.9937, 12.0);

        var ecef = GeocentricCoordinate.FromWgs84(lat, lon, alt);
        var back = GeocentricCoordinate.ToWgs84(ecef);

        Assert.NotNull(back);
        Assert.Equal(lat, back!.Value.Latitude, 9);
        Assert.Equal(lon, back.Value.Longitude, 9);
        Assert.Equal(alt, back.Value.Altitude, 6);
    }

    [Theory]
    // Equator / prime meridian — the frame's own origin direction.
    [InlineData(0.0, 0.0, 0.0)]
    // Southern and western hemispheres, so a sign error cannot hide.
    [InlineData(-33.8688, 151.2093, 58.0)]
    [InlineData(-54.8019, -68.3030, 3.0)]
    // High altitude: a DIS exercise may legitimately fly something.
    [InlineData(54.0, 11.5, 11_000.0)]
    // Below the ellipsoid, which is where a lot of real terrain sits.
    [InlineData(31.5, 35.5, -420.0)]
    public void RoundTrips_AcrossTheGlobe(double lat, double lon, double alt)
    {
        var back = GeocentricCoordinate.ToWgs84(
            GeocentricCoordinate.FromWgs84(lat, lon, alt));

        Assert.NotNull(back);
        Assert.Equal(lat, back!.Value.Latitude, 8);
        Assert.Equal(lon, back.Value.Longitude, 8);
        Assert.Equal(alt, back.Value.Altitude, 5);
    }

    [Theory]
    [InlineData(90.0)]
    [InlineData(-90.0)]
    public void HandlesThePoles_WhereLongitudeIsUndefined(double latitude)
    {
        // p == 0 there: every meridian passes through, and the iteration
        // would divide by cos(±90°). The answer is given directly.
        var ecef = GeocentricCoordinate.FromWgs84(latitude, 0.0, 25.0);
        var back = GeocentricCoordinate.ToWgs84(ecef);

        Assert.NotNull(back);
        Assert.Equal(latitude, back!.Value.Latitude, 6);
        Assert.Equal(25.0, back.Value.Altitude, 3);
    }

    [Fact]
    public void RejectsTheOrigin_RatherThanPlottingTheGulfOfGuinea()
    {
        // An unpopulated EntityLocation is all-zero. Converted, that is
        // 0°N/0°E at −6378 km — which renders as a perfectly ordinary
        // pin off the coast of Africa, in the one spot where wrong data
        // looks exactly like right data.
        Assert.Null(GeocentricCoordinate.ToWgs84(Vector3Double.Zero));
    }

    [Theory]
    [InlineData(double.NaN, 0.0, 0.0)]
    [InlineData(0.0, double.PositiveInfinity, 0.0)]
    [InlineData(0.0, 0.0, double.NegativeInfinity)]
    public void RejectsNonFiniteComponents(double x, double y, double z)
    {
        Assert.Null(GeocentricCoordinate.ToWgs84(new Vector3Double(x, y, z)));
    }

    [Fact]
    public void MatchesAKnownFix()
    {
        // Independent check against a published ECEF/geodetic pair rather
        // than against this file's own inverse: a round trip through two
        // functions that share a mistake still round trips.
        // 45°N 0°E at sea level.
        var back = GeocentricCoordinate.ToWgs84(
            new Vector3Double(4_517_590.878, 0.0, 4_487_348.409));

        Assert.NotNull(back);
        Assert.Equal(45.0, back!.Value.Latitude, 5);
        Assert.Equal(0.0, back.Value.Longitude, 9);
        Assert.True(Math.Abs(back.Value.Altitude) < 0.01,
            $"expected sea level, got {back.Value.Altitude:F4} m");
    }
}
