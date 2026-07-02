using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public class NotificationRowViewModel : ViewModelBase
{
    public string Body    { get; }
    public string Time    { get; }
    public bool   IsRead  { get; }
    public bool   IsNew   => !IsRead;
    public string Icon    { get; }

    public NotificationRowViewModel(NotifDto n)
    {
        Body   = n.Body;
        IsRead = n.IsRead;
        Time   = FormatAge(n.CreatedAt);
        Icon   = n.Type switch
        {
            "dm"      => "✉",
            "post"    => "📸",
            "follow"  => "👤",
            "like"    => "♥",
            "comment" => "💬",
            "friend"  => "👋",
            _         => "🔔"
        };
    }
}

public partial class NotificationsViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowViewModel _main;
    private bool _hasUnread;

    public ObservableCollection<NotificationRowViewModel> Items { get; } = new();
    [ObservableProperty] private bool   _hasNotifications;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _emptyLabel = "You're all caught up!";

    public NotificationsViewModel(MainWindowViewModel main)
    {
        _main = main;
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        IsLoading = true;
        try
        {
            var list = await ServerClient.Instance.GetNotificationsAsync();

            Items.Clear();
            _hasUnread = false;
            if (list != null)
                foreach (var n in list)
                {
                    Items.Add(new NotificationRowViewModel(n));
                    if (!n.IsRead) _hasUnread = true;
                }

            HasNotifications = Items.Count > 0;
        }
        catch { }
        finally { IsLoading = false; }
    }

    // Called when the user navigates away — mark read only after they've seen the list
    public void Dispose()
    {
        if (!_hasUnread) return;
        _ = ServerClient.Instance.MarkAllNotificationsReadAsync();
        _main.ClearNotificationBadge();
    }
}
