using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramDependencies;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

// ── Story bubble in the horizontal strip on the feed ─────────────────────────

public partial class StoryBubbleViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public User   Author      { get; }
    public new string Initial { get; }
    public bool   HasUnseen   { get; }
    public string AccentColor { get; }

    public StoryBubbleViewModel(User author, MainWindowViewModel main)
    {
        _main       = main;
        Author      = author;
        Initial     = author.DisplayName.Length > 0 ? author.DisplayName[0].ToString().ToUpper() : "?";
        AccentColor = author.AccentColor;
        var me      = AppState.Instance.CurrentUser!;
        HasUnseen   = AppState.Instance.Stories.HasUnseenStories(author.Id, me.Id);
    }

    [RelayCommand]
    void Open() => _main.Navigate(new StoryViewerViewModel(_main, Author));
}

// ── Viewer: shows all active stories by one user, one at a time ───────────────

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
    [ObservableProperty] private string _imageUrl     = "";
    [ObservableProperty] private bool   _hasImage;
    [ObservableProperty] private string _timeLabel    = "";
    [ObservableProperty] private int    _currentIndex = 1;
    [ObservableProperty] private int    _totalCount   = 1;
    [ObservableProperty] private bool   _hasPrev;
    [ObservableProperty] private bool   _hasNext;
    [ObservableProperty] private string _taggedUsersLabel = "";
    [ObservableProperty] private bool   _hasTaggedUsers;

    // Text overlay position (0‒1 fractions, converted in view)
    [ObservableProperty] private double _textOffsetX = 0.5;
    [ObservableProperty] private double _textOffsetY = 0.5;
    [ObservableProperty] private double _textScale   = 1.0;

    public StoryViewerViewModel(MainWindowViewModel main, User author)
    {
        _main    = main;
        Author   = author;
        _stories = AppState.Instance.Stories.GetByUser(author.Id);
        _index   = 0;
        ShowCurrent();
    }

    void ShowCurrent()
    {
        if (_stories.Count == 0) { StoryText = "(no active stories)"; return; }
        var s  = _stories[_index];
        var me = AppState.Instance.CurrentUser!;
        AppState.Instance.Stories.MarkSeen(s.Id, me.Id);
        AppState.Instance.Save();

        StoryText    = s.Text;
        BgColor      = s.BackgroundColor;
        CurrentIndex = _index + 1;
        TotalCount   = _stories.Count;
        HasPrev      = _index > 0;
        HasNext      = _index < _stories.Count - 1;
        TimeLabel    = FormatAge(s.CreatedAt);

        // Tagged users
        if (!string.IsNullOrWhiteSpace(s.TaggedUsers))
        {
            var tags = s.TaggedUsers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => $"@{t.Trim()}")
                          .ToList();
            TaggedUsersLabel = string.Join("  ", tags);
            HasTaggedUsers   = true;
        }
        else
        {
            TaggedUsersLabel = "";
            HasTaggedUsers   = false;
        }
    }

    [RelayCommand] void Prev() { if (_index > 0) { _index--; ShowCurrent(); } }
    [RelayCommand] void Next() { if (_index < _stories.Count - 1) { _index++; ShowCurrent(); } }
    [RelayCommand] void Close() => _main.Navigate(new FeedViewModel(_main));
}

// ── Tag suggestion row in the mention picker ─────────────────────────────────

public class TagSuggestionViewModel
{
    public Guid   Id          { get; }
    public string Username    { get; }
    public string DisplayName { get; }
    public string AccentColor { get; }
    public string Initial     => DisplayName.Length > 0 ? DisplayName[0].ToString().ToUpper() : "?";
    public string Label       => $"@{Username}  ·  {DisplayName}";

    public TagSuggestionViewModel(Guid id, string username, string displayName, string accentColor)
    {
        Id          = id;
        Username    = username;
        DisplayName = displayName;
        AccentColor = accentColor;
    }
}

// ── Tagged-user chip shown beneath the text box ───────────────────────────────

public partial class TaggedUserChipViewModel : ViewModelBase
{
    public string Username { get; }
    private readonly StoryComposeViewModel _parent;

    public TaggedUserChipViewModel(string username, StoryComposeViewModel parent)
    {
        Username = username;
        _parent  = parent;
    }

    [RelayCommand]
    void Remove() => _parent.RemoveTag(Username);
}

// ── Composer: post a new story ────────────────────────────────────────────────

