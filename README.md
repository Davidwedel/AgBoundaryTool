# AgBoundaryTool

A cross-platform boundary manipulation tool for agricultural fields, compatible with AgValoniaGPS and AgOpenGPS field formats.

## Features

- **GPS Connectivity**: Connect to GPS receivers via USB serial port
- **GPS Simulator**: Test without hardware using built-in vehicle simulator
- **Real-time GPS Data**: View live GPS coordinates, speed, heading, and fix quality
- **Boundary Recording**: Record field boundaries by driving around the perimeter
- **Field Management**: Create and manage fields with proper origin points
- **Compatible Format**: Saves boundaries in AgOpenGPS/AgValoniaGPS format (Boundary.txt, Field.txt)
- **Cross-Platform**: Runs on Linux, Windows, and macOS

## System Requirements

- .NET 8.0 or later
- GPS receiver with USB serial connection (NMEA 0183 protocol) OR use built-in simulator
- Supported platforms: Linux, Windows, macOS

## Installation

### Build from Source

```bash
cd AgBoundaryTool
dotnet restore
dotnet build
dotnet run
```

### Run

```bash
dotnet run
```

## Usage

### 1. Connect GPS Receiver (Real or Simulated)

#### Option A: Real GPS Receiver

1. Connect your GPS receiver via USB
2. Select the serial port from the dropdown (e.g., `/dev/ttyUSB0` on Linux, `COM3` on Windows)
3. Click "Connect"
4. Wait for GPS fix (you'll see latitude/longitude updating and satellite count > 0)

#### Option B: GPS Simulator (No Hardware Required)

1. Enter a starting position (Lat/Lon) or use the default (Kansas, USA)
2. Click "Start Simulator"
3. Use simulator controls:
   - **Steer Angle Slider**: Adjust steering (-40° to +40°)
   - **Forward Button**: Accelerate forward (up to 25 km/h)
   - **Backward Button**: Accelerate backward (up to 10 km/h)
   - **Stop Button**: Coast to a stop
   - **Reset Position**: Return to starting position
4. The simulator provides realistic vehicle physics with smooth steering and acceleration

### 2. Create a New Field

1. Enter a field name in the "Field Name" textbox
2. Drive/walk to the field location where you want to set the origin point
3. Wait for a good GPS fix (preferably RTK fixed, quality = 4)
4. Click "Create New Field"
   - This sets the current GPS position as the field origin
   - All boundary coordinates will be relative to this point
   - A new field directory will be created with a Field.txt file

### 3. Record Boundary

1. Drive/walk to the field boundary starting point
2. Click "Start Recording"
3. Drive around the field perimeter at steady speed
   - Points are automatically recorded every 2 meters (configurable)
   - You'll see the point count increasing
4. Return to the starting point to close the boundary
5. Click "Stop Recording"
   - The boundary will be saved as Boundary.txt in the field directory
   - Area will be calculated and displayed

### 4. Multiple Boundaries

- **First recording**: Creates the outer boundary
- **Subsequent recordings**: Creates inner boundaries (holes/exclusions)
  - Use this for ponds, obstacles, or other no-go areas

### 5. Field Files

Fields are saved in the following format (compatible with AgOpenGPS/AgValoniaGPS):

```
~/Documents/AgBoundaryTool/Fields/MyField/
  ├── Field.txt         # Field metadata (origin, convergence, offsets)
  ├── Boundary.txt      # Boundary polygon(s)
  └── Headland.Txt      # Headland polygon (if created)
```

## File Format Compatibility

AgBoundaryTool uses the exact same file format as AgOpenGPS and AgValoniaGPS:

- **Field.txt**: Contains field origin (WGS84), convergence angle, and offsets
- **Boundary.txt**: Contains boundary polygons (outer + inner) in local coordinates
- **Coordinates**: Local coordinates are in meters (Easting/Northing) relative to field origin

You can use boundaries created with AgBoundaryTool directly in AgOpenGPS or AgValoniaGPS, and vice versa.

## GPS Receiver Compatibility

AgBoundaryTool supports any GPS receiver that outputs NMEA 0183 sentences via USB serial:

### Supported NMEA Sentences

- **$GPGGA / $GNGGA**: Position and fix data (primary)
- **$GPRMC / $GNRMC**: Recommended minimum data (position, speed, heading)
- **$GPVTG / $GNVTG**: Track and ground speed

### Tested GPS Receivers

- u-blox NEO-M8N
- u-blox ZED-F9P (RTK capable)
- Generic USB GPS receivers

### Baud Rates

- Default: 9600 baud
- Most GPS receivers use 9600, 38400, or 115200 baud
- Modify in `GpsService.ConnectAsync()` if needed

## Architecture

### Models

- **Position**: GPS position with lat/lon/altitude and local coordinates
- **Field**: Field metadata with origin point
- **Boundary**: Outer and inner boundary polygons
- **BoundaryPolygon**: Collection of boundary points
- **BoundaryPoint**: Single point (easting, northing, heading)

### Services

- **GpsService**: Serial port connection and NMEA parsing
- **CoordinateConversionService**: GPS (lat/lon) to local (easting/northing) conversion
- **BoundaryRecordingService**: Records GPS positions into boundaries
- **FieldPlaneFileService**: Reads/writes Field.txt files
- **BoundaryFileService**: Reads/writes Boundary.txt files

### MVVM Pattern

- **ViewModels**: Business logic and state management
- **Views**: Avalonia UI (XAML)
- **CommunityToolkit.Mvvm**: MVVM framework with observable properties and relay commands

## Coordinate System

### WGS84 (Global Coordinates)

- Latitude/Longitude in decimal degrees
- Used for GPS positions and field origin

### Local Coordinates (Field Coordinates)

- Easting/Northing in meters relative to field origin
- Uses Equirectangular projection (flat-earth approximation)
- Accurate for fields up to ~50km from origin
- All boundary points stored in local coordinates

### Conversion Formula

```csharp
easting = (longitude - originLon) * DEG_TO_RAD * EARTH_RADIUS * cos(originLat)
northing = (latitude - originLat) * DEG_TO_RAD * EARTH_RADIUS
```

## Troubleshooting

### GPS Not Connecting

- Check USB cable connection
- Verify serial port name (use `ls /dev/ttyUSB*` on Linux, Device Manager on Windows)
- Try different baud rates (9600, 38400, 115200)
- Check GPS receiver power/antenna

### No GPS Fix

- Move to open area with clear sky view
- Wait for satellite acquisition (can take 1-2 minutes from cold start)
- Check antenna connection
- For RTK: Ensure base station connection (if using RTK GPS)

### Boundary Not Saving

- Check field directory permissions
- Ensure field was created first
- Verify at least 3 points were recorded

### Permission Denied on Serial Port (Linux)

```bash
sudo usermod -a -G dialout $USER
# Log out and back in
```

## Development

### Project Structure

```
AgBoundaryTool/
├── Models/              # Data models
├── Services/            # Business logic services
├── ViewModels/          # MVVM view models
├── Views/               # Avalonia UI views
├── Assets/              # Images and resources
└── AgBoundaryTool.csproj
```

### Dependencies

- Avalonia 11.2.1 (Cross-platform UI framework)
- CommunityToolkit.Mvvm 8.2.1 (MVVM helpers)
- System.IO.Ports 9.0.0 (Serial port communication)

### Building

```bash
dotnet restore
dotnet build
```

### Running

```bash
dotnet run
```

### Publishing

```bash
# Linux
dotnet publish -c Release -r linux-x64 --self-contained

# Windows
dotnet publish -c Release -r win-x64 --self-contained

# macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

## License

This project is designed to be compatible with AgValoniaGPS and AgOpenGPS.
Follows the same GPL v3 license as AgValoniaGPS.

## Contributing

Contributions are welcome! Areas for improvement:

- [ ] Boundary visualization on canvas
- [ ] Edit existing boundaries (add/remove/move points)
- [ ] Export to KML/GeoJSON
- [ ] Import from KML/GeoJSON
- [ ] Support for headland recording
- [ ] Boundary smoothing/simplification
- [ ] Field browser dialog
- [ ] Multiple field support in UI
- [ ] Undo/redo functionality

## Support

For issues related to:
- **AgBoundaryTool**: Create an issue on this repository
- **AgValoniaGPS**: https://github.com/AgValoniaGPS
- **AgOpenGPS**: https://discourse.agopengps.com/
