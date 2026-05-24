using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramDependencies;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class NewPostViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _caption = "";
    [ObservableProperty] private string _status  = "";

    public NewPostViewModel(MainWindowViewModel main) => _main = main;

    [RelayCommand]
    void Submit()
    {
        var me   = AppState.Instance.CurrentUser!;
        var text = Caption.Trim();
        if (string.IsNullOrEmpty(text)) { Status = "Caption cannot be empty."; return; }

        var tags = text.Split(' ')
                       .Where(w => w.StartsWith('#') && w.Length > 1)
                       .Select(w => w.TrimStart('#').ToLower())
                       .ToList();

        var post = AppState.Instance.Posts.CreatePost(me, text, MediaType.Text, "", tags);

        // Notify each follower who has opted in
        var snippet = text.Length > 50 ? text[..47] + "…" : text;
        foreach (var followerId in me.Followers)
        {
            var follower = AppState.Instance.Accounts.GetById(followerId);
            if (follower == null) continue;
            AppState.Instance.PushNotification(follower, me,
                NotificationType.NewPost,
                $"@{me.Username} posted: {snippet}",
                post.Id);
        }

        AppState.Instance.Save();
        Status  = "Posted!";
        Caption = "";
        _main.Navigate(new FeedViewModel(_main));
    }

    [RelayCommand] void Cancel() => _main.Navigate(new FeedViewModel(_main));
}
