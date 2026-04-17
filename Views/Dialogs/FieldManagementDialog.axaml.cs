using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgBoundaryTool.Views.Dialogs;

public partial class FieldManagementDialog : Window
{
    public FieldManagementDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
