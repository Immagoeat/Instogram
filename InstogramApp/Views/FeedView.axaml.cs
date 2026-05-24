using Avalonia.Controls;
using Avalonia.Interactivity;
using InstogramApp.ViewModels;

namespace InstogramApp.Views;

public partial class FeedView : UserControl
{
    public FeedView() => InitializeComponent();
    FeedViewModel VM => (FeedViewModel)DataContext!;

    void ShowFollowing(object? s, RoutedEventArgs e)   => VM.ShowFollowingCommand.Execute(null);
    void ShowRecommended(object? s, RoutedEventArgs e) => VM.ShowRecommendedCommand.Execute(null);
    void DoRefresh(object? s, RoutedEventArgs e)       => VM.RefreshCommand.Execute(null);
    void OnNewStory(object? s, RoutedEventArgs e)      => VM.NewStoryCommand.Execute(null);
    void OnOpenStory(object? s, RoutedEventArgs e)
    {
        if (s is Button btn && btn.Tag is StoryBubbleViewModel bubble)
            bubble.OpenCommand.Execute(null);
    }
}
