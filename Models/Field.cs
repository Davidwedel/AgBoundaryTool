// AgBoundaryTool
// Boundary manipulation tool compatible with AgValoniaGPS/AgOpenGPS field format

using System;

namespace AgBoundaryTool.Models;

/// <summary>
/// Represents an agricultural field with boundaries and metadata
/// Matches AgOpenGPS/AgValoniaGPS Field.txt format
/// </summary>
public class Field
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field directory path (full path to field folder)
    /// </summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Field origin point (WGS84 coordinates)
    /// All local coordinates are relative to this point
    /// </summary>
    public Position Origin { get; set; } = new Position();

    /// <summary>
    /// Convergence angle in degrees
    /// </summary>
    public double Convergence { get; set; }

    /// <summary>
    /// X offset in meters
    /// </summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// Y offset in meters
    /// </summary>
    public double OffsetY { get; set; }

    /// <summary>
    /// Field boundary (outer + inner boundaries)
    /// </summary>
    public Boundary? Boundary { get; set; }

    /// <summary>
    /// Total area in hectares (calculated from boundary)
    /// </summary>
    public double TotalArea => Boundary?.AreaHectares ?? 0;

    /// <summary>
    /// Date when field was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Date when field was last modified
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.Now;
}
