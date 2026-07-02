using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class ServerProfileViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly Guid _userId;

    [ObservableProperty] private string _displayName   = "";
    [ObservableProperty] private string _username      = "";
    [ObservableProperty] private string _bio           = "";
    [ObservableProperty] private string _website       = "";
    [ObservableProperty] private string _accentColor   = "#8b5cf6";
    [ObservableProperty] private string _initial       = "?";
    [ObservableProperty] private int    _followerCount;
    [ObservableProperty] private int    _followingCount;
    [ObservableProperty] private bool   _isFollowing;
    [ObservableProperty] private bool   _isOwnProfile;
    [ObservableProperty] private bool   _isLoading = true;
    [ObservableProperty] private string _followLabel       = "Follow";
    [ObservableProperty] private bool   _isVerified;
    [ObservableProperty] private bool   _isMaster;
    [ObservableProperty] private bool   _canVerify;
    [ObservableProperty] private string _verifyLabel       = "✓ Verify";
    [ObservableProperty] private string _friendRequestLabel = "Add Friend";
    [ObservableProperty] private bool   _friendRequestSent;

    public ObservableCollection<ServerPostCardViewModel> Posts { get; } = new();

    public ServerProfileViewModel(MainWindowViewModel main, Guid userId)
    {
        _main   = main;
        _userId = userId;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var profile = await ServerClient.Instance.GetUserAsync(_userId);
            if (profile == null) return;

            var u = profile.User;
            DisplayName    = u.DisplayName;
            Username       = u.Username;
            Bio            = u.Bio;
            Website        = u.Website;
            AccentColor    = u.AccentColor;
            Initial        = u.DisplayName.Length > 0 ? u.DisplayName[0].ToString().ToUpper() : "?";
            FollowerCount  = profile.FollowerCount;
            FollowingCount = profile.FollowingCount;
            IsFollowing    = profile.IsFollowing;
            FollowLabel    = IsFollowing ? "Following" : "Follow";
            IsVerified = u.IsVerified;
            IsMaster   = u.IsMaster;

            var me = AppState.Instance.ServerUserId;
            IsOwnProfile = !string.IsNullOrEmpty(me) &&
                           Guid.TryParse(me, out var myId) &&
                           myId == _userId;

            // Keep AppState in sync (handles case where it was stale from login)
            if (IsOwnProfile)
            {
                AppState.Instance.ServerIsVerified = u.IsVerified;
                AppState.Instance.ServerIsMaster   = u.IsMaster;
            }

            CanVerify   = AppState.Instance.ServerIsMaster && !IsOwnProfile;
            VerifyLabel = IsVerified ? "✓ Unverify" : "✓ Verify";

            if (profile.IsFriend)
            {
                FriendRequestSent  = true;
                FriendRequestLabel = "Friends";
            }
            else if (profile.HasPendingRequest)
            {
                FriendRequestSent  = true;
                FriendRequestLabel = "Request Sent";
            }

            var posts = await ServerClient.Instance.GetExploreAsync(q: u.Username);
            Posts.Clear();
            if (posts != null)
                foreach (var p in posts.FindAll(p => p.AuthorId == _userId))
                    Posts.Add(new ServerPostCardViewModel(p, _main));
        }
        catch { }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    async Task ToggleFollow()
    {
        try
        {
            if (IsFollowing)
            {
                await ServerClient.Instance.UnfollowAsync(_userId);
                IsFollowing   = false;
                FollowLabel   = "Follow";
                FollowerCount = Math.Max(0, FollowerCount - 1);
            }
            else
            {
                await ServerClient.Instance.FollowAsync(_userId);
                IsFollowing   = true;
                FollowLabel   = "Following";
                FollowerCount++;
            }
        }
        catch { }
    }

    [RelayCommand]
    async Task ToggleVerify()
    {
        try
        {
            await ServerClient.Instance.VerifyUserAsync(_userId);
            IsVerified  = !IsVerified;
            VerifyLabel = IsVerified ? "✓ Unverify" : "✓ Verify";
        }
        catch { }
    }

    [RelayCommand]
    async Task SendFriendRequest()
    {
        if (FriendRequestSent) return;
        try
        {
            await ServerClient.Instance.SendFriendRequestAsync(_userId);
            FriendRequestSent   = true;
            FriendRequestLabel  = "Request Sent";
        }
        catch { }
    }

    [RelayCommand] void Back() => _main.Navigate(new ExploreViewModel(_main));
    [RelayCommand] void SendMessage() => _ = OpenDmAsync();
    [RelayCommand] void StartCall() => _main.Navigate(new CallViewModel(_main, _userId, DisplayName));

    private async Task OpenDmAsync()
    {
        var conv = await ServerClient.Instance.GetOrCreateDmAsync(_userId);
        if (conv != null)
            _main.Navigate(new ConversationViewModel(_main,
                new InstogramDependencies.User
                {
                    Id          = _userId,
                    Username    = Username,
                    DisplayName = DisplayName,
                    AccentColor = AccentColor
                }));
    }
}
