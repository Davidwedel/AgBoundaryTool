// AgBoundaryTool
// GPS connection and NMEA parsing service for USB serial ports

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Services;

/// <summary>
/// Service for connecting to GPS receivers via USB serial port and parsing NMEA sentences
/// </summary>
public class GpsService : IDisposable
{
    private SerialPort? _serialPort;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _readTask;
    private string _buffer = string.Empty;
    private GpsSimulatorService? _simulator;
    private System.Timers.Timer? _simulatorTimer;
    private bool _isSimulatorMode = false;
    private NtripService? _ntripService;

    /// <summary>
    /// Event fired when a new GPS position is received
    /// </summary>
    public event EventHandler<Position>? PositionReceived;

    /// <summary>
    /// Event fired when GPS connection status changes
    /// </summary>
    public event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Current GPS position
    /// </summary>
    public Position? CurrentPosition { get; private set; }

    /// <summary>
    /// Is GPS connected (real or simulated)
    /// </summary>
    public bool IsConnected => _isSimulatorMode || (_serialPort?.IsOpen ?? false);

    /// <summary>
    /// Is in simulator mode
    /// </summary>
    public bool IsSimulatorMode => _isSimulatorMode;

    /// <summary>
    /// Get the GPS simulator (only available in simulator mode)
    /// </summary>
    public GpsSimulatorService? Simulator => _simulator;

    /// <summary>
    /// Get the NTRIP service
    /// </summary>
    public NtripService? NtripService => _ntripService;

    /// <summary>
    /// Is NTRIP connected
    /// </summary>
    public bool IsNtripConnected => _ntripService?.IsConnected ?? false;

    /// <summary>
    /// Get list of available serial ports
    /// </summary>
    public static string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    /// <summary>
    /// Connect to NTRIP caster for RTK corrections
    /// </summary>
    public async Task<bool> ConnectNtripAsync(string host, int port, string mountPoint,
        string username, string password, bool useSsl = false)
    {
        try
        {
            if (_ntripService == null)
            {
                _ntripService = new NtripService();
                _ntripService.DataReceived += OnNtripDataReceived;
            }

            // Use current position if available for initial GGA
            double lat = CurrentPosition?.Latitude ?? 0;
            double lon = CurrentPosition?.Longitude ?? 0;

            bool connected = await _ntripService.ConnectAsync(host, port, mountPoint, username, password, lat, lon, useSsl);

            if (connected)
            {
                Console.WriteLine("[GPS] NTRIP connected successfully");
            }

            return connected;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GPS] NTRIP connection error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disconnect from NTRIP caster
    /// </summary>
    public async Task DisconnectNtripAsync()
    {
        if (_ntripService != null)
        {
            await _ntripService.DisconnectAsync();
        }
    }

    /// <summary>
    /// Handle RTCM data received from NTRIP and forward to GPS receiver
    /// </summary>
    private void OnNtripDataReceived(object? sender, byte[] data)
    {
        try
        {
            // Forward RTCM data to GPS receiver over USB
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Write(data, 0, data.Length);
                Console.WriteLine($"[GPS] Forwarded {data.Length} bytes of RTCM data to GPS receiver");
            }
            else
            {
                Console.WriteLine($"[GPS] Received {data.Length} bytes of RTCM data but no GPS port open");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GPS] Error forwarding RTCM data: {ex.Message}");
        }
    }

