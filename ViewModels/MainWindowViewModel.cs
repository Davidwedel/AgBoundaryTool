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

        var dialog = new Views.Dialogs.BoundaryRecordingDialog
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

        var dialog = new Views.Dialogs.BoundaryNotchDialog
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

            // Record notch points if notch recording is active
            if (IsRecordingNotch && _currentField != null)
            {
                // Check if we should record this point (1m minimum distance)
                if (NotchPoints.Count == 0)
                {
                    // First point - always record
                    var (e, n) = CoordinateConversionService.ToLocal(position, _currentField.Origin);
                    NotchPoints.Add(new BoundaryPoint(e, n, 0));
                    NotchPointCount = NotchPoints.Count;
                    Console.WriteLine($"[VIEWMODEL] Notch point #{NotchPointCount} recorded: E={e:F2}m, N={n:F2}m");

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
                        Console.WriteLine($"[VIEWMODEL] Notch point #{NotchPointCount} recorded: E={e:F2}m, N={n:F2}m (dist={distance:F2}m)");

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
    private void SimulatorAccelerateForward()
    {
        if (_gpsService.Simulator != null)
        {
            // Toggle forward acceleration
            _gpsService.Simulator.IsAcceleratingForward = !_gpsService.Simulator.IsAcceleratingForward;
            if (_gpsService.Simulator.IsAcceleratingForward)
            {
                _gpsService.Simulator.IsAcceleratingBackward = false;
                StatusText = "Accelerating forward...";
            }
            else
            {
                StatusText = "Coasting...";
            }
        }
    }

    [RelayCommand]
    private void SimulatorAccelerateBackward()
    {
        if (_gpsService.Simulator != null)
        {
            // Toggle backward acceleration
            _gpsService.Simulator.IsAcceleratingBackward = !_gpsService.Simulator.IsAcceleratingBackward;
            if (_gpsService.Simulator.IsAcceleratingBackward)
            {
                _gpsService.Simulator.IsAcceleratingForward = false;
                StatusText = "Reversing...";
            }
            else
            {
                StatusText = "Coasting...";
            }
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
        if (!IsRecordingNotch)
        {
            return;
        }

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
        CanApplyNotch = crossings.Count >= 2;

        if (CanApplyNotch)
        {
            StatusText = $"Notch recorded: {NotchPoints.Count} points, {crossings.Count} boundary crossings";
            Console.WriteLine($"[VIEWMODEL] Notch recording stopped. Found {crossings.Count} crossings");
        }
        else
        {
            StatusText = "Notch must cross boundary at least twice";
            Console.WriteLine("[VIEWMODEL] Notch recording stopped but not enough crossings");
        }
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
            var crossingsWithNotchIndex = FindBoundaryCrossingsWithNotchIndex(NotchPoints.ToList());
            if (crossingsWithNotchIndex.Count < 2)
            {
                StatusText = "Cannot apply notch - must cross boundary at least twice";
                return;
            }

            // Get first two crossings (in chronological order - order they were crossed in time)
            var crossing1 = crossingsWithNotchIndex[0];
            var crossing2 = crossingsWithNotchIndex[1];

            Console.WriteLine($"[VIEWMODEL] Applying notch between crossings at boundary segment {crossing1.BoundarySegmentIndex} and {crossing2.BoundarySegmentIndex}");
            Console.WriteLine($"[VIEWMODEL] Crossing 1 (time): E={crossing1.CrossingPoint.Easting:F2}, N={crossing1.CrossingPoint.Northing:F2}, notch index={crossing1.NotchSegmentIndex}");
            Console.WriteLine($"[VIEWMODEL] Crossing 2 (time): E={crossing2.CrossingPoint.Easting:F2}, N={crossing2.CrossingPoint.Northing:F2}, notch index={crossing2.NotchSegmentIndex}");

            // Build new boundary with notch inserted at intersection points
            var oldBoundary = _currentField.Boundary.OuterBoundary.Points;
            var newBoundary = new List<BoundaryPoint>();

            // Determine which crossing comes first/second in the boundary (spatially, not temporally)
            int firstBoundaryIdx = Math.Min(crossing1.BoundarySegmentIndex, crossing2.BoundarySegmentIndex);
            int secondBoundaryIdx = Math.Max(crossing1.BoundarySegmentIndex, crossing2.BoundarySegmentIndex);

            // Keep track of which crossing (chronologically) corresponds to which boundary position
            var firstBoundaryCrossing = crossing1.BoundarySegmentIndex <= crossing2.BoundarySegmentIndex ? crossing1 : crossing2;
            var secondBoundaryCrossing = crossing1.BoundarySegmentIndex <= crossing2.BoundarySegmentIndex ? crossing2 : crossing1;

            // Add boundary points up to first boundary crossing
            for (int i = 0; i <= firstBoundaryIdx; i++)
            {
                newBoundary.Add(oldBoundary[i]);
            }

            // Add first boundary intersection point
            newBoundary.Add(firstBoundaryCrossing.CrossingPoint);

            // Add the notch points between the chronological first and second crossings
            // Use crossing1 and crossing2 (chronological order) to get the notch segment
            int startNotchIdx = Math.Min(crossing1.NotchSegmentIndex, crossing2.NotchSegmentIndex);
            int endNotchIdx = Math.Max(crossing1.NotchSegmentIndex, crossing2.NotchSegmentIndex);

            Console.WriteLine($"[VIEWMODEL] Adding notch points from index {startNotchIdx} to {endNotchIdx}");

            var notchPointsToAdd = new List<BoundaryPoint>();
            for (int i = startNotchIdx + 1; i <= endNotchIdx; i++)
            {
                if (i < NotchPoints.Count)
                {
                    notchPointsToAdd.Add(NotchPoints[i]);
                }
            }

            // Determine if we need to reverse based on which boundary crossing came first
            // If crossing1 (chronological first) is the second boundary crossing (spatial),
            // then we need to reverse the notch points
            if (firstBoundaryCrossing.NotchSegmentIndex > secondBoundaryCrossing.NotchSegmentIndex)
            {
                notchPointsToAdd.Reverse();
                Console.WriteLine($"[VIEWMODEL] Reversing notch points to match boundary direction");
            }

            newBoundary.AddRange(notchPointsToAdd);

            // Add second boundary intersection point
            newBoundary.Add(secondBoundaryCrossing.CrossingPoint);

            // Skip boundary points between the crossings, continue after second crossing
            // This removes the original boundary segment and replaces it with the notch
            for (int i = secondBoundaryIdx + 1; i < oldBoundary.Count; i++)
            {
                newBoundary.Add(oldBoundary[i]);
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

            StatusText = $"Notch applied to boundary";
            Console.WriteLine($"[VIEWMODEL] Notch applied successfully. New boundary: {newBoundary.Count} points");

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