public partial class StoryComposeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    // ── Text + background ──────────────────────────────────────────────────────
    [ObservableProperty] private string _text          = "";
    [ObservableProperty] private string _selectedColor = "#1a0a3a";
    [ObservableProperty] private string _errorMessage  = "";
    [ObservableProperty] private bool   _isBusy;

    // ── Background image ───────────────────────────────────────────────────────
    [ObservableProperty] private string _bgImagePath = "";
    [ObservableProperty] private bool   _hasBgImage;
    partial void OnBgImagePathChanged(string value) => HasBgImage = !string.IsNullOrEmpty(value);

    // ── Text overlay position/scale/rotation ──────────────────────────────────
    // Position is fractional (0‒1), translated to pixel offsets in the view
    [ObservableProperty] private double _textOffsetX   = 0.5;
    [ObservableProperty] private double _textOffsetY   = 0.5;
    [ObservableProperty] private double _textScale     = 1.0;
    [ObservableProperty] private double _textRotation  = 0.0;

    // ── @mention system ────────────────────────────────────────────────────────
    [ObservableProperty] private string _mentionQuery  = "";
    [ObservableProperty] private bool   _showSuggestions;

    public ObservableCollection<TagSuggestionViewModel>  Suggestions  { get; } = new();
    public ObservableCollection<TaggedUserChipViewModel> TaggedUsers  { get; } = new();

    private readonly HashSet<string> _taggedSet = new(StringComparer.OrdinalIgnoreCase);

    partial void OnMentionQueryChanged(string value) => _ = SearchUsersAsync(value);

    private async Task SearchUsersAsync(string q)
    {
        var trimmed = q.Trim();
        if (trimmed.Length < 1)
        {
            Suggestions.Clear();
            ShowSuggestions = false;
            return;
        }
        try
        {
            var results = await ServerClient.Instance.SearchUsersAsync(trimmed);
            Suggestions.Clear();
            if (results != null)
                foreach (var r in results.Take(5))
                    Suggestions.Add(new TagSuggestionViewModel(r.Id, r.Username, r.DisplayName, r.AccentColor));
            ShowSuggestions = Suggestions.Count > 0;
        }
        catch { Suggestions.Clear(); ShowSuggestions = false; }
    }

    [RelayCommand]
    void AddTag(TagSuggestionViewModel suggestion)
    {
        if (_taggedSet.Add(suggestion.Username))
            TaggedUsers.Add(new TaggedUserChipViewModel(suggestion.Username, this));
        MentionQuery    = "";
        ShowSuggestions = false;
    }

    public void RemoveTag(string username)
    {
        _taggedSet.Remove(username);
        var chip = TaggedUsers.FirstOrDefault(c => c.Username == username);
        if (chip != null) TaggedUsers.Remove(chip);
    }

    // ── Background colors ──────────────────────────────────────────────────────
    public List<string> BackgroundOptions { get; } = new()
    {
        "#0f0c1e", "#0a1628", "#0d2010", "#1e0a0a",
        "#1a1a0a", "#1c0a1c", "#0a1e1e", "#1a0e00",
        "#1e1e1e", "#0c0c28", "#280c0c", "#0c2828"
    };

    public StoryComposeViewModel(MainWindowViewModel main) => _main = main;

    [RelayCommand]
    async Task Post()
    {
        var text = Text.Trim();
        if (string.IsNullOrEmpty(text) && !HasBgImage)
        { ErrorMessage = "Add some text or a background image first."; return; }

        IsBusy = true;
        ErrorMessage = "";
        try
        {
            var tagged = string.Join(",", _taggedSet);
            var story  = await ServerClient.Instance.CreateStoryAsync(
                text, SelectedColor, TextOffsetX, TextOffsetY, TextScale, TextRotation, tagged);
            if (story == null) { ErrorMessage = "Failed to post story — try again."; return; }

            if (HasBgImage)
            {
                var imageUrl = await ServerClient.Instance.UploadStoryImageAsync(story.Id, BgImagePath);
                if (imageUrl == null) ErrorMessage = "Story posted, but image upload failed.";
            }

            _main.Navigate(new FeedViewModel(_main));
        }
        catch { ErrorMessage = "Could not reach server."; }
        finally { IsBusy = false; }
    }

    [RelayCommand] void SelectColor(string color) => SelectedColor = color;
    [RelayCommand] void Cancel() => _main.Navigate(new FeedViewModel(_main));
}