    /// <summary>
    /// Connect to GPS receiver on specified port
    /// </summary>
    public async Task<bool> ConnectAsync(string portName, int baudRate = 9600)
    {
        try
        {
            Console.WriteLine($"[GPS] Attempting to connect to {portName} at {baudRate} baud...");

            if (IsConnected)
            {
                Console.WriteLine($"[GPS] Already connected, disconnecting first...");
                await DisconnectAsync();
            }

            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            _serialPort.Open();

            _cancellationTokenSource = new CancellationTokenSource();
            _readTask = Task.Run(() => ReadLoop(_cancellationTokenSource.Token));

            Console.WriteLine($"[GPS] Successfully connected to {portName}");
            ConnectionStatusChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GPS] Connection ERROR: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, false);
            return false;
        }
    }

    /// <summary>
    /// Disconnect from GPS receiver
    /// </summary>
    public async Task DisconnectAsync()
    {
        Console.WriteLine("[GPS] Disconnecting...");

        _cancellationTokenSource?.Cancel();

        if (_readTask != null)
        {
            await _readTask;
            _readTask = null;
        }

        if (_serialPort?.IsOpen == true)
        {
            _serialPort.Close();
        }

        _serialPort?.Dispose();
        _serialPort = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        Console.WriteLine("[GPS] Disconnected");
        ConnectionStatusChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Main read loop for GPS data
    /// </summary>
    private async Task ReadLoop(CancellationToken cancellationToken)
    {
        if (_serialPort == null) return;

        while (!cancellationToken.IsCancellationRequested && _serialPort.IsOpen)
        {
            try
            {
                if (_serialPort.BytesToRead > 0)
                {
                    char c = (char)_serialPort.ReadChar();

                    if (c == '\n')
                    {
                        ProcessNmeaSentence(_buffer);
                        _buffer = string.Empty;
                    }
                    else if (c != '\r')
                    {
                        _buffer += c;
                    }
                }
                else
                {
                    await Task.Delay(10, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"GPS read error: {ex.Message}");
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Process a single NMEA sentence
    /// </summary>
    private void ProcessNmeaSentence(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence) || !sentence.StartsWith("$"))
            return;

        try
        {
            // Verify checksum if present
            if (sentence.Contains('*'))
            {
                if (!VerifyChecksum(sentence))
                    return;
            }

            var parts = sentence.Split(',');
            if (parts.Length < 2) return;

            // Parse different NMEA sentence types
            if (sentence.StartsWith("$GPGGA") || sentence.StartsWith("$GNGGA"))
            {
                ParseGGA(parts);
            }
            else if (sentence.StartsWith("$GPRMC") || sentence.StartsWith("$GNRMC"))
            {
                ParseRMC(parts);
            }
            else if (sentence.StartsWith("$GPVTG") || sentence.StartsWith("$GNVTG"))
            {
                ParseVTG(parts);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"NMEA parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse GGA sentence (position and fix data)
    /// $GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47
    /// </summary>
    private void ParseGGA(string[] parts)
    {
        if (parts.Length < 15) return;

        try
        {
            if (string.IsNullOrEmpty(parts[2]) || string.IsNullOrEmpty(parts[4]))
                return;

            double latitude = ParseLatLon(parts[2], parts[3]);
            double longitude = ParseLatLon(parts[4], parts[5]);
            int fixQuality = string.IsNullOrEmpty(parts[6]) ? 0 : int.Parse(parts[6]);
            int satellites = string.IsNullOrEmpty(parts[7]) ? 0 : int.Parse(parts[7]);
            double altitude = string.IsNullOrEmpty(parts[9]) ? 0 : double.Parse(parts[9], CultureInfo.InvariantCulture);

            var position = CurrentPosition ?? new Position();
            position.Latitude = latitude;
            position.Longitude = longitude;
            position.Altitude = altitude;
            position.FixQuality = fixQuality;
            position.SatelliteCount = satellites;

            CurrentPosition = position;

            // Log first fix and quality changes
            if (fixQuality > 0 && (CurrentPosition == null || CurrentPosition.FixQuality != fixQuality))
            {
                Console.WriteLine($"[GPS] Fix quality: {fixQuality}, Satellites: {satellites}, Position: {latitude:F6}, {longitude:F6}");
            }

            OnPositionReceivedInternal(position);
            PositionReceived?.Invoke(this, position);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GPS] Parse GGA error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse RMC sentence (recommended minimum data)
    /// $GPRMC,123519,A,4807.038,N,01131.000,E,022.4,084.4,230394,003.1,W*6A
    /// </summary>
    private void ParseRMC(string[] parts)
    {
        if (parts.Length < 10) return;

        try
        {
            // parts[2] = status (A = active, V = void)
            // parts[3] = latitude
            // parts[4] = N/S
            // parts[5] = longitude
            // parts[6] = E/W
            // parts[7] = speed in knots
            // parts[8] = track angle (heading)

            if (parts[2] != "A") return;  // Only process active fixes
            if (string.IsNullOrEmpty(parts[3]) || string.IsNullOrEmpty(parts[5]))
                return;

            double latitude = ParseLatLon(parts[3], parts[4]);
            double longitude = ParseLatLon(parts[5], parts[6]);
            double speedKnots = string.IsNullOrEmpty(parts[7]) ? 0 : double.Parse(parts[7], CultureInfo.InvariantCulture);
            double heading = string.IsNullOrEmpty(parts[8]) ? 0 : double.Parse(parts[8], CultureInfo.InvariantCulture);

            var position = CurrentPosition ?? new Position();
            position.Latitude = latitude;
            position.Longitude = longitude;
            position.Speed = speedKnots * 0.514444; // knots to m/s
            position.Heading = heading;

            CurrentPosition = position;
            OnPositionReceivedInternal(position);
            PositionReceived?.Invoke(this, position);
        }
        catch { }
    }

    /// <summary>
    /// Parse VTG sentence (track made good and ground speed)
    /// $GPVTG,054.7,T,034.4,M,005.5,N,010.2,K*48
    /// </summary>
    private void ParseVTG(string[] parts)
    {
        if (parts.Length < 9) return;

        try
        {
            // parts[1] = track degrees true
            // parts[5] = speed knots
            // parts[7] = speed km/h

            double heading = string.IsNullOrEmpty(parts[1]) ? 0 : double.Parse(parts[1], CultureInfo.InvariantCulture);
            double speedKnots = string.IsNullOrEmpty(parts[5]) ? 0 : double.Parse(parts[5], CultureInfo.InvariantCulture);

            if (CurrentPosition != null)
            {
                CurrentPosition.Heading = heading;
                CurrentPosition.Speed = speedKnots * 0.514444; // knots to m/s
            }
        }
        catch { }
    }

    /// <summary>
    /// Parse latitude or longitude from NMEA format (DDMM.MMMM or DDDMM.MMMM)
    /// </summary>
    private double ParseLatLon(string value, string direction)
    {
        if (string.IsNullOrEmpty(value)) return 0;

        // Find decimal point
        int dotIndex = value.IndexOf('.');
        if (dotIndex < 2) return 0;

        // Extract degrees and minutes
        int degreeDigits = dotIndex - 2;
        string degreesStr = value.Substring(0, degreeDigits);
        string minutesStr = value.Substring(degreeDigits);

        double degrees = double.Parse(degreesStr, CultureInfo.InvariantCulture);
        double minutes = double.Parse(minutesStr, CultureInfo.InvariantCulture);

        double result = degrees + (minutes / 60.0);

        // Apply direction
        if (direction == "S" || direction == "W")
        {
            result = -result;
        }

        return result;
    }

    /// <summary>
    /// Verify NMEA sentence checksum
    /// </summary>
    private bool VerifyChecksum(string sentence)
    {
        try
        {
            int starIndex = sentence.IndexOf('*');
            if (starIndex < 0 || starIndex + 2 >= sentence.Length)
                return false;

            string checksumStr = sentence.Substring(starIndex + 1, 2);
            byte expectedChecksum = Convert.ToByte(checksumStr, 16);

            // Calculate checksum (XOR of all characters between $ and *)
            byte calculatedChecksum = 0;
            for (int i = 1; i < starIndex; i++)
            {
                calculatedChecksum ^= (byte)sentence[i];
            }

            return calculatedChecksum == expectedChecksum;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Start GPS simulator at given position
    /// </summary>
    public void StartSimulator(double latitude, double longitude)
    {
        Console.WriteLine($"[SIMULATOR] Starting at position: {latitude:F6}, {longitude:F6}");

        if (IsConnected && !_isSimulatorMode)
        {
            Console.WriteLine("[SIMULATOR] Disconnecting real GPS first...");
            DisconnectAsync().Wait();
        }

        _simulator = new GpsSimulatorService();
        _simulator.Initialize(new Position
        {
            Latitude = latitude,
            Longitude = longitude,
            Altitude = 200,
            FixQuality = 4,
            SatelliteCount = 12
        });

        _simulator.PositionUpdated += OnSimulatorPositionUpdated;

        // Create timer to tick simulator at 10Hz
        _simulatorTimer = new System.Timers.Timer(100); // 100ms = 10Hz
        _simulatorTimer.Elapsed += (s, e) => _simulator?.Tick();
        _simulatorTimer.Start();

        _isSimulatorMode = true;
        Console.WriteLine("[SIMULATOR] Started successfully, updating at 10Hz");
        ConnectionStatusChanged?.Invoke(this, true);
    }

    /// <summary>
    /// Stop GPS simulator
    /// </summary>
    public void StopSimulator()
    {
        if (!_isSimulatorMode) return;

        Console.WriteLine("[SIMULATOR] Stopping...");

        _simulatorTimer?.Stop();
        _simulatorTimer?.Dispose();
        _simulatorTimer = null;

        if (_simulator != null)
        {
            _simulator.PositionUpdated -= OnSimulatorPositionUpdated;
            _simulator = null;
        }

        _isSimulatorMode = false;
        Console.WriteLine("[SIMULATOR] Stopped");
        ConnectionStatusChanged?.Invoke(this, false);
    }

    private void OnSimulatorPositionUpdated(object? sender, Position position)
    {
        CurrentPosition = position;
        PositionReceived?.Invoke(this, position);
    }

    private void OnPositionReceivedInternal(Position position)
    {
        // Update NTRIP with current position (some casters require periodic GGA updates)
        if (_ntripService?.IsConnected == true)
        {
            _ = _ntripService.UpdatePositionAsync(position.Latitude, position.Longitude);
        }
    }

    public void Dispose()
    {
        StopSimulator();
        DisconnectAsync().Wait();
        DisconnectNtripAsync().Wait();
        _ntripService?.Dispose();
    }
}
