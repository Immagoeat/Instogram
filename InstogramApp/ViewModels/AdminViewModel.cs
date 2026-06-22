using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;

namespace InstogramApp.ViewModels;

// ── Row VMs ───────────────────────────────────────────────────────────────────

public partial class BannedWordRowViewModel : ViewModelBase
{
    private readonly AdminViewModel _parent;
    public Guid   Id      { get; }
    public string Word    { get; }
    public string AddedBy { get; }

    public BannedWordRowViewModel(BannedWordDto dto, AdminViewModel parent)
    {
        _parent = parent;
        Id      = dto.Id;
        Word    = dto.Word;
        AddedBy = $"added by @{dto.AddedBy}";
    }

    [RelayCommand]
    async Task Remove() => await _parent.RemoveWordAsync(Id);
}

public partial class FlagRowViewModel : ViewModelBase
{
    private readonly AdminViewModel _parent;
    public Guid   Id          { get; }
    public string AuthorName  { get; }
    public string ContentType { get; }
    public string Snippet     { get; }
    public string MatchedWord { get; }
    public string TimeLabel   { get; }
    public Guid?  ContentId   { get; }
    [ObservableProperty] private bool _isResolved;

    public FlagRowViewModel(AutomodFlagDto dto, AdminViewModel parent)
    {
        _parent      = parent;
        Id           = dto.Id;
        AuthorName   = $"@{dto.AuthorName}";
        ContentType  = dto.ContentType;
        Snippet      = dto.Snippet;
        MatchedWord  = dto.MatchedWord;
        TimeLabel    = FormatAge(dto.CreatedAt);
        ContentId    = dto.ContentId;
        IsResolved   = dto.IsResolved;
    }

    [RelayCommand] async Task Dismiss() => await _parent.ResolveFlagAsync(Id, "dismissed");
    [RelayCommand] async Task Delete()  => await _parent.ResolveFlagAsync(Id, "deleted");
}

public partial class AdminUserRowViewModel : ViewModelBase
{
    private readonly AdminViewModel _parent;
    public Guid   Id          { get; }
    public string Username    { get; }
    public string DisplayName { get; }
    public bool   IsMaster    { get; }
    [ObservableProperty] private bool   _isBanned;
    [ObservableProperty] private string _banReason = "";

    public AdminUserRowViewModel(AdminUserDto dto, AdminViewModel parent)
    {
        _parent      = parent;
        Id           = dto.Id;
        Username     = $"@{dto.Username}";
        DisplayName  = dto.DisplayName;
        IsMaster     = dto.IsMaster;
        IsBanned     = dto.IsBanned;
        BanReason    = dto.BanReason;
    }

    [RelayCommand] async Task Ban()   => await _parent.BanUserAsync(Id, BanReason);
    [RelayCommand] async Task Unban() => await _parent.UnbanUserAsync(Id);
}

// ── Main AdminViewModel ───────────────────────────────────────────────────────

