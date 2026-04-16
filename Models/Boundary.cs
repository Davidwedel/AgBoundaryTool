// AgBoundaryTool
// Boundary manipulation tool compatible with AgValoniaGPS/AgOpenGPS field format

using System.Collections.Generic;

namespace AgBoundaryTool.Models;

/// <summary>
/// Represents a field boundary with outer boundary and optional inner boundaries (holes)
/// Matches AgOpenGPS/AgValoniaGPS Boundary.txt format
/// </summary>
public class Boundary
{
    /// <summary>
    /// Outer boundary polygon (required)
    /// </summary>
    public BoundaryPolygon? OuterBoundary { get; set; }

    /// <summary>
    /// Inner boundary polygons (holes/exclusions like ponds)
    /// </summary>
    public List<BoundaryPolygon> InnerBoundaries { get; set; } = new List<BoundaryPolygon>();

    /// <summary>
    /// Headland polygon - defines the inner working area boundary
    /// </summary>
    public BoundaryPolygon? HeadlandPolygon { get; set; }

    /// <summary>
    /// Total area in hectares (calculated from outer boundary minus inner boundaries)
    /// </summary>
    public double AreaHectares
    {
        get
        {
            double area = OuterBoundary?.AreaHectares ?? 0;
            foreach (var inner in InnerBoundaries)
            {
                area -= inner.AreaHectares;
            }
            return area;
        }
    }

    /// <summary>
    /// Check if this boundary is valid (has a valid outer boundary)
    /// </summary>
    public bool IsValid => OuterBoundary?.IsValid ?? false;

    /// <summary>
    /// Check if a point is inside the boundary area
    /// </summary>
    public bool IsPointInside(double easting, double northing)
    {
        // First check if inside outer boundary
        if (OuterBoundary == null || !OuterBoundary.IsPointInside(easting, northing))
        {
            return false;
        }

        // Check if inside any inner boundary (hole) - if so, it's outside the usable area
        foreach (var innerBoundary in InnerBoundaries)
        {
            if (!innerBoundary.IsDriveThrough && innerBoundary.IsPointInside(easting, northing))
            {
                return false;
            }
        }

        return true;
    }
}
