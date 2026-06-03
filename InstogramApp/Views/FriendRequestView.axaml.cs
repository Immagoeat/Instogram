using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class FriendRequestView : UserControl
{
    public FriendRequestView() => InitializeComponent();
    FriendsViewModel VM => (FriendsViewModel)DataContext!;

    void OnFriendsTab(object? s, RoutedEventArgs e)  => VM.ShowFriendsTabCommand.Execute(null);
    void OnIncomingTab(object? s, RoutedEventArgs e) => VM.ShowIncomingTabCommand.Execute(null);
    void OnOutgoingTab(object? s, RoutedEventArgs e) => VM.ShowOutgoingTabCommand.Execute(null);

    void OnAccept(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is FriendRequestRowViewModel row)
            VM.AcceptCommand.Execute(row);
    }

    void OnDecline(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is FriendRequestRowViewModel row)
            VM.DeclineCommand.Execute(row);
    }

    void OnFriendCall(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is FriendRowViewModel row)
            VM.CallCommand.Execute(row);
    }

    void OnFriendMessage(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is FriendRowViewModel row)
            VM.MessageCommand.Execute(row);
    }

    void OnFriendProfile(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is FriendRowViewModel row)
            VM.OpenProfileCommand.Execute(row);
    }
}
