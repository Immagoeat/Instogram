using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class FeedViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _feedTitle = "Following";
    [ObservableProperty] private bool   _showingRecommended;

    public StoryStripViewModel  StoryStrip { get; }
    public ObservableCollection<PostCardViewModel> Posts { get; } = new();
    public bool NoPosts => Posts.Count == 0;

    public FeedViewModel(MainWindowViewModel main)
    {
        _main      = main;
        StoryStrip = new StoryStripViewModel(main);
        Load(recommended: false);
    }

    void Load(bool recommended)
    {
        Posts.Clear();
        OnPropertyChanged(nameof(NoPosts));
        ShowingRecommended = recommended;
        FeedTitle = recommended ? "For You" : "Following";
        var me    = AppState.Instance.CurrentUser!;
        var items = recommended
            ? AppState.Instance.Feed.GetRecommendedFeed(me)
            : AppState.Instance.Feed.GetFollowingFeed(me);
        foreach (var p in items)
            Posts.Add(new PostCardViewModel(p, _main));
        OnPropertyChanged(nameof(NoPosts));
    }

    [RelayCommand] void ShowFollowing()   => Load(recommended: false);
    [RelayCommand] void ShowRecommended() => Load(recommended: true);
    [RelayCommand] void Refresh()         => Load(ShowingRecommended);
    [RelayCommand] void NewStory()        => _main.Navigate(new StoryComposeViewModel(_main));
}
