using Avalonia.Controls;
using Avalonia.Interactivity;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views.Dialogs;

public partial class InnerBoundaryModifyDialog : Window
{
    public InnerBoundaryModifyDialog()
    {
        InitializeComponent();
    }

    private void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        // Start recording the modification path
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.StartInnerBoundaryRecordingCommand.Execute(null);
        }

        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
