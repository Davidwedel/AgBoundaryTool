using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgBoundaryTool.Views.Dialogs;

public partial class BoundaryRecordingDialog : Window
{
    public BoundaryRecordingDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
