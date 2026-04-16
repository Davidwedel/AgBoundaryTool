// AgBoundaryTool
// Custom control for visualizing boundaries and GPS position

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Views.Controls;

/// <summary>
/// Custom control for visualizing field boundaries and GPS position
/// </summary>
public class BoundaryVisualizationControl : Control
{
    private List<BoundaryPoint> _boundaryPoints = new List<BoundaryPoint>();
    private Position? _vehiclePosition;
    private double _zoom = 1.0;
    private Point _offset = new Point(0, 0);
    private bool _autoZoom = true;
    private int _vehicleUpdateCount = 0;

    // Visual settings
    private const double GridSize = 50.0; // meters
    private const double CrosshairSize = 20.0; // pixels
    private const double PointRadius = 4.0; // pixels
    private const double LineThickness = 2.0;

    // Styling
    private readonly SolidColorBrush _gridBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
    private readonly SolidColorBrush _boundaryBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
    private readonly SolidColorBrush _vehicleBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
    private readonly SolidColorBrush _backgroundBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40));
    private readonly Pen _gridPen;
    private readonly Pen _boundaryPen;
    private readonly Pen _vehiclePen;

    public BoundaryVisualizationControl()
    {
        _gridPen = new Pen(_gridBrush, 1.0);
        _boundaryPen = new Pen(_boundaryBrush, LineThickness);
        _vehiclePen = new Pen(_vehicleBrush, 2.0);

        // Update on data changes
        ClipToBounds = true;
    }

    /// <summary>
    /// Set boundary points to visualize
    /// </summary>
    public void SetBoundaryPoints(IEnumerable<BoundaryPoint> points)
    {
        _boundaryPoints = points.ToList();

        Console.WriteLine($"[VISUALIZATION] SetBoundaryPoints called: {_boundaryPoints.Count} points");

        if (_autoZoom && _boundaryPoints.Count > 0)
        {
            AutoZoomToBoundary();
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Set vehicle position
    /// </summary>
    public void SetVehiclePosition(Position position, double easting, double northing)
    {
        _vehiclePosition = new Position
        {
            Latitude = position.Latitude,
            Longitude = position.Longitude,
            Heading = position.Heading,
            Speed = position.Speed,
            Easting = easting,
            Northing = northing,
            FixQuality = position.FixQuality,
            SatelliteCount = position.SatelliteCount
        };

        // Log first few updates
        if (_vehicleUpdateCount++ < 5)
        {
            Console.WriteLine($"[VISUALIZATION] SetVehiclePosition #{_vehicleUpdateCount}: E={easting:F2}, N={northing:F2}, Heading={position.Heading:F1}°");
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Clear all visualizations
    /// </summary>
    public void Clear()
    {
        _boundaryPoints.Clear();
        _vehiclePosition = null;
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    /// <summary>
    /// Auto-zoom to fit boundary
    /// </summary>
    private void AutoZoomToBoundary()
    {
        if (_boundaryPoints.Count == 0) return;

        double minE = _boundaryPoints.Min(p => p.Easting);
        double maxE = _boundaryPoints.Max(p => p.Easting);
        double minN = _boundaryPoints.Min(p => p.Northing);
        double maxN = _boundaryPoints.Max(p => p.Northing);

        double width = maxE - minE;
        double height = maxN - minN;

        if (width < 10) width = 100;
        if (height < 10) height = 100;

        double centerE = (minE + maxE) / 2.0;
        double centerN = (minN + maxN) / 2.0;

        // Calculate zoom to fit with 10% padding
        double zoomX = (Bounds.Width * 0.9) / width;
        double zoomY = (Bounds.Height * 0.9) / height;
        _zoom = Math.Min(zoomX, zoomY);

        // Center offset
        _offset = new Point(
            Bounds.Width / 2.0 - centerE * _zoom,
            Bounds.Height / 2.0 + centerN * _zoom // Flip Y axis
        );
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Console.WriteLine($"[VISUALIZATION] Render called: Bounds={Bounds.Width}x{Bounds.Height}, Points={_boundaryPoints.Count}, Vehicle={(_vehiclePosition != null ? "Yes" : "No")}");

        // Draw background
        context.FillRectangle(_backgroundBrush, Bounds);

        // Draw grid
        DrawGrid(context);

        // Draw boundary
        DrawBoundary(context);

        // Draw vehicle crosshair
        DrawVehicle(context);

        // Draw info text
        DrawInfoText(context);
    }

    private void DrawGrid(DrawingContext context)
    {
        double gridSpacing = GridSize * _zoom;

        if (gridSpacing < 10) return; // Don't draw if too dense

        // Calculate grid origin offset
        double offsetX = _offset.X % gridSpacing;
        double offsetY = _offset.Y % gridSpacing;

        // Vertical lines
        for (double x = offsetX; x < Bounds.Width; x += gridSpacing)
        {
            context.DrawLine(_gridPen, new Point(x, 0), new Point(x, Bounds.Height));
        }

        // Horizontal lines
        for (double y = offsetY; y < Bounds.Height; y += gridSpacing)
        {
            context.DrawLine(_gridPen, new Point(0, y), new Point(Bounds.Width, y));
        }
    }

    private void DrawBoundary(DrawingContext context)
    {
        if (_boundaryPoints.Count < 2) return;

        // Draw boundary lines
        for (int i = 0; i < _boundaryPoints.Count; i++)
        {
            var p1 = _boundaryPoints[i];
            var p2 = _boundaryPoints[(i + 1) % _boundaryPoints.Count];

            var screen1 = WorldToScreen(p1.Easting, p1.Northing);
            var screen2 = WorldToScreen(p2.Easting, p2.Northing);

            context.DrawLine(_boundaryPen, screen1, screen2);
        }

        // Draw boundary points
        foreach (var point in _boundaryPoints)
        {
            var screenPos = WorldToScreen(point.Easting, point.Northing);
            context.FillRectangle(
                _boundaryBrush,
                new Rect(screenPos.X - PointRadius / 2, screenPos.Y - PointRadius / 2,
                         PointRadius, PointRadius)
            );
        }
    }

    private void DrawVehicle(DrawingContext context)
    {
        if (_vehiclePosition == null) return;

        var screenPos = WorldToScreen(_vehiclePosition.Easting, _vehiclePosition.Northing);

        // Make crosshair bigger and more visible
        double halfSize = CrosshairSize;

        // Draw outer circle first
        var outerCircle = new EllipseGeometry(new Rect(
            screenPos.X - halfSize, screenPos.Y - halfSize,
            CrosshairSize * 2, CrosshairSize * 2));
        context.DrawGeometry(null, _vehiclePen, outerCircle);

        // Draw crosshair lines (thicker)
        var thickPen = new Pen(_vehicleBrush, 3.0);

        // Horizontal line
        context.DrawLine(thickPen,
            new Point(screenPos.X - halfSize, screenPos.Y),
            new Point(screenPos.X + halfSize, screenPos.Y));

        // Vertical line
        context.DrawLine(thickPen,
            new Point(screenPos.X, screenPos.Y - halfSize),
            new Point(screenPos.X, screenPos.Y + halfSize));

        // Draw center dot
        var centerDot = new EllipseGeometry(new Rect(
            screenPos.X - 3, screenPos.Y - 3, 6, 6));
        context.DrawGeometry(_vehicleBrush, new Pen(_vehicleBrush, 1), centerDot);

        // Draw heading indicator (small line showing direction)
        double headingRad = _vehiclePosition.Heading * Math.PI / 180.0;
        double lineLength = halfSize * 1.8;
        double endX = screenPos.X + Math.Sin(headingRad) * lineLength;
        double endY = screenPos.Y - Math.Cos(headingRad) * lineLength; // Flip Y

        var headingPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 0)), 4.0);
        context.DrawLine(headingPen, screenPos, new Point(endX, endY));
    }

    private void DrawInfoText(DrawingContext context)
    {
        var textBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
        var typeface = new Typeface("Arial");

        string info = $"Points: {_boundaryPoints.Count}";
        if (_vehiclePosition != null)
        {
            info += $" | Position: {_vehiclePosition.Easting:F1}m E, {_vehiclePosition.Northing:F1}m N";
        }
        info += $" | Zoom: {_zoom:F2}x";

        var text = new FormattedText(
            info,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            textBrush);
        context.DrawText(text, new Point(10, 10));

        // Draw scale bar (bottom left)
        double scaleBarMeters = 50.0;
        double scaleBarPixels = scaleBarMeters * _zoom;
        double scaleY = Bounds.Height - 30;

        context.DrawLine(new Pen(textBrush, 2),
            new Point(10, scaleY),
            new Point(10 + scaleBarPixels, scaleY));

        // Tick marks
        context.DrawLine(new Pen(textBrush, 2),
            new Point(10, scaleY - 5),
            new Point(10, scaleY + 5));
        context.DrawLine(new Pen(textBrush, 2),
            new Point(10 + scaleBarPixels, scaleY - 5),
            new Point(10 + scaleBarPixels, scaleY + 5));

        var scaleText = new FormattedText(
            $"{scaleBarMeters}m",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            10,
            textBrush);
        context.DrawText(scaleText,
            new Point(10 + scaleBarPixels / 2 - scaleText.Width / 2, scaleY + 8));
    }

    private Point WorldToScreen(double easting, double northing)
    {
        return new Point(
            easting * _zoom + _offset.X,
            -northing * _zoom + _offset.Y // Flip Y axis (screen Y increases downward)
        );
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Re-zoom if auto-zoom is enabled
        if (_autoZoom && _boundaryPoints.Count > 0)
        {
            AutoZoomToBoundary();
        }
        return finalSize;
    }
}
