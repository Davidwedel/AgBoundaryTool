using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgBoundaryTool.Views.Dialogs;

public partial class BoundaryNotchDialog : Window
{
    public BoundaryNotchDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
