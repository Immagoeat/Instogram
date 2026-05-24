using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class AuthView : UserControl
{
    public AuthView() => InitializeComponent();

    AuthViewModel VM => (AuthViewModel)DataContext!;

    void OnToggleRegister(object? sender, RoutedEventArgs e) => VM.ToggleRegisterCommand.Execute(null);
    void OnLogin(object? sender, RoutedEventArgs e)          => VM.LoginCommand.Execute(null);
    void OnRegister(object? sender, RoutedEventArgs e)       => VM.RegisterCommand.Execute(null);
}
