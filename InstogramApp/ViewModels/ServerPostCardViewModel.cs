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

    public string AuthorDisplayName => _post.AuthorDisplayName;
    public string AuthorUsername    => $"@{_post.AuthorUsername}";
    public string AuthorInitial => Initial(_post.AuthorDisplayName);
    public string AuthorAccent  => _post.AuthorAccent;
    public string Caption       => _post.Caption;
    public string TimeLabel     => FormatAge(_post.CreatedAt);
    public string ImageUrl  => ResolveUrl(_post.ImageUrl);
    public bool   HasImage  => !string.IsNullOrEmpty(_post.ImageUrl);

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

    public int CommentCount => _post.Comments?.Count() ?? 0;

    public ServerPostCardViewModel(PostDto post, MainWindowViewModel main)
    {
        _post = post;
        _main = main;
        Refresh();
    }

    void Refresh()
    {
        LikeCount       = _post.LikeCount;
        IsLiked         = _post.IsLiked;
        LikeLabel       = MakeLikeLabel(_post.IsLiked, _post.LikeCount);
        CommentPreviews = _post.Comments?
            .TakeLast(3)
            .Select(c => $"@{c.AuthorUsername}: {c.Text}")
            .ToList() ?? new();
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
    async Task AddComment()
    {
        var text = CommentText.Trim();
        if (string.IsNullOrEmpty(text)) return;
        var c = await ServerClient.Instance.AddCommentAsync(_post.Id, text);
        if (c != null)
        {
            CommentText = "";
            var preview = $"@{c.AuthorUsername}: {c.Text}";
            var list = new List<string>(CommentPreviews) { preview };
            if (list.Count > 3) list = list.TakeLast(3).ToList();
            CommentPreviews = list;
            OnPropertyChanged(nameof(CommentCount));
        }
    }

}
