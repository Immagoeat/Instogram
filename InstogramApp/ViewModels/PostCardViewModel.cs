using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramDependencies;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class PostCardViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public Post Post { get; }

    [ObservableProperty] private int    _likeCount;
    [ObservableProperty] private string _authorUsername    = "";
    [ObservableProperty] private string _authorDisplayName = "";
    [ObservableProperty] private string _authorInitial     = "";
    [ObservableProperty] private string _likeLabel         = "";
    [ObservableProperty] private string _commentText       = "";
    [ObservableProperty] private List<string> _commentPreviews = new();

    public PostCardViewModel(Post post, MainWindowViewModel main)
    {
        Post  = post;
        _main = main;
        Refresh();
    }

    public void Refresh()
    {
        var author = AppState.Instance.Accounts.GetById(Post.AuthorId);
        AuthorUsername    = author?.Username    ?? "unknown";
        AuthorDisplayName = author?.DisplayName ?? "Unknown";
        AuthorInitial     = Initial(AuthorDisplayName);

        var me = AppState.Instance.CurrentUser;
        var liked = me != null && Post.Likes.Contains(me.Id);
        LikeCount = Post.Likes.Count;
        LikeLabel = MakeLikeLabel(liked, LikeCount);

        CommentPreviews = Post.Comments
            .TakeLast(3)
            .Select(c =>
            {
                var ca = AppState.Instance.Accounts.GetById(c.AuthorId);
                return $"@{ca?.Username ?? "?"}: {c.Text}";
            })
            .ToList();
    }

    [RelayCommand]
    void ToggleLike()
    {
        var me = AppState.Instance.CurrentUser;
        if (me == null) return;
        if (Post.Likes.Contains(me.Id))
            AppState.Instance.Posts.Unlike(Post, me);
        else
            AppState.Instance.Posts.Like(Post, me);
        AppState.Instance.Save();
        Refresh();
    }

    [RelayCommand]
    void AddComment()
    {
        var me = AppState.Instance.CurrentUser;
        if (me == null || string.IsNullOrWhiteSpace(CommentText)) return;
        AppState.Instance.Posts.AddComment(Post, me, CommentText.Trim());
        AppState.Instance.Save();
        CommentText = "";
        Refresh();
    }

    [RelayCommand]
    void ViewAuthorProfile()
    {
        var author = AppState.Instance.Accounts.GetById(Post.AuthorId);
        if (author != null)
            _main.Navigate(new ProfileViewModel(_main, author));
    }
}
