using Avalonia.Controls;
using Avalonia.Interactivity;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views.Dialogs;

public partial class BoundaryApplyDialog : Window
{
    public BoundaryApplyDialog()
    {
        InitializeComponent();
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        // Apply the modification to the boundary
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ApplyBoundaryModificationCommand.Execute(null);
        }

        // Close the dialog
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        // Cancel the modification (clears modification points)
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelBoundaryModificationCommand.Execute(null);
        }

        Close();
    }
}
