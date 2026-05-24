using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentPage;
    [ObservableProperty] private bool   _isLoggedIn;
    [ObservableProperty] private bool   _isNotLoggedIn = true;
    [ObservableProperty] private string _loggedInAs      = "";
    [ObservableProperty] private string _loggedInDisplay = "";

    // Notification badge
    [ObservableProperty] private int  _notificationCount;
    [ObservableProperty] private bool _hasNotifications;

    // Sidebar avatar
    [ObservableProperty] private string _sidebarAvatarPath  = "";
    [ObservableProperty] private string _sidebarAccent      = "#8b5cf6";
    [ObservableProperty] private bool   _sidebarHasAvatar;
    [ObservableProperty] private bool   _sidebarHasNoAvatar = true;

    public MainWindowViewModel()
    {
        _currentPage = new AuthViewModel(this);
    }

    public void Navigate(ViewModelBase page) => CurrentPage = page;

    public void OnServerLogin(string username, string displayName)
    {
        IsLoggedIn = true; IsNotLoggedIn = false;
        LoggedInAs      = $"@{username}";
        LoggedInDisplay = displayName;
        NotificationCount = 0; HasNotifications = false;
        RefreshSidebarAvatar();

        ServerClient.Instance.OnNotificationCount += count =>
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                NotificationCount = count;
                HasNotifications  = count > 0;
            });

        ServerClient.Instance.OnIncomingCall += (callerId, callerName, sdpOffer) =>
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                Navigate(new CallViewModel(this, callerId, callerName, sdpOffer)));

        Navigate(new FeedViewModel(this));
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
        IsLoggedIn = false; IsNotLoggedIn = true;
        LoggedInAs = ""; LoggedInDisplay = "";
        NotificationCount = 0; HasNotifications = false;
        Navigate(new AuthViewModel(this));
    }

    [RelayCommand] void GoFeed()          => Navigate(new FeedViewModel(this));
    [RelayCommand] void GoExplore()       => Navigate(new ExploreViewModel(this));
    [RelayCommand] void GoNewPost()       => Navigate(new NewPostViewModel(this));
    [RelayCommand] void GoProfile()       => Navigate(new ProfileViewModel(this, AppState.Instance.CurrentUser!));
    [RelayCommand] void GoDMs()           => Navigate(new DMListViewModel(this));
    [RelayCommand] void GoNotifications() => Navigate(new NotificationsViewModel(this));
    [RelayCommand] void GoNewStory()      => Navigate(new StoryComposeViewModel(this));
    [RelayCommand] void GoFriends()       => Navigate(new FriendRequestViewModel(this));
    [RelayCommand] void Logout()          => OnLogout();
}
