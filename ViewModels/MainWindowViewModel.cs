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

    public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>();
    public ObservableCollection<BoundaryPoint> BoundaryPoints { get; } = new ObservableCollection<BoundaryPoint>();

    public MainWindowViewModel()
    {
        _gpsService = new GpsService();
        _recordingService = new BoundaryRecordingService();

        _gpsService.PositionReceived += OnPositionReceived;
        _gpsService.ConnectionStatusChanged += OnConnectionStatusChanged;
        _recordingService.PointRecorded += OnPointRecorded;

        RefreshPorts();

        // Set default field directory
        FieldDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AgBoundaryTool", "Fields");
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
    private void LoadField()
    {
        // TODO: Implement field browser dialog
        StatusText = "Load field not yet implemented";
    }

    [RelayCommand]
    private void SaveField()
    {
        if (_currentField == null)
        {
            StatusText = "No field to save";
            return;
        }

        try
        {
            FieldPlaneFileService.SaveField(_currentField, _currentField.DirectoryPath);
            if (_currentField.Boundary != null)
            {
                BoundaryFileService.SaveBoundary(_currentField.Boundary, _currentField.DirectoryPath);
            }
            StatusText = $"Field '{_currentField.Name}' saved";
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving field: {ex.Message}";
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
                    Console.WriteLine($"[VIEWMODEL] Using field origin: {origin.Latitude:F6}, {origin.Longitude:F6}");
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
                Console.WriteLine($"[VIEWMODEL] Position: Lat={position.Latitude:F6}, Lon={position.Longitude:F6} -> E={easting:F2}, N={northing:F2}");

                _visualizationControl.SetVehiclePosition(position, easting, northing);
            }

            if (IsRecording)
            {
                _recordingService.RecordPosition(position);
            }
        });
    }

    [RelayCommand]
    private void StartSimulator()
    {
        _gpsService.StartSimulator(SimulatorLatitude, SimulatorLongitude);
        IsSimulatorMode = true;
        StatusText = $"Simulator started at {SimulatorLatitude:F6}, {SimulatorLongitude:F6}";
    }

    [RelayCommand]
    private void StopSimulatorGps()
    {
        _gpsService.StopSimulator();
        IsSimulatorMode = false;
        StatusText = "Simulator stopped";
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
            StatusText = "Stopped - coasting to halt";
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

            // Update visualization with new boundary points
            _visualizationControl?.SetBoundaryPoints(BoundaryPoints);
        });
    }
}
