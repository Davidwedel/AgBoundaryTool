// AgBoundaryTool
// GPS simulation service for testing without real GPS hardware
// Based on AgValoniaGPS GpsSimulationService

using System;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Services;

/// <summary>
/// GPS simulator for testing without real GPS hardware
/// Simulates vehicle movement based on steering input and acceleration
/// </summary>
public class GpsSimulatorService
{
    private Position _currentPosition;
    private double _headingRadians;
    private double _steerAngle;
    private double _steerAngleAverage;
    private double _stepDistance;
    private bool _isAcceleratingForward;
    private bool _isAcceleratingBackward;
    private Position _initialPosition;

    // Constants
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;
    private const double TwoPI = Math.PI * 2.0;
    private const double EarthRadius = 6371000.0; // meters

    // Simulation constants
    private const double MaxForwardStep = 0.625;   // 25 kph max speed
    private const double MaxReverseStep = -0.25;   // -10 kph max reverse
    private const double AccelStep = 0.03;         // Acceleration rate
    private const double DecelStep = 0.02;         // Deceleration rate

    public event EventHandler<Position>? PositionUpdated;

    public Position CurrentPosition => _currentPosition;
    public double HeadingDegrees => _headingRadians * RadiansToDegrees;
    public double SpeedKmh => Math.Abs(Math.Round(4.0 * _stepDistance * 10.0, 2));

    public double StepDistance
    {
        get => _stepDistance;
        set => _stepDistance = value;
    }

    public double SteerAngle
    {
        get => _steerAngle;
        set => _steerAngle = value;
    }

    public bool IsAcceleratingForward
    {
        get => _isAcceleratingForward;
        set => _isAcceleratingForward = value;
    }

    public bool IsAcceleratingBackward
    {
        get => _isAcceleratingBackward;
        set => _isAcceleratingBackward = value;
    }

    public GpsSimulatorService()
    {
        _currentPosition = new Position();
        _initialPosition = new Position();
        _headingRadians = 0;
        _steerAngle = 0;
        _steerAngleAverage = 0;
        _stepDistance = 0;
    }

    /// <summary>
    /// Initialize simulator with starting position
    /// </summary>
    public void Initialize(Position startPosition)
    {
        _currentPosition = new Position
        {
            Latitude = startPosition.Latitude,
            Longitude = startPosition.Longitude,
            Altitude = startPosition.Altitude,
            FixQuality = 4, // RTK Fixed
            SatelliteCount = 12
        };
        _initialPosition = _currentPosition;
        _headingRadians = 0;
        _steerAngle = 0;
        _steerAngleAverage = 0;
        _stepDistance = 0;
        _isAcceleratingForward = false;
        _isAcceleratingBackward = false;
    }

    /// <summary>
    /// Process one simulation tick
    /// </summary>
    public void Tick()
    {
        // Smooth the steer angle
        SmoothSteerAngle();

        // Calculate heading change based on steering angle
        // Using simplified bicycle model: heading_change = step_distance * tan(steer_angle) / 2
        double headingChange = _stepDistance * Math.Tan(_steerAngleAverage * DegreesToRadians) / 2.0;
        _headingRadians += headingChange;

        // Normalize heading to [0, 2π)
        while (_headingRadians >= TwoPI)
            _headingRadians -= TwoPI;
        while (_headingRadians < 0)
            _headingRadians += TwoPI;

        // Calculate next position using WGS84 bearing/distance
        if (Math.Abs(_stepDistance) > 0.001)
        {
            var newPos = CalculateNewPosition(_currentPosition.Latitude, _currentPosition.Longitude,
                                               _headingRadians, _stepDistance);

            _currentPosition.Latitude = newPos.latitude;
            _currentPosition.Longitude = newPos.longitude;
            _currentPosition.Heading = _headingRadians * RadiansToDegrees;
            _currentPosition.Speed = _stepDistance * 10.0; // Convert to m/s approx
            _currentPosition.Altitude = SimulateAltitude(_currentPosition.Latitude, _currentPosition.Longitude);
        }

        // Update local coordinates if we have origin
        // (Will be done by the caller who knows the field origin)

        // Handle acceleration
        UpdateAcceleration();

        // Raise event
        PositionUpdated?.Invoke(this, _currentPosition);
    }

