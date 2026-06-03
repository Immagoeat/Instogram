using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
    SettingsViewModel VM => (SettingsViewModel)DataContext!;

    void OnSave(object? s, RoutedEventArgs e) => VM.SaveCommand.Execute(null);
}
