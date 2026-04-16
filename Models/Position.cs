// AgBoundaryTool
// Boundary manipulation tool compatible with AgValoniaGPS/AgOpenGPS field format

namespace AgBoundaryTool.Models;

/// <summary>
/// Represents a geographic position with latitude, longitude, and local coordinates
/// </summary>
public class Position
{
    /// <summary>
    /// Latitude in decimal degrees
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Longitude in decimal degrees
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Altitude in meters above sea level
    /// </summary>
    public double Altitude { get; set; }

    /// <summary>
    /// Local Easting coordinate in meters (relative to field origin)
    /// </summary>
    public double Easting { get; set; }

    /// <summary>
    /// Local Northing coordinate in meters (relative to field origin)
    /// </summary>
    public double Northing { get; set; }

    /// <summary>
    /// Heading in degrees (0-360)
    /// </summary>
    public double Heading { get; set; }

    /// <summary>
    /// Speed in meters per second
    /// </summary>
    public double Speed { get; set; }

    /// <summary>
    /// GPS fix quality (0 = no fix, 1 = GPS, 2 = DGPS, 4 = RTK fixed, 5 = RTK float)
    /// </summary>
    public int FixQuality { get; set; }

    /// <summary>
    /// Number of satellites in use
    /// </summary>
    public int SatelliteCount { get; set; }
}
