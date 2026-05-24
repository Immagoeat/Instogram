using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramDependencies;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class ProfileViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public User ProfileUser { get; }

    [ObservableProperty] private bool   _isOwnProfile;
    [ObservableProperty] private bool   _isNotOwnProfile;
    [ObservableProperty] private bool   _isFollowing;
    [ObservableProperty] private int    _followerCount;
    [ObservableProperty] private int    _followingCount;
    [ObservableProperty] private int    _postCount;
    [ObservableProperty] private string _followButtonText = "Follow";
    [ObservableProperty] private string _avatarInitial    = "?";
    [ObservableProperty] private string _avatarPath       = "";
    [ObservableProperty] private bool   _hasAvatar;
    [ObservableProperty] private bool   _hasNoAvatar      = true;

    public ObservableCollection<PostCardViewModel> Posts         { get; } = new();
    public ObservableCollection<string>            FollowingNames { get; } = new();
    public ObservableCollection<string>            FollowerNames  { get; } = new();

    public ProfileViewModel(MainWindowViewModel main, User user)
    {
        _main = main;
        ProfileUser = user;
        Refresh();
    }

    public void Refresh()
    {
        var me = AppState.Instance.CurrentUser!;
        IsOwnProfile     = ProfileUser.Id == me.Id;
        IsNotOwnProfile  = !IsOwnProfile;
        IsFollowing      = me.IsFollowing(ProfileUser);
        FollowButtonText = IsFollowing ? "Unfollow" : "Follow";
        FollowerCount    = ProfileUser.Followers.Count;
        FollowingCount   = ProfileUser.Following.Count;
        AvatarInitial    = ProfileUser.DisplayName.Length > 0
                           ? ProfileUser.DisplayName[0].ToString().ToUpper()
                           : "?";
        AvatarPath  = ProfileUser.AvatarPath;
        HasAvatar   = !string.IsNullOrEmpty(AvatarPath) && System.IO.File.Exists(AvatarPath);
        HasNoAvatar = !HasAvatar;

        Posts.Clear();
        foreach (var p in AppState.Instance.Posts.GetPostsByUser(ProfileUser.Id))
            Posts.Add(new PostCardViewModel(p, _main));
        PostCount = Posts.Count;

        FollowingNames.Clear();
        foreach (var id in ProfileUser.Following)
        {
            var u = AppState.Instance.Accounts.GetById(id);
            if (u != null) FollowingNames.Add($"@{u.Username}");
        }

        FollowerNames.Clear();
        foreach (var id in ProfileUser.Followers)
        {
            var u = AppState.Instance.Accounts.GetById(id);
            if (u != null) FollowerNames.Add($"@{u.Username}");
        }
    }

    [RelayCommand]
    void ToggleFollow()
    {
        var me = AppState.Instance.CurrentUser!;
        if (IsFollowing) me.Unfollow(ProfileUser);
        else             me.Follow(ProfileUser);
        AppState.Instance.Save();
        Refresh();
    }

    [RelayCommand]
    void Message() => _main.Navigate(new ConversationViewModel(_main, ProfileUser));

    [RelayCommand]
    void EditProfile() => _main.Navigate(new EditProfileViewModel(_main));
}
