using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

// ── Single user result row ────────────────────────────────────────────────────

public partial class UserResultViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public Guid   Id          { get; }
    public string Username    { get; }
    public string DisplayName { get; }
    public string AccentColor { get; }
    public bool   IsVerified  { get; }
    public bool   IsMaster    { get; }
    public new string Initial => DisplayName.Length > 0 ? DisplayName[0].ToString().ToUpper() : "?";
    public string AtUsername  => $"@{Username}";

    [ObservableProperty] private bool _isFollowing;
    [ObservableProperty] private string _followLabel = "Follow";

    public UserResultViewModel(UserSearchResult r, MainWindowViewModel main)
    {
        Id          = r.Id;
        Username    = r.Username;
        DisplayName = r.DisplayName;
        AccentColor = r.AccentColor;
        IsVerified  = r.IsVerified;
        IsMaster    = r.IsMaster;
        _main       = main;
    }

    [RelayCommand]
    async Task ToggleFollow()
    {
        try
        {
            if (IsFollowing)
            {
                await ServerClient.Instance.UnfollowAsync(Id);
                IsFollowing = false;
                FollowLabel = "Follow";
            }
            else
            {
                await ServerClient.Instance.FollowAsync(Id);
                IsFollowing = true;
                FollowLabel = "Following";
            }
        }
        catch { }
    }

    [RelayCommand]
    void OpenProfile() => _main.Navigate(new ServerProfileViewModel(_main, Id));
}

// ── Trending tag chip ─────────────────────────────────────────────────────────

public class TrendingTagChipViewModel
{
    public string Tag   { get; }
    public int    Count { get; }
    public string Label => $"#{Tag}";
    public string CountLabel => Count == 1 ? "1 post" : $"{Count} posts";

    public TrendingTagChipViewModel(TrendingTagDto dto)
    {
        Tag   = dto.Tag;
        Count = dto.Count;
    }
}

// ── Main Explore / Search ViewModel ──────────────────────────────────────────

public partial class ExploreViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    // ── Search bar ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private string _activeTab = "posts"; // "posts" | "people"

    // ── Posts tab ─────────────────────────────────────────────────────────────
    public ObservableCollection<ServerPostCardViewModel> Posts        { get; } = new();
    public ObservableCollection<TrendingTagChipViewModel> TrendingTags { get; } = new();
    [ObservableProperty] private bool   _isLoadingPosts;
    [ObservableProperty] private bool   _noPostResults;
    [ObservableProperty] private bool   _showTrending = true;
    [ObservableProperty] private string _activeTagFilter = "";

    // ── People tab ────────────────────────────────────────────────────────────
    public ObservableCollection<UserResultViewModel> People { get; } = new();
    [ObservableProperty] private bool _isLoadingPeople;
    [ObservableProperty] private bool _noPeopleResults;

    // Computed
    public bool IsPostsTab  => ActiveTab == "posts";
    public bool IsPeopleTab => ActiveTab == "people";

    // Debounce
    private CancellationTokenSource? _debounce;

    public ExploreViewModel(MainWindowViewModel main)
    {
        _main = main;
        _ = LoadTrendingAsync();
        _ = LoadPostsAsync();
    }

    partial void OnQueryChanged(string value)
    {
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        _ = Task.Delay(320, token).ContinueWith(_ =>
        {
            if (token.IsCancellationRequested) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => RunSearch());
        }, TaskScheduler.Default);
    }

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsPostsTab));
        OnPropertyChanged(nameof(IsPeopleTab));
        RunSearch();
    }

    private void RunSearch()
    {
        if (ActiveTab == "people")
            _ = LoadPeopleAsync(Query.Trim());
        else
            _ = LoadPostsAsync(Query.Trim(), ActiveTagFilter);
    }

    // ── Trending tags ─────────────────────────────────────────────────────────

    private async Task LoadTrendingAsync()
    {
        try
        {
            var tags = await ServerClient.Instance.GetTrendingTagsAsync();
            TrendingTags.Clear();
            if (tags != null)
                foreach (var t in tags)
                    TrendingTags.Add(new TrendingTagChipViewModel(t));
        }
        catch { }
    }

    // ── Posts ─────────────────────────────────────────────────────────────────

    private async Task LoadPostsAsync(string? q = null, string? tag = null)
    {
        IsLoadingPosts = true;
        ShowTrending   = string.IsNullOrEmpty(q) && string.IsNullOrEmpty(tag);
        Posts.Clear();
        try
        {
            var posts = await ServerClient.Instance.GetExploreAsync(
                tag: string.IsNullOrEmpty(tag) ? null : tag,
                q:   string.IsNullOrEmpty(q)   ? null : q);
            if (posts != null)
                foreach (var p in posts)
                    Posts.Add(new ServerPostCardViewModel(p, _main));
        }
        catch { }
        finally
        {
            IsLoadingPosts = false;
            NoPostResults  = Posts.Count == 0 && (!string.IsNullOrEmpty(q) || !string.IsNullOrEmpty(tag));
        }
    }

    [RelayCommand]
    void FilterByTag(TrendingTagChipViewModel chip)
    {
        ActiveTagFilter = chip.Tag;
        Query           = "";
        _ = LoadPostsAsync(tag: chip.Tag);
    }

    [RelayCommand]
    void ClearTagFilter()
    {
        ActiveTagFilter = "";
        _ = LoadPostsAsync(q: string.IsNullOrEmpty(Query) ? null : Query);
    }

    // ── People ────────────────────────────────────────────────────────────────

    private async Task LoadPeopleAsync(string q)
    {
        if (string.IsNullOrEmpty(q))
        {
            People.Clear();
            NoPeopleResults = false;
            return;
        }
        IsLoadingPeople = true;
        People.Clear();
        try
        {
            var results = await ServerClient.Instance.SearchUsersAsync(q);
            if (results != null)
                foreach (var r in results)
                    People.Add(new UserResultViewModel(r, _main));
        }
        catch { }
        finally
        {
            IsLoadingPeople = false;
            NoPeopleResults = People.Count == 0;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand] void SwitchToPosts()  { ActiveTab = "posts";  }
    [RelayCommand] void SwitchToPeople() { ActiveTab = "people"; }
    [RelayCommand] void Refresh()
    {
        _ = LoadTrendingAsync();
        RunSearch();
    }
}
