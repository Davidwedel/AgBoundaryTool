// AgBoundaryTool
// Application settings that persist between sessions

using System;
using System.Collections.Generic;

namespace AgBoundaryTool.Models;

/// <summary>
/// Application settings that are persisted between sessions
/// </summary>
public class AppSettings
{
    // Window settings
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; } = false;

    // GPS settings
    public string LastGpsPort { get; set; } = string.Empty;
    public int GpsBaudRate { get; set; } = 9600;

    // NTRIP settings
    public bool NtripEnabled { get; set; } = false;
    public string NtripHost { get; set; } = string.Empty;
    public int NtripPort { get; set; } = 2101;
    public string NtripMountPoint { get; set; } = string.Empty;
    public string NtripUsername { get; set; } = string.Empty;
    public string NtripPassword { get; set; } = string.Empty;
    public bool NtripUseSsl { get; set; } = false;

    // Simulator settings
    public bool SimulatorWasRunning { get; set; } = false;
    public double SimulatorLatitude { get; set; } = 39.828234;
    public double SimulatorLongitude { get; set; } = -98.580452;
    public double SimulatorSteerAngle { get; set; } = 0.0;

    // Field settings
    public string FieldsDirectory { get; set; } = string.Empty;
    public string LastFieldName { get; set; } = string.Empty;

    // Visualization settings
    public double CameraZoom { get; set; } = 1.0;

    // First run
    public bool IsFirstRun { get; set; } = true;
    public DateTime LastRunDate { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Validate and clamp all settings to valid ranges.
    /// Returns list of fields that were corrected.
    /// </summary>
    public List<string> ValidateAndFix()
    {
        var defaults = new AppSettings();
        var fixes = new List<string>();

        // Simulator coordinates
        if (SimulatorLatitude < -90 || SimulatorLatitude > 90)
        {
            fixes.Add($"SimulatorLatitude was {SimulatorLatitude}, reset to {defaults.SimulatorLatitude}");
            SimulatorLatitude = defaults.SimulatorLatitude;
        }
        if (SimulatorLongitude < -180 || SimulatorLongitude > 180)
        {
            fixes.Add($"SimulatorLongitude was {SimulatorLongitude}, reset to {defaults.SimulatorLongitude}");
            SimulatorLongitude = defaults.SimulatorLongitude;
        }

        // Window dimensions
        if (WindowWidth < 100 || WindowWidth > 10000)
        {
            fixes.Add($"WindowWidth was {WindowWidth}, reset to {defaults.WindowWidth}");
            WindowWidth = defaults.WindowWidth;
        }
        if (WindowHeight < 100 || WindowHeight > 10000)
        {
            fixes.Add($"WindowHeight was {WindowHeight}, reset to {defaults.WindowHeight}");
            WindowHeight = defaults.WindowHeight;
        }

        // Camera zoom
        if (CameraZoom < 0.1 || CameraZoom > 20.0)
        {
            fixes.Add($"CameraZoom was {CameraZoom}, reset to {defaults.CameraZoom}");
            CameraZoom = defaults.CameraZoom;
        }

        return fixes;
    }
}
