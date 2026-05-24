using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramDependencies;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

// One story bubble in the horizontal strip on the feed
public partial class StoryBubbleViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public User   Author       { get; }
    public string Initial      { get; }
    public bool   HasUnseen    { get; }
    public string AccentColor  { get; }

    public StoryBubbleViewModel(User author, MainWindowViewModel main)
    {
        _main       = main;
        Author      = author;
        Initial     = author.DisplayName.Length > 0
                      ? author.DisplayName[0].ToString().ToUpper() : "?";
        AccentColor = author.AccentColor;
        var me      = AppState.Instance.CurrentUser!;
        HasUnseen   = AppState.Instance.Stories.HasUnseenStories(author.Id, me.Id);
    }

    [RelayCommand]
    void Open() => _main.Navigate(new StoryViewerViewModel(_main, Author));
}

// Viewer: shows all active stories by one user, one at a time
public partial class StoryViewerViewModel : ViewModelBase
{
    private readonly MainWindowViewModel  _main;
    private readonly IReadOnlyList<Story> _stories;
    private int _index;

    public User   Author      { get; }
    public string AuthorName  => Author.DisplayName;
    public string AuthorAt    => $"@{Author.Username}";
    public string AccentColor => Author.AccentColor;

    [ObservableProperty] private string _storyText    = "";
    [ObservableProperty] private string _bgColor      = "#1a0a3a";
    [ObservableProperty] private string _timeLabel    = "";
    [ObservableProperty] private int    _currentIndex = 1;
    [ObservableProperty] private int    _totalCount   = 1;
    [ObservableProperty] private bool   _hasPrev;
    [ObservableProperty] private bool   _hasNext;

    public StoryViewerViewModel(MainWindowViewModel main, User author)
    {
        _main   = main;
        Author  = author;
        _stories = AppState.Instance.Stories.GetByUser(author.Id);
        _index  = 0;
        ShowCurrent();
    }

    void ShowCurrent()
    {
        if (_stories.Count == 0) { StoryText = "(no active stories)"; return; }
        var s = _stories[_index];
        var me = AppState.Instance.CurrentUser!;
        AppState.Instance.Stories.MarkSeen(s.Id, me.Id);
        AppState.Instance.Save();

        StoryText    = s.Text;
        BgColor      = s.BackgroundColor;
        CurrentIndex = _index + 1;
        TotalCount   = _stories.Count;
        HasPrev      = _index > 0;
        HasNext      = _index < _stories.Count - 1;

        var age = DateTime.UtcNow - s.CreatedAt;
        TimeLabel = age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes}m ago"
                  : $"{(int)age.TotalHours}h ago";
    }

    [RelayCommand] void Prev() { if (_index > 0) { _index--; ShowCurrent(); } }
    [RelayCommand] void Next() { if (_index < _stories.Count - 1) { _index++; ShowCurrent(); } }
    [RelayCommand] void Close() => _main.Navigate(new FeedViewModel(_main));
}

// Composer: post a new story
public partial class StoryComposeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _selectedColor = "#1a0a3a";
    [ObservableProperty] private string _errorMessage  = "";

    public List<string> BackgroundOptions { get; } = new()
    {
        "#1a0a3a", "#0a1a3a", "#0a3a1a", "#3a0a1a",
        "#1a1a0a", "#2a0a2a", "#0a2a2a", "#2d1b00"
    };

    public StoryComposeViewModel(MainWindowViewModel main) => _main = main;

    [RelayCommand]
    void Post()
    {
        var text = Text.Trim();
        if (string.IsNullOrEmpty(text)) { ErrorMessage = "Story text cannot be empty."; return; }
        var me = AppState.Instance.CurrentUser!;
        AppState.Instance.Stories.Post(me.Id, text, SelectedColor);
        AppState.Instance.Save();
        _main.Navigate(new FeedViewModel(_main));
    }

    [RelayCommand] void SelectColor(string color) => SelectedColor = color;
    [RelayCommand] void Cancel() => _main.Navigate(new FeedViewModel(_main));
}

// Strip shown at the top of the feed
public partial class StoryStripViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public ObservableCollection<StoryBubbleViewModel> Bubbles { get; } = new();
    [ObservableProperty] private bool _hasStories;

    public StoryStripViewModel(MainWindowViewModel main)
    {
        _main = main;
        Reload();
    }

    public void Reload()
    {
        Bubbles.Clear();
        var me      = AppState.Instance.CurrentUser!;
        var stories = AppState.Instance.Stories.GetStoriesForViewer(me.Id, me.Following);

        // Group by author, show each author once
        var seen = new HashSet<Guid>();
        foreach (var s in stories)
        {
            if (seen.Contains(s.AuthorId)) continue;
            seen.Add(s.AuthorId);
            var author = AppState.Instance.Accounts.GetById(s.AuthorId);
            if (author != null) Bubbles.Add(new StoryBubbleViewModel(author, _main));
        }
        HasStories = Bubbles.Count > 0;
    }
}
