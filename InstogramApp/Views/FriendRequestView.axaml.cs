using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class FriendRequestView : UserControl
{
    public FriendRequestView() => InitializeComponent();
    FriendRequestViewModel VM => (FriendRequestViewModel)DataContext!;

    void OnBack(object? s, RoutedEventArgs e)        => VM.BackCommand.Execute(null);
    void OnIncomingTab(object? s, RoutedEventArgs e)  => VM.ShowIncomingTabCommand.Execute(null);
    void OnOutgoingTab(object? s, RoutedEventArgs e)  => VM.ShowOutgoingTabCommand.Execute(null);

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
}
