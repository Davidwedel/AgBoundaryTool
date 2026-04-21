using Avalonia.Controls;
using Avalonia.Interactivity;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views.Dialogs;

public partial class FieldSplitDialog : Window
{
    public FieldSplitDialog()
    {
        InitializeComponent();
    }

    private void DropSplitPoint1_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.DropSplitPoint1Command.Execute(null);
        }
    }

    private void DropSplitPoint2_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.DropSplitPoint2Command.Execute(null);
        }
    }

    private void SplitButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ExecuteFieldSplitCommand.Execute(null);
        }

        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelFieldSplitCommand.Execute(null);
        }

        Close();
    }
}
