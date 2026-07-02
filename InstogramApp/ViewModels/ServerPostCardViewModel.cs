using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class ServerPostCardViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private PostDto _post;

    public Guid PostId => _post.Id;

    public string AuthorDisplayName => _post.AuthorDisplayName;
    public string AuthorUsername    => $"@{_post.AuthorUsername}";
    public string AuthorInitial => Initial(_post.AuthorDisplayName);
    public string AuthorAccent  => _post.AuthorAccent;
    public string Caption       => _post.Caption;
    public string TimeLabel     => FormatAge(_post.CreatedAt);
    public string ImageUrl  => ResolveUrl(_post.ImageUrl);
    public bool   HasImage  => !string.IsNullOrEmpty(_post.ImageUrl);
    public string VideoUrl  => ResolveUrl(_post.VideoUrl);
    public bool   HasVideo  => !string.IsNullOrEmpty(_post.VideoUrl);

    private static string ResolveUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
        return ServerClient.Instance.BaseUrl.TrimEnd('/') + url;
    }

    public List<string> Tags => _post.Tags
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => t.Trim())
        .Where(t => t.Length > 0)
        .ToList();

    public bool HasTags => Tags.Count > 0;

    [ObservableProperty] private int    _likeCount;
    [ObservableProperty] private bool   _isLiked;
    [ObservableProperty] private string _likeLabel = "";
    [ObservableProperty] private string _commentText = "";
    [ObservableProperty] private List<string> _commentPreviews = new();
    [ObservableProperty] private int    _commentCount;

    private readonly HashSet<Guid> _seenCommentIds = new();

    public void AddComment(CommentDto c)
    {
        if (!_seenCommentIds.Add(c.Id)) return; // already applied (e.g. own comment + SignalR echo)
        CommentCount++;
        var preview = $"@{c.AuthorUsername}: {c.Text}";
        var list = CommentPreviews.ToList();
        list.Add(preview);
        if (list.Count > 3) list.RemoveAt(0);
        CommentPreviews = list;
    }
    public bool IsMyPost => AppState.Instance.ServerUserId == _post.AuthorId.ToString();
    public bool IsNotMyPost => !IsMyPost;

    [ObservableProperty] private bool   _menuOpen = false;
    [ObservableProperty] private string _menuStatus = "";

    public ServerPostCardViewModel(PostDto post, MainWindowViewModel main)
    {
        _post = post;
        _main = main;
        Refresh();
    }

    void Refresh()
    {
        LikeCount    = _post.LikeCount;
        IsLiked      = _post.IsLiked;
        LikeLabel    = MakeLikeLabel(_post.IsLiked, _post.LikeCount);
        CommentCount = _post.Comments?.Count() ?? 0;
        CommentPreviews = _post.Comments?
            .TakeLast(3)
            .Select(c => $"@{c.AuthorUsername}: {c.Text}")
            .ToList() ?? new();
        // Seed seen IDs so SignalR echoes of already-loaded comments are ignored
        if (_post.Comments != null)
            foreach (var c in _post.Comments)
                _seenCommentIds.Add(c.Id);
    }

    [RelayCommand]
    async Task ToggleLike()
    {
        var (liked, count) = await ServerClient.Instance.ToggleLikeAsync(_post.Id);
        LikeCount = count;
        IsLiked   = liked;
        LikeLabel = MakeLikeLabel(liked, count);
    }

    [RelayCommand]
    void OpenAuthorProfile()
    {
        if (Guid.TryParse(_post.AuthorId.ToString(), out var id))
            _main.Navigate(new ServerProfileViewModel(_main, id));
    }

    [RelayCommand]
    void ToggleMenu() => MenuOpen = !MenuOpen;

    [RelayCommand]
    void CloseMenu() => MenuOpen = false;

    [RelayCommand]
    void SharePost()
    {
        MenuOpen = false;
        var url = ServerClient.Instance.BaseUrl.TrimEnd('/') + $"/posts/{_post.Id}";
        MenuStatus = $"Link: {url}";
    }

    [RelayCommand]
    async Task ReportPost()
    {
        MenuOpen = false;
        await ServerClient.Instance.ReportPostAsync(_post.Id, "Reported by user");
        MenuStatus = "Reported. Thank you.";
    }

    [RelayCommand]
    async Task DeletePost()
    {
        MenuOpen = false;
        var ok = await ServerClient.Instance.DeletePostAsync(_post.Id);
        if (ok) MenuStatus = "deleted";
    }

    [RelayCommand]
    async Task AddComment()
    {
        var text = CommentText.Trim();
        if (string.IsNullOrEmpty(text)) return;
        var c = await ServerClient.Instance.AddCommentAsync(_post.Id, text);
        if (c != null)
        {
            CommentText = "";
            AddComment(c);
        }
    }

}
