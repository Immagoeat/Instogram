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
    [ObservableProperty] private bool   _isMaster;
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

    [RelayCommand] async Task Ban()     => await _parent.BanUserAsync(Id, BanReason);
    [RelayCommand] async Task Unban()   => await _parent.UnbanUserAsync(Id);
    [RelayCommand] async Task Promote() => await _parent.PromoteUserAsync(Id);
    [RelayCommand] async Task Demote()  => await _parent.DemoteUserAsync(Id);
}

public partial class AdminPostRowViewModel : ViewModelBase
{
    private readonly AdminViewModel _parent;
    public Guid   Id           { get; }
    public string Caption      { get; }
    public string AuthorName   { get; }
    public string TimeLabel    { get; }
    public string HasMedia     { get; }
    public int    CommentCount { get; }
    [ObservableProperty] private bool _expanded;
    public ObservableCollection<AdminCommentRowViewModel> Comments { get; } = new();

    public AdminPostRowViewModel(AdminPostDto dto, AdminViewModel parent)
    {
        _parent      = parent;
        Id           = dto.Id;
        Caption      = string.IsNullOrWhiteSpace(dto.Caption) ? "(no caption)" : dto.Caption;
        AuthorName   = $"@{dto.AuthorName}";
        TimeLabel    = FormatAge(dto.CreatedAt);
        CommentCount = dto.CommentCount;
        HasMedia     = !string.IsNullOrEmpty(dto.ImageUrl) ? "📷 Photo"
                     : !string.IsNullOrEmpty(dto.VideoUrl) ? "🎥 Video" : "";
    }

    [RelayCommand] async Task DeletePost() => await _parent.DeleteAdminPostAsync(Id);

    [RelayCommand]
    async Task ToggleComments()
    {
        Expanded = !Expanded;
        if (Expanded && Comments.Count == 0)
            await _parent.LoadPostCommentsAsync(this);
    }
}

public partial class AdminCommentRowViewModel : ViewModelBase
{
    private readonly AdminViewModel _parent;
    public Guid   Id         { get; }
    public string AuthorName { get; }
    public string Text       { get; }
    public string TimeLabel  { get; }

    public AdminCommentRowViewModel(AdminCommentDto dto, AdminViewModel parent)
    {
        _parent    = parent;
        Id         = dto.Id;
        AuthorName = $"@{dto.AuthorName}";
        Text       = dto.Text;
        TimeLabel  = FormatAge(dto.CreatedAt);
    }

    [RelayCommand] async Task Delete() => await _parent.DeleteAdminCommentAsync(Id, this);
}

public partial class AdminReportRowViewModel : ViewModelBase
{
    public Guid   Id            { get; }
    public string Body          { get; }
    public string TimeLabel     { get; }
    public Guid?  RelatedPostId { get; }

    public AdminReportRowViewModel(AdminReportDto dto)
    {
        Id            = dto.Id;
        Body          = dto.Body;
        TimeLabel     = FormatAge(dto.CreatedAt);
        RelatedPostId = dto.RelatedPostId;
    }
}

// ── Main AdminViewModel ───────────────────────────────────────────────────────

