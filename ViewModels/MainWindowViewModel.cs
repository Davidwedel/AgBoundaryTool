// AgBoundaryTool
// Main window view model

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgBoundaryTool.Models;
using AgBoundaryTool.Services;

namespace AgBoundaryTool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GpsService _gpsService;
    private readonly BoundaryRecordingService _recordingService;
    private readonly SettingsService _settingsService;
    private Field? _currentField;
    private Position? _temporaryOrigin; // Used when no field exists
    private Views.Controls.BoundaryVisualizationControl? _visualizationControl;

    [ObservableProperty]
    private string _statusText = "Not connected";

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private bool _isRecording = false;

    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private double _latitude;

    [ObservableProperty]
    private double _longitude;

    [ObservableProperty]
    private double _altitude;

    [ObservableProperty]
    private double _heading;

    [ObservableProperty]
    private double _speed;

    [ObservableProperty]
    private int _fixQuality;

    [ObservableProperty]
    private int _satelliteCount;

    [ObservableProperty]
    private int _pointCount;

    [ObservableProperty]
    private double _boundaryArea;

    [ObservableProperty]
    private string _fieldName = "NewField";

    [ObservableProperty]
    private string _fieldDirectory = string.Empty;

    [ObservableProperty]
    private double _simulatorLatitude = 39.8283;  // Default to Kansas, USA

    [ObservableProperty]
    private double _simulatorLongitude = -98.5795;

    [ObservableProperty]
    private double _simulatorSteerAngle = 0.0;

    [ObservableProperty]
    private bool _isSimulatorMode = false;

    [ObservableProperty]
    private bool _isRecordingNotch = false;

    [ObservableProperty]
    private int _notchPointCount = 0;

    [ObservableProperty]
    private bool _canApplyNotch = false;

    // Inner Boundary Modification properties
    [ObservableProperty]
    private int _selectedInnerBoundaryIndex = 0;

    partial void OnSelectedInnerBoundaryIndexChanged(int value)
    {
        // Update visualization to highlight the selected inner boundary
        _visualizationControl?.SetSelectedInnerBoundary(value);
    }

    [ObservableProperty]
    private bool _isInnerNotchOperation = true;

    [ObservableProperty]
    private bool _isInnerBulgeOperation = false;

    [ObservableProperty]
    private bool _canStartInnerModify = false;

    private int _targetInnerBoundaryIndex = -1;
    private string _innerModifyOperation = ""; // "notch" or "bulge"

    public ObservableCollection<string> InnerBoundaryChoices { get; } = new ObservableCollection<string>();
    // NTRIP properties
    [ObservableProperty]
    private bool _isNtripConnected = false;

    [ObservableProperty]
    private bool _ntripEnabled = false;

    [ObservableProperty]
    private string _ntripHost = string.Empty;

    [ObservableProperty]
    private int _ntripPort = 2101;

    [ObservableProperty]
    private string _ntripMountPoint = string.Empty;

    [ObservableProperty]
    private string _ntripUsername = string.Empty;

    [ObservableProperty]
    private string _ntripPassword = string.Empty;

    [ObservableProperty]
    private bool _ntripUseSsl = false;

    [ObservableProperty]
    private bool _ntripSettingsVisible = false;

    // Point Recording Dialog properties
    [ObservableProperty]
    private string _pointRecordingTitle = "Point Recording";

    [ObservableProperty]
    private string _pointRecordingDescription = "Record GPS points";

    [ObservableProperty]
    private bool _isRecordingContinuously = false;

    [ObservableProperty]
    private string _continuousRecordingButtonText = "Start Continuous";

    private string _pointRecordingMode = ""; // "boundary", "notch", or "inner"

    public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>();
    public ObservableCollection<BoundaryPoint> BoundaryPoints { get; } = new ObservableCollection<BoundaryPoint>();
    public ObservableCollection<BoundaryPoint> NotchPoints { get; } = new ObservableCollection<BoundaryPoint>();

    public MainWindowViewModel()
    {
        _gpsService = new GpsService();
        _recordingService = new BoundaryRecordingService();
        _settingsService = new SettingsService();

        _gpsService.PositionReceived += OnPositionReceived;
        _gpsService.ConnectionStatusChanged += OnConnectionStatusChanged;
        _recordingService.PointRecorded += OnPointRecorded;

        RefreshPorts();

        // Load settings from disk
        LoadSettings();
    }

    private void LoadSettings()
    {
        _settingsService.Load();
        var settings = _settingsService.Settings;

        // Apply settings
        FieldDirectory = settings.FieldsDirectory;
        SimulatorLatitude = settings.SimulatorLatitude;
        SimulatorLongitude = settings.SimulatorLongitude;
        SimulatorSteerAngle = settings.SimulatorSteerAngle;

        // Apply NTRIP settings
        NtripEnabled = settings.NtripEnabled;
        NtripHost = settings.NtripHost;
        NtripPort = settings.NtripPort;
        NtripMountPoint = settings.NtripMountPoint;
        NtripUsername = settings.NtripUsername;
        NtripPassword = settings.NtripPassword;
        NtripUseSsl = settings.NtripUseSsl;

        // Select last used GPS port if available
        if (!string.IsNullOrEmpty(settings.LastGpsPort) && AvailablePorts.Contains(settings.LastGpsPort))
        {
            SelectedPort = settings.LastGpsPort;
        }

        Console.WriteLine("[VIEWMODEL] Settings loaded");
        Console.WriteLine($"[VIEWMODEL] Simulator was running: {settings.SimulatorWasRunning}");
        Console.WriteLine($"[VIEWMODEL] Last field: {settings.LastFieldName}");

        // Auto-load last field if available
        if (!string.IsNullOrEmpty(settings.LastFieldName))
        {
            var lastFieldPath = Path.Combine(settings.FieldsDirectory, settings.LastFieldName);
            if (Directory.Exists(lastFieldPath))
            {
                Console.WriteLine($"[VIEWMODEL] Auto-loading last field: {settings.LastFieldName}");
                Dispatcher.UIThread.Post(() => LoadFieldByPath(lastFieldPath), DispatcherPriority.Background);
            }
            else
            {
                Console.WriteLine($"[VIEWMODEL] Last field not found: {lastFieldPath}");
            }
        }

        // Auto-start simulator if it was running last time
        if (settings.SimulatorWasRunning)
        {
            Console.WriteLine("[VIEWMODEL] Auto-starting simulator...");
            Dispatcher.UIThread.Post(() => StartSimulator(), DispatcherPriority.Background);
        }
    }

    public void SaveSettings()
    {
        var settings = _settingsService.Settings;

        // Update current state
        settings.SimulatorWasRunning = IsSimulatorMode;
        settings.SimulatorLatitude = SimulatorLatitude;
        settings.SimulatorLongitude = SimulatorLongitude;
        settings.SimulatorSteerAngle = SimulatorSteerAngle;
        settings.LastGpsPort = SelectedPort;
        settings.FieldsDirectory = FieldDirectory;

        // Save NTRIP settings
        settings.NtripEnabled = NtripEnabled;
        settings.NtripHost = NtripHost;
        settings.NtripPort = NtripPort;
        settings.NtripMountPoint = NtripMountPoint;
        settings.NtripUsername = NtripUsername;
        settings.NtripPassword = NtripPassword;
        settings.NtripUseSsl = NtripUseSsl;

        if (_currentField != null)
        {
            settings.LastFieldName = _currentField.Name;
        }

        _settingsService.Save();
    }

    [RelayCommand]
    private void OpenGpsConnectionDialog()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        var dialog = new Views.Dialogs.GpsConnectionDialog
        {
            DataContext = this
        };
        dialog.Show(mainWindow);
    }

    [RelayCommand]
    private void OpenGpsSimulatorDialog()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        var dialog = new Views.Dialogs.GpsSimulatorDialog
        {
            DataContext = this
        };
        dialog.Show(mainWindow);
    }

    [RelayCommand]
    private void OpenFieldManagementDialog()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        var dialog = new Views.Dialogs.FieldManagementDialog
        {
            DataContext = this
        };
        dialog.Show(mainWindow);
    }

    [RelayCommand]
    private void OpenBoundaryRecordingDialog()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        // Set up for boundary recording mode
        _pointRecordingMode = "boundary";
        PointRecordingTitle = "Boundary Recording";
        PointRecordingDescription = "Drive around the field perimeter to record the boundary. Points are recorded every 1 meter.";
        IsRecordingContinuously = false;
        UpdateContinuousRecordingButtonText();

        var dialog = new Views.Dialogs.PointRecordingDialog
        {
            DataContext = this
        };
        dialog.Show(mainWindow);
    }

    [RelayCommand]
    private void OpenBoundaryNotchDialog()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        // Set up for notch recording mode
        _pointRecordingMode = "notch";
        PointRecordingTitle = "Boundary Notch";
        PointRecordingDescription = "Drive a path that crosses the boundary at least twice to create a notch (cutout). Start and end outside the boundary.";
        IsRecordingContinuously = false;
        UpdateContinuousRecordingButtonText();

        var dialog = new Views.Dialogs.PointRecordingDialog
        {
            DataContext = this
        };
        dialog.Show(mainWindow);
    }

    [RelayCommand]
    private void OpenInnerBoundaryModifyDialog()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        if (_currentField?.Boundary?.InnerBoundaries == null || _currentField.Boundary.InnerBoundaries.Count == 0)
        {
            StatusText = "No inner boundaries to modify";
            return;
        }

        // Populate inner boundary choices
        InnerBoundaryChoices.Clear();
        for (int i = 0; i < _currentField.Boundary.InnerBoundaries.Count; i++)
        {
            var ib = _currentField.Boundary.InnerBoundaries[i];
            InnerBoundaryChoices.Add($"Inner Boundary {i + 1} ({ib.Points.Count} points, {ib.AreaHectares:F2} ha)");
        }

        // Enable inner boundary selection mode in visualization
        _visualizationControl?.EnableInnerBoundarySelectionMode();

        // Get currently selected inner boundary from visualization (if any)
        int visualizationSelection = _visualizationControl?.GetSelectedInnerBoundary() ?? -1;
        SelectedInnerBoundaryIndex = visualizationSelection >= 0 ? visualizationSelection : 0;

        // Highlight the selected boundary in visualization
        _visualizationControl?.SetSelectedInnerBoundary(SelectedInnerBoundaryIndex);

        IsInnerNotchOperation = true;
        IsInnerBulgeOperation = false;
        CanStartInnerModify = true;

        var dialog = new Views.Dialogs.InnerBoundaryModifyDialog
        {
            DataContext = this
        };

        // When dialog closes, disable selection mode
        dialog.Closed += (s, e) =>
        {
            _visualizationControl?.DisableInnerBoundarySelectionMode();
        };

        dialog.Show(mainWindow);
        StatusText = "Click on an inner boundary in the visualization to select it";
    }

    [RelayCommand]
    private void TrimInnerBoundary()
    {
        if (_currentField?.Boundary?.OuterBoundary == null || _currentField.Boundary.InnerBoundaries == null)
        {
            StatusText = "No boundary data available";
            return;
        }

        if (SelectedInnerBoundaryIndex < 0 || SelectedInnerBoundaryIndex >= _currentField.Boundary.InnerBoundaries.Count)
        {
            StatusText = "Invalid inner boundary selection";
            return;
        }

        var innerBoundary = _currentField.Boundary.InnerBoundaries[SelectedInnerBoundaryIndex];
        var outerBoundary = _currentField.Boundary.OuterBoundary.Points;

        Console.WriteLine($"[VIEWMODEL] Trimming inner boundary {SelectedInnerBoundaryIndex} with {innerBoundary.Points.Count} points");

        // Find all points that are inside the outer boundary
        var trimmedPoints = TrimPolygonToOuterBoundary(innerBoundary.Points, outerBoundary);

        if (trimmedPoints.Count < 3)
        {
            StatusText = "Inner boundary is completely outside outer boundary - cannot trim";
            Console.WriteLine("[VIEWMODEL] Trim failed: result has less than 3 points");
            return;
        }

        // Update the inner boundary with trimmed points
        innerBoundary.Points.Clear();
        foreach (var point in trimmedPoints)
        {
            innerBoundary.Points.Add(point);
        }

        // Update visualization
        _visualizationControl?.SetInnerBoundaries(
            _currentField.Boundary.InnerBoundaries.Select(ib => ib.Points));

        // Recalculate area
        BoundaryArea = _currentField.Boundary.AreaHectares;

        // Save modified boundary
        BoundaryFileService.SaveBoundary(_currentField.Boundary, _currentField.DirectoryPath);

        StatusText = $"Inner boundary trimmed: {innerBoundary.Points.Count} points, {innerBoundary.AreaHectares:F2} ha";
        Console.WriteLine($"[VIEWMODEL] Inner boundary trimmed to {innerBoundary.Points.Count} points");
    }

    private List<BoundaryPoint> TrimPolygonToOuterBoundary(IList<BoundaryPoint> innerPoints, IList<BoundaryPoint> outerPoints)
    {
        var result = new List<BoundaryPoint>();

        for (int i = 0; i < innerPoints.Count; i++)
        {
            var currentPoint = innerPoints[i];
            var nextPoint = innerPoints[(i + 1) % innerPoints.Count];

            bool currentInside = IsPointInsideBoundary(currentPoint, outerPoints);
            bool nextInside = IsPointInsideBoundary(nextPoint, outerPoints);

            if (currentInside)
            {
                // Current point is inside - add it
                result.Add(currentPoint);
            }

            // Check if the segment crosses the outer boundary
            if (currentInside != nextInside)
            {
                // Segment crosses - find intersection point
                BoundaryPoint? intersection = FindSegmentBoundaryIntersection(currentPoint, nextPoint, outerPoints);
                if (intersection != null)
                {
                    result.Add(intersection);
                }
            }
        }

        return result;
    }

    private BoundaryPoint? FindSegmentBoundaryIntersection(BoundaryPoint p1, BoundaryPoint p2, IList<BoundaryPoint> boundary)
    {
        // Find the first intersection of segment p1-p2 with the boundary
        for (int i = 0; i < boundary.Count; i++)
        {
            var boundaryP1 = boundary[i];
            var boundaryP2 = boundary[(i + 1) % boundary.Count];

            if (LineSegmentsIntersect(p1, p2, boundaryP1, boundaryP2, out var intersection))
            {
                return intersection;
            }
        }

        return null;
    }

    private bool IsPointInsideBoundary(BoundaryPoint point, IList<BoundaryPoint> boundary)
    {
        // Ray casting algorithm - count how many times a ray from the point crosses the boundary
        int crossings = 0;
        double px = point.Easting;
        double py = point.Northing;

        for (int i = 0; i < boundary.Count; i++)
        {
            var p1 = boundary[i];
            var p2 = boundary[(i + 1) % boundary.Count];

            // Check if ray from point going right crosses this edge
            if (((p1.Northing <= py) && (p2.Northing > py)) || ((p1.Northing > py) && (p2.Northing <= py)))
            {
                // Calculate x coordinate of intersection
                double intersectX = p1.Easting + (py - p1.Northing) / (p2.Northing - p1.Northing) * (p2.Easting - p1.Easting);

                if (px < intersectX)
                {
                    crossings++;
                }
            }
        }

        // Odd number of crossings = inside
        return (crossings % 2) == 1;
    }

    [RelayCommand]
    private void StartInnerBoundaryRecording()
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null) return;

        if (_currentField == null || _currentField.Boundary?.InnerBoundaries == null)
        {
            StatusText = "No field or inner boundaries loaded";
            return;
        }

        if (!IsConnected)
        {
            StatusText = "No GPS fix - cannot start recording";
            return;
        }

        // Save the selected inner boundary and operation
        _targetInnerBoundaryIndex = SelectedInnerBoundaryIndex;
        _innerModifyOperation = IsInnerNotchOperation ? "notch" : "bulge";

        // Clear previous notch points
        NotchPoints.Clear();
        NotchPointCount = 0;
        CanApplyNotch = false;
        IsRecordingNotch = false;

        // Set up for inner boundary modification mode
        _pointRecordingMode = "inner";
        PointRecordingTitle = $"Inner Boundary {(_innerModifyOperation == "notch" ? "Notch" : "Bulge")}";
        PointRecordingDescription = _innerModifyOperation == "notch"
            ? "Drive a path that crosses the inner boundary at least twice to create a notch (makes hole smaller - adds farmable area)."
            : "Drive a path that crosses the inner boundary at least twice to create a bulge (makes hole bigger - reduces farmable area).";
        IsRecordingContinuously = false;
        UpdateContinuousRecordingButtonText();

        StatusText = $"Recording inner boundary {_innerModifyOperation} points...";
        Console.WriteLine($"[VIEWMODEL] Started recording inner boundary {_innerModifyOperation} for boundary #{_targetInnerBoundaryIndex}");

        var dialog = new Views.Dialogs.PointRecordingDialog
        {
            DataContext = this
        };
        dialog.Show(mainWindow);
    }

    private Avalonia.Controls.Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        var ports = GpsService.GetAvailablePorts();
        AvailablePorts.Clear();
        foreach (var port in ports)
        {
            AvailablePorts.Add(port);
        }

        if (AvailablePorts.Count > 0 && string.IsNullOrEmpty(SelectedPort))
        {
            SelectedPort = AvailablePorts[0];
        }
    }

    [RelayCommand]
    private async Task ConnectGpsAsync()
    {
        if (string.IsNullOrEmpty(SelectedPort))
        {
            StatusText = "Please select a port";
            return;
        }

        StatusText = $"Connecting to {SelectedPort}...";
        bool connected = await _gpsService.ConnectAsync(SelectedPort);

        if (connected)
        {
            StatusText = $"Connected to {SelectedPort}";
            SaveSettings(); // Auto-save GPS port selection
        }
        else
        {
            StatusText = $"Failed to connect to {SelectedPort}";
        }
    }

    [RelayCommand]
    private async Task DisconnectGpsAsync()
    {
        await _gpsService.DisconnectAsync();
        StatusText = "Disconnected";
    }

    [RelayCommand]
    private void ToggleNtripSettings()
    {
        NtripSettingsVisible = !NtripSettingsVisible;
    }

    [RelayCommand]
    private async Task ConnectNtripAsync()
    {
        if (string.IsNullOrWhiteSpace(NtripHost) || string.IsNullOrWhiteSpace(NtripMountPoint))
        {
            StatusText = "Please enter NTRIP host and mount point";
            return;
        }

        StatusText = $"Connecting to NTRIP {NtripHost}...";
        bool connected = await _gpsService.ConnectNtripAsync(NtripHost, NtripPort, NtripMountPoint, NtripUsername, NtripPassword, NtripUseSsl);

        if (connected)
        {
            IsNtripConnected = true;
            NtripEnabled = true;
            StatusText = $"NTRIP connected to {NtripHost}/{NtripMountPoint}";
            SaveSettings();
        }
        else
        {
            IsNtripConnected = false;
            StatusText = $"Failed to connect to NTRIP {NtripHost}";
        }
    }

    [RelayCommand]
    private async Task DisconnectNtripAsync()
    {
        await _gpsService.DisconnectNtripAsync();
        IsNtripConnected = false;
        NtripEnabled = false;
        StatusText = "NTRIP disconnected";
        SaveSettings();
    }

    [RelayCommand]
    private void CreateNewField()
    {
        if (_gpsService.CurrentPosition == null)
        {
            StatusText = "No GPS fix - cannot create field";
            return;
        }

        if (string.IsNullOrWhiteSpace(FieldName))
        {
            StatusText = "Please enter a field name";
            return;
        }

        // Create field directory
        var fieldPath = Path.Combine(FieldDirectory, FieldName);
        if (Directory.Exists(fieldPath))
        {
            StatusText = $"Field '{FieldName}' already exists";
            return;
        }

        Directory.CreateDirectory(fieldPath);

        // Create new field with current GPS position as origin
        // IMPORTANT: Create a NEW Position object, not a reference to CurrentPosition
        // (CurrentPosition gets updated on every GPS tick)
        var currentPos = _gpsService.CurrentPosition;
        _currentField = new Field
        {
            Name = FieldName,
            DirectoryPath = fieldPath,
            Origin = new Position
            {
                Latitude = currentPos.Latitude,
                Longitude = currentPos.Longitude,
                Altitude = currentPos.Altitude,
                FixQuality = currentPos.FixQuality,
                SatelliteCount = currentPos.SatelliteCount
            },
            CreatedDate = DateTime.Now,
            Boundary = new Boundary()
        };

        // Save field
        FieldPlaneFileService.SaveField(_currentField, fieldPath);

        // Clear temporary origin since we now have a field origin
        _temporaryOrigin = null;

        StatusText = $"Created field '{FieldName}' at {_currentField.Origin.Latitude:F6}, {_currentField.Origin.Longitude:F6}";

        // Save to settings
        SaveSettings();
    }

    [RelayCommand]
    private void StartRecording()
    {
        if (_currentField == null)
        {
            StatusText = "Please create a field first";
            return;
        }

        if (_gpsService.CurrentPosition == null)
        {
            StatusText = "No GPS fix - cannot start recording";
            return;
        }

        BoundaryPoints.Clear();
        _recordingService.StartRecording(_currentField.Origin);
        IsRecording = true;
        StatusText = "Recording boundary...";
    }

    [RelayCommand]
    private void StopRecording()
    {
        if (!IsRecording || _currentField == null)
        {
            return;
        }

        var polygon = _recordingService.StopRecording();
        if (polygon == null)
        {
            StatusText = "Not enough points to create boundary (minimum 3 required)";
            IsRecording = false;
            return;
        }

        // Add polygon to field boundary
        if (_currentField.Boundary == null)
        {
            _currentField.Boundary = new Boundary();
        }

        if (_currentField.Boundary.OuterBoundary == null)
        {
            _currentField.Boundary.OuterBoundary = polygon;
        }
        else
        {
            _currentField.Boundary.InnerBoundaries.Add(polygon);
        }

        // Save boundary
        BoundaryFileService.SaveBoundary(_currentField.Boundary, _currentField.DirectoryPath);

        BoundaryArea = _currentField.Boundary.AreaHectares;
        IsRecording = false;
        StatusText = $"Boundary saved: {polygon.Points.Count} points, {polygon.AreaHectares:F2} ha";
    }

    [RelayCommand]
    private void CancelRecording()
    {
        _recordingService.CancelRecording();
        BoundaryPoints.Clear();
        PointCount = 0;
        IsRecording = false;
        StatusText = "Recording cancelled";
    }

    [RelayCommand]
    private void RemoveLastPoint()
    {
        if (_recordingService.RemoveLastPoint() && BoundaryPoints.Count > 0)
        {
            BoundaryPoints.RemoveAt(BoundaryPoints.Count - 1);
            PointCount = BoundaryPoints.Count;
        }
    }

    // Point Recording Dialog Commands

    [RelayCommand]
    private void AddSinglePoint()
    {
        if (_gpsService.CurrentPosition == null)
        {
            StatusText = "No GPS fix - cannot add point";
            return;
        }

        if (_pointRecordingMode == "boundary")
        {
            // Auto-start recording if not already started
            if (!IsRecording)
            {
                StartRecording();
            }

            // Force add this specific point by temporarily lowering the minimum distance
            var savedMinDistance = _recordingService.MinimumPointDistance;
            _recordingService.MinimumPointDistance = 0.01; // Allow very close points for manual adds
            _recordingService.RecordPosition(_gpsService.CurrentPosition);
            _recordingService.MinimumPointDistance = savedMinDistance; // Restore

            StatusText = $"Point added - Total: {PointCount}";
        }
        else if (_pointRecordingMode == "notch" || _pointRecordingMode == "inner")
        {
            // Auto-start recording if not already started
            if (!IsRecordingNotch)
            {
                IsRecordingNotch = true;
            }

            if (_currentField == null)
            {
                StatusText = "No field loaded";
                return;
            }

            var (e, n) = CoordinateConversionService.ToLocal(_gpsService.CurrentPosition, _currentField.Origin);
            NotchPoints.Add(new BoundaryPoint(e, n, 0));
            NotchPointCount = NotchPoints.Count;
            _visualizationControl?.SetNotchPoints(NotchPoints);

            string modeName = _pointRecordingMode == "inner" ? "Inner modification" : "Notch";
            StatusText = $"{modeName} point added - Total: {NotchPointCount}";
            Console.WriteLine($"[VIEWMODEL] Manually added {modeName} point #{NotchPointCount}: E={e:F2}m, N={n:F2}m");
        }
    }

    [RelayCommand]
    private void DeleteLastPoint()
    {
        if (_pointRecordingMode == "boundary")
        {
            RemoveLastPoint();
        }
        else if (_pointRecordingMode == "notch" || _pointRecordingMode == "inner")
        {
            if (NotchPoints.Count > 0)
            {
                NotchPoints.RemoveAt(NotchPoints.Count - 1);
                NotchPointCount = NotchPoints.Count;
                _visualizationControl?.SetNotchPoints(NotchPoints);
                string modeName = _pointRecordingMode == "inner" ? "inner modification" : "notch";
                Console.WriteLine($"[VIEWMODEL] Deleted last {modeName} point. Remaining: {NotchPointCount}");
            }
        }
    }

    [RelayCommand]
    private void ToggleContinuousRecording()
    {
        IsRecordingContinuously = !IsRecordingContinuously;
        UpdateContinuousRecordingButtonText();

        if (_pointRecordingMode == "boundary")
        {
            IsRecording = IsRecordingContinuously;
            if (IsRecordingContinuously)
            {
                StartRecording();
            }
            else
            {
                // Don't stop recording completely, just pause continuous mode
                StatusText = "Continuous recording paused - use Add Point for manual points";
            }
        }
        else if (_pointRecordingMode == "notch" || _pointRecordingMode == "inner")
        {
            IsRecordingNotch = IsRecordingContinuously;
            if (IsRecordingContinuously)
            {
                // Just set the flag - actual recording happens in OnPositionReceived
                Console.WriteLine($"[VIEWMODEL] Started continuous recording for {_pointRecordingMode} mode");
            }
            else
            {
                StatusText = "Continuous recording paused - use Add Point for manual points";
            }
        }

        Console.WriteLine($"[VIEWMODEL] Continuous recording: {IsRecordingContinuously}");
    }

    [RelayCommand]
    private void FinishPointRecording()
    {
        if (_pointRecordingMode == "boundary")
        {
            StopRecording();
        }
        else if (_pointRecordingMode == "notch")
        {
            StopRecordingNotch();

            // Always open the notch dialog (validation status is shown in the dialog)
            Console.WriteLine($"[VIEWMODEL] FinishPointRecording for notch mode. NotchPoints={NotchPointCount}, CanApplyNotch={CanApplyNotch}");

            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                Console.WriteLine($"[VIEWMODEL] Opening BoundaryNotchDialog");
                var dialog = new Views.Dialogs.BoundaryNotchDialog
                {
                    DataContext = this
                };
                dialog.Show(mainWindow);
            }
            else
            {
                Console.WriteLine($"[VIEWMODEL] ERROR: mainWindow is null!");
            }
        }
        else if (_pointRecordingMode == "inner")
        {
            StopRecordingInnerModify();

            // Open inner boundary apply dialog
            Console.WriteLine($"[VIEWMODEL] FinishPointRecording for inner mode. NotchPoints={NotchPointCount}, CanApplyNotch={CanApplyNotch}");

            var mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                Console.WriteLine($"[VIEWMODEL] Opening InnerBoundaryApplyDialog");
                var dialog = new Views.Dialogs.InnerBoundaryApplyDialog
                {
                    DataContext = this
                };
                dialog.Show(mainWindow);
            }
            else
            {
                Console.WriteLine($"[VIEWMODEL] ERROR: mainWindow is null!");
            }
        }

        // Reset continuous recording state
        IsRecordingContinuously = false;
        UpdateContinuousRecordingButtonText();
    }

    private void UpdateContinuousRecordingButtonText()
    {
        ContinuousRecordingButtonText = IsRecordingContinuously
            ? "Stop Continuous"
            : "Start Continuous";
    }

    [RelayCommand]
    private async Task LoadField()
    {
        try
        {
            // Use Avalonia's folder picker, starting in the fields directory
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null)
            {
                StatusText = "Cannot open folder picker";
                return;
            }

            // Get suggested start location (fields directory)
            var startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(FieldDirectory));

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Field to Load",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            if (folders.Count > 0)
            {
                var selectedPath = folders[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    Console.WriteLine($"[VIEWMODEL] Loading field from: {selectedPath}");
                    LoadFieldByPath(selectedPath);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading field: {ex.Message}";
            Console.WriteLine($"[VIEWMODEL] Load field error: {ex.Message}");
        }
    }

    private void LoadFieldByPath(string fieldPath)
    {
        try
        {
            var fieldName = Path.GetFileName(fieldPath);
            Console.WriteLine($"[VIEWMODEL] Loading field: {fieldName} from {fieldPath}");

            // Load field data
            var field = FieldPlaneFileService.LoadField(fieldPath);
            if (field == null)
            {
                StatusText = $"Failed to load field '{fieldName}'";
                return;
            }

            // Load boundary if exists
            var boundary = BoundaryFileService.LoadBoundary(fieldPath);
            field.Boundary = boundary;

            _currentField = field;
            FieldName = field.Name;

            // Clear temporary origin since we now have a field
            _temporaryOrigin = null;

            // Snap simulator to field origin if simulator is running
            if (IsSimulatorMode && _gpsService.Simulator != null && field.Origin != null)
            {
                Console.WriteLine($"[VIEWMODEL] Snapping simulator to field origin: {field.Origin.Latitude:F6}, {field.Origin.Longitude:F6}");

                // Stop current simulator
                _gpsService.StopSimulator();

                // Update simulator position
                SimulatorLatitude = field.Origin.Latitude;
                SimulatorLongitude = field.Origin.Longitude;

                // Restart simulator at new position
                _gpsService.StartSimulator(SimulatorLatitude, SimulatorLongitude);
                IsSimulatorMode = true;
            }

            // Update visualization with boundary
            if (field.Boundary?.OuterBoundary != null && field.Boundary.OuterBoundary.IsValid)
            {
                BoundaryPoints.Clear();
                foreach (var point in field.Boundary.OuterBoundary.Points)
                {
                    BoundaryPoints.Add(point);
                }
                _visualizationControl?.SetBoundaryPoints(BoundaryPoints);

                // Set inner boundaries if any exist
                if (field.Boundary.InnerBoundaries != null && field.Boundary.InnerBoundaries.Count > 0)
                {
                    _visualizationControl?.SetInnerBoundaries(
                        field.Boundary.InnerBoundaries.Select(ib => ib.Points));
                    Console.WriteLine($"[VIEWMODEL] Loaded {field.Boundary.InnerBoundaries.Count} inner boundaries");
                }

                StatusText = $"Loaded field '{field.Name}' with {BoundaryPoints.Count} boundary points";
                Console.WriteLine($"[VIEWMODEL] Field loaded: {BoundaryPoints.Count} points, Area: {field.Boundary.OuterBoundary.AreaHectares:F2} ha");
            }
            else
            {
                StatusText = $"Loaded field '{field.Name}' (no boundary)";
                Console.WriteLine($"[VIEWMODEL] Field loaded: no boundary");
            }

            // Update point count
            PointCount = BoundaryPoints.Count;

            // Save to settings
            SaveSettings();
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading field: {ex.Message}";
            Console.WriteLine($"[VIEWMODEL] Load field error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveFieldAs()
    {
        if (_currentField == null)
        {
            StatusText = "No field to save";
            return;
        }

        if (string.IsNullOrWhiteSpace(FieldName))
        {
            StatusText = "Please enter a field name";
            return;
        }

        try
        {
            // Use Avalonia's folder picker to select parent directory where field folder will be created
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null)
            {
                StatusText = "Cannot open folder picker";
                return;
            }

            // Get suggested start location (fields directory)
            var startFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(FieldDirectory));

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = $"Select Directory to Save Field '{FieldName}'",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });

            if (folders.Count > 0)
            {
                var parentPath = folders[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    // Create field directory in selected parent location
                    var newFieldPath = Path.Combine(parentPath, FieldName);

                    Console.WriteLine($"[VIEWMODEL] Saving field to: {newFieldPath}");

                    // Check if directory already exists
                    if (Directory.Exists(newFieldPath))
                    {
                        StatusText = $"Field '{FieldName}' already exists at {newFieldPath}";
                        Console.WriteLine($"[VIEWMODEL] Field already exists, overwriting...");
                    }
                    else
                    {
                        Directory.CreateDirectory(newFieldPath);
                    }

                    // Update field name and directory path
                    _currentField.Name = FieldName;
                    _currentField.DirectoryPath = newFieldPath;

                    // Save field to new location
                    FieldPlaneFileService.SaveField(_currentField, newFieldPath);
                    Console.WriteLine($"[VIEWMODEL] Saved Field.txt to {newFieldPath}");

                    if (_currentField.Boundary != null)
                    {
                        BoundaryFileService.SaveBoundary(_currentField.Boundary, newFieldPath);
                        Console.WriteLine($"[VIEWMODEL] Saved Boundary.txt to {newFieldPath}");
                    }

                    StatusText = $"Field '{FieldName}' saved to {newFieldPath}";
                    Console.WriteLine($"[VIEWMODEL] Field save complete");

                    // Save to settings
                    SaveSettings();
                }
            }
            else
            {
                StatusText = "Save cancelled";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving field: {ex.Message}";
            Console.WriteLine($"[VIEWMODEL] Save field as error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CloseField()
    {
        if (_currentField == null)
        {
            StatusText = "No field is open";
            return;
        }

        var fieldName = _currentField.Name;

        // Clear current field
        _currentField = null;
        FieldName = "NewField";

        // Clear boundary points
        BoundaryPoints.Clear();
        PointCount = 0;
        BoundaryArea = 0;

        // Clear visualization
        _visualizationControl?.SetBoundaryPoints(BoundaryPoints);

        StatusText = $"Closed field '{fieldName}'";
        Console.WriteLine($"[VIEWMODEL] Closed field: {fieldName}");

        // Save settings to clear last field
        SaveSettings();
    }

    [RelayCommand]
    private async Task ImportField()
    {
        try
        {
            // Use Avalonia's folder picker
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null)
            {
                StatusText = "Cannot open folder picker";
                return;
            }

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select Field Directory to Import",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var selectedPath = folders[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    Console.WriteLine($"[VIEWMODEL] Importing field from: {selectedPath}");
                    LoadFieldByPath(selectedPath);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error importing field: {ex.Message}";
            Console.WriteLine($"[VIEWMODEL] Import field error: {ex.Message}");
        }
    }

    /// <summary>
    /// Set the visualization control (called by MainWindow after construction)
    /// </summary>
    public void SetVisualizationControl(Views.Controls.BoundaryVisualizationControl control)
    {
        _visualizationControl = control;

        // Subscribe to point deletion requests
        _visualizationControl.PointsDeleteRequested += OnPointsDeleteRequested;

        // Subscribe to vehicle snap requests (right-click)
        _visualizationControl.VehicleSnapRequested += OnVehicleSnapRequested;

        // Subscribe to inner boundary selection
        _visualizationControl.InnerBoundarySelected += OnInnerBoundarySelected;
    }

    private void OnInnerBoundarySelected(object? sender, int boundaryIndex)
    {
        SelectedInnerBoundaryIndex = boundaryIndex;
        Console.WriteLine($"[VIEWMODEL] Inner boundary {boundaryIndex} selected from visualization");
    }

    private void OnVehicleSnapRequested(object? sender, (double easting, double northing) localCoords)
    {
        if (!IsSimulatorMode || _gpsService.Simulator == null)
        {
            StatusText = "Vehicle snap only works in simulator mode";
            return;
        }

        // Get the origin (field or temporary)
        Position origin;
        if (_currentField != null)
        {
            origin = _currentField.Origin;
        }
        else if (_temporaryOrigin != null)
        {
            origin = _temporaryOrigin;
        }
        else
        {
            StatusText = "No origin set - cannot snap vehicle";
            return;
        }

        // Convert local coordinates to GPS coordinates
        var (lat, lon) = CoordinateConversionService.ToGlobal(localCoords.easting, localCoords.northing, origin);

        // Update simulator position
        var newPosition = new Position
        {
            Latitude = lat,
            Longitude = lon,
            Altitude = 200,
            FixQuality = 4,
            SatelliteCount = 12
        };

        // Reinitialize simulator at new position
        _gpsService.Simulator.Initialize(newPosition);

        StatusText = $"Vehicle snapped to E={localCoords.easting:F2}, N={localCoords.northing:F2}";
        Console.WriteLine($"[VIEWMODEL] Vehicle snapped to {lat:F6}, {lon:F6}");
    }

    private void OnPointsDeleteRequested(object? sender, List<int> indices)
    {
        if (_currentField?.Boundary?.OuterBoundary == null || indices.Count == 0)
        {
            return;
        }

        Console.WriteLine($"[VIEWMODEL] Deleting {indices.Count} boundary points");

        var outerBoundary = _currentField.Boundary.OuterBoundary.Points;
        int outerCount = outerBoundary.Count;

        // Separate indices into outer and inner boundary groups
        var outerIndices = new List<int>();
        var innerIndices = new Dictionary<int, List<int>>(); // innerBoundaryIndex -> list of point indices

        foreach (var index in indices)
        {
            if (index < outerCount)
            {
                // Outer boundary point
                outerIndices.Add(index);
            }
            else
            {
                // Inner boundary point - find which inner boundary it belongs to
                int currentIndex = outerCount;
                for (int i = 0; i < _currentField.Boundary.InnerBoundaries.Count; i++)
                {
                    var innerBoundary = _currentField.Boundary.InnerBoundaries[i];
                    int innerBoundarySize = innerBoundary.Points.Count;

                    if (index < currentIndex + innerBoundarySize)
                    {
                        // This point belongs to inner boundary i
                        int localIndex = index - currentIndex;
                        if (!innerIndices.ContainsKey(i))
                        {
                            innerIndices[i] = new List<int>();
                        }
                        innerIndices[i].Add(localIndex);
                        break;
                    }

                    currentIndex += innerBoundarySize;
                }
            }
        }

        // Delete from outer boundary (in reverse order to avoid index shifts)
        outerIndices.Sort();
        outerIndices.Reverse();
        foreach (var index in outerIndices)
        {
            if (index >= 0 && index < outerBoundary.Count)
            {
                outerBoundary.RemoveAt(index);
                Console.WriteLine($"[VIEWMODEL] Deleted outer boundary point at index {index}");
            }
        }

        // Delete from inner boundaries (in reverse order)
        foreach (var kvp in innerIndices)
        {
            int innerBoundaryIndex = kvp.Key;
            var pointIndices = kvp.Value;
            pointIndices.Sort();
            pointIndices.Reverse();

            var innerBoundary = _currentField.Boundary.InnerBoundaries[innerBoundaryIndex].Points;
            foreach (var index in pointIndices)
            {
                if (index >= 0 && index < innerBoundary.Count)
                {
                    innerBoundary.RemoveAt(index);
                    Console.WriteLine($"[VIEWMODEL] Deleted inner boundary {innerBoundaryIndex} point at index {index}");
                }
            }
        }

        // Update outer boundary visualization
        BoundaryPoints.Clear();
        foreach (var pt in outerBoundary)
        {
            BoundaryPoints.Add(pt);
        }
        PointCount = BoundaryPoints.Count;
        _visualizationControl?.SetBoundaryPoints(BoundaryPoints);

        // Update inner boundaries visualization
        if (_currentField.Boundary.InnerBoundaries != null && _currentField.Boundary.InnerBoundaries.Count > 0)
        {
            _visualizationControl?.SetInnerBoundaries(
                _currentField.Boundary.InnerBoundaries.Select(ib => ib.Points));
        }

        // Recalculate area
        BoundaryArea = _currentField.Boundary.AreaHectares;

        // Save modified boundary
        BoundaryFileService.SaveBoundary(_currentField.Boundary, _currentField.DirectoryPath);

        StatusText = $"Deleted {indices.Count} point(s) - {PointCount} points remaining";
        Console.WriteLine($"[VIEWMODEL] Boundary now has {PointCount} points");
    }

    private void OnPositionReceived(object? sender, Position position)
    {
        // Update UI on main thread
        Dispatcher.UIThread.Post(() =>
        {
            Latitude = position.Latitude;
            Longitude = position.Longitude;
            Altitude = position.Altitude;
            Heading = position.Heading;
            Speed = position.Speed;
            FixQuality = position.FixQuality;
            SatelliteCount = position.SatelliteCount;

            // Update visualization with vehicle position
            if (_visualizationControl != null)
            {
                // Use field origin if available, otherwise use first GPS position as temporary origin
                Position origin;
                if (_currentField != null)
                {
                    origin = _currentField.Origin;
                }
                else
                {
                    // Set temporary origin on first position
                    if (_temporaryOrigin == null)
                    {
                        _temporaryOrigin = new Position
                        {
                            Latitude = position.Latitude,
                            Longitude = position.Longitude
                        };
                        Console.WriteLine($"[VIEWMODEL] Temporary origin set: {_temporaryOrigin.Latitude:F6}, {_temporaryOrigin.Longitude:F6}");
                    }
                    origin = _temporaryOrigin;
                }

                var (easting, northing) = CoordinateConversionService.ToLocal(position, origin);
                _visualizationControl.SetVehiclePosition(position, easting, northing);
            }

            if (IsRecording)
            {
                _recordingService.RecordPosition(position);
            }

            // Record notch/inner modification points if recording is active
            if (IsRecordingNotch && _currentField != null)
            {
                string modeName = _pointRecordingMode == "inner" ? "Inner modification" : "Notch";

                // Check if we should record this point (1m minimum distance)
                if (NotchPoints.Count == 0)
                {
                    // First point - always record
                    var (e, n) = CoordinateConversionService.ToLocal(position, _currentField.Origin);
                    NotchPoints.Add(new BoundaryPoint(e, n, 0));
                    NotchPointCount = NotchPoints.Count;
                    Console.WriteLine($"[VIEWMODEL] {modeName} point #{NotchPointCount} recorded: E={e:F2}m, N={n:F2}m");

                    // Update visualization
                    _visualizationControl?.SetNotchPoints(NotchPoints);
                }
                else
                {
                    // Check distance from last point
                    var lastPoint = NotchPoints[NotchPoints.Count - 1];
                    var (e, n) = CoordinateConversionService.ToLocal(position, _currentField.Origin);

                    double distance = Math.Sqrt(
                        Math.Pow(e - lastPoint.Easting, 2) +
                        Math.Pow(n - lastPoint.Northing, 2)
                    );

                    if (distance >= 1.0) // 1 meter minimum
                    {
                        NotchPoints.Add(new BoundaryPoint(e, n, 0));
                        NotchPointCount = NotchPoints.Count;
                        Console.WriteLine($"[VIEWMODEL] {modeName} point #{NotchPointCount} recorded: E={e:F2}m, N={n:F2}m (dist={distance:F2}m)");

                        // Update visualization
                        _visualizationControl?.SetNotchPoints(NotchPoints);
                    }
                }
            }
        });
    }

    [RelayCommand]
    private void StartSimulator()
    {
        _gpsService.StartSimulator(SimulatorLatitude, SimulatorLongitude);
        IsSimulatorMode = true;
        StatusText = $"Simulator started at {SimulatorLatitude:F6}, {SimulatorLongitude:F6}";
        SaveSettings(); // Auto-save when simulator starts
    }

    [RelayCommand]
    private void StopSimulatorGps()
    {
        _gpsService.StopSimulator();
        IsSimulatorMode = false;
        StatusText = "Simulator stopped";
        SaveSettings(); // Auto-save when simulator stops
    }

    [RelayCommand]
    private void SimulatorSpeedUp()
    {
        if (_gpsService.Simulator != null)
        {
            // Get current speed in mph
            double currentSpeedKmh = _gpsService.Simulator.SpeedKmh;
            double currentSpeedMph = currentSpeedKmh * 0.621371;

            // Variable increment based on current speed
            // 1 mph ≈ 0.04 stepDistance
            double increment;
            if (Math.Abs(currentSpeedMph) < 10)
            {
                // At low speeds (0-10 mph): increment by 1 mph
                increment = 0.04;
            }
            else if (Math.Abs(currentSpeedMph) < 20)
            {
                // At medium speeds (10-20 mph): increment by 2 mph
                increment = 0.08;
            }
            else
            {
                // At high speeds (20+ mph): increment by 3 mph
                increment = 0.12;
            }

            // Increase speed
            _gpsService.Simulator.StepDistance += increment;

            // Clamp to max forward speed (25 km/h ≈ 15.5 mph)
            if (_gpsService.Simulator.StepDistance > 0.625)
                _gpsService.Simulator.StepDistance = 0.625;

            double newSpeedMph = _gpsService.Simulator.SpeedKmh * 0.621371;
            StatusText = $"Speed: {newSpeedMph:F1} mph ({_gpsService.Simulator.SpeedKmh:F1} km/h)";
        }
    }

    [RelayCommand]
    private void SimulatorSlowDown()
    {
        if (_gpsService.Simulator != null)
        {
            // Get current speed in mph
            double currentSpeedKmh = _gpsService.Simulator.SpeedKmh;
            double currentSpeedMph = currentSpeedKmh * 0.621371;

            // Variable increment based on current speed
            // 1 mph ≈ 0.04 stepDistance
            double decrement;
            if (Math.Abs(currentSpeedMph) < 10)
            {
                // At low speeds (0-10 mph): decrement by 1 mph
                decrement = 0.04;
            }
            else if (Math.Abs(currentSpeedMph) < 20)
            {
                // At medium speeds (10-20 mph): decrement by 2 mph
                decrement = 0.08;
            }
            else
            {
                // At high speeds (20+ mph): decrement by 3 mph
                decrement = 0.12;
            }

            // Decrease speed
            _gpsService.Simulator.StepDistance -= decrement;

            // Clamp to max reverse speed (-10 km/h ≈ -6.2 mph)
            if (_gpsService.Simulator.StepDistance < -0.25)
                _gpsService.Simulator.StepDistance = -0.25;

            double newSpeedMph = _gpsService.Simulator.SpeedKmh * 0.621371;
            StatusText = $"Speed: {newSpeedMph:F1} mph ({_gpsService.Simulator.SpeedKmh:F1} km/h)";
        }
    }

    [RelayCommand]
    private void SimulatorStopAcceleration()
    {
        if (_gpsService.Simulator != null)
        {
            _gpsService.Simulator.IsAcceleratingForward = false;
            _gpsService.Simulator.IsAcceleratingBackward = false;
            _gpsService.Simulator.StepDistance = 0; // Stop instantly, no coast
            StatusText = "Stopped";
        }
    }

    [RelayCommand]
    private void SimulatorReverseDirection()
    {
        if (_gpsService.Simulator != null)
        {
            // Reverse heading by 180 degrees
            double currentHeading = _gpsService.Simulator.HeadingDegrees;
            double newHeading = (currentHeading + 180) % 360;
            _gpsService.Simulator.SetHeading(newHeading);
            StatusText = $"Reversed direction - Heading: {newHeading:F1}°";
        }
    }

    [RelayCommand]
    private void SimulatorResetPosition()
    {
        if (_gpsService.Simulator != null)
        {
            _gpsService.Simulator.Reset();
            StatusText = "Simulator position reset";
        }
    }

    partial void OnSimulatorSteerAngleChanged(double value)
    {
        if (_gpsService.Simulator != null)
        {
            _gpsService.Simulator.SteerAngle = value;
        }
    }

    [RelayCommand]
    private void StartRecordingNotch()
    {
        if (_currentField == null || _currentField.Boundary?.OuterBoundary == null)
        {
            StatusText = "Please create a boundary first";
            return;
        }

        if (!IsConnected)
        {
            StatusText = "No GPS fix - cannot start recording notch";
            return;
        }

        NotchPoints.Clear();
        NotchPointCount = 0;
        CanApplyNotch = false;
        IsRecordingNotch = true;
        StatusText = "Recording notch points...";
        Console.WriteLine("[VIEWMODEL] Started recording notch");
    }

    [RelayCommand]
    private void StopRecordingNotch()
    {
        // Always validate, even if continuous recording was already stopped
        IsRecordingNotch = false;

        // Check if we have enough points and if they cross the boundary twice
        if (NotchPoints.Count < 2)
        {
            StatusText = "Not enough notch points (minimum 2 required)";
            CanApplyNotch = false;
            return;
        }

        // Check if notch path crosses boundary
        var crossings = FindBoundaryCrossings(NotchPoints.ToList());

        if (crossings.Count < 2)
        {
            StatusText = "Notch must cross boundary at least twice";
            CanApplyNotch = false;
            Console.WriteLine("[VIEWMODEL] Notch recording stopped but not enough crossings");
            return;
        }

        // Check if we have an even number of crossings (pairs of in/out)
        if (crossings.Count % 2 != 0)
        {
            StatusText = $"Invalid notch: Found {crossings.Count} crossings (must be even number)";
            CanApplyNotch = false;
            Console.WriteLine($"[VIEWMODEL] Notch invalid: odd number of crossings ({crossings.Count})");
            return;
        }

        // Check if both ends of the notch are outside the boundary
        var firstPoint = NotchPoints[0];
        var lastPoint = NotchPoints[NotchPoints.Count - 1];

        bool firstInside = IsPointInsideBoundary(firstPoint);
        bool lastInside = IsPointInsideBoundary(lastPoint);
        bool firstOutside = !firstInside;
        bool lastOutside = !lastInside;

        Console.WriteLine($"[VIEWMODEL] First point E={firstPoint.Easting:F2}, N={firstPoint.Northing:F2}, inside={firstInside}");
        Console.WriteLine($"[VIEWMODEL] Last point E={lastPoint.Easting:F2}, N={lastPoint.Northing:F2}, inside={lastInside}");
        Console.WriteLine($"[VIEWMODEL] Boundary has {_currentField?.Boundary?.OuterBoundary?.Points.Count ?? 0} points");

        if (!firstOutside || !lastOutside)
        {
            StatusText = "Invalid notch: Both start and end points must be outside the boundary";
            CanApplyNotch = false;
            Console.WriteLine($"[VIEWMODEL] Notch invalid: start outside={firstOutside}, end outside={lastOutside}");
            return;
        }

        int numNotches = crossings.Count / 2;
        CanApplyNotch = true;
        StatusText = $"Notch recorded: {NotchPoints.Count} points, {crossings.Count} crossings, {numNotches} notch(es)";
        Console.WriteLine($"[VIEWMODEL] Notch recording stopped. Found {crossings.Count} crossings ({numNotches} notches), both ends outside boundary");
    }

    [RelayCommand]
    private void ApplyNotch()
    {
        if (!CanApplyNotch || _currentField?.Boundary?.OuterBoundary == null)
        {
            return;
        }

        try
        {
            var allCrossings = FindBoundaryCrossingsWithNotchIndex(NotchPoints.ToList());
            if (allCrossings.Count < 2 || allCrossings.Count % 2 != 0)
            {
                StatusText = "Cannot apply notch - must have even number of crossings";
                return;
            }

            int numNotches = allCrossings.Count / 2;
            Console.WriteLine($"[VIEWMODEL] Applying {numNotches} notch(es) from {allCrossings.Count} crossings");

            // Group crossings into pairs (crossing 0-1 = notch 1, crossing 2-3 = notch 2, etc.)
            var notchPairs = new List<(BoundaryCrossingWithNotchIndex first, BoundaryCrossingWithNotchIndex second)>();
            for (int i = 0; i < allCrossings.Count; i += 2)
            {
                notchPairs.Add((allCrossings[i], allCrossings[i + 1]));
                Console.WriteLine($"[VIEWMODEL] Notch pair {i/2 + 1}: crossings {i} and {i+1}");
            }

            // Sort pairs by boundary segment index so we can process them in order
            var sortedPairs = notchPairs
                .Select((pair, index) => new {
                    PairIndex = index,
                    FirstBoundaryIdx = Math.Min(pair.first.BoundarySegmentIndex, pair.second.BoundarySegmentIndex),
                    SecondBoundaryIdx = Math.Max(pair.first.BoundarySegmentIndex, pair.second.BoundarySegmentIndex),
                    FirstCrossing = pair.first.BoundarySegmentIndex <= pair.second.BoundarySegmentIndex ? pair.first : pair.second,
                    SecondCrossing = pair.first.BoundarySegmentIndex <= pair.second.BoundarySegmentIndex ? pair.second : pair.first,
                    ChronologicalFirst = pair.first,
                    ChronologicalSecond = pair.second
                })
                .OrderBy(p => p.FirstBoundaryIdx)
                .ToList();

            // Build new boundary by walking through old boundary and inserting notches
            var oldBoundary = _currentField.Boundary.OuterBoundary.Points;
            var newBoundary = new List<BoundaryPoint>();
            int currentBoundaryIdx = 0;
            int currentPairIdx = 0;

            while (currentBoundaryIdx < oldBoundary.Count)
            {
                // Check if we're at a notch insertion point
                if (currentPairIdx < sortedPairs.Count &&
                    currentBoundaryIdx == sortedPairs[currentPairIdx].FirstBoundaryIdx)
                {
                    var pair = sortedPairs[currentPairIdx];
                    Console.WriteLine($"[VIEWMODEL] Processing notch {currentPairIdx + 1} at boundary index {currentBoundaryIdx}");

                    // Add boundary points up to this crossing
                    newBoundary.Add(oldBoundary[currentBoundaryIdx]);

                    // Add first intersection point
                    newBoundary.Add(pair.FirstCrossing.CrossingPoint);

                    // Add notch points between the two crossings
                    int startNotchIdx = Math.Min(pair.ChronologicalFirst.NotchSegmentIndex, pair.ChronologicalSecond.NotchSegmentIndex);
                    int endNotchIdx = Math.Max(pair.ChronologicalFirst.NotchSegmentIndex, pair.ChronologicalSecond.NotchSegmentIndex);

                    var notchPointsToAdd = new List<BoundaryPoint>();
                    for (int i = startNotchIdx + 1; i <= endNotchIdx; i++)
                    {
                        if (i < NotchPoints.Count)
                        {
                            notchPointsToAdd.Add(NotchPoints[i]);
                        }
                    }

                    // Reverse if needed to match boundary direction
                    if (pair.FirstCrossing.NotchSegmentIndex > pair.SecondCrossing.NotchSegmentIndex)
                    {
                        notchPointsToAdd.Reverse();
                    }

                    newBoundary.AddRange(notchPointsToAdd);

                    // Add second intersection point
                    newBoundary.Add(pair.SecondCrossing.CrossingPoint);

                    // Skip boundary points between the two crossings
                    currentBoundaryIdx = pair.SecondBoundaryIdx + 1;
                    currentPairIdx++;
                }
                else
                {
                    // Regular boundary point, just add it
                    newBoundary.Add(oldBoundary[currentBoundaryIdx]);
                    currentBoundaryIdx++;
                }
            }

            // Replace boundary with new one
            _currentField.Boundary.OuterBoundary.Points = newBoundary;

            // Update boundary visualization
            BoundaryPoints.Clear();
            foreach (var pt in newBoundary)
            {
                BoundaryPoints.Add(pt);
            }
            _visualizationControl?.SetBoundaryPoints(BoundaryPoints);

            // Save modified boundary
            BoundaryFileService.SaveBoundary(_currentField.Boundary, _currentField.DirectoryPath);

            StatusText = $"{numNotches} notch(es) applied to boundary";
            Console.WriteLine($"[VIEWMODEL] {numNotches} notch(es) applied successfully. New boundary: {newBoundary.Count} points");

            // Clear notch data
            NotchPoints.Clear();
            NotchPointCount = 0;
            CanApplyNotch = false;

            // Clear notch visualization
            _visualizationControl?.SetNotchPoints(NotchPoints);
        }
        catch (Exception ex)
        {
            StatusText = $"Error applying notch: {ex.Message}";
            Console.WriteLine($"[VIEWMODEL] Error applying notch: {ex.Message}");
            Console.WriteLine($"[VIEWMODEL] Stack trace: {ex.StackTrace}");
        }
    }

    [RelayCommand]
    private void CancelNotch()
    {
        NotchPoints.Clear();
        NotchPointCount = 0;
        CanApplyNotch = false;
        IsRecordingNotch = false;
        StatusText = "Notch cancelled";
        Console.WriteLine("[VIEWMODEL] Notch cancelled");

        // Update visualization to clear notch points
        _visualizationControl?.SetNotchPoints(NotchPoints);
    }

    // Inner boundary modification methods
    private void StopRecordingInnerModify()
    {
        IsRecordingNotch = false; // Reusing the same flag

        // Check if we have enough points
        if (NotchPoints.Count < 2)
        {
            StatusText = "Not enough points (minimum 2 required)";
            CanApplyNotch = false;
            return;
        }

        if (_targetInnerBoundaryIndex < 0 ||
            _currentField?.Boundary?.InnerBoundaries == null ||
            _targetInnerBoundaryIndex >= _currentField.Boundary.InnerBoundaries.Count)
        {
            StatusText = "Invalid inner boundary selection";
            CanApplyNotch = false;
            return;
        }

        // Check if path crosses the target inner boundary
        var targetInnerBoundary = _currentField.Boundary.InnerBoundaries[_targetInnerBoundaryIndex];
        var crossings = FindInnerBoundaryCrossings(NotchPoints.ToList(), targetInnerBoundary.Points);

        if (crossings.Count < 2)
        {
            StatusText = "Path must cross the inner boundary at least twice";
            CanApplyNotch = false;
            Console.WriteLine("[VIEWMODEL] Inner modify: not enough crossings");
            return;
        }

        // Check for even number of crossings
        if (crossings.Count % 2 != 0)
        {
            StatusText = $"Invalid path: Found {crossings.Count} crossings (must be even number)";
            CanApplyNotch = false;
            Console.WriteLine($"[VIEWMODEL] Inner modify: odd number of crossings ({crossings.Count})");
            return;
        }

        int numModifications = crossings.Count / 2;
        string operationName = _innerModifyOperation == "notch" ? "Notch" : "Bulge";
        CanApplyNotch = true;
        StatusText = $"{operationName} recorded: {NotchPoints.Count} points, {crossings.Count} crossings, {numModifications} modification(s)";
        Console.WriteLine($"[VIEWMODEL] Inner {operationName} recording stopped. Found {crossings.Count} crossings ({numModifications} modifications)");
    }

    [RelayCommand]
    private void ApplyInnerModification()
    {
        if (!CanApplyNotch || _currentField?.Boundary?.InnerBoundaries == null ||
            _targetInnerBoundaryIndex < 0 || _targetInnerBoundaryIndex >= _currentField.Boundary.InnerBoundaries.Count)
        {
            return;
        }

        try
        {
            var targetInnerBoundary = _currentField.Boundary.InnerBoundaries[_targetInnerBoundaryIndex];
            var allCrossings = FindInnerBoundaryCrossingsWithNotchIndex(NotchPoints.ToList(), targetInnerBoundary.Points);

            if (allCrossings.Count < 2 || allCrossings.Count % 2 != 0)
            {
                StatusText = "Cannot apply modification - must have even number of crossings";
                return;
            }

            int numModifications = allCrossings.Count / 2;
            string operationName = _innerModifyOperation == "notch" ? "notch" : "bulge";
            Console.WriteLine($"[VIEWMODEL] Applying {numModifications} inner {operationName}(es) from {allCrossings.Count} crossings");

            // Apply the modification to the inner boundary
            var modifiedBoundary = ApplyInnerBoundaryModification(
                targetInnerBoundary.Points,
                NotchPoints.ToList(),
                allCrossings);

            // Update the inner boundary
            targetInnerBoundary.Points.Clear();
            foreach (var point in modifiedBoundary)
            {
                targetInnerBoundary.Points.Add(point);
            }

            // Update visualization
            _visualizationControl?.SetInnerBoundaries(
                _currentField.Boundary.InnerBoundaries.Select(ib => ib.Points));

            // Clear notch points from visualization
            NotchPoints.Clear();
            NotchPointCount = 0;
            _visualizationControl?.SetNotchPoints(NotchPoints);

            // Recalculate area
            BoundaryArea = _currentField.Boundary.AreaHectares;

            // Save modified boundary
            BoundaryFileService.SaveBoundary(_currentField.Boundary, _currentField.DirectoryPath);

            StatusText = $"Inner boundary {operationName} applied successfully - Area: {BoundaryArea:F2} ha";
            Console.WriteLine($"[VIEWMODEL] Inner boundary {operationName} applied. New inner boundary has {targetInnerBoundary.Points.Count} points");
        }
        catch (Exception ex)
        {
            StatusText = $"Error applying modification: {ex.Message}";
            Console.WriteLine($"[VIEWMODEL] Error applying inner modification: {ex.Message}");
        }
    }

    private class BoundaryCrossing
    {
        public int SegmentIndex { get; set; }
        public BoundaryPoint CrossingPoint { get; set; } = new BoundaryPoint(0, 0, 0);
    }

    private class BoundaryCrossingWithNotchIndex
    {
        public int BoundarySegmentIndex { get; set; }
        public int NotchSegmentIndex { get; set; }
        public BoundaryPoint CrossingPoint { get; set; } = new BoundaryPoint(0, 0, 0);
    }

    private List<BoundaryCrossing> FindBoundaryCrossings(List<BoundaryPoint> notchPath)
    {
        var crossings = new List<BoundaryCrossing>();

        if (_currentField?.Boundary?.OuterBoundary == null || notchPath.Count < 2)
        {
            return crossings;
        }

        var boundaryPoints = _currentField.Boundary.OuterBoundary.Points;

        // Check each segment of the notch path against each segment of the boundary
        for (int i = 0; i < notchPath.Count - 1; i++)
        {
            var notchP1 = notchPath[i];
            var notchP2 = notchPath[i + 1];

            for (int j = 0; j < boundaryPoints.Count; j++)
            {
                var boundaryP1 = boundaryPoints[j];
                var boundaryP2 = boundaryPoints[(j + 1) % boundaryPoints.Count];

                if (LineSegmentsIntersect(notchP1, notchP2, boundaryP1, boundaryP2, out var intersection))
                {
                    crossings.Add(new BoundaryCrossing
                    {
                        SegmentIndex = j,
                        CrossingPoint = intersection
                    });
                }
            }
        }

        return crossings;
    }

    private List<BoundaryCrossingWithNotchIndex> FindBoundaryCrossingsWithNotchIndex(List<BoundaryPoint> notchPath)
    {
        var crossings = new List<BoundaryCrossingWithNotchIndex>();

        if (_currentField?.Boundary?.OuterBoundary == null || notchPath.Count < 2)
        {
            return crossings;
        }

        var boundaryPoints = _currentField.Boundary.OuterBoundary.Points;

        // Check each segment of the notch path against each segment of the boundary
        for (int i = 0; i < notchPath.Count - 1; i++)
        {
            var notchP1 = notchPath[i];
            var notchP2 = notchPath[i + 1];

            for (int j = 0; j < boundaryPoints.Count; j++)
            {
                var boundaryP1 = boundaryPoints[j];
                var boundaryP2 = boundaryPoints[(j + 1) % boundaryPoints.Count];

                if (LineSegmentsIntersect(notchP1, notchP2, boundaryP1, boundaryP2, out var intersection))
                {
                    crossings.Add(new BoundaryCrossingWithNotchIndex
                    {
                        BoundarySegmentIndex = j,
                        NotchSegmentIndex = i,
                        CrossingPoint = intersection
                    });
                }
            }
        }

        return crossings;
    }

    // Inner boundary crossing detection methods
    private List<BoundaryCrossing> FindInnerBoundaryCrossings(List<BoundaryPoint> modifyPath, IList<BoundaryPoint> innerBoundaryPoints)
    {
        var crossings = new List<BoundaryCrossing>();

        if (modifyPath.Count < 2 || innerBoundaryPoints.Count < 2)
        {
            return crossings;
        }

        // Check each segment of the modify path against each segment of the inner boundary
        for (int i = 0; i < modifyPath.Count - 1; i++)
        {
            var pathP1 = modifyPath[i];
            var pathP2 = modifyPath[i + 1];

            for (int j = 0; j < innerBoundaryPoints.Count; j++)
            {
                var boundaryP1 = innerBoundaryPoints[j];
                var boundaryP2 = innerBoundaryPoints[(j + 1) % innerBoundaryPoints.Count];

                if (LineSegmentsIntersect(pathP1, pathP2, boundaryP1, boundaryP2, out var intersection))
                {
                    crossings.Add(new BoundaryCrossing
                    {
                        SegmentIndex = j,
                        CrossingPoint = intersection
                    });
                }
            }
        }

        return crossings;
    }

    private List<BoundaryCrossingWithNotchIndex> FindInnerBoundaryCrossingsWithNotchIndex(List<BoundaryPoint> modifyPath, IList<BoundaryPoint> innerBoundaryPoints)
    {
        var crossings = new List<BoundaryCrossingWithNotchIndex>();

        if (modifyPath.Count < 2 || innerBoundaryPoints.Count < 2)
        {
            return crossings;
        }

        // Check each segment of the modify path against each segment of the inner boundary
        for (int i = 0; i < modifyPath.Count - 1; i++)
        {
            var pathP1 = modifyPath[i];
            var pathP2 = modifyPath[i + 1];

            for (int j = 0; j < innerBoundaryPoints.Count; j++)
            {
                var boundaryP1 = innerBoundaryPoints[j];
                var boundaryP2 = innerBoundaryPoints[(j + 1) % innerBoundaryPoints.Count];

                if (LineSegmentsIntersect(pathP1, pathP2, boundaryP1, boundaryP2, out var intersection))
                {
                    crossings.Add(new BoundaryCrossingWithNotchIndex
                    {
                        BoundarySegmentIndex = j,
                        NotchSegmentIndex = i,
                        CrossingPoint = intersection
                    });
                }
            }
        }

        return crossings;
    }

    private List<BoundaryPoint> ApplyInnerBoundaryModification(
        IList<BoundaryPoint> innerBoundaryPoints,
        List<BoundaryPoint> modifyPath,
        List<BoundaryCrossingWithNotchIndex> allCrossings)
    {
        // For bulge operation, we reverse the modification path traversal
        bool isBulge = _innerModifyOperation == "bulge";

        Console.WriteLine($"[VIEWMODEL] ApplyInnerBoundaryModification: {allCrossings.Count} crossings, operation={_innerModifyOperation}");

        // Group crossings into pairs (crossing 0-1 = modification 1, crossing 2-3 = modification 2, etc.)
        var modificationPairs = new List<(BoundaryCrossingWithNotchIndex first, BoundaryCrossingWithNotchIndex second)>();
        for (int i = 0; i < allCrossings.Count; i += 2)
        {
            modificationPairs.Add((allCrossings[i], allCrossings[i + 1]));
            Console.WriteLine($"[VIEWMODEL] Modification pair {i/2 + 1}: crossings {i} and {i+1}");
        }

        // Sort pairs by boundary segment index so we can process them in order
        var sortedPairs = modificationPairs
            .Select((pair, index) => new {
                PairIndex = index,
                FirstBoundaryIdx = Math.Min(pair.first.BoundarySegmentIndex, pair.second.BoundarySegmentIndex),
                SecondBoundaryIdx = Math.Max(pair.first.BoundarySegmentIndex, pair.second.BoundarySegmentIndex),
                FirstCrossing = pair.first.BoundarySegmentIndex <= pair.second.BoundarySegmentIndex ? pair.first : pair.second,
                SecondCrossing = pair.first.BoundarySegmentIndex <= pair.second.BoundarySegmentIndex ? pair.second : pair.first,
                ChronologicalFirst = pair.first,
                ChronologicalSecond = pair.second
            })
            .OrderBy(p => p.FirstBoundaryIdx)
            .ToList();

        // Build new boundary by walking through old boundary and inserting modifications
        var newBoundary = new List<BoundaryPoint>();
        int currentBoundaryIdx = 0;
        int currentPairIdx = 0;

        while (currentBoundaryIdx < innerBoundaryPoints.Count)
        {
            // Check if we're at a modification insertion point
            if (currentPairIdx < sortedPairs.Count &&
                currentBoundaryIdx == sortedPairs[currentPairIdx].FirstBoundaryIdx)
            {
                var pair = sortedPairs[currentPairIdx];
                Console.WriteLine($"[VIEWMODEL] Processing modification {currentPairIdx + 1} at boundary index {currentBoundaryIdx}");

                // Add boundary points up to this crossing
                newBoundary.Add(innerBoundaryPoints[currentBoundaryIdx]);

                // Add first intersection point
                newBoundary.Add(pair.FirstCrossing.CrossingPoint);

                // Add modification points between the two crossings
                int startModifyIdx = Math.Min(pair.ChronologicalFirst.NotchSegmentIndex, pair.ChronologicalSecond.NotchSegmentIndex);
                int endModifyIdx = Math.Max(pair.ChronologicalFirst.NotchSegmentIndex, pair.ChronologicalSecond.NotchSegmentIndex);

                var modifyPointsToAdd = new List<BoundaryPoint>();
                for (int i = startModifyIdx + 1; i <= endModifyIdx; i++)
                {
                    if (i < modifyPath.Count)
                    {
                        modifyPointsToAdd.Add(modifyPath[i]);
                    }
                }

                // For notch (hole smaller), reverse to match opposite direction
                // For bulge (hole bigger), use same logic as outer boundary notch
                bool shouldReverse = isBulge
                    ? (pair.FirstCrossing.NotchSegmentIndex > pair.SecondCrossing.NotchSegmentIndex)
                    : (pair.FirstCrossing.NotchSegmentIndex <= pair.SecondCrossing.NotchSegmentIndex);

                if (shouldReverse)
                {
                    modifyPointsToAdd.Reverse();
                    Console.WriteLine($"[VIEWMODEL] Reversed modification points for {_innerModifyOperation} (First={pair.FirstCrossing.NotchSegmentIndex}, Second={pair.SecondCrossing.NotchSegmentIndex})");
                }
                else
                {
                    Console.WriteLine($"[VIEWMODEL] No reverse for {_innerModifyOperation} (First={pair.FirstCrossing.NotchSegmentIndex}, Second={pair.SecondCrossing.NotchSegmentIndex})");
                }

                newBoundary.AddRange(modifyPointsToAdd);

                // Add second intersection point
                newBoundary.Add(pair.SecondCrossing.CrossingPoint);

                // Skip boundary points between the two crossings
                currentBoundaryIdx = pair.SecondBoundaryIdx + 1;
                currentPairIdx++;
            }
            else
            {
                // Regular boundary point, just add it
                newBoundary.Add(innerBoundaryPoints[currentBoundaryIdx]);
                currentBoundaryIdx++;
            }
        }

        Console.WriteLine($"[VIEWMODEL] Modified inner boundary: {innerBoundaryPoints.Count} -> {newBoundary.Count} points");
        return newBoundary;
    }

    private bool LineSegmentsIntersect(BoundaryPoint p1, BoundaryPoint p2, BoundaryPoint p3, BoundaryPoint p4, out BoundaryPoint intersection)
    {
        intersection = new BoundaryPoint(0, 0, 0);

        double x1 = p1.Easting, y1 = p1.Northing;
        double x2 = p2.Easting, y2 = p2.Northing;
        double x3 = p3.Easting, y3 = p3.Northing;
        double x4 = p4.Easting, y4 = p4.Northing;

        double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
        if (Math.Abs(denom) < 0.001) return false; // Parallel lines

        double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
        double u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denom;

        if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
        {
            // Intersection found
            double x = x1 + t * (x2 - x1);
            double y = y1 + t * (y2 - y1);
            intersection = new BoundaryPoint(x, y, 0);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a point is inside the boundary using ray casting algorithm
    /// </summary>
    private bool IsPointInsideBoundary(BoundaryPoint point)
    {
        if (_currentField?.Boundary?.OuterBoundary == null)
        {
            Console.WriteLine($"[VIEWMODEL] IsPointInsideBoundary: No boundary exists");
            return false;
        }

        var boundaryPoints = _currentField.Boundary.OuterBoundary.Points;
        if (boundaryPoints.Count < 3)
        {
            Console.WriteLine($"[VIEWMODEL] IsPointInsideBoundary: Boundary has less than 3 points");
            return false;
        }

        // Ray casting algorithm: count how many times a ray from the point
        // to infinity crosses the polygon boundary
        int crossings = 0;
        double px = point.Easting;
        double py = point.Northing;

        for (int i = 0; i < boundaryPoints.Count; i++)
        {
            var p1 = boundaryPoints[i];
            var p2 = boundaryPoints[(i + 1) % boundaryPoints.Count];

            double x1 = p1.Easting, y1 = p1.Northing;
            double x2 = p2.Easting, y2 = p2.Northing;

            // Check if the ray crosses this edge
            // Ray goes from point horizontally to the right (positive X direction)
            if ((y1 > py) != (y2 > py))
            {
                // Calculate X coordinate of intersection
                double xIntersection = x1 + (py - y1) * (x2 - x1) / (y2 - y1);

                // If intersection is to the right of the point, count it
                if (px < xIntersection)
                {
                    crossings++;
                }
            }
        }

        // Odd number of crossings means inside, even means outside
        bool isInside = (crossings % 2) == 1;
        Console.WriteLine($"[VIEWMODEL] IsPointInsideBoundary: point({px:F2}, {py:F2}), crossings={crossings}, inside={isInside}");
        return isInside;
    }

    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = isConnected;
            IsSimulatorMode = _gpsService.IsSimulatorMode;
        });
    }

    private void OnPointRecorded(object? sender, BoundaryPoint point)
    {
        Dispatcher.UIThread.Post(() =>
        {
            BoundaryPoints.Add(point);
            PointCount = BoundaryPoints.Count;

            Console.WriteLine($"[VIEWMODEL] Point recorded, total: {PointCount}. Updating visualization...");

            // Update visualization with new boundary points
            _visualizationControl?.SetBoundaryPoints(BoundaryPoints);
        });
    }
}
