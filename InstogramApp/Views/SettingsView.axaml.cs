using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
    SettingsViewModel VM => (SettingsViewModel)DataContext!;

    void OnSave(object? s, RoutedEventArgs e)          => VM.SaveCommand.Execute(null);
    void OnPromptDelete(object? s, RoutedEventArgs e)  => VM.PromptDeleteAccountCommand.Execute(null);
    void OnCancelDelete(object? s, RoutedEventArgs e)  => VM.CancelDeleteAccountCommand.Execute(null);
    void OnConfirmDelete(object? s, RoutedEventArgs e) => VM.ConfirmDeleteAccountCommand.Execute(null);
}