public partial class AdminViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

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

    // ── Posts tab ─────────────────────────────────────────────────────────────
    public ObservableCollection<AdminPostRowViewModel> AdminPosts { get; } = new();
    [ObservableProperty] private string _postSearch  = "";
    [ObservableProperty] private bool   _postsBusy;
    [ObservableProperty] private string _postsStatus = "";

    // ── Reports tab ───────────────────────────────────────────────────────────
    public ObservableCollection<AdminReportRowViewModel> Reports { get; } = new();
    [ObservableProperty] private bool   _reportsBusy;
    [ObservableProperty] private string _reportsStatus = "";

    // ── Claim master (shown when server returns 403) ───────────────────────────
    [ObservableProperty] private string _claimStatus = "";
    [ObservableProperty] private bool   _showClaimButton;
    [ObservableProperty] private string _serverVersion = "";

    public AdminViewModel(MainWindowViewModel main)
    {
        _main = main;
        _ = LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        // Ping first so we can show which server build is running
        ServerVersion = $"server {await ServerClient.Instance.PingAsync()}";

        await Task.WhenAll(
            LoadFlagsAsync(), LoadWordsAsync(),
            LoadUsersAsync(), LoadPostsAsync(), LoadReportsAsync());

        bool allFailed = FlagsStatus.StartsWith("Error") && WordsStatus.StartsWith("Error")
                      && UsersStatus.StartsWith("Error");

        if (!allFailed) { ShowClaimButton = false; ClaimStatus = ""; return; }

        if (FlagsStatus.Contains("404"))
        {
            ShowClaimButton = false;
            ClaimStatus = "";
            var msg = $"Server returned 404 — admin routes missing. Server version: {ServerVersion}. Make sure the server is running the latest build.";
            FlagsStatus = WordsStatus = UsersStatus = PostsStatus = ReportsStatus = msg;
        }
        else if (FlagsStatus.Contains("403"))
        {
            ShowClaimButton = true;
        }
    }

    [RelayCommand]
    async Task ClaimMaster()
    {
        ClaimStatus = "Claiming…";
        var error = await ServerClient.Instance.ClaimMasterAsync();
        if (error == null)
        {
            AppState.Instance.ServerIsMaster = true;
            ClaimStatus = "You are now master. Reloading…";
            ShowClaimButton = false;
            await LoadAllAsync();
        }
        else if (error.StartsWith("400"))
        {
            ClaimStatus = "A master already exists — ask them to promote you.";
        }
        else if (error.StartsWith("404"))
        {
            ClaimStatus = "Server is outdated — restart it with the latest build first.";
            ShowClaimButton = false;
        }
        else
        {
            ClaimStatus = $"Could not claim: {error}";
        }
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
        catch (Exception ex) { FlagsStatus = $"Error: {ex.Message}"; }
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
            if (row != null) Flags.Remove(row);
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
        catch (Exception ex) { WordsStatus = $"Error: {ex.Message}"; }
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
        catch (Exception ex) { UsersStatus = $"Error: {ex.Message}"; }
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

    public async Task PromoteUserAsync(Guid userId)
    {
        var ok = await ServerClient.Instance.PromoteUserAsync(userId);
        UsersStatus = ok ? "User promoted to master." : "Failed to promote user.";
        if (ok) await LoadUsersAsync();
    }

    public async Task DemoteUserAsync(Guid userId)
    {
        var ok = await ServerClient.Instance.DemoteUserAsync(userId);
        UsersStatus = ok ? "User demoted." : "Failed to demote user.";
        if (ok) await LoadUsersAsync();
    }

    // ── Posts ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task SearchPosts()
    {
        PostsBusy = true;
        PostsStatus = "";
        try
        {
            var q    = PostSearch.Trim();
            var list = await ServerClient.Instance.GetAdminPostsAsync(string.IsNullOrEmpty(q) ? null : q);
            AdminPosts.Clear();
            if (list != null) foreach (var p in list) AdminPosts.Add(new AdminPostRowViewModel(p, this));
        }
        catch (Exception ex) { PostsStatus = $"Error: {ex.Message}"; }
        finally { PostsBusy = false; }
    }

    private Task LoadPostsAsync() => SearchPostsCommand.ExecuteAsync(null);

    public async Task DeleteAdminPostAsync(Guid postId)
    {
        var ok = await ServerClient.Instance.DeletePostAsync(postId);
        PostsStatus = ok ? "Post deleted." : "Failed to delete post.";
        if (ok)
        {
            AdminPostRowViewModel? row = null;
            foreach (var p in AdminPosts) if (p.Id == postId) { row = p; break; }
            if (row != null) AdminPosts.Remove(row);
        }
    }

    public async Task LoadPostCommentsAsync(AdminPostRowViewModel post)
    {
        var list = await ServerClient.Instance.GetAdminPostCommentsAsync(post.Id);
        post.Comments.Clear();
        if (list != null)
            foreach (var c in list) post.Comments.Add(new AdminCommentRowViewModel(c, this));
    }

    public async Task DeleteAdminCommentAsync(Guid commentId, AdminCommentRowViewModel row)
    {
        var ok = await ServerClient.Instance.DeleteAdminCommentAsync(commentId);
        if (!ok) { PostsStatus = "Failed to delete comment."; return; }
        foreach (var p in AdminPosts)
        {
            if (p.Comments.Contains(row)) { p.Comments.Remove(row); break; }
        }
        PostsStatus = "Comment deleted.";
    }

    // ── Reports ───────────────────────────────────────────────────────────────

    [RelayCommand]
    async Task LoadReports()
    {
        ReportsBusy = true;
        ReportsStatus = "";
        try
        {
            var list = await ServerClient.Instance.GetAdminReportsAsync();
            Reports.Clear();
            if (list != null) foreach (var r in list) Reports.Add(new AdminReportRowViewModel(r));
        }
        catch (Exception ex) { ReportsStatus = $"Error: {ex.Message}"; }
        finally { ReportsBusy = false; }
    }

    private Task LoadReportsAsync() => LoadReportsCommand.ExecuteAsync(null);
}
