// AgBoundaryTool
// Boundary history service for undo/redo functionality

using System;
using System.Collections.Generic;
using System.Linq;
using AgBoundaryTool.Models;

namespace AgBoundaryTool.Services;

/// <summary>
/// Represents a snapshot of boundary state with metadata
/// </summary>
public class BoundaryHistoryEntry
{
    public Boundary? BoundarySnapshot { get; set; }
    public string ActionDescription { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Manages undo/redo history for boundary modifications
/// </summary>
public class BoundaryHistoryService
{
    private readonly List<BoundaryHistoryEntry> _history = new();
    private int _currentIndex = -1; // Points to current state in history
    private readonly int _maxHistorySize;

    public BoundaryHistoryService(int maxHistorySize = 50)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Get all history entries for display
    /// </summary>
    public IReadOnlyList<BoundaryHistoryEntry> History => _history.AsReadOnly();

    /// <summary>
    /// Current position in history (0-based index)
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Can undo to previous state
    /// </summary>
    public bool CanUndo => _currentIndex > 0;

    /// <summary>
    /// Can redo to next state
    /// </summary>
    public bool CanRedo => _currentIndex < _history.Count - 1;

    /// <summary>
    /// Number of states available for undo
    /// </summary>
    public int UndoCount => _currentIndex;

    /// <summary>
    /// Number of states available for redo
    /// </summary>
    public int RedoCount => _history.Count - _currentIndex - 1;

    /// <summary>
    /// Save current boundary state to history before making changes
    /// </summary>
    public void SaveState(Boundary? boundary, string actionDescription)
    {
        Console.WriteLine($"[HISTORY] Saving state: {actionDescription}");

        // If we're not at the end of history, remove all entries after current position
        // (user made a new action after undoing, so redo history is discarded)
        if (_currentIndex < _history.Count - 1)
        {
            int removeCount = _history.Count - _currentIndex - 1;
            _history.RemoveRange(_currentIndex + 1, removeCount);
            Console.WriteLine($"[HISTORY] Removed {removeCount} redo entries");
        }

        // Create deep copy of boundary
        var snapshot = DeepCopyBoundary(boundary);

        // Add new entry
        var entry = new BoundaryHistoryEntry
        {
            BoundarySnapshot = snapshot,
            ActionDescription = actionDescription,
            Timestamp = DateTime.Now
        };

        _history.Add(entry);
        _currentIndex = _history.Count - 1;

        // Enforce max history size (remove oldest entries)
        if (_history.Count > _maxHistorySize)
        {
            int removeCount = _history.Count - _maxHistorySize;
            _history.RemoveRange(0, removeCount);
            _currentIndex -= removeCount;
            Console.WriteLine($"[HISTORY] Removed {removeCount} old entries to maintain max size");
        }

        Console.WriteLine($"[HISTORY] State saved. History size: {_history.Count}, Current index: {_currentIndex}");
    }

    /// <summary>
    /// Undo to previous state
    /// </summary>
    public Boundary? Undo()
    {
        if (!CanUndo)
        {
            Console.WriteLine("[HISTORY] Cannot undo - at beginning of history");
            return null;
        }

        _currentIndex--;
        Console.WriteLine($"[HISTORY] Undo to index {_currentIndex}: {_history[_currentIndex].ActionDescription}");

        return DeepCopyBoundary(_history[_currentIndex].BoundarySnapshot);
    }

    /// <summary>
    /// Redo to next state
    /// </summary>
    public Boundary? Redo()
    {
        if (!CanRedo)
        {
            Console.WriteLine("[HISTORY] Cannot redo - at end of history");
            return null;
        }

        _currentIndex++;
        Console.WriteLine($"[HISTORY] Redo to index {_currentIndex}: {_history[_currentIndex].ActionDescription}");

        return DeepCopyBoundary(_history[_currentIndex].BoundarySnapshot);
    }

    /// <summary>
    /// Undo to a specific state in history
    /// </summary>
    public Boundary? UndoToIndex(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _history.Count)
        {
            Console.WriteLine($"[HISTORY] Cannot undo to index {targetIndex} - out of range");
            return null;
        }

        if (targetIndex == _currentIndex)
        {
            Console.WriteLine($"[HISTORY] Already at index {targetIndex}");
            return null;
        }

        _currentIndex = targetIndex;
        Console.WriteLine($"[HISTORY] Jumped to index {_currentIndex}: {_history[_currentIndex].ActionDescription}");

        return DeepCopyBoundary(_history[_currentIndex].BoundarySnapshot);
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _currentIndex = -1;
        Console.WriteLine("[HISTORY] History cleared");
    }

    /// <summary>
    /// Deep copy a boundary object
    /// </summary>
    private Boundary? DeepCopyBoundary(Boundary? source)
    {
        if (source == null)
            return null;

        var copy = new Boundary();

        // Copy outer boundary
        if (source.OuterBoundary != null)
        {
            copy.OuterBoundary = DeepCopyPolygon(source.OuterBoundary);
        }

        // Copy inner boundaries
        foreach (var inner in source.InnerBoundaries)
        {
            copy.InnerBoundaries.Add(DeepCopyPolygon(inner));
        }

        // Copy headland if exists
        if (source.HeadlandPolygon != null)
        {
            copy.HeadlandPolygon = DeepCopyPolygon(source.HeadlandPolygon);
        }

        return copy;
    }

    /// <summary>
    /// Deep copy a boundary polygon
    /// </summary>
    private BoundaryPolygon DeepCopyPolygon(BoundaryPolygon source)
    {
        var copy = new BoundaryPolygon
        {
            IsDriveThrough = source.IsDriveThrough
        };

        // Copy all points
        foreach (var point in source.Points)
        {
            copy.Points.Add(new BoundaryPoint(point.Easting, point.Northing, point.Heading));
        }

        return copy;
    }
}
