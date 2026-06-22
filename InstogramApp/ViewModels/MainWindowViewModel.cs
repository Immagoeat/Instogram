using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool   _isLoggedIn;
    [ObservableProperty] private string _loggedInAs      = "";
    [ObservableProperty] private string _loggedInDisplay = "";

    [ObservableProperty] private int  _notificationCount;

    public bool IsNotLoggedIn   => !IsLoggedIn;
    public bool HasNotifications => NotificationCount > 0;
    [ObservableProperty] private bool _isMaster;

    // Sidebar avatar
    [ObservableProperty] private string _sidebarAvatarPath  = "";
    [ObservableProperty] private string _sidebarAccent      = "#8b5cf6";
    [ObservableProperty] private bool   _sidebarHasAvatar;
    [ObservableProperty] private bool   _sidebarHasNoAvatar = true;

    private bool _handlersRegistered;

    public MainWindowViewModel()
    {
        _currentPage = new AuthViewModel(this);
    }

    public void Navigate(ViewModelBase page) => CurrentPage = page;

    public void OnServerLogin(string username, string displayName)
    {
        IsLoggedIn = true;
        IsMaster   = AppState.Instance.ServerIsMaster;
        OnPropertyChanged(nameof(IsNotLoggedIn));
        LoggedInAs      = $"@{username}";
        LoggedInDisplay = displayName;
        NotificationCount = 0;
        OnPropertyChanged(nameof(HasNotifications));
        RefreshSidebarAvatar();

        if (!_handlersRegistered)
        {
            _handlersRegistered = true;
            ServerClient.Instance.OnNotificationCount += count =>
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    NotificationCount = count;
                    OnPropertyChanged(nameof(HasNotifications));
                });

            ServerClient.Instance.OnIncomingCall += (callerId, callerName, sdpOffer) =>
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    Navigate(new CallViewModel(this, callerId, callerName, sdpOffer)));
        }

        Navigate(new FeedViewModel(this));
    }

    public void ClearNotificationBadge()
    {
        NotificationCount = 0;
        OnPropertyChanged(nameof(HasNotifications));
    }

    public void RefreshSidebarAvatar()
    {
        SidebarAccent = AppState.Instance.ServerAccent.Length > 0
            ? AppState.Instance.ServerAccent : "#8b5cf6";

        var path = AppState.Instance.CurrentUser?.AvatarPath ?? "";
        SidebarAvatarPath  = path;
        SidebarHasAvatar   = !string.IsNullOrEmpty(path);
        SidebarHasNoAvatar = !SidebarHasAvatar;
    }

    public void OnLogout()
    {
        AppState.Instance.IsServerMode = false;
        AppState.Instance.CurrentUser  = null;
        ServerConfig.Clear();
        IsLoggedIn = false;
        IsMaster   = false;
        OnPropertyChanged(nameof(IsNotLoggedIn));
        LoggedInAs = ""; LoggedInDisplay = "";
        NotificationCount = 0;
        OnPropertyChanged(nameof(HasNotifications));
        Navigate(new AuthViewModel(this));
    }

    [RelayCommand] void GoFeed()          => Navigate(new FeedViewModel(this));
    [RelayCommand] void GoExplore()       => Navigate(new ExploreViewModel(this));
    [RelayCommand] void GoNewPost()       => Navigate(new NewPostViewModel(this));
    [RelayCommand] void GoProfile()
    {
        if (AppState.Instance.IsServerMode &&
            Guid.TryParse(AppState.Instance.ServerUserId, out var myId))
            Navigate(new ServerProfileViewModel(this, myId));
        else
            Navigate(new ProfileViewModel(this, AppState.Instance.CurrentUser!));
    }
    [RelayCommand] void GoAdmin()          => Navigate(new AdminViewModel(this));
    [RelayCommand] void GoDMs()           => Navigate(new DMListViewModel(this));
    [RelayCommand] void GoNotifications() => Navigate(new NotificationsViewModel(this));
    [RelayCommand] void GoNewStory()      => Navigate(new StoryComposeViewModel(this));
    [RelayCommand] void GoFriends()       => Navigate(new FriendsViewModel(this));
    [RelayCommand] void GoSettings()      => Navigate(new SettingsViewModel(this));
    [RelayCommand] void Logout()          => OnLogout();
}
