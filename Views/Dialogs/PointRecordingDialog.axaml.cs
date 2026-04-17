using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AgBoundaryTool.ViewModels;

namespace AgBoundaryTool.Views.Dialogs;

public partial class PointRecordingDialog : Window
{
    public PointRecordingDialog()
    {
        InitializeComponent();
    }

    private async void DoneButton_Click(object? sender, RoutedEventArgs e)
    {
        // Call the ViewModel command first
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.FinishPointRecordingCommand.Execute(null);
        }

        // Wait a bit for the next dialog to open, then close this one
        await System.Threading.Tasks.Task.Delay(100);
        Close();
    }
}
