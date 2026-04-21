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
    private List<List<BoundaryPoint>> _innerBoundaries = new List<List<BoundaryPoint>>();
    private List<BoundaryPoint> _notchPoints = new List<BoundaryPoint>();
    private List<Flag> _flags = new List<Flag>();
    private Position? _vehiclePosition;
    private double _zoom = 1.0;
    private Point _offset = new Point(0, 0);
    private bool _autoZoom = true;
    private int _vehicleUpdateCount = 0;
    private (double x1, double y1, double x2, double y2)? _splitLine = null;

    // Panning state
    private bool _isPanning = false;
    private Point _panStartPoint;
    private bool _manualPanActive = false; // User has manually panned, disable auto-center

    // Point selection state
    private HashSet<int> _selectedPointIndices = new HashSet<int>();
    private int? _firstSelectedIndex = null; // For range selection

    // Boundary selection state
    private int _selectedInnerBoundaryIndex = -2; // -2 = none, -1 = outer boundary, 0+ = inner boundary index
    private bool _innerBoundarySelectionMode = false; // When true, clicking selects boundaries

    // Visual settings
    private const double GridSize = 50.0; // meters
    private const double CrosshairSize = 20.0; // pixels
    private const double PointRadius = 4.0; // pixels
    private const double LineThickness = 2.0;

    // Styling
    private readonly SolidColorBrush _gridBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
    private readonly SolidColorBrush _boundaryBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0)); // Green
    private readonly SolidColorBrush _innerBoundaryBrush = new SolidColorBrush(Color.FromRgb(200, 0, 0)); // Red
    private readonly SolidColorBrush _selectedInnerBoundaryBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange - highlighted inner boundary
    private readonly SolidColorBrush _vehicleBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
    private readonly SolidColorBrush _notchBrush = new SolidColorBrush(Color.FromRgb(200, 0, 200)); // Purple/Magenta
    private readonly SolidColorBrush _selectedBrush = new SolidColorBrush(Color.FromRgb(255, 255, 0)); // Yellow
    private readonly SolidColorBrush _flagTextBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255)); // White
    private readonly SolidColorBrush _splitLineBrush = new SolidColorBrush(Color.FromRgb(0, 255, 255)); // Cyan
    private readonly SolidColorBrush _backgroundBrush = new SolidColorBrush(Color.FromRgb(40, 40, 40));
    private readonly Pen _gridPen;
    private readonly Pen _boundaryPen;
    private readonly Pen _innerBoundaryPen;
    private readonly Pen _selectedInnerBoundaryPen;
    private readonly Pen _vehiclePen;
    private readonly Pen _notchPen;
    private readonly Pen _selectedPen;
    private readonly Pen _splitLinePen;

    public BoundaryVisualizationControl()
    {
        _gridPen = new Pen(_gridBrush, 1.0);
        _boundaryPen = new Pen(_boundaryBrush, LineThickness);
        _innerBoundaryPen = new Pen(_innerBoundaryBrush, LineThickness);
        _selectedInnerBoundaryPen = new Pen(_selectedInnerBoundaryBrush, LineThickness + 1);
        _vehiclePen = new Pen(_vehicleBrush, 2.0);
        _notchPen = new Pen(_notchBrush, LineThickness + 1);
        _selectedPen = new Pen(_selectedBrush, 3.0);
        _splitLinePen = new Pen(_splitLineBrush, 3.0); // Thick cyan line for split

        // Update on data changes
        ClipToBounds = true;
        Focusable = true; // Enable keyboard input

        // Enable mouse wheel zoom
        this.PointerWheelChanged += OnPointerWheelChanged;

        // Enable panning and point selection
        this.PointerPressed += OnPointerPressed;
        this.PointerMoved += OnPointerMoved;
        this.PointerReleased += OnPointerReleased;

        // Enable keyboard for Delete key
        this.KeyDown += OnKeyDown;
    }

    // Event for notifying when points should be deleted
    public event EventHandler<List<int>>? PointsDeleteRequested;

    // Event for notifying when vehicle should be snapped to a position (right-click)
    public event EventHandler<(double easting, double northing)>? VehicleSnapRequested;

    // Event for notifying when an inner boundary is selected
    public event EventHandler<int>? InnerBoundarySelected;

    private void OnPointerWheelChanged(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        // Get wheel delta (positive = zoom in, negative = zoom out)
        double delta = e.Delta.Y;

        // Adjust zoom (multiply by 1.1 for each wheel tick)
        double zoomFactor = delta > 0 ? 1.1 : 0.9;
        double newZoom = _zoom * zoomFactor;

        // Clamp zoom between 0.1 and 20
        newZoom = Math.Max(0.1, Math.Min(20.0, newZoom));

        if (Math.Abs(newZoom - _zoom) > 0.001)
        {
            // Get mouse position for zoom center
            var mousePos = e.GetPosition(this);

            // Calculate world coordinates at mouse position before zoom
            double worldX = (mousePos.X - _offset.X) / _zoom;
            double worldY = (mousePos.Y - _offset.Y) / -_zoom;

            // Update zoom
            _zoom = newZoom;

            // Adjust offset to keep mouse position at same world coordinates
            _offset = new Point(
                mousePos.X - worldX * _zoom,
                mousePos.Y + worldY * _zoom
            );

            Console.WriteLine($"[VISUALIZATION] Zoom: {_zoom:F2}x");

            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsRightButtonPressed)
        {
            // Right-click: Snap vehicle to clicked location
            var mousePos = e.GetPosition(this);

            // Convert screen coordinates to world coordinates
            double worldX = (mousePos.X - _offset.X) / _zoom;
            double worldY = (mousePos.Y - _offset.Y) / -_zoom; // Negative because Y is inverted

            // Fire event to notify that vehicle should be snapped
            VehicleSnapRequested?.Invoke(this, (worldX, worldY));

            Console.WriteLine($"[VISUALIZATION] Right-click snap requested: E={worldX:F2}, N={worldY:F2}");
            e.Handled = true;
        }
        else if (properties.IsLeftButtonPressed)
        {
            var mousePos = e.GetPosition(this);
            var modifiers = e.KeyModifiers;

            // If in boundary selection mode, check for boundary clicks first
            if (_innerBoundarySelectionMode)
            {
                // Check inner boundaries first (they're on top visually)
                int clickedInnerBoundary = FindInnerBoundaryAtPosition(mousePos);
                if (clickedInnerBoundary >= 0)
                {
                    _selectedInnerBoundaryIndex = clickedInnerBoundary;
                    InnerBoundarySelected?.Invoke(this, clickedInnerBoundary);
                    Console.WriteLine($"[VISUALIZATION] Inner boundary {clickedInnerBoundary} selected");
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                // Check outer boundary
                bool clickedOuterBoundary = IsOuterBoundaryAtPosition(mousePos);
                if (clickedOuterBoundary)
                {
                    _selectedInnerBoundaryIndex = -1; // -1 means outer boundary
                    InnerBoundarySelected?.Invoke(this, -1); // Fire event with -1 for outer boundary
                    Console.WriteLine($"[VISUALIZATION] Outer boundary selected");
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }
            }

            // Check if clicking on a boundary point
            int? clickedPointIndex = FindPointAtPosition(mousePos);

            if (clickedPointIndex.HasValue)
            {
                // Clicked on a point - handle selection
                Focus(); // Ensure we have focus for keyboard events

                if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
                {
                    // Ctrl+Click: Toggle selection
                    if (_selectedPointIndices.Contains(clickedPointIndex.Value))
                    {
                        _selectedPointIndices.Remove(clickedPointIndex.Value);
                        _firstSelectedIndex = null;
                    }
                    else
                    {
                        _selectedPointIndices.Add(clickedPointIndex.Value);
                        _firstSelectedIndex = clickedPointIndex.Value;
                    }
                }
                else
                {
                    // Regular click: Handle range selection or single selection
                    if (_firstSelectedIndex.HasValue && _selectedPointIndices.Contains(_firstSelectedIndex.Value))
                    {
                        // Second click - select range
                        SelectRange(_firstSelectedIndex.Value, clickedPointIndex.Value);
                        _firstSelectedIndex = null;
                    }
                    else
                    {
                        // First click - single selection
                        _selectedPointIndices.Clear();
                        _selectedPointIndices.Add(clickedPointIndex.Value);
                        _firstSelectedIndex = clickedPointIndex.Value;
                    }
                }

                InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                // Didn't click on a point - start panning and clear selection
                _selectedPointIndices.Clear();
                _firstSelectedIndex = null;
                _isPanning = true;
                _panStartPoint = mousePos;
                InvalidateVisual();
                e.Handled = true;
            }
        }
    }

    private void OnPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_isPanning)
        {
            var currentPos = e.GetPosition(this);
            var delta = currentPos - _panStartPoint;

            // Update offset by the drag delta
            _offset = new Point(_offset.X + delta.X, _offset.Y + delta.Y);

            // Update start point for next move
            _panStartPoint = currentPos;

            // Disable auto-centering once user has manually panned
            _manualPanActive = true;

            InvalidateVisual();
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Set boundary points to visualize
    /// </summary>
    public void SetBoundaryPoints(IEnumerable<BoundaryPoint> points)
    {
        _boundaryPoints = points.ToList();

        if (_autoZoom && _boundaryPoints.Count > 0)
        {
            AutoZoomToBoundary();
            // Reset manual pan when auto-zooming to boundary
            _manualPanActive = false;
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Set notch points to visualize
    /// </summary>
    public void SetNotchPoints(IEnumerable<BoundaryPoint> points)
    {
        _notchPoints = points.ToList();
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Set inner boundaries (holes/exclusions) to visualize
    /// </summary>
    public void SetInnerBoundaries(IEnumerable<IEnumerable<BoundaryPoint>> innerBoundaries)
    {
        _innerBoundaries = innerBoundaries.Select(b => b.ToList()).ToList();
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

        // Log every update for debugging
        _vehicleUpdateCount++;
        if (_vehicleUpdateCount % 10 == 0) // Log every 10th update
        {
            Console.WriteLine($"[VISUALIZATION] SetVehiclePosition #{_vehicleUpdateCount}: E={easting:F2}, N={northing:F2}, Heading={position.Heading:F1}°");
        }

        // Center camera on vehicle only if user hasn't manually panned
        if (!_manualPanActive)
        {
            CenterOnVehicle();
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    public void SetFlags(IEnumerable<Flag> flags)
    {
        _flags = flags.ToList();
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    /// <summary>
    /// Set the split line for visualization (used during field splitting)
    /// </summary>
    public void SetSplitLine(double x1, double y1, double x2, double y2)
    {
        _splitLine = (x1, y1, x2, y2);
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Clear the split line visualization
    /// </summary>
    public void ClearSplitLine()
    {
        _splitLine = null;
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Enable inner boundary selection mode (clicking selects inner boundaries)
    /// </summary>
    public void EnableInnerBoundarySelectionMode()
    {
        _innerBoundarySelectionMode = true;
        Console.WriteLine("[VISUALIZATION] Inner boundary selection mode enabled");
    }

    /// <summary>
    /// Disable inner boundary selection mode
    /// </summary>
    public void DisableInnerBoundarySelectionMode()
    {
        _innerBoundarySelectionMode = false;
        _selectedInnerBoundaryIndex = -2; // -2 = no selection
        Console.WriteLine("[VISUALIZATION] Boundary selection mode disabled");
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Set the selected inner boundary index
    /// </summary>
    public void SetSelectedInnerBoundary(int index)
    {
        _selectedInnerBoundaryIndex = index;
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    /// <summary>
    /// Get the currently selected inner boundary index (-1 if none)
    /// </summary>
    public int GetSelectedInnerBoundary()
    {
        return _selectedInnerBoundaryIndex;
    }

    /// <summary>
    /// Clear all visualizations
    /// </summary>
    public void Clear()
    {
        _boundaryPoints.Clear();
        _innerBoundaries.Clear();
        _notchPoints.Clear();
        _flags.Clear();
        _vehiclePosition = null;
        _splitLine = null;
        _manualPanActive = false; // Reset manual pan flag
        _selectedPointIndices.Clear();
        _firstSelectedIndex = null;
        Dispatcher.UIThread.Post(InvalidateVisual);
    }

    /// <summary>
    /// Find if a boundary point is at the given screen position
    /// Uses flat indexing: outer boundary points first, then inner boundary points
    /// </summary>
    private int? FindPointAtPosition(Point screenPos)
    {
        const double hitRadius = 10.0; // pixels - click tolerance

        // Check outer boundary points
        for (int i = 0; i < _boundaryPoints.Count; i++)
        {
            var point = _boundaryPoints[i];
            var pointScreen = WorldToScreen(point.Easting, point.Northing);

            double distance = Math.Sqrt(
                Math.Pow(screenPos.X - pointScreen.X, 2) +
                Math.Pow(screenPos.Y - pointScreen.Y, 2)
            );

            if (distance <= hitRadius)
            {
                return i;
            }
        }

        // Check inner boundary points (indexed after outer boundary)
        int currentIndex = _boundaryPoints.Count;
        foreach (var innerBoundary in _innerBoundaries)
        {
            for (int i = 0; i < innerBoundary.Count; i++)
            {
                var point = innerBoundary[i];
                var pointScreen = WorldToScreen(point.Easting, point.Northing);

                double distance = Math.Sqrt(
                    Math.Pow(screenPos.X - pointScreen.X, 2) +
                    Math.Pow(screenPos.Y - pointScreen.Y, 2)
                );

                if (distance <= hitRadius)
                {
                    return currentIndex + i;
                }
            }
            currentIndex += innerBoundary.Count;
        }

        return null;
    }

    /// <summary>
    /// Find if the outer boundary contains the given screen position
    /// Returns true if clicking near the outer boundary
    /// </summary>
    private bool IsOuterBoundaryAtPosition(Point screenPos)
    {
        const double hitRadius = 15.0; // pixels - click tolerance

        if (_boundaryPoints.Count < 2) return false;

        // Check if clicking near any point of the outer boundary
        for (int i = 0; i < _boundaryPoints.Count; i++)
        {
            var point = _boundaryPoints[i];
            var pointScreen = WorldToScreen(point.Easting, point.Northing);

            double distance = Math.Sqrt(
                Math.Pow(screenPos.X - pointScreen.X, 2) +
                Math.Pow(screenPos.Y - pointScreen.Y, 2)
            );

            if (distance <= hitRadius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Find which inner boundary (if any) contains the given screen position
    /// Returns the inner boundary index, or -1 if none found
    /// </summary>
    private int FindInnerBoundaryAtPosition(Point screenPos)
    {
        const double hitRadius = 15.0; // pixels - click tolerance (slightly larger for whole boundaries)

        for (int boundaryIdx = 0; boundaryIdx < _innerBoundaries.Count; boundaryIdx++)
        {
            var innerBoundary = _innerBoundaries[boundaryIdx];
            if (innerBoundary.Count < 2) continue;

            // Check if clicking near any point or line segment of this inner boundary
            for (int i = 0; i < innerBoundary.Count; i++)
            {
                var point = innerBoundary[i];
                var pointScreen = WorldToScreen(point.Easting, point.Northing);

                double distance = Math.Sqrt(
                    Math.Pow(screenPos.X - pointScreen.X, 2) +
                    Math.Pow(screenPos.Y - pointScreen.Y, 2)
                );

                if (distance <= hitRadius)
                {
                    return boundaryIdx;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Select all points between two indices (inclusive)
    /// </summary>
    private void SelectRange(int index1, int index2)
    {
        int start = Math.Min(index1, index2);
        int end = Math.Max(index1, index2);

        _selectedPointIndices.Clear();
        for (int i = start; i <= end; i++)
        {
            _selectedPointIndices.Add(i);
        }

        Console.WriteLine($"[VISUALIZATION] Selected range: {start} to {end} ({_selectedPointIndices.Count} points)");
    }

    /// <summary>
    /// Handle keyboard input (Delete key)
    /// </summary>
    private void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Delete && _selectedPointIndices.Count > 0)
        {
            // Notify listeners that points should be deleted
            var indicesToDelete = _selectedPointIndices.OrderByDescending(i => i).ToList();
            PointsDeleteRequested?.Invoke(this, indicesToDelete);

            _selectedPointIndices.Clear();
            _firstSelectedIndex = null;
            InvalidateVisual();
            e.Handled = true;

            Console.WriteLine($"[VISUALIZATION] Delete key pressed, requesting deletion of {indicesToDelete.Count} points");
        }
        else if (e.Key == Avalonia.Input.Key.Escape)
        {
            // Escape clears selection
            _selectedPointIndices.Clear();
            _firstSelectedIndex = null;
            InvalidateVisual();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Center camera on vehicle position
    /// </summary>
    private void CenterOnVehicle()
    {
        if (_vehiclePosition == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        // Default zoom if not set
        if (_zoom < 0.1)
        {
            _zoom = 2.0; // 2 pixels per meter - shows ~400m x 300m area
        }

        // Center the vehicle in the view
        var newOffset = new Point(
            Bounds.Width / 2.0 - _vehiclePosition.Easting * _zoom,
            Bounds.Height / 2.0 + _vehiclePosition.Northing * _zoom // Flip Y axis
        );

        if (_vehicleUpdateCount % 10 == 0) // Log every 10th update
        {
            Console.WriteLine($"[VISUALIZATION] CenterOnVehicle: E={_vehiclePosition.Easting:F2}, N={_vehiclePosition.Northing:F2}, Offset=({newOffset.X:F1},{newOffset.Y:F1}), Zoom={_zoom:F2}");
        }

        _offset = newOffset;
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

        // Draw background
        context.FillRectangle(_backgroundBrush, Bounds);

        // Draw grid
        DrawGrid(context);

        // Draw boundary
        DrawBoundary(context);

        // Draw inner boundaries (holes/exclusions)
        DrawInnerBoundaries(context);

        // Draw notch points
        DrawNotchPoints(context);

        // Draw split line
        DrawSplitLine(context);

        // Draw flags
        DrawFlags(context);

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

        // Check if outer boundary is selected (use orange highlighting)
        bool isOuterBoundarySelected = (_selectedInnerBoundaryIndex == -1);
        var pen = isOuterBoundarySelected ? _selectedInnerBoundaryPen : _boundaryPen;
        var brush = isOuterBoundarySelected ? _selectedInnerBoundaryBrush : _boundaryBrush;

        // Draw boundary lines
        for (int i = 0; i < _boundaryPoints.Count; i++)
        {
            var p1 = _boundaryPoints[i];
            var p2 = _boundaryPoints[(i + 1) % _boundaryPoints.Count];

            var screen1 = WorldToScreen(p1.Easting, p1.Northing);
            var screen2 = WorldToScreen(p2.Easting, p2.Northing);

            context.DrawLine(pen, screen1, screen2);
        }

        // Draw boundary points
        for (int i = 0; i < _boundaryPoints.Count; i++)
        {
            var point = _boundaryPoints[i];
            var screenPos = WorldToScreen(point.Easting, point.Northing);
            bool isSelected = _selectedPointIndices.Contains(i);

            if (isSelected)
            {
                // Draw selected points as larger yellow circles
                double selectedRadius = PointRadius * 2;
                var circle = new EllipseGeometry(new Rect(
                    screenPos.X - selectedRadius, screenPos.Y - selectedRadius,
                    selectedRadius * 2, selectedRadius * 2));
                context.DrawGeometry(_selectedBrush, _selectedPen, circle);
            }
            else
            {
                // Draw normal points as rectangles (green or orange based on selection)
                context.FillRectangle(
                    brush,
                    new Rect(screenPos.X - PointRadius / 2, screenPos.Y - PointRadius / 2,
                             PointRadius, PointRadius)
                );
            }
        }
    }

    private void DrawInnerBoundaries(DrawingContext context)
    {
        if (_innerBoundaries.Count == 0) return;

        int currentIndex = _boundaryPoints.Count; // Start indexing after outer boundary

        for (int boundaryIdx = 0; boundaryIdx < _innerBoundaries.Count; boundaryIdx++)
        {
            var innerBoundary = _innerBoundaries[boundaryIdx];

            if (innerBoundary.Count < 2)
            {
                currentIndex += innerBoundary.Count;
                continue;
            }

            // Check if this inner boundary is selected for highlighting
            bool isBoundarySelected = (boundaryIdx == _selectedInnerBoundaryIndex);
            var pen = isBoundarySelected ? _selectedInnerBoundaryPen : _innerBoundaryPen;
            var brush = isBoundarySelected ? _selectedInnerBoundaryBrush : _innerBoundaryBrush;

            // Draw inner boundary lines (red or orange if selected)
            for (int i = 0; i < innerBoundary.Count; i++)
            {
                var p1 = innerBoundary[i];
                var p2 = innerBoundary[(i + 1) % innerBoundary.Count];

                var screen1 = WorldToScreen(p1.Easting, p1.Northing);
                var screen2 = WorldToScreen(p2.Easting, p2.Northing);

                context.DrawLine(pen, screen1, screen2);
            }

            // Draw inner boundary points (red/orange rectangles, or yellow circles if point selected)
            for (int i = 0; i < innerBoundary.Count; i++)
            {
                var point = innerBoundary[i];
                var screenPos = WorldToScreen(point.Easting, point.Northing);
                bool isPointSelected = _selectedPointIndices.Contains(currentIndex + i);

                if (isPointSelected)
                {
                    // Draw selected points as larger yellow circles
                    double selectedRadius = PointRadius * 2;
                    var circle = new EllipseGeometry(new Rect(
                        screenPos.X - selectedRadius, screenPos.Y - selectedRadius,
                        selectedRadius * 2, selectedRadius * 2));
                    context.DrawGeometry(_selectedBrush, _selectedPen, circle);
                }
                else
                {
                    // Draw normal points as red/orange rectangles
                    context.FillRectangle(
                        brush,
                        new Rect(screenPos.X - PointRadius / 2, screenPos.Y - PointRadius / 2,
                                 PointRadius, PointRadius)
                    );
                }
            }

            currentIndex += innerBoundary.Count;
        }
    }

    private void DrawNotchPoints(DrawingContext context)
    {
        if (_notchPoints.Count < 1) return;

        // Draw notch lines
        for (int i = 0; i < _notchPoints.Count - 1; i++)
        {
            var p1 = _notchPoints[i];
            var p2 = _notchPoints[i + 1];

            var screen1 = WorldToScreen(p1.Easting, p1.Northing);
            var screen2 = WorldToScreen(p2.Easting, p2.Northing);

            context.DrawLine(_notchPen, screen1, screen2);
        }

        // Draw notch points (larger and more visible)
        foreach (var point in _notchPoints)
        {
            var screenPos = WorldToScreen(point.Easting, point.Northing);

            // Draw as circle instead of rectangle
            var circle = new EllipseGeometry(new Rect(
                screenPos.X - PointRadius, screenPos.Y - PointRadius,
                PointRadius * 2, PointRadius * 2));
            context.DrawGeometry(_notchBrush, _notchPen, circle);
        }
    }

    private void DrawSplitLine(DrawingContext context)
    {
        if (!_splitLine.HasValue) return;

        var (x1, y1, x2, y2) = _splitLine.Value;
        var screen1 = WorldToScreen(x1, y1);
        var screen2 = WorldToScreen(x2, y2);

        // Draw the split line with dashed pattern
        var dashedPen = new Pen(_splitLineBrush, 3.0, new DashStyle(new double[] { 4, 2 }, 0));
        context.DrawLine(dashedPen, screen1, screen2);

        // Draw endpoint circles
        double endpointRadius = 6.0;
        var circle1 = new EllipseGeometry(new Rect(
            screen1.X - endpointRadius, screen1.Y - endpointRadius,
            endpointRadius * 2, endpointRadius * 2));
        var circle2 = new EllipseGeometry(new Rect(
            screen2.X - endpointRadius, screen2.Y - endpointRadius,
            endpointRadius * 2, endpointRadius * 2));

        context.DrawGeometry(_splitLineBrush, _splitLinePen, circle1);
        context.DrawGeometry(_splitLineBrush, _splitLinePen, circle2);
    }

    private void DrawFlags(DrawingContext context)
    {
        if (_flags.Count == 0) return;

        var typeface = new Typeface("Arial");
        var fontSize = 11.0;

        foreach (var flag in _flags)
        {
            var screenPos = WorldToScreen(flag.Easting, flag.Northing);

            // Parse hex color
            var hexColor = Flag.ColorToHex(flag.Color);
            var color = Color.Parse(hexColor);
            var flagBrush = new SolidColorBrush(color);

            // Draw flag pole (vertical line)
            var poleLength = 30.0;
            context.DrawLine(
                new Pen(flagBrush, 2.0),
                screenPos,
                new Point(screenPos.X, screenPos.Y - poleLength)
            );

            // Draw flag triangle
            var flagWidth = 16.0;
            var flagHeight = 12.0;
            var flagTop = new Point(screenPos.X, screenPos.Y - poleLength);
            var flagTopRight = new Point(screenPos.X + flagWidth, screenPos.Y - poleLength + flagHeight / 2);
            var flagBottom = new Point(screenPos.X, screenPos.Y - poleLength + flagHeight);

            var flagGeometry = new StreamGeometry();
            using (var ctx = flagGeometry.Open())
            {
                ctx.BeginFigure(flagTop, true);
                ctx.LineTo(flagTopRight);
                ctx.LineTo(flagBottom);
                ctx.EndFigure(true);
            }

            context.DrawGeometry(flagBrush, new Pen(Brushes.Black, 1.0), flagGeometry);

            // Draw flag name below with black outline
            var blackText = new FormattedText(
                flag.Name,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black
            );

            var whiteText = new FormattedText(
                flag.Name,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                _flagTextBrush
            );

            // Draw black outline
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        context.DrawText(
                            blackText,
                            new Point(screenPos.X - blackText.Width / 2 + dx, screenPos.Y + 5 + dy)
                        );
                    }
                }
            }

            // Draw white text on top
            context.DrawText(
                whiteText,
                new Point(screenPos.X - whiteText.Width / 2, screenPos.Y + 5)
            );
        }
    }

    private void DrawVehicle(DrawingContext context)
    {
        if (_vehiclePosition == null) return;

        var screenPos = WorldToScreen(_vehiclePosition.Easting, _vehiclePosition.Northing);

        // Make crosshair bigger and more visible
        double halfSize = CrosshairSize;

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
        if (_innerBoundaries.Count > 0)
        {
            info += $" | Inner: {_innerBoundaries.Count}";
        }
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
