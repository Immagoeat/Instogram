using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class ExploreView : UserControl
{
    public ExploreView() => InitializeComponent();
    ExploreViewModel VM => (ExploreViewModel)DataContext!;

    void OnRefresh(object? s, RoutedEventArgs e)        => VM.RefreshCommand.Execute(null);
    void OnSwitchToPosts(object? s, RoutedEventArgs e)  => VM.SwitchToPostsCommand.Execute(null);
    void OnSwitchToPeople(object? s, RoutedEventArgs e) => VM.SwitchToPeopleCommand.Execute(null);
    void OnClearTagFilter(object? s, RoutedEventArgs e) => VM.ClearTagFilterCommand.Execute(null);

    void OnFilterByTag(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is TrendingTagChipViewModel chip)
            VM.FilterByTagCommand.Execute(chip);
    }

    void OnToggleFollow(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is UserResultViewModel user)
            user.ToggleFollowCommand.Execute(null);
    }

    void OnOpenProfile(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is UserResultViewModel user)
            user.OpenProfileCommand.Execute(null);
    }
}
