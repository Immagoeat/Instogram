using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

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

public partial class FriendRequestViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private bool _showIncoming = true;
    [ObservableProperty] private bool _showOutgoing;

    public ObservableCollection<FriendRequestRowViewModel> Incoming { get; } = [];
    public ObservableCollection<FriendRequestRowViewModel> Outgoing { get; } = [];

    public FriendRequestViewModel(MainWindowViewModel main)
    {
        _main = main;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!AppState.Instance.IsServerMode) return;

        var incoming = await ServerClient.Instance.GetIncomingRequestsAsync();
        Incoming.Clear();
        if (incoming != null)
            foreach (var r in incoming)
                Incoming.Add(new FriendRequestRowViewModel
                {
                    RequestId = r.Id, UserId = r.SenderId,
                    Username = r.Username, DisplayName = r.DisplayName,
                    AccentColor = r.AccentColor, IsIncoming = true
                });

        var outgoing = await ServerClient.Instance.GetOutgoingRequestsAsync();
        Outgoing.Clear();
        if (outgoing != null)
            foreach (var r in outgoing)
                Outgoing.Add(new FriendRequestRowViewModel
                {
                    RequestId = r.Id, UserId = r.RecipientId,
                    Username  = r.Username, DisplayName = r.DisplayName,
                    AccentColor = "#555555", IsIncoming = false
                });
    }

    [RelayCommand]
    async Task Accept(FriendRequestRowViewModel row)
    {
        if (row.IsActioned) return;
        await ServerClient.Instance.AcceptFriendRequestAsync(row.RequestId);
        row.ActionResult = "Accepted!";
        row.IsActioned   = true;
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
    void ShowIncomingTab()  { ShowIncoming = true; ShowOutgoing = false; }

    [RelayCommand]
    void ShowOutgoingTab()  { ShowIncoming = false; ShowOutgoing = true; }

    [RelayCommand]
    void Back() => _main.Navigate(new NotificationsViewModel(_main));
}
