using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgBoundaryTool.Views.Dialogs;

public partial class FlagManagementDialog : Window
{
    public FlagManagementDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