    /// <summary>
    /// Reset simulation to initial state
    /// </summary>
    public void Reset()
    {
        _currentPosition = new Position
        {
            Latitude = _initialPosition.Latitude,
            Longitude = _initialPosition.Longitude,
            Altitude = _initialPosition.Altitude,
            FixQuality = 4,
            SatelliteCount = 12
        };
        _headingRadians = 0;
        _steerAngle = 0;
        _steerAngleAverage = 0;
        _stepDistance = 0;
        _isAcceleratingForward = false;
        _isAcceleratingBackward = false;
    }

    /// <summary>
    /// Set heading directly (in degrees)
    /// </summary>
    public void SetHeading(double headingDegrees)
    {
        _headingRadians = headingDegrees * DegreesToRadians;

        // Normalize heading to [0, 2π)
        while (_headingRadians >= TwoPI)
            _headingRadians -= TwoPI;
        while (_headingRadians < 0)
            _headingRadians += TwoPI;
    }

    /// <summary>
    /// Smooth steer angle using original CSim algorithm from AgValoniaGPS
    /// </summary>
    private void SmoothSteerAngle()
    {
        double diff = Math.Abs(_steerAngle - _steerAngleAverage);

        if (diff > 11)
        {
            _steerAngleAverage += (_steerAngle > _steerAngleAverage) ? 6.0 : -6.0;
        }
        else if (diff > 5)
        {
            _steerAngleAverage += (_steerAngle > _steerAngleAverage) ? 2.0 : -2.0;
        }
        else if (diff > 1)
        {
            _steerAngleAverage += (_steerAngle > _steerAngleAverage) ? 0.5 : -0.5;
        }
        else
        {
            _steerAngleAverage = _steerAngle;
        }
    }

    /// <summary>
    /// Update step distance based on acceleration state
    /// </summary>
    private void UpdateAcceleration()
    {
        if (_isAcceleratingForward)
        {
            _stepDistance += AccelStep;
            if (_stepDistance > MaxForwardStep)
            {
                _stepDistance = MaxForwardStep;
            }
        }
        else if (_isAcceleratingBackward)
        {
            _stepDistance -= AccelStep;
            if (_stepDistance < MaxReverseStep)
            {
                _stepDistance = MaxReverseStep;
            }
        }
        else
        {
            // Natural deceleration when not accelerating
            if (_stepDistance > 0)
            {
                _stepDistance -= DecelStep * 0.5;
                if (_stepDistance < 0) _stepDistance = 0;
            }
            else if (_stepDistance < 0)
            {
                _stepDistance += DecelStep * 0.5;
                if (_stepDistance > 0) _stepDistance = 0;
            }
        }
    }

    /// <summary>
    /// Calculate new position from bearing and distance (Haversine formula)
    /// </summary>
    private (double latitude, double longitude) CalculateNewPosition(double lat, double lon, double bearingRadians, double distanceMeters)
    {
        double latRad = lat * DegreesToRadians;
        double lonRad = lon * DegreesToRadians;

        double angularDistance = distanceMeters / EarthRadius;

        double newLatRad = Math.Asin(Math.Sin(latRad) * Math.Cos(angularDistance) +
                                     Math.Cos(latRad) * Math.Sin(angularDistance) * Math.Cos(bearingRadians));

        double newLonRad = lonRad + Math.Atan2(Math.Sin(bearingRadians) * Math.Sin(angularDistance) * Math.Cos(latRad),
                                               Math.Cos(angularDistance) - Math.Sin(latRad) * Math.Sin(newLatRad));

        return (newLatRad * RadiansToDegrees, newLonRad * RadiansToDegrees);
    }

    /// <summary>
    /// Simulate altitude based on latitude/longitude
    /// </summary>
    private double SimulateAltitude(double latitude, double longitude)
    {
        double temp = Math.Abs(latitude * 100);
        temp -= (int)temp;
        temp *= 100;
        double altitude = temp + 200;

        temp = Math.Abs(longitude * 100);
        temp -= (int)temp;
        temp *= 100;
        altitude += temp;

        return altitude;
    }
}
