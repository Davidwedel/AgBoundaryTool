// AgBoundaryTool
// Boundary history dialog - shows undo/redo history

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AgBoundaryTool.ViewModels;
using AgBoundaryTool.Services;

namespace AgBoundaryTool.Views.Dialogs;

public partial class BoundaryHistoryDialog : Window
{
    public BoundaryHistoryDialog()
    {
        InitializeComponent();

        // Update history entries when window is opened
        this.Opened += BoundaryHistoryDialog_Opened;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void BoundaryHistoryDialog_Opened(object? sender, EventArgs e)
    {
        // Populate history list when dialog opens
        if (DataContext is MainWindowViewModel vm)
        {
            UpdateHistoryList(vm);
        }
    }

    private void HistoryListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedIndex >= 0 && DataContext is MainWindowViewModel vm)
        {
            var historyService = vm.GetHistoryService();
            int targetIndex = listBox.SelectedIndex;

            // Only undo/redo if user clicked on a different state
            if (targetIndex != historyService.CurrentIndex)
            {
                Console.WriteLine($"[HISTORY DIALOG] User selected index {targetIndex}, current is {historyService.CurrentIndex}");

                // Undo or redo to the selected state
                var restoredBoundary = historyService.UndoToIndex(targetIndex);
                if (restoredBoundary != null && vm.GetType().GetMethod("ApplyBoundaryState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
                {
                    // Use reflection to call the private ApplyBoundaryState method
                    var method = vm.GetType().GetMethod("ApplyBoundaryState",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(vm, new object[] { restoredBoundary, "Jump to History" });

                    // Update UI
                    var updateMethod = vm.GetType().GetMethod("UpdateHistoryButtons",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    updateMethod?.Invoke(vm, null);

                    // Refresh the list
                    UpdateHistoryList(vm);
                }
            }
        }
    }

    private void UpdateHistoryList(MainWindowViewModel vm)
    {
        var historyService = vm.GetHistoryService();
        var listBox = this.FindControl<ListBox>("HistoryListBox");

        if (listBox != null)
        {
            var entries = new ObservableCollection<HistoryDisplayEntry>();

            for (int i = 0; i < historyService.History.Count; i++)
            {
                var entry = historyService.History[i];
                entries.Add(new HistoryDisplayEntry
                {
                    Index = i,
                    ActionDescription = entry.ActionDescription,
                    Timestamp = entry.Timestamp,
                    IsCurrent = i == historyService.CurrentIndex
                });
            }

            listBox.ItemsSource = entries;
            listBox.SelectedIndex = historyService.CurrentIndex;

            // Scroll to current item
            if (listBox.SelectedIndex >= 0)
            {
                listBox.ScrollIntoView(entries[listBox.SelectedIndex]);
            }
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// Display entry for history list
/// </summary>
public class HistoryDisplayEntry
{
    public int Index { get; set; }
    public string ActionDescription { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsCurrent { get; set; }
}
