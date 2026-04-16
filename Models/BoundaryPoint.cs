// AgBoundaryTool
// Boundary manipulation tool compatible with AgValoniaGPS/AgOpenGPS field format

namespace AgBoundaryTool.Models;

/// <summary>
/// Represents a single point in a boundary polygon
/// Coordinates are in local system (meters from field origin)
/// </summary>
public class BoundaryPoint
{
    /// <summary>
    /// Easting (X coordinate) in meters
    /// </summary>
    public double Easting { get; set; }

    /// <summary>
    /// Northing (Y coordinate) in meters
    /// </summary>
    public double Northing { get; set; }

    /// <summary>
    /// Heading/direction at this point in radians
    /// </summary>
    public double Heading { get; set; }

    public BoundaryPoint() { }

    public BoundaryPoint(double easting, double northing, double heading = 0)
    {
        Easting = easting;
        Northing = northing;
        Heading = heading;
    }

    public override string ToString()
    {
        return $"E:{Easting:F2}, N:{Northing:F2}, H:{Heading:F3}";
    }
}
