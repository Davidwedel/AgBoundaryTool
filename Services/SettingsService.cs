// AgBoundaryTool
// Service for managing application settings persistence using JSON

using System;
using System.IO;
using System.Text.Json;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Services;

/// <summary>
/// Service for managing application settings persistence using JSON
/// </summary>
public class SettingsService
{
    private const string SettingsFileName = "appsettings.json";
    private readonly string _settingsDirectory;
    private readonly string _settingsFilePath;

    public AppSettings Settings { get; private set; }

    public event EventHandler<AppSettings>? SettingsLoaded;
    public event EventHandler<AppSettings>? SettingsSaved;

    public SettingsService()
    {
        // Store settings in Documents/AgBoundaryTool (same pattern as AgValoniaGPS)
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Fallback to Personal if MyDocuments is empty
        if (string.IsNullOrEmpty(documentsPath))
        {
            documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        // Last resort fallback
        if (string.IsNullOrEmpty(documentsPath))
        {
            documentsPath = Environment.CurrentDirectory;
        }

        _settingsDirectory = Path.Combine(documentsPath, "AgBoundaryTool");
        _settingsFilePath = Path.Combine(_settingsDirectory, SettingsFileName);

        // Initialize with defaults
        Settings = new AppSettings();
    }

    public bool Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                // First run - use defaults and set up fields directory
                Settings = new AppSettings { IsFirstRun = true };
                InitializeFieldsDirectory();
                Console.WriteLine("[SETTINGS] First run, using defaults");
                return false;
            }

            var json = File.ReadAllText(_settingsFilePath);

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
                PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate
            };

            var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json, options);

            if (loadedSettings != null)
            {
                Settings = loadedSettings;

                // Validate loaded settings and fix out-of-range values
                var fixes = Settings.ValidateAndFix();
                foreach (var fix in fixes)
                {
                    Console.WriteLine($"[SETTINGS] Validation fix: {fix}");
                }

                Settings.IsFirstRun = false;
                Settings.LastRunDate = DateTime.Now;

                // Ensure fields directory exists
                if (string.IsNullOrEmpty(Settings.FieldsDirectory) || !Directory.Exists(Settings.FieldsDirectory))
                {
                    InitializeFieldsDirectory();
                }

                Console.WriteLine($"[SETTINGS] Loaded from {_settingsFilePath}");
                Console.WriteLine($"[SETTINGS] Simulator was running: {Settings.SimulatorWasRunning}");

                SettingsLoaded?.Invoke(this, Settings);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SETTINGS] Load error: {ex.Message}");
            Settings = new AppSettings();
            InitializeFieldsDirectory();
            return false;
        }
    }

    /// <summary>
    /// Initialize fields directory to default location
    /// </summary>
    private void InitializeFieldsDirectory()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Settings.FieldsDirectory = Path.Combine(documentsPath, "AgBoundaryTool", "Fields");

        // Create the directory if it doesn't exist
        if (!Directory.Exists(Settings.FieldsDirectory))
        {
            Directory.CreateDirectory(Settings.FieldsDirectory);
            Console.WriteLine($"[SETTINGS] Created fields directory: {Settings.FieldsDirectory}");
        }
    }

    public bool Save()
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_settingsDirectory))
            {
                Directory.CreateDirectory(_settingsDirectory);
            }

            // Update last run date
            Settings.LastRunDate = DateTime.Now;

            // Serialize with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };

            var json = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(_settingsFilePath, json);

            Console.WriteLine($"[SETTINGS] Saved to {_settingsFilePath}");
            Console.WriteLine($"[SETTINGS] Simulator running: {Settings.SimulatorWasRunning}");

            SettingsSaved?.Invoke(this, Settings);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SETTINGS] Save error: {ex.Message}");
            return false;
        }
    }

    public void ResetToDefaults()
    {
        Settings = new AppSettings
        {
            IsFirstRun = false,
            LastRunDate = DateTime.Now
        };
        InitializeFieldsDirectory();
    }

    public string GetSettingsFilePath()
    {
        return _settingsFilePath;
    }
}
