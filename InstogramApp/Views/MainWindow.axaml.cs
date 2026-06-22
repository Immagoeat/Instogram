using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    void SetActiveNav(Button active)
    {
        var btns = new[] { NavHomeBtn, NavExploreBtn, NavNewPostBtn, NavNewStoryBtn,
                           NavMessagesBtn, NavCallBtn, NavNotifBtn, NavFriendsBtn,
                           NavSettingsBtn, NavProfileBtn, NavAdminBtn };
        foreach (var b in btns)
        {
            if (b is null) continue;
            b.Classes.Remove("active");
        }
        active.Classes.Add("active");
    }

    void NavHome(object? s, RoutedEventArgs e)          { SetActiveNav(NavHomeBtn!);     VM.GoFeedCommand.Execute(null); }
    void NavExplore(object? s, RoutedEventArgs e)       { SetActiveNav(NavExploreBtn!);  VM.GoExploreCommand.Execute(null); }
    void NavNewPost(object? s, RoutedEventArgs e)       { SetActiveNav(NavNewPostBtn!);  VM.GoNewPostCommand.Execute(null); }
    void NavNewStory(object? s, RoutedEventArgs e)      { SetActiveNav(NavNewStoryBtn!); VM.GoNewStoryCommand.Execute(null); }
    void NavMessages(object? s, RoutedEventArgs e)      { SetActiveNav(NavMessagesBtn!); VM.GoDMsCommand.Execute(null); }
    void NavFriendCall(object? s, RoutedEventArgs e)    { SetActiveNav(NavCallBtn!);     VM.GoFriendsCommand.Execute(null); }
    void NavNotifications(object? s, RoutedEventArgs e) { SetActiveNav(NavNotifBtn!);    VM.GoNotificationsCommand.Execute(null); }
    void NavFriends(object? s, RoutedEventArgs e)       { SetActiveNav(NavFriendsBtn!);  VM.GoFriendsCommand.Execute(null); }
    void NavSettings(object? s, RoutedEventArgs e)      { SetActiveNav(NavSettingsBtn!); VM.GoSettingsCommand.Execute(null); }
    void NavProfile(object? s, RoutedEventArgs e)       { SetActiveNav(NavProfileBtn!);  VM.GoProfileCommand.Execute(null); }
    void NavAdmin(object? s, RoutedEventArgs e)         { SetActiveNav(NavAdminBtn!);   VM.GoAdminCommand.Execute(null); }
    void NavLogout(object? s, RoutedEventArgs e)        => VM.LogoutCommand.Execute(null);
}
