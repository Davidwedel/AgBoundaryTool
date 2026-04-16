// AgBoundaryTool
// Coordinate conversion service for GPS to local coordinates

using System;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Services;

/// <summary>
/// Service for converting between WGS84 (lat/lon) and local (easting/northing) coordinates
/// Uses simplified flat-earth approximation suitable for small agricultural fields
/// </summary>
public static class CoordinateConversionService
{
    private const double EARTH_RADIUS = 6371000.0; // meters
    private const double DEG_TO_RAD = Math.PI / 180.0;

    /// <summary>
    /// Convert GPS position (lat/lon) to local coordinates (easting/northing) relative to origin
    /// </summary>
    public static (double easting, double northing) ToLocal(Position gpsPosition, Position origin)
    {
        return ToLocal(gpsPosition.Latitude, gpsPosition.Longitude, origin.Latitude, origin.Longitude);
    }

    /// <summary>
    /// Convert GPS coordinates (lat/lon) to local coordinates (easting/northing) relative to origin
    /// Uses simple Equirectangular projection (flat-earth approximation)
    /// Accurate enough for fields up to ~50km from origin
    /// </summary>
    public static (double easting, double northing) ToLocal(double latitude, double longitude, double originLat, double originLon)
    {
        double latRad = originLat * DEG_TO_RAD;

        // Calculate easting (X) and northing (Y) in meters
        double easting = (longitude - originLon) * DEG_TO_RAD * EARTH_RADIUS * Math.Cos(latRad);
        double northing = (latitude - originLat) * DEG_TO_RAD * EARTH_RADIUS;

        return (easting, northing);
    }

    /// <summary>
    /// Convert local coordinates (easting/northing) to GPS coordinates (lat/lon) relative to origin
    /// </summary>
    public static (double latitude, double longitude) ToGlobal(double easting, double northing, Position origin)
    {
        return ToGlobal(easting, northing, origin.Latitude, origin.Longitude);
    }

    /// <summary>
    /// Convert local coordinates (easting/northing) to GPS coordinates (lat/lon) relative to origin
    /// </summary>
    public static (double latitude, double longitude) ToGlobal(double easting, double northing, double originLat, double originLon)
    {
        double latRad = originLat * DEG_TO_RAD;

        double latitude = originLat + (northing / EARTH_RADIUS) / DEG_TO_RAD;
        double longitude = originLon + (easting / (EARTH_RADIUS * Math.Cos(latRad))) / DEG_TO_RAD;

        return (latitude, longitude);
    }

    /// <summary>
    /// Calculate distance between two GPS positions in meters
    /// </summary>
    public static double Distance(Position pos1, Position pos2)
    {
        return Distance(pos1.Latitude, pos1.Longitude, pos2.Latitude, pos2.Longitude);
    }

    /// <summary>
    /// Calculate distance between two GPS coordinates in meters (Haversine formula)
    /// </summary>
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * DEG_TO_RAD;
        double dLon = (lon2 - lon1) * DEG_TO_RAD;

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * DEG_TO_RAD) * Math.Cos(lat2 * DEG_TO_RAD) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EARTH_RADIUS * c;
    }

    /// <summary>
    /// Calculate bearing from pos1 to pos2 in degrees (0-360)
    /// </summary>
    public static double Bearing(Position pos1, Position pos2)
    {
        return Bearing(pos1.Latitude, pos1.Longitude, pos2.Latitude, pos2.Longitude);
    }

    /// <summary>
    /// Calculate bearing from point 1 to point 2 in degrees (0-360)
    /// </summary>
    public static double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = (lon2 - lon1) * DEG_TO_RAD;
        double lat1Rad = lat1 * DEG_TO_RAD;
        double lat2Rad = lat2 * DEG_TO_RAD;

        double y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                   Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

        double bearingRad = Math.Atan2(y, x);
        double bearingDeg = bearingRad / DEG_TO_RAD;

        // Normalize to 0-360
        return (bearingDeg + 360) % 360;
    }
}
