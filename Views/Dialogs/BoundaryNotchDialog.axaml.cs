using Avalonia.Controls;
using Avalonia.Interactivity;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views.Dialogs;

public partial class BoundaryNotchDialog : Window
{
    public BoundaryNotchDialog()
    {
        InitializeComponent();
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        // Apply the notch to the boundary
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ApplyNotchCommand.Execute(null);
        }

        // Close the dialog
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        // Cancel the notch (clears notch points)
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelNotchCommand.Execute(null);
        }

        Close();
    }
}
