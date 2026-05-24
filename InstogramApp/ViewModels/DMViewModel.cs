using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramDependencies;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

// A single message bubble in the conversation view
public class MessageBubbleViewModel
{
    public string Text      { get; }
    public string Time      { get; }
    public bool   IsMine    { get; }
    public bool   IsNotMine => !IsMine;

    public MessageBubbleViewModel(DirectMessage msg, Guid myId)
    {
        Text   = msg.Text;
        Time   = msg.SentAt.ToLocalTime().ToString("HH:mm");
        IsMine = msg.SenderId == myId;
    }
}

// A row in the conversation list (inbox)
public partial class ConversationRowViewModel : ViewModelBase
{
    public User   Partner      { get; }
    public string LastSnippet  { get; }
    public string TimeLabel    { get; }
    public int    UnreadCount  { get; }
    public bool   HasUnread    => UnreadCount > 0;
    public string AvatarLetter => Partner.Username.Length > 0
                                  ? Partner.Username[0].ToString().ToUpper()
                                  : "?";

    public ConversationRowViewModel(User partner, DirectMessage? lastMsg, int unread)
    {
        Partner     = partner;
        UnreadCount = unread;
        LastSnippet = lastMsg?.Text.Length > 50
                      ? lastMsg.Text[..47] + "…"
                      : lastMsg?.Text ?? "";
        TimeLabel   = lastMsg != null
                      ? lastMsg.SentAt.ToLocalTime().ToString("MMM d")
                      : "";
    }
}

// Full conversation with one partner
public partial class ConversationViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    public User Partner { get; }
    public string Title  => $"@{Partner.Username}";
    public string AvatarLetter => Partner.Username.Length > 0
                                  ? Partner.Username[0].ToString().ToUpper()
                                  : "?";

    public ObservableCollection<MessageBubbleViewModel> Messages { get; } = new();
    [ObservableProperty] private string _newMessage = "";

    public ConversationViewModel(MainWindowViewModel main, User partner)
    {
        _main   = main;
        Partner = partner;
        Reload();
    }

    public void Reload()
    {
        var me = AppState.Instance.CurrentUser!;
        AppState.Instance.DMs.MarkRead(Partner.Id, me.Id);
        Messages.Clear();
        foreach (var msg in AppState.Instance.DMs.GetConversation(me.Id, Partner.Id))
            Messages.Add(new MessageBubbleViewModel(msg, me.Id));
    }

    [RelayCommand]
    void Send()
    {
        var text = NewMessage.Trim();
        if (string.IsNullOrEmpty(text)) return;
        var me = AppState.Instance.CurrentUser!;
        AppState.Instance.DMs.Send(me.Id, Partner.Id, text);
        // Notify recipient if they allow DM notifications
        AppState.Instance.PushNotification(Partner, me,
            InstogramDependencies.NotificationType.DM,
            $"@{me.Username} sent you a message: {(text.Length > 40 ? text[..37] + "…" : text)}");
        AppState.Instance.Save();
        NewMessage = "";
        Reload();
    }

    [RelayCommand]
    void Back() => _main.Navigate(new DMListViewModel(_main));

    [RelayCommand]
    void ViewProfile() => _main.Navigate(new ProfileViewModel(_main, Partner));
}

// Inbox / conversation list
public partial class DMListViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<ConversationRowViewModel> Conversations { get; } = new();
    [ObservableProperty] private string _searchQuery = "";
    [ObservableProperty] private bool   _noConversations;

    public DMListViewModel(MainWindowViewModel main)
    {
        _main = main;
        Reload();
    }

    public void Reload()
    {
        var me = AppState.Instance.CurrentUser!;
        Conversations.Clear();

        var partners = AppState.Instance.DMs.GetConversationPartners(me.Id);

        // Also include anyone we explicitly searched — but by default show existing convos
        foreach (var pid in partners)
        {
            var partner = AppState.Instance.Accounts.GetById(pid);
            if (partner == null) continue;
            var msgs    = AppState.Instance.DMs.GetConversation(me.Id, pid);
            var last    = msgs.LastOrDefault();
            var unread  = msgs.Count(m => m.RecipientId == me.Id && !m.IsRead);
            Conversations.Add(new ConversationRowViewModel(partner, last, unread));
        }

        NoConversations = Conversations.Count == 0;
    }

    [RelayCommand]
    void OpenConversation(ConversationRowViewModel row)
        => _main.Navigate(new ConversationViewModel(_main, row.Partner));

    [RelayCommand]
    void NewMessage()
    {
        // Navigate to user search within DMs
        _main.Navigate(new DMUserSearchViewModel(_main));
    }
}

// User search to start a new DM
public partial class DMUserSearchViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    [ObservableProperty] private string _query = "";
    public ObservableCollection<User> Results { get; } = new();
    [ObservableProperty] private bool _noResults;

    public DMUserSearchViewModel(MainWindowViewModel main) => _main = main;

    [RelayCommand]
    void Search()
    {
        var q = Query.Trim().ToLower();
        Results.Clear();
        var me = AppState.Instance.CurrentUser!;
        foreach (var u in AppState.Instance.Accounts.AllUsers()
                     .Where(u => u.Id != me.Id &&
                            (u.Username.ToLower().Contains(q) ||
                             u.DisplayName.ToLower().Contains(q)))
                     .Take(20))
            Results.Add(u);
        NoResults = Results.Count == 0 && !string.IsNullOrEmpty(q);
    }

    [RelayCommand]
    void StartConversation(User user)
        => _main.Navigate(new ConversationViewModel(_main, user));

    [RelayCommand]
    void Back() => _main.Navigate(new DMListViewModel(_main));
}
