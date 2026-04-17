using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgBoundaryTool.Views.Dialogs;

public partial class GpsSimulatorDialog : Window
{
    public GpsSimulatorDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
