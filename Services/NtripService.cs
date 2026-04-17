// AgBoundaryTool
// NTRIP client service for receiving RTK correction data

using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AgBoundaryTool.Services;

/// <summary>
/// Service for connecting to NTRIP casters and receiving RTK correction data
/// </summary>
public class NtripService : IDisposable
{
    private TcpClient? _client;
    private Stream? _stream;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private bool _useSsl = false;

    /// <summary>
    /// Event fired when RTCM data is received from NTRIP caster
    /// </summary>
    public event EventHandler<byte[]>? DataReceived;

    /// <summary>
    /// Event fired when NTRIP connection status changes
    /// </summary>
    public event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Is NTRIP connected
    /// </summary>
    public bool IsConnected => _client?.Connected ?? false;

    /// <summary>
    /// Connect to NTRIP caster (supports both HTTP and HTTPS)
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port, string mountPoint,
        string username, string password, double latitude = 0, double longitude = 0, bool useSsl = false)
    {
        try
        {
            _useSsl = useSsl;
            Console.WriteLine($"[NTRIP] Connecting to {host}:{port}/{mountPoint} (SSL: {useSsl})...");

            if (IsConnected)
            {
                Console.WriteLine("[NTRIP] Already connected, disconnecting first...");
                await DisconnectAsync();
            }

            // Connect to NTRIP caster with timeout
            _client = new TcpClient();
            var connectTask = _client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _client?.Close();
                _client?.Dispose();
                _client = null;
                Console.WriteLine("[NTRIP] Connection failed: Timeout connecting to server");
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }

            await connectTask; // Ensure we catch any exceptions

            // Set up stream (with or without SSL)
            Stream networkStream = _client.GetStream();

            if (_useSsl)
            {
                Console.WriteLine($"[NTRIP] Establishing SSL/TLS connection to {host}...");
                var sslStream = new SslStream(networkStream, false);
                await sslStream.AuthenticateAsClientAsync(host);
                _stream = sslStream;
                Console.WriteLine($"[NTRIP] SSL/TLS connection established to {host}:{port}");
            }
            else
            {
                _stream = networkStream;
                Console.WriteLine($"[NTRIP] TCP connection established to {host}:{port}");
            }

            // Build NTRIP request
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            var request = new StringBuilder();
            request.Append($"GET /{mountPoint} HTTP/1.0\r\n");
            request.Append($"User-Agent: NTRIP AgBoundaryTool/1.0\r\n");
            request.Append($"Authorization: Basic {credentials}\r\n");

            // Add GGA position if provided (required by some casters)
            if (latitude != 0 && longitude != 0)
            {
                var gga = GenerateGGA(latitude, longitude);
                request.Append($"Ntrip-GGA: {gga}\r\n");
            }

            request.Append("\r\n"); // Empty line to end HTTP request

            var requestString = request.ToString();
            Console.WriteLine($"[NTRIP] Sending request:");
            Console.WriteLine($"[NTRIP] ---");
            foreach (var line in requestString.Split(new[] { "\r\n" }, StringSplitOptions.None).Take(5))
            {
                Console.WriteLine($"[NTRIP] {line}");
            }
            Console.WriteLine($"[NTRIP] ---");

            // Send request
            var requestBytes = Encoding.ASCII.GetBytes(requestString);
            await _stream.WriteAsync(requestBytes, 0, requestBytes.Length);
            await _stream.FlushAsync();
            Console.WriteLine($"[NTRIP] Sent {requestBytes.Length} bytes");

            // Read response header with timeout
            // Give server a moment to respond
            await Task.Delay(100);

            var buffer = new byte[4096];
            var readTask = _stream.ReadAsync(buffer, 0, buffer.Length);
            var readTimeoutTask = Task.Delay(5000);

            var readCompletedTask = await Task.WhenAny(readTask, readTimeoutTask);

            if (readCompletedTask == readTimeoutTask)
            {
                Console.WriteLine("[NTRIP] Connection failed: Timeout waiting for server response");
                await DisconnectAsync();
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }

            var bytesRead = await readTask;

            if (bytesRead == 0)
            {
                Console.WriteLine("[NTRIP] Connection failed: Server returned 0 bytes (connection closed)");
                await DisconnectAsync();
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }

            var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"[NTRIP] Server response ({bytesRead} bytes): {response.Substring(0, Math.Min(200, response.Length))}");

            // Check if connection was successful
            if (!response.StartsWith("ICY 200 OK") && !response.StartsWith("HTTP/1.1 200 OK") &&
                !response.StartsWith("HTTP/1.0 200 OK"))
            {
                var firstLine = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "No response";
                Console.WriteLine($"[NTRIP] Connection failed: {firstLine}");

                // Log more details if available
                var lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1)
                {
                    for (int i = 1; i < Math.Min(5, lines.Length); i++)
                    {
                        Console.WriteLine($"[NTRIP]   {lines[i]}");
                    }
                }

                await DisconnectAsync();
                ConnectionStatusChanged?.Invoke(this, false);
                return false;
            }

            Console.WriteLine("[NTRIP] Connected successfully");

            // Start receiving data
            _cancellationTokenSource = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));

            ConnectionStatusChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NTRIP] Connection error: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, false);
            return false;
        }
    }

    /// <summary>
    /// Disconnect from NTRIP caster
    /// </summary>
    public async Task DisconnectAsync()
    {
        Console.WriteLine("[NTRIP] Disconnecting...");

        _cancellationTokenSource?.Cancel();

        if (_receiveTask != null)
        {
            await _receiveTask;
            _receiveTask = null;
        }

        _stream?.Close();
        _stream?.Dispose();
        _stream = null;

        _client?.Close();
        _client?.Dispose();
        _client = null;

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        Console.WriteLine("[NTRIP] Disconnected");
        ConnectionStatusChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Update position (sends GGA to caster if needed)
    /// </summary>
    public async Task UpdatePositionAsync(double latitude, double longitude)
    {
        if (!IsConnected || _stream == null)
            return;

        try
        {
            var gga = GenerateGGA(latitude, longitude);
            var ggaBytes = Encoding.ASCII.GetBytes(gga + "\r\n");
            await _stream.WriteAsync(ggaBytes, 0, ggaBytes.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NTRIP] Error sending GGA: {ex.Message}");
        }
    }

    /// <summary>
    /// Main receive loop for RTCM data
    /// </summary>
    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        if (_stream == null) return;

        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (bytesRead > 0)
                {
                    // Create copy of data and fire event
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    DataReceived?.Invoke(this, data);
                }
                else
                {
                    // Connection closed
                    Console.WriteLine("[NTRIP] Connection closed by server");
                    break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"[NTRIP] Receive error: {ex.Message}");
                await Task.Delay(100, cancellationToken);
            }
        }

        ConnectionStatusChanged?.Invoke(this, false);
    }

    /// <summary>
    /// Generate NMEA GGA sentence for position
    /// </summary>
    private string GenerateGGA(double latitude, double longitude)
    {
        // Convert to NMEA format (DDMM.MMMM)
        var latDeg = Math.Abs((int)latitude);
        var latMin = (Math.Abs(latitude) - latDeg) * 60.0;
        var latDir = latitude >= 0 ? "N" : "S";

        var lonDeg = Math.Abs((int)longitude);
        var lonMin = (Math.Abs(longitude) - lonDeg) * 60.0;
        var lonDir = longitude >= 0 ? "E" : "W";

        // Build GGA sentence (simplified - quality=1, satellites=8, HDOP=1.0, altitude=0)
        var time = DateTime.UtcNow.ToString("HHmmss.00");
        var gga = $"$GPGGA,{time},{latDeg:00}{latMin:07.4f},{latDir}," +
                  $"{lonDeg:000}{lonMin:07.4f},{lonDir},1,08,1.0,0.0,M,0.0,M,,";

        // Calculate checksum
        byte checksum = 0;
        for (int i = 1; i < gga.Length; i++)
        {
            checksum ^= (byte)gga[i];
        }

        return $"{gga}*{checksum:X2}";
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }
}
