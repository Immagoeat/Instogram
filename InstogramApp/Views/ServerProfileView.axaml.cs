using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class ServerProfileView : UserControl
{
    public ServerProfileView() => InitializeComponent();
    ServerProfileViewModel VM => (ServerProfileViewModel)DataContext!;
    void OnBack(object? s, RoutedEventArgs e)             => VM.BackCommand.Execute(null);
    void OnToggleFollow(object? s, RoutedEventArgs e)     => VM.ToggleFollowCommand.Execute(null);
    void OnMessage(object? s, RoutedEventArgs e)          => VM.SendMessageCommand.Execute(null);
    void OnToggleVerify(object? s, RoutedEventArgs e)     => VM.ToggleVerifyCommand.Execute(null);
    void OnSendFriendRequest(object? s, RoutedEventArgs e) => VM.SendFriendRequestCommand.Execute(null);
    void OnCall(object? s, RoutedEventArgs e)               => VM.StartCallCommand.Execute(null);
}