// ── Strip shown at the top of the feed ───────────────────────────────────────

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

// ── Server-backed story bubble ────────────────────────────────────────────────

public partial class ServerStoryBubbleViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly List<StoryDto>      _stories;

    public string AuthorUsername  { get; }
    public string AuthorName      { get; }
    public string AccentColor     { get; }
    public new string Initial     { get; }
    public bool   HasUnseen       { get; }

    public ServerStoryBubbleViewModel(List<StoryDto> stories, MainWindowViewModel main)
    {
        _main        = main;
        _stories     = stories;
        var first    = stories[0];
        AuthorUsername = first.AuthorUsername;
        AuthorName   = first.AuthorDisplayName;
        AccentColor  = first.AuthorAccent;
        Initial      = first.AuthorDisplayName.Length > 0
            ? first.AuthorDisplayName[0].ToString().ToUpper() : "?";
        HasUnseen    = stories.Any(s => !s.HasSeen);
    }

    [RelayCommand]
    void Open() => _main.Navigate(new ServerStoryViewerViewModel(_main, _stories));
}

// ── Server-backed story viewer ────────────────────────────────────────────────

public partial class ServerStoryViewerViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly IReadOnlyList<StoryDto> _stories;
    private int _index;

    public string AuthorName  => _stories[0].AuthorDisplayName;
    public string AuthorAt    => $"@{_stories[0].AuthorUsername}";
    public string AccentColor => _stories[0].AuthorAccent;

    [ObservableProperty] private string _storyText    = "";
    [ObservableProperty] private string _bgColor      = "#1a0a3a";
    [ObservableProperty] private string _imageUrl     = "";
    [ObservableProperty] private bool   _hasImage;
    [ObservableProperty] private string _timeLabel    = "";
    [ObservableProperty] private int    _currentIndex = 1;
    [ObservableProperty] private int    _totalCount   = 1;
    [ObservableProperty] private bool   _hasPrev;
    [ObservableProperty] private bool   _hasNext;
    [ObservableProperty] private string _taggedUsersLabel = "";
    [ObservableProperty] private bool   _hasTaggedUsers;
    [ObservableProperty] private double _textOffsetX   = 0.5;
    [ObservableProperty] private double _textOffsetY   = 0.5;
    [ObservableProperty] private double _textScale     = 1.0;
    [ObservableProperty] private double _textRotation  = 0.0;

    public ServerStoryViewerViewModel(MainWindowViewModel main, List<StoryDto> stories)
    {
        _main    = main;
        _stories = stories;
        _index   = 0;
        ShowCurrent();
    }

    void ShowCurrent()
    {
        var s        = _stories[_index];
        StoryText    = s.Text;
        BgColor      = s.BackgroundColor;
        var raw      = s.ImageUrl ?? "";
        ImageUrl     = string.IsNullOrEmpty(raw) ? "" :
                       raw.StartsWith("http") ? raw :
                       ServerClient.Instance.BaseUrl.TrimEnd('/') + raw;
        HasImage     = !string.IsNullOrEmpty(raw);
        TextOffsetX  = s.TextX;
        TextOffsetY  = s.TextY;
        TextScale    = s.TextScale;
        TextRotation = s.TextRotation;
        CurrentIndex = _index + 1;
        TotalCount   = _stories.Count;
        HasPrev      = _index > 0;
        HasNext      = _index < _stories.Count - 1;
        TimeLabel    = FormatAge(s.CreatedAt);

        if (!string.IsNullOrWhiteSpace(s.TaggedUsers))
        {
            var tags = s.TaggedUsers.Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => $"@{t.Trim()}").ToList();
            TaggedUsersLabel = string.Join("  ", tags);
            HasTaggedUsers   = true;
        }
        else { TaggedUsersLabel = ""; HasTaggedUsers = false; }

        _ = MarkSeenAsync(s.Id);
    }

    private static async Task MarkSeenAsync(Guid storyId)
    {
        try { await ServerClient.Instance.MarkStorySeenAsync(storyId); } catch { }
    }

    [RelayCommand] void Prev()  { if (_index > 0) { _index--; ShowCurrent(); } }
    [RelayCommand] void Next()  { if (_index < _stories.Count - 1) { _index++; ShowCurrent(); } }
    [RelayCommand] void Close() => _main.Navigate(new FeedViewModel(_main));
}
