using Avalonia.Controls;
using Avalonia.Interactivity;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views.Dialogs;

public partial class InnerBoundaryApplyDialog : Window
{
    public InnerBoundaryApplyDialog()
    {
        InitializeComponent();
    }

    private void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        // Apply the modification to the inner boundary
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ApplyInnerModificationCommand.Execute(null);
        }

        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        // Cancel the modification (clears notch points)
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelNotchCommand.Execute(null);
        }

        Close();
    }
}
