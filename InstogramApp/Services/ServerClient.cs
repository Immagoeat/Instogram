using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace InstogramApp.Services;

// ── DTOs (mirror server responses) ───────────────────────────────────────────

public record UserDto(
    Guid Id, string Username, string DisplayName, string Bio, string Website,
    string AccentColor, string AvatarUrl, string Email, string Phone, string Address,
    bool IsVerified, bool IsMaster, DateTime CreatedAt);

public record PostDto(
    Guid Id, Guid AuthorId, string AuthorUsername, string AuthorDisplayName,
    string AuthorAccent, string Caption, string Tags, string ImageUrl, string VideoUrl, DateTime CreatedAt,
    int LikeCount, bool IsLiked, IEnumerable<CommentDto> Comments);

public record CommentDto(
    Guid Id, Guid PostId, Guid AuthorId, string AuthorUsername, string Text, DateTime CreatedAt);

public record StoryDto(
    Guid Id, Guid AuthorId, string AuthorUsername, string AuthorDisplayName,
    string AuthorAccent, string Text, string BackgroundColor, string ImageUrl, string VideoUrl,
    double TextX, double TextY, double TextScale, double TextRotation, string TaggedUsers,
    DateTime CreatedAt, DateTime ExpiresAt, bool HasSeen);

public record ConvDto(
    Guid Id, string Name, bool IsGroup, DateTime CreatedAt,
    IEnumerable<ConvMemberDto> Members, ConvLastMessageDto? LastMessage);

public record ConvMemberDto(
    Guid UserId, string Username, string DisplayName, string AccentColor, bool IsAdmin);

public record ConvLastMessageDto(Guid Id, Guid SenderId, string Text, DateTime SentAt);

public record MessageDto(
    Guid Id, Guid ConversationId, Guid SenderId, string SenderUsername,
    string Text, DateTime SentAt);

public record NotifDto(
    Guid Id, Guid ActorId, string Type, string Body,
    Guid? RelatedPostId, bool IsRead, DateTime CreatedAt);

public record FriendRequestDto(
    Guid Id, Guid SenderId, string Username, string DisplayName,
    string AccentColor, DateTime CreatedAt);

public record OutgoingFriendRequestDto(
    Guid Id, Guid RecipientId, string Username, string DisplayName, DateTime CreatedAt);

public record FriendDto(
    Guid Id, string Username, string DisplayName, string AccentColor,
    bool IsVerified = false, bool IsMaster = false);

public record UserProfileDto(UserDto User, int FollowerCount, int FollowingCount, bool IsFollowing,
    bool HasPendingRequest = false, bool IsFriend = false);

public record BannedWordDto(Guid Id, string Word, string AddedBy, DateTime AddedAt);

public record AutomodFlagDto(
    Guid Id, Guid AuthorId, string AuthorName, string ContentType,
    Guid? ContentId, string Snippet, string MatchedWord,
    bool IsResolved, string ResolvedBy, string Resolution, DateTime CreatedAt);

public record AdminUserDto(
    Guid Id, string Username, string DisplayName,
    bool IsVerified, bool IsMaster, bool IsBanned, string BanReason, DateTime CreatedAt);

public record AdminPostDto(
    Guid Id, string Caption, string Tags, string ImageUrl, string VideoUrl, DateTime CreatedAt,
    Guid AuthorId, string AuthorName, int CommentCount);

public record AdminCommentDto(
    Guid Id, string Text, DateTime CreatedAt, Guid AuthorId, string AuthorName);

public record AdminReportDto(
    Guid Id, string Body, Guid? RelatedPostId, DateTime CreatedAt, bool IsRead, Guid ReporterActorId);

// ── ServerClient ──────────────────────────────────────────────────────────────

public class ServerClient
{
    public static ServerClient Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private HttpClient _http = new();
    private HubConnection? _hub;
    private string _baseUrl = "";
    private string _token   = "";

    public string BaseUrl => _baseUrl;
    public bool   IsConnected => _hub?.State == HubConnectionState.Connected;

