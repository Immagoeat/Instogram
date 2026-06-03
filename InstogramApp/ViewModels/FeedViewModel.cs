using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class FeedViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _noPosts;
    [ObservableProperty] private bool _hasStories;

    public ObservableCollection<ServerPostCardViewModel>    Posts   { get; } = new();
    public ObservableCollection<ServerStoryBubbleViewModel> Stories { get; } = new();

    public FeedViewModel(MainWindowViewModel main)
    {
        _main = main;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        Posts.Clear();
        Stories.Clear();
        try
        {
            var feedTask   = ServerClient.Instance.GetFeedAsync();
            var storiesTask = ServerClient.Instance.GetStoryFeedAsync();
            await Task.WhenAll(feedTask, storiesTask);

            var feed       = feedTask.Result;
            var storyFeed  = storiesTask.Result;

            if (feed != null)
                foreach (var p in feed)
                    Posts.Add(new ServerPostCardViewModel(p, _main));

            if (storyFeed != null)
            {
                var grouped = storyFeed
                    .GroupBy(s => s.AuthorId)
                    .Select(g => g.ToList());
                foreach (var group in grouped)
                    Stories.Add(new ServerStoryBubbleViewModel(group, _main));
            }
        }
        catch { }
        finally
        {
            IsLoading  = false;
            NoPosts    = Posts.Count == 0;
            HasStories = Stories.Count > 0;
        }
    }

    [RelayCommand] void Refresh()  => _ = LoadAsync();
    [RelayCommand] void NewStory() => _main.Navigate(new StoryComposeViewModel(_main));
}
