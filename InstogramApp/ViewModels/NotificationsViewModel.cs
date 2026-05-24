using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

public class NotificationRowViewModel
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
        var age = DateTime.UtcNow - n.CreatedAt;
        Time = age.TotalMinutes < 1  ? "just now"
             : age.TotalHours   < 1  ? $"{(int)age.TotalMinutes}m ago"
             : age.TotalDays    < 1  ? $"{(int)age.TotalHours}h ago"
             : age.TotalDays    < 7  ? $"{(int)age.TotalDays}d ago"
             : n.CreatedAt.ToLocalTime().ToString("MMM d");
    }
}

public partial class NotificationsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

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
            await ServerClient.Instance.MarkAllNotificationsReadAsync();

            Items.Clear();
            if (list != null)
                foreach (var n in list)
                    Items.Add(new NotificationRowViewModel(n));

            HasNotifications = Items.Count > 0;

            _main.NotificationCount = 0;
            _main.HasNotifications  = false;
        }
        catch { }
        finally { IsLoading = false; }
    }
}
