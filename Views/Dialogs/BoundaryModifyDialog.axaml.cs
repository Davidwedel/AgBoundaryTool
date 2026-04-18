using Avalonia.Controls;
using Avalonia.Interactivity;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views.Dialogs;

public partial class BoundaryModifyDialog : Window
{
    public BoundaryModifyDialog()
    {
        InitializeComponent();
    }

    private void TrimButton_Click(object? sender, RoutedEventArgs e)
    {
        // Trim inner boundary to outer boundary
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.TrimInnerBoundaryCommand.Execute(null);
        }

        // Close the dialog
        Close();
    }

    private void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        // Start recording the modification path
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.StartBoundaryRecordingCommand.Execute(null);
        }

        // Close the dialog
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        // Disable selection mode and close
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelBoundaryModifyCommand.Execute(null);
        }

        Close();
    }
}
