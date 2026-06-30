using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class FeedViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowViewModel _main;
    private readonly DispatcherTimer _refreshTimer;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _noPosts;
    [ObservableProperty] private bool _hasStories;

    public ObservableCollection<ServerPostCardViewModel>     Posts   { get; } = new();
    public ObservableCollection<ServerStoryBubbleViewModel>  Stories { get; } = new();

    public FeedViewModel(MainWindowViewModel main)
    {
        _main = main;

        ServerClient.Instance.OnNewPost += OnNewPostArrived;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += (_, _) => _ = SilentRefreshAsync();
        _refreshTimer.Start();

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        Posts.Clear();
        Stories.Clear();
        try
        {
            var feedTask    = ServerClient.Instance.GetFeedAsync();
            var storiesTask = ServerClient.Instance.GetStoryFeedAsync();
            await Task.WhenAll(feedTask, storiesTask);

            var feed      = feedTask.Result;
            var storyFeed = storiesTask.Result;

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

    // Silently merge new posts from server without clearing the list.
    private async Task SilentRefreshAsync()
    {
        try
        {
            var feed = await ServerClient.Instance.GetFeedAsync();
            if (feed == null) return;

            var existingIds = Posts.Select(p => p.PostId).ToHashSet();
            var toAdd = feed.Where(p => !existingIds.Contains(p.Id)).ToList();
            for (int i = toAdd.Count - 1; i >= 0; i--)
                Posts.Insert(0, new ServerPostCardViewModel(toAdd[i], _main));

            if (Posts.Count > 0) NoPosts = false;
        }
        catch { }
    }

    // Real-time: a new post was broadcast by the server.
    private void OnNewPostArrived(PostDto dto)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Posts.Any(p => p.PostId == dto.Id)) return;
            Posts.Insert(0, new ServerPostCardViewModel(dto, _main));
            NoPosts = false;
        });
    }

    [RelayCommand] void Refresh()  => _ = LoadAsync();
    [RelayCommand] void NewStory() => _main.Navigate(new StoryComposeViewModel(_main));

    public void Dispose()
    {
        _refreshTimer.Stop();
        ServerClient.Instance.OnNewPost -= OnNewPostArrived;
    }
}
