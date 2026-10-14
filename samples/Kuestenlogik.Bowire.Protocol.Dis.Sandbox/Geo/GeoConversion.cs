// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

/// <summary>A WGS84 position: degrees, degrees, metres above the ellipsoid.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "A parameter type of the public Blazor components (EntityEditor, EntityList, MapView).")]
public readonly record struct GeoPosition(double Latitude, double Longitude, double Altitude);

/// <summary>Earth-centred, earth-fixed coordinates in metres: DIS world coordinates.</summary>
internal readonly record struct Ecef(double X, double Y, double Z);

/// <summary>DIS Euler angles ψ/θ/φ in radians, relative to world coordinates.</summary>
internal readonly record struct DisOrientation(double Psi, double Theta, double Phi);

/// <summary>
/// The geometry the sandbox needs to speak DIS. Written here rather than
/// taken from the plugin, so that the tests compare two implementations.
/// </summary>
internal static class GeoConversion
{
    private const double SemiMajorAxis = 6_378_137.0;
    private const double Flattening = 1.0 / 298.257223563;
    private const double EccentricitySquared = Flattening * (2.0 - Flattening);
    private const double DegToRad = Math.PI / 180.0;

    public static Ecef ToEcef(GeoPosition p)
    {
        var (sinLat, cosLat) = Math.SinCos(p.Latitude * DegToRad);
        var (sinLon, cosLon) = Math.SinCos(p.Longitude * DegToRad);
        var n = SemiMajorAxis / Math.Sqrt(1.0 - (EccentricitySquared * sinLat * sinLat));
        return new Ecef(
            (n + p.Altitude) * cosLat * cosLon,
            (n + p.Altitude) * cosLat * sinLon,
            ((n * (1.0 - EccentricitySquared)) + p.Altitude) * sinLat);
    }

    public static GeoPosition FromEcef(Ecef e)
    {
        var lon = Math.Atan2(e.Y, e.X);
        var p = Math.Sqrt((e.X * e.X) + (e.Y * e.Y));
        if (p < 1e-6)
        {
            var semiMinorAxis = SemiMajorAxis * (1.0 - Flattening);
            return new GeoPosition(e.Z >= 0 ? 90.0 : -90.0, 0.0, Math.Abs(e.Z) - semiMinorAxis);
        }

        var lat = Math.Atan2(e.Z, p * (1.0 - EccentricitySquared));
        for (var i = 0; i < 6; i++)
        {
            var n = RadiusOfCurvature(lat);
            var h = (p / Math.Cos(lat)) - n;
            lat = Math.Atan2(e.Z, p * (1.0 - (EccentricitySquared * n / (n + h))));
        }
        var alt = (p / Math.Cos(lat)) - RadiusOfCurvature(lat);
        return new GeoPosition(lat / DegToRad, lon / DegToRad, alt);
    }

    public static DisOrientation ToDisOrientation(GeoPosition at, double headingDegrees)
    {
        var bodyToWorld = Multiply(NedToWorld(at), Rotation(headingDegrees * DegToRad, 0.0, 0.0));
        return new DisOrientation(
            Math.Atan2(bodyToWorld[1][0], bodyToWorld[0][0]),
            Math.Asin(Math.Clamp(-bodyToWorld[2][0], -1.0, 1.0)),
            Math.Atan2(bodyToWorld[2][1], bodyToWorld[2][2]));
    }

    public static double HeadingFromDisOrientation(GeoPosition at, DisOrientation orientation)
    {
        var bodyToWorld = Rotation(orientation.Psi, orientation.Theta, orientation.Phi);
        var bodyToNed = Multiply(Transpose(NedToWorld(at)), bodyToWorld);
        return NormalizeDegrees(Math.Atan2(bodyToNed[1][0], bodyToNed[0][0]) / DegToRad);
    }

    public static Ecef VelocityToEcef(GeoPosition at, double speedMetresPerSecond, double headingDegrees)
    {
        var (sinHeading, cosHeading) = Math.SinCos(headingDegrees * DegToRad);
        var ned = NedToWorld(at);
        var north = speedMetresPerSecond * cosHeading;
        var east = speedMetresPerSecond * sinHeading;
        return new Ecef(
            (ned[0][0] * north) + (ned[0][1] * east),
            (ned[1][0] * north) + (ned[1][1] * east),
            (ned[2][0] * north) + (ned[2][1] * east));
    }

    public static double NormalizeDegrees(double degrees)
    {
        var d = degrees % 360.0;
        return d < 0 ? d + 360.0 : d;
    }

    private static double RadiusOfCurvature(double latitudeRadians)
    {
        var sinLat = Math.Sin(latitudeRadians);
        return SemiMajorAxis / Math.Sqrt(1.0 - (EccentricitySquared * sinLat * sinLat));
    }

    // Columns: north, east and down at `at`, expressed in world coordinates.
    private static double[][] NedToWorld(GeoPosition at)
    {
        var (sinLat, cosLat) = Math.SinCos(at.Latitude * DegToRad);
        var (sinLon, cosLon) = Math.SinCos(at.Longitude * DegToRad);
        return
        [
            [-sinLat * cosLon, -sinLon, -cosLat * cosLon],
            [-sinLat * sinLon, cosLon, -cosLat * sinLon],
            [cosLat, 0.0, -sinLat],
        ];
    }

    // Z-Y-X rotation (yaw, pitch, roll) from body to the reference frame.
    private static double[][] Rotation(double yaw, double pitch, double roll)
    {
        var (sy, cy) = Math.SinCos(yaw);
        var (sp, cp) = Math.SinCos(pitch);
        var (sr, cr) = Math.SinCos(roll);
        return
        [
            [cy * cp, (cy * sp * sr) - (sy * cr), (cy * sp * cr) + (sy * sr)],
            [sy * cp, (sy * sp * sr) + (cy * cr), (sy * sp * cr) - (cy * sr)],
            [-sp, cp * sr, cp * cr],
        ];
    }

    private static double[][] Multiply(double[][] a, double[][] b)
    {
        var result = new double[3][];
        for (var i = 0; i < 3; i++)
        {
            result[i] = new double[3];
            for (var j = 0; j < 3; j++)
                result[i][j] = (a[i][0] * b[0][j]) + (a[i][1] * b[1][j]) + (a[i][2] * b[2][j]);
        }
        return result;
    }

    private static double[][] Transpose(double[][] m) =>
    [
        [m[0][0], m[1][0], m[2][0]],
        [m[0][1], m[1][1], m[2][1]],
        [m[0][2], m[1][2], m[2][2]],
    ];
}
