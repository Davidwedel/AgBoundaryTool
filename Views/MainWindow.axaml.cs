using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AgBoundaryTool.Views.Controls;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views;

public partial class MainWindow : Window
{
    private BoundaryVisualizationControl? _visualizationControl;

    public MainWindow()
    {
        InitializeComponent();

        // Create and add visualization control after initialization
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        Console.WriteLine("[MAINWINDOW] Window closing, saving settings...");

        // Save settings before closing
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveSettings();
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Console.WriteLine("[MAINWINDOW] OnLoaded called");

        // Find the named border
        var border = this.FindControl<Border>("VisualizationBorder");

        if (border != null)
        {
            Console.WriteLine($"[MAINWINDOW] Found VisualizationBorder, size: {border.Bounds.Width}x{border.Bounds.Height}");

            _visualizationControl = new BoundaryVisualizationControl
            {
                Width = double.NaN,  // Auto-size
                Height = double.NaN
            };
            border.Child = _visualizationControl;

            Console.WriteLine("[MAINWINDOW] BoundaryVisualizationControl created and added");

            // Wire up ViewModel to visualization control
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.SetVisualizationControl(_visualizationControl);
                Console.WriteLine("[MAINWINDOW] ViewModel wired to visualization control");
            }
        }
        else
        {
            Console.WriteLine("[MAINWINDOW] ERROR: Could not find VisualizationBorder!");
        }
    }

    public BoundaryVisualizationControl? VisualizationControl => _visualizationControl;
}