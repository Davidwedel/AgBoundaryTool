# AgBoundaryTool - Claude Memory

This file contains important architectural decisions and patterns to maintain consistency.

## Architecture Overview

- **Framework**: Avalonia UI 11.2.1 with .NET 8.0 LTS
- **Pattern**: MVVM with CommunityToolkit.Mvvm
- **GPS**: NMEA 0183 via System.IO.Ports
- **Coordinate System**: WGS84 → Local (Equirectangular projection)

## File Format Compatibility

**CRITICAL**: AgBoundaryTool MUST maintain 100% compatibility with AgOpenGPS and AgValoniaGPS file formats:
- `Field.txt` - Field metadata and origin
- `Boundary.txt` - Boundary polygon points

All file I/O uses:
- `FieldPlaneFileService` for Field.txt
- `BoundaryFileService` for Boundary.txt

## Point Recording Pattern - REUSE THIS!

**IMPORTANT**: All GPS point recording features MUST use the PointRecordingDialog pattern.

### PointRecordingDialog (Views/Dialogs/PointRecordingDialog.axaml)

A **reusable dialog** for recording GPS points. Use this for ANY feature that needs to record GPS positions.

**Features:**
- Add Point button (manual single point) - PointAdd.png
- Delete Last Point button - PointDelete.png
- Record Continuously toggle (start/stop) - boundaryPlay.png / boundaryPause.png
- Done button (finish and move to next step) - OK64.png
- Adapts title/description based on mode

**How to use:**

1. Set recording mode in ViewModel before opening:
```csharp
_pointRecordingMode = "boundary"; // or "notch", or any new mode
PointRecordingTitle = "Your Title";
PointRecordingDescription = "Your description";
```

2. Open the dialog:
```csharp
var dialog = new Views.Dialogs.PointRecordingDialog
{
    DataContext = this
};
dialog.Show(mainWindow);
```

3. Handle the recording in `AddSinglePoint()`, `DeleteLastPoint()`, and `ToggleContinuousRecording()` based on mode

4. Handle completion in `FinishPointRecording()` - opens next dialog or saves

**DO NOT create separate recording dialogs!** Extend PointRecordingDialog for new recording modes.

## GPS Point Recording Rules

- **Minimum distance**: 1.0 meter between points
- **Continuous recording**: Auto-records while enabled (10Hz GPS, 1m filter)
- **Manual recording**: Bypasses distance check (sets MinimumPointDistance to 0.01m temporarily)
- **Coordinates**: Always stored as local Easting/Northing relative to field origin

## Dialog Management

All popup dialogs MUST:
- Use `dialog.Show(mainWindow)` to stay on top and auto-close with main window
- Have proper owner relationship
- Call ViewModel commands, then close themselves

## Boundary Notch Feature

The notch feature uses TWO dialogs in sequence:
1. **PointRecordingDialog** - Record the notch path
2. **BoundaryNotchDialog** - Apply or cancel the notch

**Validation rules:**
- Minimum 2 crossings (even number required for multiple notches)
- Start and end points must be OUTSIDE boundary
- Uses ray-casting algorithm for inside/outside detection
- Supports multiple notches in one path (pairs of crossings)

**Algorithm:**
- Groups crossings into pairs (0-1, 2-3, etc.)
- Sorts pairs by boundary position
- Inserts notch segments between intersection points
- Removes original boundary segments

## Inner Boundary Modification Feature

Inner boundaries (holes/exclusions like ponds) can be modified with TWO operations:

**Operations:**
- **Notch**: Cut into inner boundary (makes hole smaller - adds farmable area)
- **Bulge**: Expand inner boundary outward (makes hole bigger - reduces farmable area)

The feature uses THREE dialogs in sequence:
1. **InnerBoundaryModifyDialog** - Select which inner boundary (click on visualization OR dropdown) and operation (notch/bulge)
2. **PointRecordingDialog** - Record the modification path
3. **InnerBoundaryApplyDialog** - Apply or cancel the modification

