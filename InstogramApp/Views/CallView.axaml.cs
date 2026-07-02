using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class CallView : UserControl
{
    public CallView() => InitializeComponent();
    CallViewModel VM => (CallViewModel)DataContext!;

    void OnAccept(object? s, RoutedEventArgs e)       => VM.AcceptCommand.Execute(null);
    void OnDecline(object? s, RoutedEventArgs e)      => VM.DeclineCommand.Execute(null);
    void OnToggleMute(object? s, RoutedEventArgs e)   => VM.ToggleMuteCommand.Execute(null);
    void OnToggleCamera(object? s, RoutedEventArgs e) => VM.ToggleCameraCommand.Execute(null);
    void OnHangUp(object? s, RoutedEventArgs e)       => VM.HangUpCommand.Execute(null);
}
