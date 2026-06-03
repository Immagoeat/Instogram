using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    MainWindowViewModel VM => (MainWindowViewModel)DataContext!;

    void NavHome(object? s, RoutedEventArgs e)          => VM.GoFeedCommand.Execute(null);
    void NavExplore(object? s, RoutedEventArgs e)       => VM.GoExploreCommand.Execute(null);
    void NavNewPost(object? s, RoutedEventArgs e)       => VM.GoNewPostCommand.Execute(null);
    void NavNewStory(object? s, RoutedEventArgs e)      => VM.GoNewStoryCommand.Execute(null);
    void NavMessages(object? s, RoutedEventArgs e)      => VM.GoDMsCommand.Execute(null);
    void NavNotifications(object? s, RoutedEventArgs e) => VM.GoNotificationsCommand.Execute(null);
    void NavProfile(object? s, RoutedEventArgs e)       => VM.GoProfileCommand.Execute(null);
    void NavSettings(object? s, RoutedEventArgs e)      => VM.GoSettingsCommand.Execute(null);
    void NavFriends(object? s, RoutedEventArgs e)       => VM.GoFriendsCommand.Execute(null);
    void NavFriendCall(object? s, RoutedEventArgs e)    => VM.GoFriendsCommand.Execute(null);
    void NavLogout(object? s, RoutedEventArgs e)        => VM.LogoutCommand.Execute(null);
}
