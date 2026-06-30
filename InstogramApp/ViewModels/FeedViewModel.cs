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

    public ObservableCollection<ServerPostCardViewModel>    Posts   { get; } = new();
    public ObservableCollection<ServerStoryBubbleViewModel> Stories { get; } = new();

    public FeedViewModel(MainWindowViewModel main)
    {
        _main = main;

        ServerClient.Instance.OnNewPost    += OnNewPostArrived;
        ServerClient.Instance.OnNewComment += OnNewCommentArrived;
        ServerClient.Instance.OnNewStory   += OnNewStoryArrived;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
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

    // Silently merge new posts/stories from server without clearing the list.
    private async Task SilentRefreshAsync()
    {
        try
        {
            var feedTask    = ServerClient.Instance.GetFeedAsync();
            var storiesTask = ServerClient.Instance.GetStoryFeedAsync();
            await Task.WhenAll(feedTask, storiesTask);

            var feed      = feedTask.Result;
            var storyFeed = storiesTask.Result;

            if (feed != null)
            {
                var existingIds = Posts.Select(p => p.PostId).ToHashSet();
                var toAdd = feed.Where(p => !existingIds.Contains(p.Id)).ToList();
                for (int i = toAdd.Count - 1; i >= 0; i--)
                    Posts.Insert(0, new ServerPostCardViewModel(toAdd[i], _main));
                if (Posts.Count > 0) NoPosts = false;
            }

            if (storyFeed != null)
            {
                var existingAuthors = Stories.Select(b => b.AuthorId).ToHashSet();
                var grouped = storyFeed
                    .GroupBy(s => s.AuthorId)
                    .Where(g => !existingAuthors.Contains(g.Key))
                    .Select(g => g.ToList());
                foreach (var group in grouped)
                {
                    Stories.Insert(0, new ServerStoryBubbleViewModel(group, _main));
                    HasStories = true;
                }
            }
        }
        catch { }
    }

    // Real-time: new post broadcast.
    private void OnNewPostArrived(PostDto dto)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Posts.Any(p => p.PostId == dto.Id)) return;
            Posts.Insert(0, new ServerPostCardViewModel(dto, _main));
            NoPosts = false;
        });
    }

    // Real-time: new comment — update the comment count on the matching post card.
    private void OnNewCommentArrived(CommentDto dto)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var card = Posts.FirstOrDefault(p => p.PostId == dto.PostId);
            card?.AddComment(dto);
        });
    }

    // Real-time: new story — add or update the author's bubble.
    private void OnNewStoryArrived(StoryDto dto)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Stories.FirstOrDefault(b => b.AuthorId == dto.AuthorId);
            if (existing != null)
            {
                existing.AddStory(dto);
            }
            else
            {
                Stories.Insert(0, new ServerStoryBubbleViewModel(new() { dto }, _main));
                HasStories = true;
            }
        });
    }

    [RelayCommand] void Refresh()  => _ = LoadAsync();
    [RelayCommand] void NewStory() => _main.Navigate(new StoryComposeViewModel(_main));

    public void Dispose()
    {
        _refreshTimer.Stop();
        ServerClient.Instance.OnNewPost    -= OnNewPostArrived;
        ServerClient.Instance.OnNewComment -= OnNewCommentArrived;
        ServerClient.Instance.OnNewStory   -= OnNewStoryArrived;
    }
}
