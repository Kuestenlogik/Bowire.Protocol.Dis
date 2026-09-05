// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Records;

/// <summary>
/// Converts a DIS entity location from the geocentric (ECEF) frame the
/// wire uses into geodetic latitude / longitude / altitude on the WGS84
/// ellipsoid.
/// </summary>
/// <remarks>
/// <para>
/// IEEE 1278.1 §5.3.32 puts <c>EntityLocation</c> in a right-handed
/// earth-centred, earth-fixed frame: metres from the centre of the
/// earth, X through 0°N/0°E, Z through the north pole. That is the right
/// representation for a simulation — no singularity at the poles, no
/// datum argument, and translation is addition — and the wrong one for
/// anything that wants to show an operator where something is.
/// </para>
/// <para>
/// Bowire's map widget reads WGS84 degrees, which is why this exists:
/// without it a DIS stream reaches the workbench carrying a position in
/// every PDU and no way to plot it. The conversion belongs here rather
/// than in the envelope builder because it is a property of the
/// coordinate frame, not of how one protocol chooses to present it.
/// </para>
/// </remarks>
public static class GeocentricCoordinate
{
    /// <summary>WGS84 semi-major axis, in metres.</summary>
    private const double SemiMajorAxis = 6_378_137.0;

    /// <summary>WGS84 flattening.</summary>
    private const double Flattening = 1.0 / 298.257_223_563;

    /// <summary>First eccentricity squared, e² = f(2 − f).</summary>
    private const double EccentricitySquared = Flattening * (2.0 - Flattening);

    /// <summary>
    /// Convert an ECEF position to geodetic latitude / longitude /
    /// altitude, or <see langword="null"/> when the input is not a
    /// position at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bowring's iteration rather than a closed form. Latitude and
    /// height are mutually defined — the prime-vertical radius depends
    /// on the latitude being solved for — so the loop refines both
    /// together. It converges to well under a millimetre in four passes
    /// for anything near the surface; five are run because the cost is
    /// nothing and a DIS exercise may legitimately place an entity in
    /// orbit.
    /// </para>
    /// <para>
    /// The all-zero vector is rejected explicitly. It is the centre of
    /// the earth, which no entity occupies, and it is what a PDU carries
    /// when the location was never populated — converting it yields a
    /// point off the coast of Africa at −6378 km altitude, which plots
    /// as a real pin at 0°N/0°E and looks like data.
    /// </para>
    /// </remarks>
    public static (double Latitude, double Longitude, double Altitude)? ToWgs84(
        Vector3Double location)
    {
        var (x, y, z) = (location.X, location.Y, location.Z);

        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            return null;
        if (x == 0.0 && y == 0.0 && z == 0.0)
            return null;

        var p = Math.Sqrt((x * x) + (y * y));
        var longitude = Math.Atan2(y, x);

        // At the poles p is zero: longitude is undefined (any meridian
        // passes through), latitude is ±90°, and the iteration below
        // would divide by cos(±90°). Answer directly instead.
        if (p < 1e-9)
        {
            var polarSign = z >= 0 ? 1.0 : -1.0;
            var polarRadius = SemiMajorAxis * (1.0 - Flattening);
            return (polarSign * 90.0, 0.0, Math.Abs(z) - polarRadius);
        }

        var latitude = Math.Atan2(z, p * (1.0 - EccentricitySquared));
        var altitude = 0.0;

        for (var i = 0; i < 5; i++)
        {
            var sinLat = Math.Sin(latitude);
            var primeVertical = SemiMajorAxis
                / Math.Sqrt(1.0 - (EccentricitySquared * sinLat * sinLat));
            altitude = (p / Math.Cos(latitude)) - primeVertical;
            latitude = Math.Atan2(
                z,
                p * (1.0 - (EccentricitySquared * primeVertical / (primeVertical + altitude))));
        }

        return (
            Latitude: latitude * 180.0 / Math.PI,
            Longitude: longitude * 180.0 / Math.PI,
            Altitude: altitude);
    }

    /// <summary>
    /// Convert geodetic latitude / longitude / altitude to ECEF — the
    /// inverse of <see cref="ToWgs84"/>.
    /// </summary>
    /// <remarks>
    /// Closed form, no iteration: the forward direction has no mutual
    /// definition to resolve. Present so a test can assert the round
    /// trip, and so anything building DIS traffic from a map position
    /// (a mock emitter, a sample generator) has one implementation to
    /// use rather than its own copy of the ellipsoid constants.
    /// </remarks>
    public static Vector3Double FromWgs84(double latitude, double longitude, double altitude)
    {
        var latRad = latitude * Math.PI / 180.0;
        var lonRad = longitude * Math.PI / 180.0;
        var sinLat = Math.Sin(latRad);
        var cosLat = Math.Cos(latRad);
        var primeVertical = SemiMajorAxis
            / Math.Sqrt(1.0 - (EccentricitySquared * sinLat * sinLat));

        return new Vector3Double(
            (primeVertical + altitude) * cosLat * Math.Cos(lonRad),
            (primeVertical + altitude) * cosLat * Math.Sin(lonRad),
            ((primeVertical * (1.0 - EccentricitySquared)) + altitude) * sinLat);
    }
}