    // Fired when real-time events arrive — ViewModels subscribe to these
    public event Action<MessageDto>?        OnNewMessage;
    public event Action<int>?               OnNotificationCount;
    public event Action<Guid, string>?      OnUserTyping;
    public event Action<Guid, string, string>? OnIncomingCall;   // callerId, callerName, sdpOffer
    public event Action<Guid, string>?      OnCallAnswered;      // answererId, sdpAnswer
    public event Action<Guid, string>?      OnIceCandidate;      // fromId, candidate
    public event Action<Guid>?              OnCallEnded;
    public event Action<Guid, string>?      OnConversationRenamed;
    public event Action<Guid, Guid, string>? OnMemberAdded;
    public event Action<PostDto>?            OnNewPost;
    public event Action<CommentDto>?         OnNewComment;
    public event Action<StoryDto>?           OnNewStory;

    private ServerClient() { }

    // ── Connection ────────────────────────────────────────────────────────────

    public void Configure(string baseUrl, string token)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _token   = token;
        _http    = new HttpClient { BaseAddress = new Uri(_baseUrl + "/") };
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
    }

    public async Task ConnectHubAsync()
    {
        if (_hub != null)
        {
            try { await _hub.DisposeAsync(); } catch { }
        }

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hub?access_token={_token}", opts =>
            {
                opts.Headers["ngrok-skip-browser-warning"] = "true";
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<object>("NewMessage", raw =>
        {
            var msg = Deserialize<MessageDto>(raw);
            if (msg != null) OnNewMessage?.Invoke(msg);
        });
        _hub.On<int>("NotificationCount", n => OnNotificationCount?.Invoke(n));
        _hub.On<object>("UserTyping", raw =>
        {
            var o = Deserialize<TypingPayload>(raw);
            if (o != null) OnUserTyping?.Invoke(o.ConversationId, o.Username);
        });
        _hub.On<object>("IncomingCall", raw =>
        {
            var o = Deserialize<IncomingCallPayload>(raw);
            if (o != null) OnIncomingCall?.Invoke(o.CallerId, o.CallerName, o.SdpOffer);
        });
        _hub.On<object>("CallAnswered", raw =>
        {
            var o = Deserialize<CallAnsweredPayload>(raw);
            if (o != null) OnCallAnswered?.Invoke(o.AnswererId, o.SdpAnswer);
        });
        _hub.On<object>("IceCandidate", raw =>
        {
            var o = Deserialize<IceCandidatePayload>(raw);
            if (o != null) OnIceCandidate?.Invoke(o.FromId, o.Candidate);
        });
        _hub.On<object>("CallEnded", raw =>
        {
            var o = Deserialize<CallEndedPayload>(raw);
            if (o != null) OnCallEnded?.Invoke(o.FromId);
        });
        _hub.On<object>("ConversationRenamed", raw =>
        {
            var o = Deserialize<ConvRenamedPayload>(raw);
            if (o != null) OnConversationRenamed?.Invoke(o.ConversationId, o.NewName);
        });
        _hub.On<object>("MemberAdded", raw =>
        {
            var o = Deserialize<MemberAddedPayload>(raw);
            if (o != null) OnMemberAdded?.Invoke(o.ConversationId, o.UserId, o.Username);
        });
        _hub.On<object>("NewPost", raw =>
        {
            var p = Deserialize<PostDto>(raw);
            if (p != null) OnNewPost?.Invoke(p);
        });
        _hub.On<object>("NewComment", raw =>
        {
            var c = Deserialize<CommentDto>(raw);
            if (c != null) OnNewComment?.Invoke(c);
        });
        _hub.On<object>("NewStory", raw =>
        {
            var s = Deserialize<StoryDto>(raw);
            if (s != null) OnNewStory?.Invoke(s);
        });

        await _hub.StartAsync();
    }

    private static T? Deserialize<T>(object raw)
    {
        try
        {
            var json = raw is string s ? s : JsonSerializer.Serialize(raw);
            return JsonSerializer.Deserialize<T>(json, JsonOpts);
        }
        catch { return default; }
    }

    // ── Hub send methods ──────────────────────────────────────────────────────

    private Task HubInvokeAsync(string method, params object?[] args) =>
        _hub?.State == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected
            ? _hub.InvokeCoreAsync(method, args)
            : Task.CompletedTask;

    public Task SendMessageAsync(Guid convId, string text) =>
        HubInvokeAsync("SendMessage", convId, text);

    public Task SendTypingAsync(Guid convId) =>
        HubInvokeAsync("Typing", convId);

    public Task CallUserAsync(Guid targetId, string sdpOffer) =>
        HubInvokeAsync("CallUser", targetId, sdpOffer);

    public Task CallAnswerAsync(Guid callerId, string sdpAnswer) =>
        HubInvokeAsync("CallAnswer", callerId, sdpAnswer);

    public Task SendIceCandidateAsync(Guid targetId, string candidate) =>
        HubInvokeAsync("IceCandidate", targetId, candidate);

    public Task HangUpAsync(Guid targetId) =>
        HubInvokeAsync("HangUp", targetId);

    // ── Auth ──────────────────────────────────────────────────────────────────

    public async Task<(UserDto? user, string token)> RegisterAsync(
        string username, string displayName, string password,
        string email = "", string phone = "", string address = "")
    {
        var resp = await _http.PostAsJsonAsync("auth/register",
            new { username, displayName, password, email, phone, address });
        if (!resp.IsSuccessStatusCode) return (null, "");
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return (body?.User, body?.Token ?? "");
    }

    public async Task<(UserDto? user, string token)> LoginAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("auth/login", new { username, password });
        if (!resp.IsSuccessStatusCode) return (null, "");
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return (body?.User, body?.Token ?? "");
    }

    public async Task<string> PingAsync()
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<System.Text.Json.JsonElement>("ping", JsonOpts);
            return resp.GetProperty("version").GetString() ?? "unknown";
        }
        catch { return "unreachable"; }
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    public Task<UserDto?> GetMeAsync() =>
        _http.GetFromJsonAsync<UserDto>("users/me", JsonOpts);

    public Task<UserProfileDto?> GetUserAsync(Guid id) =>
        _http.GetFromJsonAsync<UserProfileDto>($"users/{id}", JsonOpts);

    public Task<List<UserSearchResult>?> SearchUsersAsync(string q) =>
        _http.GetFromJsonAsync<List<UserSearchResult>>($"users/search?q={Uri.EscapeDataString(q)}", JsonOpts);

    public async Task<bool> UpdateProfileAsync(
        string displayName, string bio, string website, string email,
        string phone, string address, string accentColor, bool notifyDMs, bool notifyFollowedPosts)
    {
        var resp = await _http.PutAsJsonAsync("users/me", new
        {
            displayName, bio, website, email, phone, address,
            accentColor, notifyDMs, notifyFollowedPosts
        });
        return resp.IsSuccessStatusCode;
    }

    public Task FollowAsync(Guid id)      => _http.PostAsync($"users/{id}/follow", null).ContinueWith(_ => { });
    public Task UnfollowAsync(Guid id)   => _http.DeleteAsync($"users/{id}/follow").ContinueWith(_ => { });
    public Task VerifyUserAsync(Guid id) => _http.PostAsync($"users/{id}/verify", null).ContinueWith(_ => { });

    public async Task<string?> UploadAvatarAsync(string filePath)
    {
        await using var fs = System.IO.File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fs), "file", System.IO.Path.GetFileName(filePath));
        var resp = await _http.PostAsync("users/me/avatar", content);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<AvatarResponse>(JsonOpts);
        return body?.AvatarUrl;
    }

    // ── Friend requests ───────────────────────────────────────────────────────

    public Task SendFriendRequestAsync(Guid targetId) =>
        _http.PostAsync($"friends/request/{targetId}", null).ContinueWith(_ => { });

    public Task AcceptFriendRequestAsync(Guid requestId) =>
        _http.PostAsync($"friends/accept/{requestId}", null).ContinueWith(_ => { });

    public Task DeclineFriendRequestAsync(Guid requestId) =>
        _http.PostAsync($"friends/decline/{requestId}", null).ContinueWith(_ => { });

    public Task<List<FriendRequestDto>?> GetIncomingRequestsAsync() =>
        _http.GetFromJsonAsync<List<FriendRequestDto>>("friends/requests/incoming", JsonOpts);

    public Task<List<OutgoingFriendRequestDto>?> GetOutgoingRequestsAsync() =>
        _http.GetFromJsonAsync<List<OutgoingFriendRequestDto>>("friends/requests/outgoing", JsonOpts);

    public Task<List<FriendDto>?> GetFriendsAsync() =>
        _http.GetFromJsonAsync<List<FriendDto>>("friends/list", JsonOpts);

    // ── Posts ─────────────────────────────────────────────────────────────────

    public async Task<PostDto?> CreatePostAsync(string caption, string tags)
    {
        var resp = await _http.PostAsJsonAsync("posts", new { caption, tags });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PostDto>(JsonOpts);
    }

    public async Task<string?> UploadPostImageAsync(Guid postId, string filePath)
    {
        await using var fs = System.IO.File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fs), "file", System.IO.Path.GetFileName(filePath));
        var resp = await _http.PostAsync($"posts/{postId}/image", content);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOpts);
        return body?.ImageUrl;
    }

    public async Task<string?> UploadPostVideoAsync(Guid postId, string filePath)
    {
        await using var fs = System.IO.File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fs), "file", System.IO.Path.GetFileName(filePath));
        var resp = await _http.PostAsync($"posts/{postId}/video", content);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<VideoUploadResponse>(JsonOpts);
        return body?.VideoUrl;
    }

    public Task<List<PostDto>?> GetFeedAsync(int page = 0) =>
        _http.GetFromJsonAsync<List<PostDto>>($"posts/feed?page={page}", JsonOpts);

    public Task<List<PostDto>?> GetExploreAsync(string? tag = null, string? q = null, int page = 0)
    {
        var url = $"posts/explore?page={page}";
        if (!string.IsNullOrEmpty(tag)) url += $"&tag={Uri.EscapeDataString(tag)}";
        if (!string.IsNullOrEmpty(q))   url += $"&q={Uri.EscapeDataString(q)}";
        return _http.GetFromJsonAsync<List<PostDto>>(url, JsonOpts);
    }

    public Task<List<TrendingTagDto>?> GetTrendingTagsAsync() =>
        _http.GetFromJsonAsync<List<TrendingTagDto>>("posts/trending-tags", JsonOpts);

    public async Task<(bool liked, int count)> ToggleLikeAsync(Guid postId)
    {
        var resp = await _http.PostAsync($"posts/{postId}/like", null);
        if (!resp.IsSuccessStatusCode) return (false, 0);
        var body = await resp.Content.ReadFromJsonAsync<LikeResponse>(JsonOpts);
        return (body?.Liked ?? false, body?.Count ?? 0);
    }

    public async Task<CommentDto?> AddCommentAsync(Guid postId, string text)
    {
        var resp = await _http.PostAsJsonAsync($"posts/{postId}/comment", new { text });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<CommentDto>(JsonOpts);
    }

    // ── Stories ───────────────────────────────────────────────────────────────

    public async Task<StoryDto?> CreateStoryAsync(string text, string backgroundColor,
        double textX, double textY, double textScale, double textRotation, string taggedUsers)
    {
        var resp = await _http.PostAsJsonAsync("stories",
            new { text, backgroundColor, textX, textY, textScale, textRotation, taggedUsers });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<StoryDto>(JsonOpts);
    }

    public async Task<string?> UploadStoryImageAsync(Guid storyId, string filePath)
    {
        await using var fs = System.IO.File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fs), "file", System.IO.Path.GetFileName(filePath));
        var resp = await _http.PostAsync($"stories/{storyId}/image", content);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<ImageUploadResponse>(JsonOpts);
        return body?.ImageUrl;
    }

    public async Task<string?> UploadStoryVideoAsync(Guid storyId, string filePath)
    {
        await using var fs = System.IO.File.OpenRead(filePath);
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(fs), "file", System.IO.Path.GetFileName(filePath));
        var resp = await _http.PostAsync($"stories/{storyId}/video", content);
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<VideoUploadResponse>(JsonOpts);
        return body?.VideoUrl;
    }

    public Task<List<StoryDto>?> GetStoryFeedAsync() =>
        _http.GetFromJsonAsync<List<StoryDto>>("stories/feed", JsonOpts);

    public Task MarkStorySeenAsync(Guid storyId) =>
        _http.PostAsync($"stories/{storyId}/seen", null).ContinueWith(_ => { });

    // ── Conversations ─────────────────────────────────────────────────────────

    public async Task<ConvDto?> GetOrCreateDmAsync(Guid targetUserId)
    {
        var resp = await _http.PostAsJsonAsync("conversations/dm", new { targetUserId });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ConvDto>(JsonOpts);
    }

    public async Task<ConvDto?> CreateGroupAsync(string name, List<Guid> memberIds)
    {
        var resp = await _http.PostAsJsonAsync("conversations/group", new { name, memberIds });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<ConvDto>(JsonOpts);
    }

    public Task<List<ConvDto>?> GetConversationsAsync() =>
        _http.GetFromJsonAsync<List<ConvDto>>("conversations", JsonOpts);

    public async Task<bool> RenameConversationAsync(Guid convId, string name)
    {
        var resp = await _http.PutAsJsonAsync($"conversations/{convId}/name", new { name });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> AddMemberAsync(Guid convId, Guid userId)
    {
        var resp = await _http.PostAsJsonAsync($"conversations/{convId}/members", new { userId });
        return resp.IsSuccessStatusCode;
    }

    public Task<List<MessageDto>?> GetMessagesAsync(Guid convId, int page = 0) =>
        _http.GetFromJsonAsync<List<MessageDto>>($"conversations/{convId}/messages?page={page}", JsonOpts);

    // ── Notifications ─────────────────────────────────────────────────────────

    public Task<List<NotifDto>?> GetNotificationsAsync(int page = 0) =>
        _http.GetFromJsonAsync<List<NotifDto>>($"notifications?page={page}", JsonOpts);

    public async Task<int> GetNotificationCountAsync()
    {
        var body = await _http.GetFromJsonAsync<NotifCountResponse>("notifications/count", JsonOpts);
        return body?.Count ?? 0;
    }

    public Task MarkAllNotificationsReadAsync() =>
        _http.PostAsync("notifications/read-all", null).ContinueWith(_ => { });

    public async Task<bool> DeletePostAsync(Guid postId)
    {
        var resp = await _http.DeleteAsync($"posts/{postId}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> ReportPostAsync(Guid postId, string reason)
    {
        var resp = await _http.PostAsJsonAsync($"posts/{postId}/report", new { reason });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAccountAsync()
    {
        var resp = await _http.DeleteAsync("users/me");
        return resp.IsSuccessStatusCode;
    }

    // ── Admin / automod ───────────────────────────────────────────────────────

    public Task<List<BannedWordDto>?> GetBannedWordsAsync() =>
        _http.GetFromJsonAsync<List<BannedWordDto>>("admin/words", JsonOpts);

    public async Task<bool> AddBannedWordAsync(string word)
    {
        var resp = await _http.PostAsJsonAsync("admin/words", new { word });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteBannedWordAsync(Guid id)
    {
        var resp = await _http.DeleteAsync($"admin/words/{id}");
        return resp.IsSuccessStatusCode;
    }

    public Task<List<AutomodFlagDto>?> GetFlagsAsync(bool resolved = false) =>
        _http.GetFromJsonAsync<List<AutomodFlagDto>>($"admin/flags?resolved={resolved}", JsonOpts);

    public async Task<bool> ResolveFlagAsync(Guid flagId, string resolution)
    {
        var resp = await _http.PostAsJsonAsync($"admin/flags/{flagId}/resolve", new { resolution });
        return resp.IsSuccessStatusCode;
    }

    public Task<List<AdminUserDto>?> GetAdminUsersAsync(string? q = null)
    {
        var url = "admin/users";
        if (!string.IsNullOrEmpty(q)) url += $"?q={Uri.EscapeDataString(q)}";
        return _http.GetFromJsonAsync<List<AdminUserDto>>(url, JsonOpts);
    }

    public async Task<bool> BanUserAsync(Guid userId, string reason)
    {
        var resp = await _http.PostAsJsonAsync($"admin/users/{userId}/ban", new { reason });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> UnbanUserAsync(Guid userId)
    {
        var resp = await _http.PostAsync($"admin/users/{userId}/unban", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<string?> ClaimMasterAsync()
    {
        var resp = await _http.PostAsync("admin/claim", null);
        if (resp.IsSuccessStatusCode) return null;
        // Return status + body so caller can distinguish 400 vs 404 vs 403
        var body = await resp.Content.ReadAsStringAsync();
        return $"{(int)resp.StatusCode}: {body}";
    }

    public async Task<bool> PromoteUserAsync(Guid userId)
    {
        var resp = await _http.PostAsync($"admin/users/{userId}/promote", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DemoteUserAsync(Guid userId)
    {
        var resp = await _http.PostAsync($"admin/users/{userId}/demote", null);
        return resp.IsSuccessStatusCode;
    }

    public Task<List<AdminPostDto>?> GetAdminPostsAsync(string? q = null, int page = 0)
    {
        var url = $"admin/posts?page={page}";
        if (!string.IsNullOrEmpty(q)) url += $"&q={Uri.EscapeDataString(q)}";
        return _http.GetFromJsonAsync<List<AdminPostDto>>(url, JsonOpts);
    }

    public Task<List<AdminCommentDto>?> GetAdminPostCommentsAsync(Guid postId) =>
        _http.GetFromJsonAsync<List<AdminCommentDto>>($"admin/posts/{postId}/comments", JsonOpts);

    public async Task<bool> DeleteAdminCommentAsync(Guid commentId)
    {
        var resp = await _http.DeleteAsync($"admin/comments/{commentId}");
        return resp.IsSuccessStatusCode;
    }

    public Task<List<AdminReportDto>?> GetAdminReportsAsync(int page = 0) =>
        _http.GetFromJsonAsync<List<AdminReportDto>>($"admin/reports?page={page}", JsonOpts);

    // ── Internal response types ───────────────────────────────────────────────

    private record AuthResponse(string Token, UserDto User);
    private record LikeResponse(bool Liked, int Count);
    private record NotifCountResponse(int Count);
    private record AvatarResponse(string AvatarUrl);
    private record ImageUploadResponse(string ImageUrl);
    private record VideoUploadResponse(string VideoUrl);

    // Hub payload shapes
    private record TypingPayload(Guid ConversationId, string Username);
    private record IncomingCallPayload(Guid CallerId, string CallerName, string SdpOffer);
    private record CallAnsweredPayload(Guid AnswererId, string SdpAnswer);
    private record IceCandidatePayload(Guid FromId, string Candidate);
    private record CallEndedPayload(Guid FromId);
    private record ConvRenamedPayload(Guid ConversationId, string NewName);
    private record MemberAddedPayload(Guid ConversationId, Guid UserId, string Username);
}

public record UserSearchResult(Guid Id, string Username, string DisplayName, string AccentColor, bool IsVerified = false, bool IsMaster = false);
public record TrendingTagDto(string Tag, int Count);