**Inner Boundary Selection:**
- Click INNER button to open modification dialog
- Visualization enters selection mode - inner boundaries are clickable
- Click on any inner boundary to select it (highlights in **orange**)
- Can also use dropdown to select
- Selected boundary shows in orange vs normal red
- Selection mode auto-disables when dialog closes

**Validation rules:**
- Minimum 2 crossings with the target inner boundary (even number required)
- No start/end position requirement (simpler than outer boundary)
- Supports multiple modifications in one path

**Algorithm:**
- Finds crossings between modification path and target inner boundary
- Groups crossings into pairs, sorts by boundary position
- For notch (hole smaller): Uses opposite reversal logic from outer boundary
- For bulge (hole bigger): Uses same reversal logic as outer boundary notch
- Updates only the target inner boundary

## Visualization

`BoundaryVisualizationControl` - Custom Avalonia control:
- Green lines/points = Outer boundary
- Red lines/points = Inner boundaries (holes/exclusions)
- Orange lines/points = Selected inner boundary (in selection mode)
- Purple/Magenta lines/circles = Notch/modification points
- Yellow circles = Selected points
- Red crosshair + yellow heading line = Vehicle
- Grid background (50m spacing)
- Mouse wheel zoom (centered on cursor)
- Click-and-drag panning (disables auto-center)
- Auto-centers on vehicle unless manually panned
- Right-click = Snap simulator vehicle to position

**Point Selection:**
- Click point = Select single point
- Ctrl+Click = Toggle point selection (multi-select)
- Click two points = Select range
- Delete key = Delete selected points
- Works for both outer and inner boundary points
- Uses flat indexing: outer points (0-N), then inner points sequentially

## Settings Persistence

Uses `SettingsService` with JSON (Documents/AgBoundaryTool/appsettings.json):
- Auto-saves simulator state
- Auto-loads last field on startup
- Saves GPS port selection
- Saves window positions (future)

## Simulator

`GpsSimulatorService`:
- Toggle-based acceleration (not auto-release)
- Stop button sets speed to 0 instantly (no coasting)
- Bicycle model vehicle physics
- 10Hz update rate

## Button Images

Standard icons in `/btnImages`:
- `AgIO.png` - GPS connection
- `FileExisting.png` - Field management
- `Boundary.png` - Boundary recording
- `PointAdd.png` - Add single point
- `PointDelete.png` - Delete last point
- `boundaryPlay.png` - Start continuous recording
- `boundaryPause.png` - Stop continuous recording
- `OK64.png` - Confirm/Done buttons

## Common Patterns

### Opening Dialogs
```csharp
var mainWindow = GetMainWindow();
if (mainWindow == null) return;

var dialog = new Views.Dialogs.SomeDialog
{
    DataContext = this
};
dialog.Show(mainWindow);
```

### Recording GPS Points
```csharp
if (_gpsService.CurrentPosition == null) return;

var (e, n) = CoordinateConversionService.ToLocal(
    _gpsService.CurrentPosition,
    _currentField.Origin
);
Points.Add(new BoundaryPoint(e, n, 0));
_visualizationControl?.SetPoints(Points);
```

### Position Copying (IMPORTANT!)
Always create copies to avoid reference bugs:
```csharp
// WRONG:
_currentField.Origin = _gpsService.CurrentPosition;

// CORRECT:
_currentField.Origin = new Position
{
    Latitude = currentPos.Latitude,
    Longitude = currentPos.Longitude,
    Altitude = currentPos.Altitude
};
```

## Code Style

- Use `Console.WriteLine()` for debugging with descriptive prefixes: `[VIEWMODEL]`, `[RECORDING]`, `[VISUALIZATION]`
- Observable properties use `[ObservableProperty]` attribute
- Commands use `[RelayCommand]` attribute
- Async methods end with `Async`
- Keep UI updates on `Dispatcher.UIThread`

## Future Features

When adding new GPS recording features (headland, AB lines, contour, etc.):
1. ✅ **USE PointRecordingDialog** - set mode and descriptions
2. Add mode handling to existing point recording commands
3. Create a separate dialog for apply/confirm if needed
4. Follow the two-dialog pattern: Record → Apply/Cancel