public partial class AdminViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    // ── Tabs ──────────────────────────────────────────────────────────────────
    [ObservableProperty] private int _tabIndex = 0;

    // ── Flags tab ─────────────────────────────────────────────────────────────
    public ObservableCollection<FlagRowViewModel> Flags { get; } = new();
    [ObservableProperty] private bool   _showResolved;
    [ObservableProperty] private bool   _flagsBusy;
    [ObservableProperty] private string _flagsStatus = "";

    // ── Words tab ─────────────────────────────────────────────────────────────
    public ObservableCollection<BannedWordRowViewModel> Words { get; } = new();
    [ObservableProperty] private string _newWord    = "";
    [ObservableProperty] private bool   _wordsBusy;
    [ObservableProperty] private string _wordsStatus = "";

    // ── Users tab ─────────────────────────────────────────────────────────────
    public ObservableCollection<AdminUserRowViewModel> AdminUsers { get; } = new();
    [ObservableProperty] private string _userSearch  = "";
    [ObservableProperty] private bool   _usersBusy;
    [ObservableProperty] private string _usersStatus = "";

    public AdminViewModel(MainWindowViewModel main)
    {
        _main = main;
        _ = LoadFlagsAsync();
        _ = LoadWordsAsync();
        _ = LoadUsersAsync();
    }

    [RelayCommand] void Back() => _main.Navigate(new FeedViewModel(_main));

    // ── Flags ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task LoadFlags()
    {
        FlagsBusy = true;
        FlagsStatus = "";
        try
        {
            var list = await ServerClient.Instance.GetFlagsAsync(ShowResolved);
            Flags.Clear();
            if (list != null) foreach (var f in list) Flags.Add(new FlagRowViewModel(f, this));
        }
        catch { FlagsStatus = "Failed to load flags."; }
        finally { FlagsBusy = false; }
    }

    partial void OnShowResolvedChanged(bool value) { _ = LoadFlagsAsync(); }

    private Task LoadFlagsAsync() => LoadFlagsCommand.ExecuteAsync(null);

    public async Task ResolveFlagAsync(Guid flagId, string resolution)
    {
        var ok = await ServerClient.Instance.ResolveFlagAsync(flagId, resolution);
        if (ok)
        {
            var row = FindFlag(flagId);
            if (row != null) { Flags.Remove(row); }
            FlagsStatus = resolution == "deleted" ? "Content deleted." : "Flag dismissed.";
        }
        else FlagsStatus = "Failed to resolve flag.";
    }

    private FlagRowViewModel? FindFlag(Guid id)
    {
        foreach (var f in Flags) if (f.Id == id) return f;
        return null;
    }

    // ── Words ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task LoadWords()
    {
        WordsBusy = true;
        try
        {
            var list = await ServerClient.Instance.GetBannedWordsAsync();
            Words.Clear();
            if (list != null) foreach (var w in list) Words.Add(new BannedWordRowViewModel(w, this));
        }
        catch { WordsStatus = "Failed to load word list."; }
        finally { WordsBusy = false; }
    }

    private Task LoadWordsAsync() => LoadWordsCommand.ExecuteAsync(null);

    [RelayCommand]
    async Task AddWord()
    {
        var word = NewWord.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(word)) return;
        WordsBusy = true;
        WordsStatus = "";
        try
        {
            var ok = await ServerClient.Instance.AddBannedWordAsync(word);
            if (ok) { NewWord = ""; await LoadWordsAsync(); WordsStatus = $"'{word}' added."; }
            else WordsStatus = "Word already exists or error.";
        }
        catch { WordsStatus = "Failed to add word."; }
        finally { WordsBusy = false; }
    }

    public async Task RemoveWordAsync(Guid id)
    {
        var ok = await ServerClient.Instance.DeleteBannedWordAsync(id);
        if (ok) await LoadWordsAsync();
        else WordsStatus = "Failed to remove word.";
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task SearchUsers()
    {
        UsersBusy = true;
        UsersStatus = "";
        try
        {
            var q    = UserSearch.Trim();
            var list = await ServerClient.Instance.GetAdminUsersAsync(string.IsNullOrEmpty(q) ? null : q);
            AdminUsers.Clear();
            if (list != null) foreach (var u in list) AdminUsers.Add(new AdminUserRowViewModel(u, this));
        }
        catch { UsersStatus = "Failed to load users."; }
        finally { UsersBusy = false; }
    }

    private Task LoadUsersAsync() => SearchUsersCommand.ExecuteAsync(null);

    public async Task BanUserAsync(Guid userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) { UsersStatus = "Enter a ban reason."; return; }
        var ok = await ServerClient.Instance.BanUserAsync(userId, reason);
        UsersStatus = ok ? "User banned." : "Failed to ban user.";
        if (ok) await LoadUsersAsync();
    }

    public async Task UnbanUserAsync(Guid userId)
    {
        var ok = await ServerClient.Instance.UnbanUserAsync(userId);
        UsersStatus = ok ? "User unbanned." : "Failed to unban user.";
        if (ok) await LoadUsersAsync();
    }
}
