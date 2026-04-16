// AgBoundaryTool
// Service for recording boundaries from GPS data

using System;
using System.Collections.Generic;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Services;

/// <summary>
/// Service for recording field boundaries from GPS positions
/// </summary>
public class BoundaryRecordingService
{
    private List<BoundaryPoint> _recordedPoints = new List<BoundaryPoint>();
    private Position? _fieldOrigin;
    private Position? _lastRecordedPosition;
    private double _minimumPointDistance = 2.0; // meters - don't record points closer than this

    /// <summary>
    /// Is currently recording a boundary
    /// </summary>
    public bool IsRecording { get; private set; }

    /// <summary>
    /// Number of points recorded in current boundary
    /// </summary>
    public int PointCount => _recordedPoints.Count;

    /// <summary>
    /// Event fired when a new point is recorded
    /// </summary>
    public event EventHandler<BoundaryPoint>? PointRecorded;

    /// <summary>
    /// Get or set the minimum distance between recorded points (meters)
    /// </summary>
    public double MinimumPointDistance
    {
        get => _minimumPointDistance;
        set => _minimumPointDistance = Math.Max(0.1, value);
    }

    /// <summary>
    /// Start recording a new boundary
    /// </summary>
    public void StartRecording(Position fieldOrigin)
    {
        if (IsRecording)
        {
            throw new InvalidOperationException("Already recording a boundary");
        }

        Console.WriteLine($"[RECORDING] Started boundary recording at origin: {fieldOrigin.Latitude:F6}, {fieldOrigin.Longitude:F6}");
        Console.WriteLine($"[RECORDING] Minimum point distance: {_minimumPointDistance}m");

        _fieldOrigin = fieldOrigin;
        _recordedPoints.Clear();
        _lastRecordedPosition = null;
        IsRecording = true;
    }

    /// <summary>
    /// Record a GPS position as a boundary point
    /// </summary>
    public bool RecordPosition(Position gpsPosition)
    {
        if (!IsRecording || _fieldOrigin == null)
        {
            return false;
        }

        // Check if we should skip this point (too close to last point)
        if (_lastRecordedPosition != null)
        {
            double distance = CoordinateConversionService.Distance(gpsPosition, _lastRecordedPosition);
            if (distance < _minimumPointDistance)
            {
                return false; // Skip point - too close to last one
            }
        }

        // Convert GPS position to local coordinates
        var (easting, northing) = CoordinateConversionService.ToLocal(gpsPosition, _fieldOrigin);

        // Calculate heading (bearing to next point or from previous point)
        double heading = 0;
        if (_lastRecordedPosition != null)
        {
            heading = CoordinateConversionService.Bearing(_lastRecordedPosition, gpsPosition);
            heading = heading * Math.PI / 180.0; // Convert to radians
        }

        // Create and add boundary point
        var boundaryPoint = new BoundaryPoint(easting, northing, heading);
        _recordedPoints.Add(boundaryPoint);
        _lastRecordedPosition = gpsPosition;

        Console.WriteLine($"[RECORDING] Point #{_recordedPoints.Count}: E={easting:F2}m, N={northing:F2}m");

        PointRecorded?.Invoke(this, boundaryPoint);
        return true;
    }

    /// <summary>
    /// Stop recording and return the recorded boundary polygon
    /// </summary>
    public BoundaryPolygon? StopRecording()
    {
        if (!IsRecording)
        {
            return null;
        }

        IsRecording = false;

        Console.WriteLine($"[RECORDING] Stopped. Total points recorded: {_recordedPoints.Count}");

        if (_recordedPoints.Count < 3)
        {
            Console.WriteLine("[RECORDING] ERROR: Not enough points for a valid polygon (minimum 3 required)");
            _recordedPoints.Clear();
            return null;
        }

        // Update headings to point along the boundary
        UpdateHeadings();

        var polygon = new BoundaryPolygon
        {
            Points = new List<BoundaryPoint>(_recordedPoints),
            IsDriveThrough = false
        };

        Console.WriteLine($"[RECORDING] Created boundary polygon: {polygon.Points.Count} points, Area: {polygon.AreaHectares:F2} ha");

        _recordedPoints.Clear();
        _fieldOrigin = null;
        _lastRecordedPosition = null;

        return polygon;
    }

    /// <summary>
    /// Cancel recording without creating a boundary
    /// </summary>
    public void CancelRecording()
    {
        IsRecording = false;
        _recordedPoints.Clear();
        _fieldOrigin = null;
        _lastRecordedPosition = null;
    }

    /// <summary>
    /// Update headings for all recorded points to point along the boundary
    /// </summary>
    private void UpdateHeadings()
    {
        if (_recordedPoints.Count < 2) return;

        for (int i = 0; i < _recordedPoints.Count; i++)
        {
            int nextIdx = (i + 1) % _recordedPoints.Count;

            var current = _recordedPoints[i];
            var next = _recordedPoints[nextIdx];

            double dE = next.Easting - current.Easting;
            double dN = next.Northing - current.Northing;

            double heading = Math.Atan2(dE, dN); // radians

            current.Heading = heading;
        }
    }

    /// <summary>
    /// Get the current recorded points (for preview)
    /// </summary>
    public IReadOnlyList<BoundaryPoint> GetRecordedPoints()
    {
        return _recordedPoints.AsReadOnly();
    }

    /// <summary>
    /// Remove the last recorded point
    /// </summary>
    public bool RemoveLastPoint()
    {
        if (!IsRecording || _recordedPoints.Count == 0)
        {
            return false;
        }

        _recordedPoints.RemoveAt(_recordedPoints.Count - 1);

        // Update last recorded position
        if (_recordedPoints.Count > 0)
        {
            var lastPoint = _recordedPoints[_recordedPoints.Count - 1];
            if (_fieldOrigin != null)
            {
                var (lat, lon) = CoordinateConversionService.ToGlobal(lastPoint.Easting, lastPoint.Northing, _fieldOrigin);
                _lastRecordedPosition = new Position { Latitude = lat, Longitude = lon };
            }
        }
        else
        {
            _lastRecordedPosition = null;
        }

        return true;
    }
}
