using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public partial class FriendRowViewModel : ViewModelBase
{
    public Guid   UserId      { get; init; }
    public string Username    { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AccentColor { get; init; } = "#8b5cf6";
    public bool   IsVerified  { get; init; }
    public bool   IsMaster    { get; init; }
    public string AvatarLetter => string.IsNullOrEmpty(DisplayName) ? "?" : DisplayName[0].ToString().ToUpper();
    public string AtUsername   => $"@{Username}";
}

public partial class FriendRequestRowViewModel : ViewModelBase
{
    public Guid   RequestId   { get; init; }
    public Guid   UserId      { get; init; }
    public string Username    { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AccentColor { get; init; } = "#8b5cf6";
    public string AvatarLetter => string.IsNullOrEmpty(DisplayName) ? "?" : DisplayName[0].ToString().ToUpper();
    public bool   IsIncoming  { get; init; }
    [ObservableProperty] private bool _isActioned;
    [ObservableProperty] private bool _isNotActioned = true;
    [ObservableProperty] private string _actionResult = "";

    partial void OnIsActionedChanged(bool value) => IsNotActioned = !value;
}

public partial class FriendsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    // Tabs: 0=Friends, 1=Incoming, 2=Sent
    [ObservableProperty] private int  _activeTab = 0;
    [ObservableProperty] private bool _showFriends  = true;
    [ObservableProperty] private bool _showIncoming;
    [ObservableProperty] private bool _showOutgoing;

    public ObservableCollection<FriendRowViewModel>       Friends  { get; } = [];
    public ObservableCollection<FriendRequestRowViewModel> Incoming { get; } = [];
    public ObservableCollection<FriendRequestRowViewModel> Outgoing { get; } = [];

    public bool HasFriends  => Friends.Count  > 0;
    public bool HasIncoming => Incoming.Count > 0;
    public bool HasOutgoing => Outgoing.Count > 0;
    public bool NoFriends   => Friends.Count  == 0;
    public bool NoIncoming  => Incoming.Count == 0;
    public bool NoOutgoing  => Outgoing.Count == 0;

    public FriendsViewModel(MainWindowViewModel main)
    {
        _main = main;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!AppState.Instance.IsServerMode) return;

        var friendsTask  = ServerClient.Instance.GetFriendsAsync();
        var incomingTask = ServerClient.Instance.GetIncomingRequestsAsync();
        var outgoingTask = ServerClient.Instance.GetOutgoingRequestsAsync();
        await Task.WhenAll(friendsTask, incomingTask, outgoingTask);

        Friends.Clear();
        if (friendsTask.Result != null)
            foreach (var f in friendsTask.Result)
                Friends.Add(new FriendRowViewModel
                {
                    UserId = f.Id, Username = f.Username, DisplayName = f.DisplayName,
                    AccentColor = f.AccentColor, IsVerified = f.IsVerified, IsMaster = f.IsMaster
                });

        Incoming.Clear();
        if (incomingTask.Result != null)
            foreach (var r in incomingTask.Result)
                Incoming.Add(new FriendRequestRowViewModel
                {
                    RequestId = r.Id, UserId = r.SenderId,
                    Username = r.Username, DisplayName = r.DisplayName,
                    AccentColor = r.AccentColor, IsIncoming = true
                });

        Outgoing.Clear();
        if (outgoingTask.Result != null)
            foreach (var r in outgoingTask.Result)
                Outgoing.Add(new FriendRequestRowViewModel
                {
                    RequestId = r.Id, UserId = r.RecipientId,
                    Username  = r.Username, DisplayName = r.DisplayName,
                    AccentColor = "#555555", IsIncoming = false
                });

        OnPropertyChanged(nameof(HasFriends));
        OnPropertyChanged(nameof(HasIncoming));
        OnPropertyChanged(nameof(HasOutgoing));
        OnPropertyChanged(nameof(NoFriends));
        OnPropertyChanged(nameof(NoIncoming));
        OnPropertyChanged(nameof(NoOutgoing));
    }

    [RelayCommand] void ShowFriendsTab()  { ShowFriends = true; ShowIncoming = false; ShowOutgoing = false; }
    [RelayCommand] void ShowIncomingTab() { ShowFriends = false; ShowIncoming = true;  ShowOutgoing = false; }
    [RelayCommand] void ShowOutgoingTab() { ShowFriends = false; ShowIncoming = false; ShowOutgoing = true;  }

    [RelayCommand]
    async Task Accept(FriendRequestRowViewModel row)
    {
        if (row.IsActioned) return;
        await ServerClient.Instance.AcceptFriendRequestAsync(row.RequestId);
        row.ActionResult = "Accepted!";
        row.IsActioned   = true;
        _ = LoadAsync();
    }

    [RelayCommand]
    async Task Decline(FriendRequestRowViewModel row)
    {
        if (row.IsActioned) return;
        await ServerClient.Instance.DeclineFriendRequestAsync(row.RequestId);
        row.ActionResult = "Declined";
        row.IsActioned   = true;
    }

    [RelayCommand]
    void OpenProfile(FriendRowViewModel row) =>
        _main.Navigate(new ServerProfileViewModel(_main, row.UserId));

    [RelayCommand]
    void Call(FriendRowViewModel row) =>
        _main.Navigate(new CallViewModel(_main, row.UserId, row.DisplayName));

    [RelayCommand]
    void Message(FriendRowViewModel row) => _ = OpenDmAsync(row);

    private async Task OpenDmAsync(FriendRowViewModel row)
    {
        var conv = await ServerClient.Instance.GetOrCreateDmAsync(row.UserId);
        if (conv != null)
            _main.Navigate(new ConversationViewModel(_main,
                new InstogramDependencies.User
                {
                    Id = row.UserId, Username = row.Username,
                    DisplayName = row.DisplayName, AccentColor = row.AccentColor
                }));
    }

    [RelayCommand]
    void Back() => _main.Navigate(new NotificationsViewModel(_main));
}

// Keep old name as alias so nothing breaks
public partial class FriendRequestViewModel : FriendsViewModel
{
    public FriendRequestViewModel(MainWindowViewModel main) : base(main) { }
}
